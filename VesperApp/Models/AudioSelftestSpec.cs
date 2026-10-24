using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VesperApp.Models
{
    /// <summary>
    /// Pass / fail thresholds of the microphone self-test (console 60) for KOL, Vesper and Pipistrelle,
    /// read from the embedded <c>Specs/audio-selftest-spec.json</c> (the factory suite should read the same
    /// file). The firmware only measures (audio_selftest.h); these values decide. "validated": false until
    /// the bench procedure has run on real units.
    /// </summary>
    public sealed class AudioSelftestSpec
    {
        public sealed class RequestSpec
        {
            [JsonPropertyName("sample_rate_hz")] public uint SampleRateHz { get; set; } = 48000;
            [JsonPropertyName("duration_ms")] public ushort DurationMs { get; set; } = 150;
            [JsonPropertyName("tones_hz")] public int[] TonesHz { get; set; } = { 1000, 4000 };
            [JsonPropertyName("snr_margin_db")] public byte SnrMarginDb { get; set; } = 12;
        }

        public sealed class StimulusSpec
        {
            [JsonPropertyName("kind")] public string Kind { get; set; } = "two-tone";
            [JsonPropertyName("description")] public string Description { get; set; } = "";
        }

        public sealed class PassSpec
        {
            [JsonPropertyName("tone_snr_min_db")] public int ToneSnrMinDb { get; set; } = 12;
            [JsonPropertyName("level_min_dbfs")] public double LevelMinDbfs { get; set; } = -70;
            [JsonPropertyName("level_max_dbfs")] public double LevelMaxDbfs { get; set; } = -1;
            [JsonPropertyName("dc_max_counts")] public int DcMaxCounts { get; set; } = 3000;
            [JsonPropertyName("clipping_allowed")] public bool ClippingAllowed { get; set; }
            [JsonPropertyName("max_channel_spread_db")] public double MaxChannelSpreadDb { get; set; } = 6;
        }

        public sealed class ProductSpec
        {
            [JsonPropertyName("channels")] public int Channels { get; set; }
            [JsonPropertyName("min_firmware")] public string MinFirmware { get; set; } = "";
        }

        [JsonPropertyName("schema")] public int Schema { get; set; }
        [JsonPropertyName("validated")] public bool Validated { get; set; }
        [JsonPropertyName("request")] public RequestSpec Request { get; set; } = new();
        [JsonPropertyName("stimulus")] public StimulusSpec Stimulus { get; set; } = new();
        [JsonPropertyName("pass")] public PassSpec Pass { get; set; } = new();
        [JsonPropertyName("products")] public Dictionary<string, ProductSpec> Products { get; set; } = new();

        public const string ResourceName = "VesperApp.Specs.audio-selftest-spec.json";

        private static AudioSelftestSpec? _default;

        /// <summary>The embedded spec (falls back to the built-in defaults if it cannot be read).</summary>
        public static AudioSelftestSpec Default => _default ??= Load();

        public static AudioSelftestSpec Load()
        {
            try
            {
                using Stream? s = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName);
                if (s != null) return Parse(new StreamReader(s).ReadToEnd());
            }
            catch { /* defaults below */ }
            return new AudioSelftestSpec();
        }

        public static AudioSelftestSpec Parse(string json) =>
            JsonSerializer.Deserialize<AudioSelftestSpec>(json, new JsonSerializerOptions { ReadCommentHandling = JsonCommentHandling.Skip })
            ?? new AudioSelftestSpec();
    }

    /// <summary>A microphone's self-test verdict.</summary>
    public enum MicVerdict { Healthy, Weak, NoSignal, TooQuiet, Overloaded, Offset, Busy, NotPresent, NoResponse, Failed }

    /// <summary>Turns a self-test reply into a verdict with the spec's thresholds (no UI, testable).</summary>
    public static class AudioSelftestEvaluator
    {
        public static (MicVerdict Verdict, string Detail) Evaluate(AudioBenchResult? r, AudioSelftestSpec.PassSpec p)
        {
            var ci = CultureInfo.InvariantCulture;
            if (r == null)
                return (MicVerdict.NoResponse, "The device didn't reply. Make sure it's on and not recording, and that its firmware has the self-test.");

            string snr = string.Join("/", r.SnrDb) + " dB";
            bool tonesPass = r.NTones > 0 && r.SnrDb.All(s => s >= p.ToneSnrMinDb);

            if (!r.HasTrailer)   // older firmware: tones only
            {
                if (r.NoSignal) return (MicVerdict.NoSignal, "No audio captured — this microphone may be faulty or disconnected.");
                return tonesPass
                    ? (MicVerdict.Healthy, $"SNR {snr} · noise floor {r.NoiseFloorDb} dB")
                    : (MicVerdict.Weak, $"Low tone SNR {snr} — make sure a tone is playing close to the device.");
            }

            var st = r.Status;
            if ((st & AudioSelftestStatus.Busy) != 0)
                return (MicVerdict.Busy, "The microphones are busy (Live Audio or a recording). Return the unit to Idle and run the check again.");
            if ((st & AudioSelftestStatus.NoMic) != 0)
                return (MicVerdict.NotPresent, "This unit has no microphone with this number.");
            if ((st & AudioSelftestStatus.Captured) == 0)
                return (MicVerdict.Failed, (st & AudioSelftestStatus.BadRequest) != 0
                    ? "The device rejected the test settings."
                    : "The capture did not complete. Try again; if it repeats, the microphone path is faulty.");

            string level = r.LevelDbfs is double l ? string.Create(ci, $"{l:0.0} dBFS (~{l + 120:0} dB SPL)") : "no level";
            if ((st & AudioSelftestStatus.Stuck) != 0)
                return (MicVerdict.NoSignal, string.Create(ci, $"The microphone's data is stuck at {r.Dc} — it is faulty, unpowered or disconnected."));
            if (((st & AudioSelftestStatus.Clipped) != 0 && !p.ClippingAllowed) || (r.LevelDbfs is double hi && hi > p.LevelMaxDbfs))
                return (MicVerdict.Overloaded, $"Too loud ({level}, {r.ClippedSamples} clipped samples) — move the tone source further away and repeat.");
            if (Math.Abs(r.Dc) > p.DcMaxCounts)
                return (MicVerdict.Offset, string.Create(ci, $"Large DC offset ({r.Dc} counts) — the microphone or its supply may be faulty."));
            if (r.LevelDbfs is double lo && lo < p.LevelMinDbfs)
                return (MicVerdict.TooQuiet, $"Hardly any sound ({level}) — check that the tone is playing, or the microphone may be blocked or faulty.");
            return tonesPass
                ? (MicVerdict.Healthy, $"SNR {snr} · level {level}")
                : (MicVerdict.Weak, $"The microphone works ({level}) but the test tones are weak (SNR {snr}) — play them closer to the device.");
        }

        /// <summary>KOL: a warning when the healthy channels' levels differ by more than the spec allows
        /// (a blocked port or a weak microphone), else null.</summary>
        public static string? SpreadWarning(IReadOnlyList<AudioBenchResult?> results, AudioSelftestSpec.PassSpec p)
        {
            var levels = results.Where(r => r is { HasTrailer: true, LevelDbfs: not null } && Evaluate(r, p).Verdict == MicVerdict.Healthy)
                                .Select(r => r!.LevelDbfs!.Value).ToList();
            if (levels.Count < 2) return null;
            double spread = levels.Max() - levels.Min();
            return spread > p.MaxChannelSpreadDb
                ? string.Create(CultureInfo.InvariantCulture, $"The microphones' levels differ by {spread:0.0} dB (more than {p.MaxChannelSpreadDb:0} dB): check for a blocked port, or keep the tone source centred.")
                : null;
        }
    }
}
