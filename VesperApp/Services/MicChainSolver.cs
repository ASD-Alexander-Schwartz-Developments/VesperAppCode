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
        /// rates the 4.8 MHz clock only approximates (44.1 kHz family, 250 / 256 / 384 kHz). The MDF bottom is 4.8 MHz / 2048.</summary>
        public static readonly uint[] CommonRates =
        {
            0, 4000, 8000, 11025, 12000, 16000, 22050, 24000, 32000, 44100, 48000, 64000,
            96000, 100000, 120000, 150000, 192000, 200000, 240000, 250000, 256000, 300000, 384000, 400000,
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

        /// <summary>Which product a report is for: the firmware that adds level matching, and whether it streams Live Audio.</summary>
        /// <param name="HasHardwareDetector">The MDF sound-activity detector (bitmask bit 16) can run: on the
        /// STM32U5 it exists only in ADF1, and the KOL / Vesper microphones are wired to MDF1 - so neither
        /// current board has it; the firmware records with the software detector instead (and flags it).</param>
        public sealed record Product(string Name, string FirmwareVersion, bool HasLiveAudio, bool HasHardwareDetector = false)
        {
            public static readonly Product Kol = new("KOL", "1.8", true);
            public static readonly Product VesperPipistrelle = new("Vesper/Pipistrelle", "1.21", false);
        }

        /// <summary>MDF high-pass cut-off as a fraction of the output rate, per config.json "hpf" value (0 = off).</summary>
        public static readonly double[] HpfFactors = { 0, 0.000625, 0.00125, 0.0025, 0.0095 };
        public const double HpfUltrasonicFromHz = 96000;     // at and above: bat / ultrasonic work
        public const double HpfUltrasonicMaxHz = 2000;       // keeps every bat call, removes wind and handling rumble
        public const double HpfAudioMaxHz = 50;              // keeps all audible sound, removes DC and rumble

        /// <summary>Cut-off (-3 dB) of high-pass setting <paramref name="hpf"/> at sample rate <paramref name="fs"/>.</summary>
        public static double HpfCutoffHz(int hpf, double fs) => hpf > 0 && hpf < HpfFactors.Length ? HpfFactors[hpf] * fs : 0;

        /// <summary>The high-pass setting the adviser recommends for the reference rate: the highest cut-off at or
        /// below 2 kHz for ultrasonic rates, at or below 50 Hz for audio rates; never off (the microphone's DC offset
        /// drifts after power-up and would stay in the recording).</summary>
        public static int RecommendedHpf(uint refFs)
        {
            Chain? c = Solve(refFs, 0);
            if (c == null) return 1;
            double limit = c.ExactHz >= HpfUltrasonicFromHz ? HpfUltrasonicMaxHz : HpfAudioMaxHz;
            int best = 1;
            for (int h = 1; h < HpfFactors.Length; h++)
                if (HpfCutoffHz(h, c.ExactHz) <= limit) best = h;
            return best;
        }

        public sealed record SolverLine(string Text, bool Warning = false, bool Detail = false);
        public sealed record SolverSection(string Title, IReadOnlyList<SolverLine> Lines);

        /// <summary>The Solve window's content: a summary, one section per configured rate, the recommendation.</summary>
        public sealed record SolverReport(string Summary, IReadOnlyList<SolverSection> Sections, string? Recommendation, string Footer,
                                          string? HpfRecommendation = null, IReadOnlyList<ResponseCurve>? Response = null)
        {
            public override string ToString()
            {
                var sb = new StringBuilder(Summary).AppendLine();
                foreach (var s in Sections)
                {
                    sb.AppendLine().AppendLine(s.Title);
                    foreach (var l in s.Lines) sb.Append("  ").AppendLine(l.Text);
                }
                if (Recommendation != null) sb.AppendLine().AppendLine(Recommendation);
                if (HpfRecommendation != null) sb.AppendLine(HpfRecommendation);
                return sb.AppendLine().Append(Footer).ToString();
            }
        }

        /// <summary>Plain-language explanation of a microphone configuration for the editor's Solve window.</summary>
        public static SolverReport BuildReport(uint rate1, uint rate2, ushort cfgGain, Product product, string? note = null, int hpf = -1)
        {
            var ci = CultureInfo.InvariantCulture;
            string footer = $"The same loudness at the second rate{(product.HasLiveAudio ? " and in Live Audio" : "")} needs {product.Name} firmware " +
                            $"{product.FirmwareVersion} or later. Older firmware uses the same filter at the first rate and applies the gain unchanged elsewhere.";
            int gain = GainCode(cfgGain);
            uint refFs = RefRate(rate1, rate2, 0);
            if (refFs == 0)
                return new SolverReport("Both sample rates are Off: the schedule does not use the microphones.", Array.Empty<SolverSection>(), null, footer);

            int refIdx = rate1 != 0 ? 1 : 2;
            int level = LevelCdb(refFs, gain);
            string summary = string.Create(ci, $"Gain {CodeCdb(gain) / 100.0:+0.0;-0.0} dB applies to Sample Rate [{refIdx}] ({Hz(refFs)}).") +
                             (rate1 != 0 && rate2 != 0 ? " The unit keeps the same loudness at Sample Rate [2]." : "") +
                             (product.HasLiveAudio ? " Live Audio uses the same loudness." : "");

            var sections = new List<SolverSection>();
            if (!string.IsNullOrEmpty(note))
                sections.Add(new SolverSection("Converted from an older configuration", new[] { new SolverLine(note, Warning: note.Contains("cannot")) }));

            foreach ((int idx, uint fs) in new[] { (1, rate1), (2, rate2) })
            {
                if (fs == 0) continue;
                Chain c = Solve(fs, level)!;
                var lines = new List<SolverLine>();
                double dev = c.ExactHz / fs - 1;
                lines.Add(c.Inexact
                    ? new SolverLine(string.Create(ci, $"Runs at {c.ExactHz:0.#} Hz ({dev * 100:+0.00;-0.00} %), the closest the 4.8 MHz microphone clock allows. Recordings store the real rate."), Warning: Math.Abs(dev) > 0.005)
                    : new SolverLine($"Runs at exactly {Hz(fs)}."));
                if (c.Reshape)
                {
                    lines.Add(new SolverLine(string.Create(ci, $"Full anti-aliasing filter, flat up to about {0.44 * c.ExactHz / 1000:0.#} kHz.")));
                    if (c.Inexact)
                    {
                        string alt = BetterRates(fs);
                        if (alt.Length > 0) lines.Add(new SolverLine($"Exact rates nearby: {alt}."));
                    }
                }
                else
                {
                    string alt = BetterRates(fs);
                    lines.Add(new SolverLine(string.Create(ci, $"Basic filter only (this rate cannot use the full filter): {-CicDroopDb(c, 0.4 * c.ExactHz):0} dB quieter at {0.4 * c.ExactHz / 1000:0.#} kHz.") +
                                             (alt.Length > 0 ? $" Rates with the full filter nearby: {alt}." : ""), Warning: true));
                }
                double lv = c.LevelCdb / 100.0;
                double clip = Math.Min(94.0 - MicSensitivityDbfs - lv, MicAopDbSpl);
                double margin = MicSensitivityDbfs - MicSnrDb + lv - QuantNoiseDbfs;
                lines.Add(new SolverLine(string.Create(ci, $"Loudest sound before clipping: about {clip:0} dB SPL") +
                                         (clip >= MicAopDbSpl ? " (the microphone's own limit)." : ".")));
                lines.Add(margin < 6
                    ? new SolverLine(string.Create(ci, $"Quiet sounds are lost: the microphone's own noise is {Math.Abs(margin):0} dB {(margin < 0 ? "below" : "above")} the recording's noise floor. Raise the gain."), Warning: true)
                    : margin > 40
                        ? new SolverLine(string.Create(ci, $"More gain than needed: loud sounds clip earlier than necessary (microphone noise {margin:0} dB above the recording's noise floor)."), Warning: true)
                        : new SolverLine(string.Create(ci, $"Quiet sounds are kept: the microphone's own noise is {margin:0} dB above the recording's noise floor.")));
                if (c.GainClamped)
                    lines.Add(new SolverLine(string.Create(ci, $"Cannot reach the loudness of Sample Rate [{refIdx}] here: it records {lv - level / 100.0:+0.0;-0.0} dB different."), Warning: true));
                lines.Add(new SolverLine(string.Create(ci, $"Technical: {c.Describe()}, full-scale input → {lv:+0.0;-0.0} dBFS, MDF gain {CodeCdb(c.GainCode) / 100.0:+0.0;-0.0} dB."), Detail: true));
                sections.Add(new SolverSection($"Sample Rate [{idx}] — {Hz(fs)}", lines));
            }

            string? hpfRec = null;
            if (hpf >= 0)
            {
                sections.Add(HpfSection(rate1, rate2, hpf, refFs, out int recHpf));
                if (recHpf != hpf)
                    hpfRec = string.Create(ci, $"Recommended high-pass filter: {HpfName(recHpf)} — {HpfCutoffHz(recHpf, Solve(refFs, 0)!.ExactHz):0.#} Hz at {Hz(refFs)}.");
            }

            int recCode = Recommended(refFs);
            double recLevel = (Solve(refFs, 0)!.BaseCdb + CodeCdb(recCode)) / 100.0;
            string? rec = recCode == gain ? null :
                string.Create(ci, $"Recommended gain for {Hz(refFs)}: {CodeCdb(recCode) / 100.0:+0.0;-0.0} dB — sounds up to about {94.0 - MicSensitivityDbfs - recLevel:0} dB SPL are recorded without clipping and quiet sounds are kept.");
            var response = new[] { (1, rate1), (2, rate2) }.Where(r => r.Item2 != 0)
                .Select(r => Response(r.Item2, Math.Max(hpf, 0), $"Sample Rate [{r.Item1}] — {Hz(r.Item2)}")).ToList();
            return new SolverReport(summary, sections, rec, footer, hpfRec, response);
        }

        /// <summary>Gain and phase of one configured chain over frequency (log spaced up to Nyquist).</summary>
        public sealed record ResponseCurve(string Label, double[] FreqHz, double[] GainDb, double[] PhaseDeg, double DelayUs, bool ReshapeApproximate);

        private const double ReshapePassband = 0.444;   // x output rate: flat to here (0.111 x the CIC output rate)
        private const double ReshapeStopDb = -70;       // reached at Nyquist (spec-level model: ST does not publish the taps)
        private const int ReshapeTaps = 24;

        /// <summary>
        /// The chain's response relative to its passband: the CIC exactly (sinc^N), the reshape filter modelled from its
        /// specification (compensates the CIC droop, flat to 0.444 fs, steep roll-off to -70 dB at Nyquist), the MDF
        /// high-pass as the first-order filter it is. CIC and reshape are linear phase, so the phase is shown without
        /// their constant delay (<see cref="ResponseCurve.DelayUs"/>): what remains is the high-pass phase shift.
        /// </summary>
        public static ResponseCurve Response(uint fs, int hpf, string label, int points = 240)
        {
            Chain c = Solve(fs, 0)!;
            double nyq = c.ExactHz / 2, fc = HpfCutoffHz(hpf, c.ExactHz);
            double fmin = Math.Max(1.0, Math.Min(10.0, fc > 0 ? fc / 10 : 10.0));
            var f = new double[points]; var g = new double[points]; var ph = new double[points];
            for (int i = 0; i < points; i++)
            {
                double fi = fmin * Math.Pow(nyq / fmin, i / (double)(points - 1));
                double mag;
                if (c.Reshape)
                {
                    double edge = ReshapePassband * c.ExactHz;
                    if (fi <= edge) mag = 1.0;                                   // droop compensated
                    else
                    {
                        double t = (fi - edge) / (nyq - edge);                   // 0..1 across the transition band
                        mag = Math.Pow(10, ReshapeStopDb * (1 - Math.Cos(Math.PI * Math.Min(t, 1))) / 2 / 20);
                    }
                }
                else
                {
                    double x = Math.PI * fi / PdmHz;
                    mag = Math.Pow(Math.Abs(Math.Sin(x * c.Cic) / (c.Cic * Math.Sin(x))), c.Order);
                }
                double phase = 0;
                if (fc > 0)
                {
                    mag *= fi / Math.Sqrt(fi * fi + fc * fc);                     // first-order high-pass
                    phase = 90.0 - Math.Atan(fi / fc) * 180 / Math.PI;
                }
                f[i] = fi; g[i] = 20 * Math.Log10(Math.Max(mag, 1e-12)); ph[i] = phase;
            }
            double cicIn = PdmHz;
            double delay = c.Order * (c.Cic - 1) / 2.0 / cicIn;                   // CIC: N(R-1)/2 input samples
            if (c.Reshape) delay += (ReshapeTaps - 1) / 2.0 / (PdmHz / (double)c.Cic);
            return new ResponseCurve(label, f, g, ph, delay * 1e6, c.Reshape);
        }

        public static string HpfName(int hpf) => hpf <= 0 ? "Off" : string.Create(CultureInfo.InvariantCulture, $"{HpfFactors[hpf]:0.######} × sample rate");

        /// <summary>The high-pass adviser: every setting's cut-off at the configured rates, current and recommended marked.</summary>
        private static SolverSection HpfSection(uint rate1, uint rate2, int hpf, uint refFs, out int recommended)
        {
            var ci = CultureInfo.InvariantCulture;
            recommended = RecommendedHpf(refFs);
            var rates = new[] { (1, rate1), (2, rate2) }.Where(r => r.Item2 != 0)
                                                        .Select(r => (r.Item1, Solve(r.Item2, 0)!)).ToList();
            var lines = new List<SolverLine>();
            for (int h = 0; h < HpfFactors.Length; h++)
            {
                string at = h == 0
                    ? "the microphone's DC offset (it drifts after power-up) and rumble stay in the recording"
                    : string.Join(", ", rates.Select(r => string.Create(ci, $"{HpfCutoffHz(h, r.Item2.ExactHz):0.#} Hz at Sample Rate [{r.Item1}]")));
                string tags = (h == hpf ? "   ← current" : "") + (h == recommended ? (h == hpf ? ", recommended" : "   ← recommended") : "");
                lines.Add(new SolverLine((h == 0 ? $"Off — {at}" : $"{HpfName(h)}: {at}") + tags, Warning: h == hpf && h == 0));
            }
            Chain refChain = Solve(refFs, 0)!;
            lines.Add(new SolverLine(refChain.ExactHz >= HpfUltrasonicFromHz
                ? string.Create(ci, $"Ultrasonic rate: the advice is the highest cut-off up to {HpfUltrasonicMaxHz / 1000:0} kHz — every bat call is kept, wind and handling rumble are removed.")
                : string.Create(ci, $"Audio rate: the advice is the highest cut-off up to {HpfAudioMaxHz:0} Hz — all audible sound is kept, DC and rumble are removed."), Detail: true));
            return new SolverSection("High-pass filter — cut-off of each setting", lines);
        }

        public static string Report(uint rate1, uint rate2, ushort cfgGain, string? note = null) =>
            BuildReport(rate1, rate2, cfgGain, Product.Kol, note).ToString();

        /// <summary>The nearest listed rates below and above <paramref name="fs"/> that are exact and use the full filter.</summary>
        private static string BetterRates(uint fs)
        {
            var better = CommonRates.Where(r => r != 0 && Solve(r, 0) is { Inexact: false, Reshape: true }).ToList();
            uint? below = better.Where(r => r < fs).Select(r => (uint?)r).LastOrDefault();
            uint? above = better.Where(r => r > fs).Select(r => (uint?)r).FirstOrDefault();
            return string.Join(" or ", new[] { below, above }.Where(r => r.HasValue).Select(r => Hz(r!.Value)));
        }

        public sealed record Migration(ushort Gain, uint Rate1, uint Rate2, string? Note);

        /// <summary>The listed rate closest to <paramref name="fs"/> (0 stays 0).</summary>
        public static uint NearestListed(uint fs) =>
            fs == 0 ? 0 : CommonRates.Where(r => r != 0).OrderBy(r => Math.Abs((long)r - fs)).First();

        /// <summary>
        /// An old config ("filter" without bit 15) used the chain its filter/cic4 bits select. The same output
        /// level at the reference rate with the automatic chain, where the hardware allows it.
        /// </summary>
        public static Migration MigrateLegacy(uint rate1, uint rate2, ushort filter, bool cic4, ushort cfgGain)
        {
            var ci = CultureInfo.InvariantCulture;
            var notes = new List<string>();
            /* rates outside the editor's list (old free-typed values) move to the nearest listed rate */
            uint r1 = NearestListed(rate1), r2 = NearestListed(rate2);
            foreach ((int idx, uint was, uint now) in new[] { (1, rate1, r1), (2, rate2, r2) })
                if (was != now) notes.Add($"Sample Rate [{idx}] {Hz(was)} is not offered any more and was changed to {Hz(now)}.");

            uint refFs = RefRate(rate1, rate2, 0);
            ushort newGain = cfgGain;
            if ((filter & AutoBit) == 0 && refFs != 0)
            {
                Chain legacy = SolveLegacy(refFs, filter, cic4, GainCode(cfgGain))!;
                Chain auto = Solve(RefRate(r1, r2, 0), legacy.LevelCdb)!;
                newGain = GainValue(auto.GainCode);
                if (newGain != cfgGain || legacy.Total != auto.Total || legacy.Reshape != auto.Reshape)
                {
                    string rate = legacy.Total != auto.Total
                        ? string.Create(ci, $" It now runs at {auto.ExactHz:0.#} Hz; the old setting ran at {legacy.ExactHz:0.#} Hz.")
                        : "";
                    notes.Add(string.Create(ci, $"The old filter setting ({legacy.Describe()}) was replaced by the automatic filter ({auto.Describe()}) at {Hz(auto.RequestedHz)}.") + rate +
                              string.Create(ci, $" Gain {CodeCdb(GainCode(cfgGain)) / 100.0:+0.0;-0.0} → {CodeCdb(auto.GainCode) / 100.0:+0.0;-0.0} dB") +
                              (auto.GainClamped
                                  ? string.Create(ci, $": the old loudness cannot be reached with this filter, recordings will be {(auto.LevelCdb - legacy.LevelCdb) / 100.0:+0.0;-0.0} dB different.")
                                  : " keeps the same loudness."));
                }
            }
            return new Migration(newGain, r1, r2, notes.Count > 0 ? string.Join(" ", notes) : null);
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
