using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace VesperApp.Services
{
    /// <summary>
    /// Sound-triggered recording settings for the KOL / Vesper / Pipistrelle microphones, stored in the existing
    /// config.json fields (asd-interfaces projects/audio-platform.md, "config.json"):
    /// a config is sound-triggered when <c>thresup</c> or <c>thresdn</c> is non-zero (and "filter" bit 15 marks it
    /// as written by this VesperApp - legacy raw thresholds never trigger); <c>thresup</c> = start level,
    /// <c>thresdn</c> = stop level (centi-dB; 0 = 3 dB below start); <c>bitmask</c> bits 16-18 = hardware detector /
    /// absolute dB SPL / ultrasound only;
    /// per config <c>wLen[n]</c> = hold after the sound, <c>wRate[n]</c> = pre-trigger (ms) — the duty-cycle window
    /// fields change their meaning when the trigger bit is set.
    /// Older firmware ignores the bits and the levels and only treats wLen/wRate as a window when rate &gt; length,
    /// which these settings never produce (pre-trigger &lt; hold), so it records continuously instead.
    /// </summary>
    public static class MicTrigger
    {
        public const uint BitHardware = 0x10000, BitAbsolute = 0x20000, BitUltrasound = 0x40000;
        public const uint AllBits = BitHardware | BitAbsolute | BitUltrasound;
        public const int DefaultHysteresisCdb = 300;            // stop level 3 dB below start when thresdn = 0
        public const uint MinHoldMs = 500, MaxPreTriggerMs = 100; // keeps pre-trigger < hold: old firmware never sees a window

        public static readonly int[] RelativeLevelsDb = { 3, 6, 9, 12, 15, 18, 21, 24, 30 };
        public static readonly int[] AbsoluteLevelsDb = { 30, 35, 40, 45, 50, 55, 60, 65, 70, 75, 80, 90, 100 };
        public static readonly int[] HysteresisDb = { 0, 1, 3, 6, 10 };      // 0 = the default (3 dB, stored as 0)
        public static readonly uint[] HoldMs = { 500, 1000, 2000, 3000, 5000, 10000, 30000 };
        public static readonly uint[] PreTriggerMs = { 0, 10, 25, 50, 100 };

        /// <summary>DMA ring per microphone (bytes): the software detector's pre-trigger comes from its older half.</summary>
        public static int RingBytes(MicChainSolver.Product p) => p == MicChainSolver.Product.Kol ? 63 * 1024 : 64 * 1024;

        /// <summary>Margin the firmware keeps ahead of the DMA when it copies the pre-trigger out of the ring.</summary>
        public const uint PreTriggerGuardMs = 20;

        /// <summary>The longest pre-trigger the firmware can deliver at <paramref name="fs"/>: half the ring (16-bit
        /// samples) minus the guard - about 60 ms at 200 kHz, 22 ms at 384 kHz. Longer requests are shortened (and
        /// flagged) by the firmware.</summary>
        public static uint PreTriggerMaxMs(MicChainSolver.Product p, uint fs)
        {
            if (fs == 0) return MaxPreTriggerMs;
            long halfMs = RingBytes(p) / 2 / 2 * 1000L / fs;
            return (uint)Math.Clamp(halfMs - PreTriggerGuardMs, 0, MaxPreTriggerMs);
        }

        /// <summary>The largest offered pre-trigger option not longer than <paramref name="ms"/> or the ring limit.</summary>
        public static uint CapPreTrigger(MicChainSolver.Product p, uint fs, uint ms)
        {
            uint max = Math.Min(ms, PreTriggerMaxMs(p, fs));
            return PreTriggerMs.Where(o => o <= max).DefaultIfEmpty(0u).Max();
        }

        public sealed record Settings(bool On, bool Hardware, bool Absolute, bool Ultrasound, int StartDb, int HysteresisDb,
                                      uint Hold1, uint Pre1, uint Hold2, uint Pre2)
        {
            public static readonly Settings Default = new(false, false, false, false, 12, 0, 2000, 25, 2000, 25);
        }

        public static Settings Read(uint bitmask, uint thresUp, uint thresDn, uint[] wLen, uint[] wRate)
        {
            /* continuous (no level): wLen/wRate are duty-cycle windows, not trigger timing - offer the defaults */
            if (thresUp == 0 && thresDn == 0)
                return Settings.Default;
            int start = (int)Math.Round(thresUp / 100.0);
            int hyst = thresDn == 0 ? 0 : Math.Max(0, (int)Math.Round((thresUp - (double)thresDn) / 100.0));
            return new Settings(true, (bitmask & BitHardware) != 0, (bitmask & BitAbsolute) != 0, (bitmask & BitUltrasound) != 0,
                                start, hyst, wLen[1], wRate[1], wLen[2], wRate[2]);
        }

        /// <summary>Write <paramref name="s"/> into the driver fields (arrays are modified in place).</summary>
        public static (uint Bitmask, uint ThresUp, uint ThresDn) Write(Settings s, uint bitmask, uint[] wLen, uint[] wRate)
        {
            bitmask &= ~AllBits;
            if (!s.On) return (bitmask, 0, 0);
            bitmask |= (s.Hardware ? BitHardware : 0) | (s.Absolute ? BitAbsolute : 0) | (s.Ultrasound && !s.Hardware ? BitUltrasound : 0);
            uint up = (uint)(Math.Max(1, s.StartDb) * 100);   /* non-zero: this is what makes the config triggered */
            uint dn = s.HysteresisDb == 0 ? 0 : (uint)Math.Max(0, up - s.HysteresisDb * 100);
            wLen[1] = Math.Max(MinHoldMs, s.Hold1); wRate[1] = Math.Min(MaxPreTriggerMs, s.Pre1);
            wLen[2] = Math.Max(MinHoldMs, s.Hold2); wRate[2] = Math.Min(MaxPreTriggerMs, s.Pre2);
            return (bitmask, up, dn);
        }

        // ── what a setting means at a site ──

        public sealed record Site(string Name, int BackgroundDbSpl, string Example);
        public static readonly Site[] Sites =
        {
            new("Quiet", 30, "night forest, remote meadow"),
            new("Moderate", 45, "suburb, farmland, daytime forest"),
            new("Noisy", 60, "near roads, streams, busy colonies"),
        };

        /// <summary>The sound level that starts a recording, in dB SPL, for a site background.</summary>
        public static int StartDbSpl(Settings s, int backgroundDbSpl) => s.Absolute ? s.StartDb : backgroundDbSpl + s.StartDb;

        /// <summary>Recorded data per minute of recording (16-bit samples).</summary>
        public static double MegabytesPerMinute(uint fs, int mics) => fs * 2.0 * Math.Max(1, mics) * 60 / 1e6;

        // ── presets ──

        public sealed record Preset(string Name, string Description, uint Rate, bool Triggered, bool Ultrasound, int StartDb,
                                    uint HoldMs, uint PreMs);
        public static readonly Preset[] Presets =
        {
            new("Bats", "Ultrasonic echolocation and social calls: 200 kHz, triggers only on ultrasound so wind and insects below 15 kHz do not start recordings.",
                200000, true, true, 12, 1000, 25),
            new("Birds & audible wildlife", "Songs, calls and frogs: 48 kHz, full audible band, a longer hold so whole songs stay in one file.",
                48000, true, false, 10, 3000, 100),
            new("Insects & general ultrasonic", "Crickets, katydids and other ultrasound up to ~100 kHz: 200 kHz, full band, a lower trigger level.",
                200000, true, false, 9, 2000, 25),
            new("Soundscape", "Everything, all the time: 48 kHz continuous recording (no trigger).",
                48000, false, false, 12, 2000, 25),
        };

        /// <summary>Plain-language description of what the unit will do (for the helper and the Solve window).</summary>
        public static string Summary(Settings s, uint rate1, uint rate2, MicChainSolver.Product product, int mics, Site site)
        {
            var ci = CultureInfo.InvariantCulture;
            uint refFs = MicChainSolver.RefRate(rate1, rate2, 0);
            if (refFs == 0) return "Both sample rates are Off: the schedule does not use the microphones.";
            double mb = MegabytesPerMinute(refFs, mics);
            if (!s.On)
                return string.Create(ci, $"Records continuously at {MicChainSolver.Hz(refFs)} (duty-cycle windows apply if set): about {mb:0} MB per minute of recording.");

            int spl = StartDbSpl(s, site.BackgroundDbSpl);
            string level = s.Absolute
                ? $"when the sound reaches {s.StartDb} dB SPL"
                : string.Create(ci, $"when the sound rises {s.StartDb} dB above the background (about {spl} dB SPL at a {site.Name.ToLowerInvariant()} site, ~{site.BackgroundDbSpl} dB SPL)");
            string band = s.Ultrasound && !s.Hardware ? " Only ultrasound (above 15 kHz) can start a recording." : "";
            int hyst = s.HysteresisDb == 0 ? DefaultHysteresisCdb / 100 : s.HysteresisDb;
            string detector = s.Hardware
                ? "Hardware detector: the unit sleeps until a sound arrives — the longest battery life; no pre-trigger and no ultrasound-only option."
                : product.HasHardwareDetector
                    ? "Software detector: pre-trigger and the ultrasound-only option; uses more power than the hardware detector (the processor checks every buffer)."
                    : "The processor checks every audio buffer for the trigger (software detector).";
            uint maxPre = PreTriggerMaxMs(product, refFs);
            string pre = s.Hardware || s.Pre1 == 0 ? "" : string.Create(ci, $", including the {Math.Min(s.Pre1, maxPre)} ms before it");
            string rate = MicChainSolver.Solve(refFs, 0) is { Inexact: true } c ? string.Create(ci, $"{MicChainSolver.Hz(refFs)} (runs at {c.ExactHz:0.#} Hz)") : MicChainSolver.Hz(refFs);
            return string.Create(ci, $"Records at {rate} {level}{pre}, and keeps recording until it has been {hyst} dB quieter for {s.Hold1 / 1000.0:0.#} s.") +
                   band + " Each recording is a separate file." + " " + detector +
                   string.Create(ci, $" About {mb:0} MB per minute of recorded sound.");
        }

        /// <summary>The Solve window's trigger card: what the trigger does, and where its start level sits in each
        /// configured chain relative to the microphone's own noise and to clipping.</summary>
        public static MicChainSolver.SolverSection SolveSection(Settings s, uint rate1, uint rate2, ushort cfgGain,
                                                                 MicChainSolver.Product product, int mics)
        {
            var ci = CultureInfo.InvariantCulture;
            var lines = new List<MicChainSolver.SolverLine> { new(Summary(s, rate1, rate2, product, mics, Sites[1])) };
            uint refFs = MicChainSolver.RefRate(rate1, rate2, 0);
            int level = MicChainSolver.LevelCdb(refFs, MicChainSolver.GainCode(cfgGain));
            int spl = StartDbSpl(s, Sites[1].BackgroundDbSpl);
            foreach ((int idx, uint fs) in new[] { (1, rate1), (2, rate2) })
            {
                if (fs == 0) continue;
                var c = MicChainSolver.Solve(fs, level)!;
                double dbfs = spl - 94.0 + MicChainSolver.MicSensitivityDbfs + c.LevelCdb / 100.0;
                lines.Add(new(string.Create(ci, $"Sample Rate [{idx}]: a {spl} dB SPL start level is {dbfs:0} dBFS in the recording.") +
                              (dbfs > -3 ? " That is at clipping: lower the gain or raise the level." : ""), Warning: dbfs > -3));
            }
            double selfNoise = 94.0 - MicChainSolver.MicSnrDb;
            if (spl < selfNoise + 6)
                lines.Add(new(string.Create(ci, $"{spl} dB SPL is close to the microphone's own noise (about {selfNoise:0} dB SPL): it may trigger on its own noise."), Warning: true));
            if (!s.Absolute)
                lines.Add(new("The level is relative to the background, so the dB SPL figures assume a moderate site (~45 dB SPL); the unit tracks the real background.", Detail: true));
            lines.Add(new(FirmwareNote(product), Detail: true));
            return new MicChainSolver.SolverSection("Sound-triggered recording", lines);
        }

        public static string FirmwareNote(MicChainSolver.Product p) =>
            $"Sound-triggered recording needs {p.Name} firmware {p.FirmwareVersion} or later. Older firmware ignores the trigger and records continuously.";
    }
}
