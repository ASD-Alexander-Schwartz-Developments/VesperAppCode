using System;
using System.Globalization;
using System.IO;

namespace VesperApp.Services
{
    /// <summary>
    /// The ExG front-end gain a recording was made with. The firmware copies config.json "eegconf1" into the
    /// file header (vesper.h _StoreFileHeader: config_data_data[0] = raw_data[0]); BinaryParser writes it to the
    /// intermediate's .txt sidecar as "Config0:" (hex). The gain code is the ADS129x CHnSET GAIN[2:0]
    /// (000 = x6, 001 = x1, 010 = x2, 011 = x3, 100 = x4, 101 = x8, 110 = x12), at the bits the firmware reads it:
    /// <list type="bullet">
    /// <item>EXG48 (ADS1298, VesperU5 vesper.h eeg_params.ggain): bits 16..18</item>
    /// <item>EXG2 (ADS1292 / ADS1192, exg2_params.ggain): bits 3..5</item>
    /// </list>
    /// </summary>
    public static class ExgGain
    {
        /// <summary>No gain known (no sidecar / no Config0): the decoders keep x1.</summary>
        public const uint Unknown = 0xFF;

        public const int Exg48GainShift = 16;
        public const int Exg2GainShift = 3;

        /// <summary>Whether the "Sensor:" name is the 2-channel EXG1292 sensor (else EXG48).</summary>
        public static bool IsExg2(string? sensor) =>
            sensor != null && sensor.Trim().Equals("EXG2", StringComparison.OrdinalIgnoreCase);

        /// <summary>Gain code from the header's first config word (= eegconf1).</summary>
        public static uint CodeFromConfig0(bool exg2, uint config0) =>
            (config0 >> (exg2 ? Exg2GainShift : Exg48GainShift)) & 0x07u;

        /// <summary>
        /// Gain code of an intermediate (.EBN) from its .txt sidecar ("Config0:"), or <see cref="Unknown"/>.
        /// <paramref name="exg2"/> null: decided by the sidecar's "Sensor:" line.
        /// </summary>
        public static uint CodeFromSidecar(string sidecarPath, bool? exg2 = null)
        {
            try
            {
                if (!File.Exists(sidecarPath)) return Unknown;

                string? sensor = null, config0 = null;
                foreach (string raw in File.ReadAllLines(sidecarPath))
                {
                    string line = raw.Trim();
                    int colon = line.IndexOf(':');
                    if (colon < 0) continue;
                    string key = line.Substring(0, colon).Trim();
                    string val = line.Substring(colon + 1).Trim();
                    if (key.Equals("Sensor", StringComparison.OrdinalIgnoreCase)) sensor ??= val;
                    else if (key.Equals("Config0", StringComparison.OrdinalIgnoreCase)) config0 ??= val;
                }

                if (config0 == null || !uint.TryParse(config0, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint c0))
                    return Unknown;
                return CodeFromConfig0(exg2 ?? IsExg2(sensor), c0);
            }
            catch (IOException) { return Unknown; }
            catch (UnauthorizedAccessException) { return Unknown; }
        }
    }
}
