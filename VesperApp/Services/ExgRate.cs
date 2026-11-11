using System;
using System.Globalization;
using System.IO;

namespace VesperApp.Services
{
    /// <summary>
    /// EXG48 (ADS1294 / 1296 / 1298) sample period for the decoders. The firmware writes config.json "sampleRate"
    /// straight into CONFIG1 DR[2:0] (VesperU5 ads1298.c) and the mode bit HR from "eegconf1" bit 0 (vesper.h
    /// eeg_params.hr; VesperApp "Enable High Performance Mode"). The file header carries both: the .txt sidecar's
    /// "SampleRate:" (the code) and "Config0:" (eegconf1, hex).
    /// <list type="bullet">
    /// <item>High resolution: 32 kSPS / 2^code (0 = 32 k ... 6 = 500 SPS)</item>
    /// <item>Low power: half of that (0 = 16 k ... 6 = 250 SPS)</item>
    /// </list>
    /// Before 2026-11-11 both decoders used 1000 / code µs: code 5 (500 SPS low power) became a 200 µs step, so a 20 s
    /// file's rows covered 2 s (bench 537A5024, asd-interfaces release-bringup-report 5g, item 22).
    /// </summary>
    public static class ExgRate
    {
        /// <summary>Samples per second of an EXG48 rate code; codes 0..6 per the ADS129x table, values from 125 up are
        /// taken as samples per second already; anything else 0 (unknown).</summary>
        public static double Exg48SamplesPerSecond(uint code, bool highResolution)
        {
            if (code <= 6)
                return (highResolution ? 32000.0 : 16000.0) / (1u << (int)code);
            return code >= 125 ? code : 0.0;
        }

        /// <summary>Sample period in µs (0 = unknown).</summary>
        public static double Exg48UsPerSample(uint code, bool highResolution)
        {
            double sps = Exg48SamplesPerSecond(code, highResolution);
            return sps > 0 ? 1e6 / sps : 0.0;
        }

        /// <summary>HR bit from the header's first config word (= eegconf1).</summary>
        public static bool Exg48HighResolution(uint config0) => (config0 & 0x01u) != 0;

        /// <summary>HR bit from an intermediate's .txt sidecar ("Config0:", hex); false when missing.</summary>
        public static bool Exg48HighResolutionFromSidecar(string sidecarPath)
        {
            try
            {
                if (!File.Exists(sidecarPath)) return false;
                foreach (string raw in File.ReadAllLines(sidecarPath))
                {
                    string line = raw.Trim();
                    int colon = line.IndexOf(':');
                    if (colon < 0) continue;
                    if (!line.Substring(0, colon).Trim().Equals("Config0", StringComparison.OrdinalIgnoreCase)) continue;
                    return uint.TryParse(line.Substring(colon + 1).Trim(), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint c0)
                           && Exg48HighResolution(c0);
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            return false;
        }
    }
}
