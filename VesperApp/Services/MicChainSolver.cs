using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Text;

namespace VesperApp.Services
{
    /// <summary>
    /// The KOL microphone filter chain for a sample rate — a mirror of the firmware's
    /// <c>Core/Src/mic_chain.c</c> (keep the two in step; asd-interfaces interfaces/kol-live-audio.md,
    /// "Microphone chain").
    ///
    /// With config.json "filter" bit 15 the firmware (1.8+) picks the chain itself: the rate closest to
    /// the request (PDM clock 4.8 MHz / D, D = 4 × CIC with the reshape filter or D = CIC without it);
    /// for equally close rates the reshape filter /4 (flat passband, real anti-aliasing); then the
    /// highest CIC order the hardware allows (SINC5 ≤ 32, SINC4 ≤ 64, SINC3 ≤ 256, SINC2 ≤ 512).
    /// "gain" stays the MDF SCALE setting at the config's reference rate (sample rate [1], else [2]);
    /// at any other rate the firmware matches the output level, since the CIC gain (ratio^order)
    /// differs by up to ~60 dB between chains. The low "filter" bits are written by the same rules so
    /// older firmware builds the same chain at the reference rate.
    /// </summary>
    public static class MicChainSolver
    {
        public const uint PdmHz = 4_800_000;
        public const ushort AutoBit = 0x8000;
        public const int CodeMin = -16, CodeMax = 24;
        private const int CicMin = 2, CicMax = 512, TotalMax = 4 * CicMax;
        private const int FullScaleCdb = 13847;      // 20 log10(2^23)
        private const int ReshapeCdb = 957;          // reshape filter DC gain x3.01, measured 2026-10-21 (firmware MC_RSF_CDB)
        private const int ClampSlackCdb = 150;

        // SPH0641LU4H (KOL microphones), datasheet values
        public const double MicSensitivityDbfs = -26.0;   // at 94 dB SPL, 1 kHz
        public const double MicSnrDb = 64.3;              // A-weighted
        public const double MicAopDbSpl = 122.5;
        private const double QuantNoiseDbfs = -101.1;     // 16-bit samples: LSB/sqrt(12) re full scale
        public const double RecommendedLevelDb = 6.0;     // full-scale PDM -> +6 dBFS: clips near 114 dB SPL

        /// <summary>Rates offered in the editor (0 = off): the exact ones plus the common audio and bat-detector
        /// rates the 4.8 MHz clock only approximates (44.1 kHz family, 250 kHz). The MDF bottom is 4.8 MHz / 2048.</summary>
        public static readonly uint[] CommonRates =
        {
            0, 4000, 8000, 11025, 12000, 16000, 22050, 24000, 32000, 44100, 48000, 64000,
            96000, 100000, 120000, 150000, 192000, 200000, 240000, 250000, 300000, 400000,
        };

        public sealed record Chain(
            uint RequestedHz, int Total, int Cic, bool Reshape, int Order,
            double ExactHz, uint RoundedHz, bool Inexact,
            int BaseCdb, int GainCode, int LevelCdb, bool GainClamped)
        {
            public string Describe() =>
                (Reshape ? $"reshape /4 + CIC {Cic} " : $"CIC {Cic} ") + (Order >= 4 ? $"SINC{Order}" : $"MCIC SINC{Order}");
        }

        /// <summary>MDF SCALE code (−16..24) → centi-dB; the steps alternate ×1.5 and ×1.333.</summary>
        public static int CodeCdb(int code)
        {
            if (code >= 0) return (code >> 1) * 602 + ((code & 1) != 0 ? 352 : 0);
            int n = -code;
            return -((n >> 1) * 602) - ((n & 1) != 0 ? 250 : 0);
        }

        /// <summary>config.json "gain" (SCALE field as written: 0..24, 0x20..0x2F for negative) → code.</summary>
        public static int GainCode(ushort cfgGain)
        {
            int b = cfgGain & 0xFF;     // config: SCALE field (0x20..0x2F negative); console: int8 (0xF0..0xFF)
            int c = b >= 0xF0 ? b - 256 : b >= 0x20 && b <= 0x2F ? b - 48 : b;
            return Math.Clamp(c, CodeMin, CodeMax);
        }

        /// <summary>Signed code → the config.json "gain" value.</summary>
        public static ushort GainValue(int code)
        {
            code = Math.Clamp(code, CodeMin, CodeMax);
            return (ushort)(code < 0 ? code + 48 : code);
        }

        /// <summary>The reshape filter /4 is preferred unless its small CIC cannot bring a full-scale input
        /// to full scale even at the maximum SCALE (300 kHz, 400 kHz) — firmware mc_build.</summary>
        private static bool Build(int total, out int cic, out bool reshape)
        {
            bool withRsf = total % 4 == 0 && total / 4 >= CicMin && total / 4 <= CicMax;
            bool cicOnly = total >= CicMin && total <= CicMax;
            if (withRsf && (!cicOnly || BaseCdb(total / 4, OrderFor(total / 4), true) + CodeCdb(CodeMax) >= 0))
            { cic = total / 4; reshape = true; return true; }
            if (cicOnly) { cic = total; reshape = false; return true; }
            cic = 0; reshape = false; return false;
        }

        private static int OrderFor(int cic) => cic <= 32 ? 5 : cic <= 64 ? 4 : cic <= 256 ? 3 : 2;

        private static int BaseCdb(int cic, int order, bool reshape) =>
            (int)Math.Round(order * 2000.0 * Math.Log10(cic), MidpointRounding.AwayFromZero) - FullScaleCdb + (reshape ? ReshapeCdb : 0);

        /// <summary>The chain for <paramref name="fsReq"/> at output level <paramref name="levelCdb"/>; null for 0 Hz.</summary>
        public static Chain? Solve(uint fsReq, int levelCdb)
        {
            if (fsReq == 0) return null;
            long d0 = (PdmHz + fsReq / 2) / fsReq;
            int best = 0, bestCic = 0; bool bestRsf = false; ulong bestErr = ulong.MaxValue;
            for (long d = Math.Max(1, d0 - 2); d <= d0 + 2; d++)
            {
                int t = (int)Math.Clamp(d, 4, TotalMax);
                if (!Build(t, out int cic, out bool rsf)) continue;
                long diff = PdmHz - (long)t * fsReq;
                ulong e = (ulong)Math.Abs(diff) * 1_000_000UL / (ulong)t;
                if (e < bestErr || (e == bestErr && rsf && !bestRsf)) { bestErr = e; best = t; bestCic = cic; bestRsf = rsf; }
            }
            if (best == 0) return null;

            int order = OrderFor(bestCic);
            int baseCdb = BaseCdb(bestCic, order, bestRsf);
            int want = levelCdb - baseCdb;
            int code = CodeMin, codeErr = int.MaxValue;
            for (int c = CodeMin; c <= CodeMax; c++)
            {
                int e = Math.Abs(CodeCdb(c) - want);
                if (e < codeErr) { codeErr = e; code = c; }
            }
            bool clamped = want < CodeCdb(CodeMin) - ClampSlackCdb || want > CodeCdb(CodeMax) + ClampSlackCdb;
            return new Chain(fsReq, best, bestCic, bestRsf, order, (double)PdmHz / best, (uint)((PdmHz + (uint)best / 2) / (uint)best),
                             (ulong)best * fsReq != PdmHz, baseCdb, code, baseCdb + CodeCdb(code), clamped);
        }

        public static int LevelCdb(uint refFs, int gainCode)
        {
            gainCode = Math.Clamp(gainCode, CodeMin, CodeMax);
            Chain? r = Solve(refFs, 0);
            return (r?.BaseCdb ?? 0) + CodeCdb(gainCode);
        }

        public static uint RefRate(uint rate1, uint rate2, uint fs) => rate1 != 0 ? rate1 : rate2 != 0 ? rate2 : fs;

        /// <summary>"filter" value VesperApp writes: auto bit + the reshape bits older firmware needs to build
        /// the same chain at the reference rate (256 = reshape /4, 257 = bypassed).</summary>
        public static ushort FilterValue(uint refFs)
        {
            Chain? c = Solve(refFs == 0 ? 48000 : refFs, 0);
            return (ushort)(AutoBit | (c is { Reshape: false } ? 0x101 : 0x100));
        }

        /// <summary>The chain the pre-auto firmware builds from "filter"/"cic4" — for migrating old configs.</summary>
        public static Chain? SolveLegacy(uint fs, ushort filter, bool cic4, int gainCode)
        {
            if (fs == 0) return null;
            bool rsf = (filter & 0x100) != 0 && (filter & 0x1) == 0;   // bit 0 set = reshape bypassed (HAL encoding)
            int cic = (int)(PdmHz / fs);
            if (rsf) cic /= 4;
            cic = Math.Clamp(cic, CicMin, CicMax);
            int order = cic > 32 ? OrderFor(cic) : (cic4 ? 4 : 5);
            int total = rsf ? 4 * cic : cic;
            int baseCdb = BaseCdb(cic, order, rsf);
            gainCode = Math.Clamp(gainCode, CodeMin, CodeMax);
            return new Chain(fs, total, cic, rsf, order, (double)PdmHz / total, (uint)((PdmHz + (uint)total / 2) / (uint)total),
                             (ulong)total * fs != PdmHz, baseCdb, gainCode, baseCdb + CodeCdb(gainCode), false);
        }

        /// <summary>Passband droop at frequency f (dB, ≤ 0) of a CIC-only chain; reshape chains are flat to ~0.44 fs.</summary>
        public static double CicDroopDb(Chain c, double f)
        {
            double x = Math.PI * f / PdmHz;
            double h = Math.Sin(x * c.Cic) / (c.Cic * Math.Sin(x));
            return c.Order * 20.0 * Math.Log10(Math.Abs(h));
        }

        /// <summary>Plain-text explanation of a configuration for the editor's Solve button.</summary>
        public static string Report(uint rate1, uint rate2, ushort cfgGain, string? note = null)
        {
            var ci = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            int gain = GainCode(cfgGain);
            uint refFs = RefRate(rate1, rate2, 0);
            if (refFs == 0)
                return "Both sample rates are off: the microphones are not used by the schedule.";

            int level = LevelCdb(refFs, gain);
            sb.AppendLine(ci, $"Gain {CodeCdb(gain) / 100.0:+0.0;-0.0} dB is set for the reference rate {Hz(refFs)} (sample rate [{(rate1 != 0 ? 1 : 2)}]).");
            foreach ((int idx, uint fs) in new[] { (1, rate1), (2, rate2) })
            {
                if (fs == 0) continue;
                Chain c = Solve(fs, level)!;
                sb.AppendLine();
                string exact = c.Inexact
                    ? string.Create(ci, $" — runs at {c.ExactHz:0.#} Hz ({(c.ExactHz / fs - 1) * 100:+0.00;-0.00} %), the closest the 4.8 MHz microphone clock divides to")
                    : " (exact)";
                sb.AppendLine(ci, $"Sample rate [{idx}]: {Hz(fs)}{exact}");
                string filter = c.Reshape
                    ? string.Create(ci, $"flat to about {0.44 * c.ExactHz / 1000:0.#} kHz")
                    : string.Create(ci, $"{CicDroopDb(c, 0.4 * c.ExactHz):0.0} dB at {0.4 * c.ExactHz / 1000:0.#} kHz (CIC only — this rate cannot use the /4 reshape filter)");
                sb.AppendLine(ci, $"  Filter: {c.Describe()}, {filter}");
                double lv = c.LevelCdb / 100.0;
                double clip = Math.Min(94.0 - MicSensitivityDbfs - lv, MicAopDbSpl);
                double margin = MicSensitivityDbfs - MicSnrDb + lv - QuantNoiseDbfs;
                string aop = clip >= MicAopDbSpl ? " (the microphone's own overload point)" : "";
                sb.AppendLine(ci, $"  Level: a full-scale microphone signal reaches {lv:+0.0;-0.0} dBFS; clips at about {clip:0} dB SPL{aop}");
                string verdict = margin < 6 ? " — TOO LOW, quiet sounds are lost; raise the gain"
                               : margin > 40 ? " — more gain than needed, it only costs headroom" : "";
                sb.AppendLine(ci, $"  Noise: the microphone's self-noise sits {margin:0} dB above the 16-bit quantisation noise{verdict}");
                if (c.Inexact || !c.Reshape)
                {
                    var better = CommonRates.Where(r => r != 0 && Solve(r, 0) is { Inexact: false, Reshape: true }).ToList();
                    uint? below = better.Where(r => r < fs).Select(r => (uint?)r).LastOrDefault();
                    uint? above = better.Where(r => r > fs).Select(r => (uint?)r).FirstOrDefault();
                    string alt = string.Join(" or ", new[] { below, above }.Where(r => r.HasValue).Select(r => Hz(r!.Value)));
                    if (alt.Length > 0)
                        sb.AppendLine(ci, $"  Closest rates with an exact clock and the full reshape filter: {alt}.");
                }
                if (c.GainClamped)
                    sb.AppendLine(ci, $"  The level of the reference rate cannot be reached here (MDF gain −48…+72 dB): recorded {lv - level / 100.0:+0.0;-0.0} dB off.");
            }

            Chain refChain = Solve(refFs, 0)!;
            int recCode = Recommended(refFs);
            sb.AppendLine();
            sb.AppendLine(ci, $"Recommended gain for {Hz(refFs)}: {CodeCdb(recCode) / 100.0:+0.0;-0.0} dB (full-scale microphone → {(refChain.BaseCdb + CodeCdb(recCode)) / 100.0:+0.0;-0.0} dBFS; clips near {94.0 - MicSensitivityDbfs - (refChain.BaseCdb + CodeCdb(recCode)) / 100.0:0} dB SPL).");
            if (!string.IsNullOrEmpty(note)) { sb.AppendLine(); sb.AppendLine(note); }
            sb.AppendLine();
            sb.Append("Firmware older than KOL 1.8 builds the same filter at the reference rate but applies the gain unchanged at the other rate and in Live Audio.");
            return sb.ToString();
        }

        /// <summary>Gain code that puts a full-scale microphone signal at <see cref="RecommendedLevelDb"/> at <paramref name="refFs"/>.</summary>
        public static int Recommended(uint refFs)
        {
            Chain? c = Solve(refFs, (int)(RecommendedLevelDb * 100));
            return c?.GainCode ?? 0;
        }

        public static string Hz(double hz) => hz >= 1000
            ? (hz / 1000).ToString(hz % 1000 == 0 ? "0" : "0.###", CultureInfo.InvariantCulture) + " kHz"
            : hz.ToString("0", CultureInfo.InvariantCulture) + " Hz";
    }
}
