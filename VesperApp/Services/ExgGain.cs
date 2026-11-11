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
    /// <item>EXG2 (ADS1292) and EEG2 / EMG2 / ECG2 (ADS1192), exg2_params.ggain: bits 3..5</item>
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

        /// <summary>
        /// Whether the "Sensor:" name is an EEG2 / EMG2 / ECG2 board (ADS1192, 16-bit). VesperU5 writes the config
        /// name into the header: "EEG2", "EMG2", "ECG2", or "EAG2" (the v1.20 config name, no variant).
        /// </summary>
        public static bool IsAds1192(string? sensor) => Ads1192PreampGain(sensor) > 0;

        /// <summary>
        /// Pre-amplifier gain of an EEG2 / EMG2 / ECG2 board: 1 + 2 x 499 kOhm / R3 (R11 for CH2), schematic
        /// VT01-EEG2: EEG2 499 Ohm -> 2001, EMG2 1 kOhm -> 999, ECG2 4.99 kOhm -> 201. "EAG2" (variant unknown) -> 1:
        /// the values are then at the ADC input. 0 = not an ADS1192 sensor.
        /// </summary>
        public static double Ads1192PreampGain(string? sensor) => (sensor ?? "").Trim().ToUpperInvariant() switch
        {
            "EEG2" => 2001.0,
            "EMG2" => 999.0,
            "ECG2" => 201.0,
            "EAG2" => 1.0,
            _ => 0.0,
        };

        /// <summary>µV of an ADS1x9x code of <paramref name="bits"/> bits: full scale VREF / gain at 2^(bits-1). The one
        /// scale of every ExG decoder and of the Device Tests test-signal check.</summary>
        public static double MicroVolts(double code, int bits, double vrefMicroVolts, double gain) =>
            code / Math.Pow(2, bits - 1) * vrefMicroVolts / gain;

        /// <summary>µV of a 24-bit ADS1292 / ADS129x code: full scale VREF / gain at 2^23 (before 1.22 the decoders
        /// divided by 2^24, half the true value).</summary>
        public static double MicroVolts24(int code, double vrefMicroVolts, double gain) => MicroVolts(code, 24, vrefMicroVolts, gain);

        /// <summary>µV of a 16-bit ADS1192 code: full scale VREF / gain at 2^15.</summary>
        public static double MicroVolts16(int code, double vrefMicroVolts, double gain) => MicroVolts(code, 16, vrefMicroVolts, gain);

        /// <summary>Input mux of an EXG2 / EEG2-family channel (CHnSET MUX[3:0]) from Config0 (= eegconf1): channel 1
        /// bits 16..19, channel 2 bits 20..23. 0 = normal electrode input; 5 = internal test signal.</summary>
        public static uint Exg2Mux(uint config0, int channel) => (config0 >> (channel == 1 ? 16 : 20)) & 0x0Fu;

        /// <summary>Pre-amp gain that applies to channel 1 / 2 of an ADS1192 recording: the board's (from the name) when
        /// the channel's mux is the electrode input, x1 for an internal input (test signal, short, supply, temperature,
        /// RLD): those do not pass the pre-amp.</summary>
        public static double Ads1192ChannelPreampGain(string? sensor, uint? config0, int channel) =>
            (config0.HasValue && Exg2Mux(config0.Value, channel) != 0) ? 1.0 : Math.Max(1.0, Ads1192PreampGain(sensor));

        /// <summary>Config0 (hex) of an intermediate's .txt sidecar, or null.</summary>
        public static uint? Config0FromSidecar(string sidecarPath)
        {
            try
            {
                if (!File.Exists(sidecarPath)) return null;
                foreach (string raw in File.ReadAllLines(sidecarPath))
                {
                    string line = raw.Trim();
                    if (line.StartsWith("Config0:", StringComparison.OrdinalIgnoreCase) &&
                        uint.TryParse(line.Substring(8).Trim(), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint v))
                        return v;
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            return null;
        }

        /// <summary>Gain code from the header's first config word (= eegconf1).</summary>
        public static uint CodeFromConfig0(bool exg2, uint config0) =>
            (config0 >> (exg2 ? Exg2GainShift : Exg48GainShift)) & 0x07u;

        /// <summary>The "Sensor:" name of an intermediate's .txt sidecar, or null.</summary>
        public static string? SensorFromSidecar(string sidecarPath)
        {
            try
            {
                if (!File.Exists(sidecarPath)) return null;
                foreach (string raw in File.ReadAllLines(sidecarPath))
                {
                    string line = raw.Trim();
                    if (line.StartsWith("Sensor:", StringComparison.OrdinalIgnoreCase))
                        return line.Substring(7).Trim();
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            return null;
        }

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
                return CodeFromConfig0(exg2 ?? (IsExg2(sensor) || IsAds1192(sensor)), c0);
            }
            catch (IOException) { return Unknown; }
            catch (UnauthorizedAccessException) { return Unknown; }
        }
    }
}
