using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace VesperApp.Services
{
    /// <summary>
    /// Decodes the LOGGER pseudo-sensor (KOL / VesperU5 firmware, logger.c) - raw /log/&lt;n&gt;H.BIN,
    /// split by BinaryParser into .HBN intermediates (16-byte start block + records).
    /// Contract: asd-interfaces interfaces/logger-file.md. Each record: u8 type, u8 size (bytes,
    /// header included, multiple of 4), u16 info, u32 RTC seconds since 2000-01-01, payload.
    ///   type 0x01 sample: info = fields present (bit 0 battery mV, 1 temperature 0.01 degC,
    ///                     2 RTC correction ppb, 3 sensor bitmap), one int32 per field in bit order
    ///   type 0x11..0x13 log: level 1 error / 2 warning / 3 debug, info = code, no payload
    /// Output: &lt;file&gt;.csv (samples) and &lt;file&gt;.log.csv (events).
    /// </summary>
    public class LoggerParser : IDisposable
    {
        public const int StartBlockLength = 16;
        public const byte RecSample = 0x01;
        public const byte RecLog = 0x10;
        public const int TempNone = unchecked((int)0x80000000);

        private static readonly DateTime Epoch = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);

        private readonly string filename;
        private List<string>? samples;
        private List<string>? events;

        public int BadRecords { get; private set; }

        /// <param name="format">Config1 of the file header: format version | device type &lt;&lt; 8.</param>
        public LoggerParser(string filename, byte[] data, uint format)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            this.filename = filename;
            string sep = Utils.GetSeparator();
            byte deviceType = (byte)(format >> 8);

            samples = new List<string> { $"Time{sep}Battery [mV]{sep}MCU temperature [C]{sep}RTC correction [ppb]{sep}Sampling sensors" };
            events = new List<string> { $"Time{sep}Level{sep}Code{sep}Event" };

            int index = (data.Length >= StartBlockLength && BitConverter.ToUInt32(data, 0) == 0xA55AA55A) ? StartBlockLength : 0;
            while (index + 8 <= data.Length)
            {
                byte type = data[index];
                int size = data[index + 1];
                ushort info = BitConverter.ToUInt16(data, index + 2);
                uint secs = BitConverter.ToUInt32(data, index + 4);

                bool ok = size >= 8 && (size & 3) == 0 && index + size <= data.Length
                          && (type == RecSample || (type > RecLog && type <= RecLog + 3));
                if (!ok)
                {
                    BadRecords++;
                    index += 4;                         // records are 4-byte aligned: resynchronise
                    continue;
                }

                string time = Epoch.AddSeconds(secs).ToString("yyyy-MM-dd HH:mm:ss");
                if (type == RecSample)
                {
                    string[] col = { "", "", "", "" };
                    int p = index + 8;
                    for (int bit = 0; bit < 4; bit++)
                    {
                        if ((info & (1 << bit)) == 0) continue;
                        if (p + 4 > index + size) break;
                        int v = BitConverter.ToInt32(data, p);
                        p += 4;
                        col[bit] = bit switch
                        {
                            0 => v < 0 ? "" : v.ToString(),
                            1 => v == TempNone ? "" : (v / 100.0).ToString("F2"),
                            2 => v.ToString(),
                            _ => SensorNames(deviceType, (uint)v),
                        };
                    }
                    samples.Add(time + sep + string.Join(sep, col));
                }
                else
                {
                    int level = type - RecLog;
                    events.Add(time + sep + LevelName(level) + sep + $"0x{info:X4}" + sep + CodeName(info));
                }
                index += size;
            }
        }

        public static string LevelName(int level) => level switch
        {
            1 => "error",
            2 => "warning",
            3 => "debug",
            _ => level.ToString(),
        };

        /// <summary>Firmware log codes (logger_codes.h, same catalogue in KOL and VesperU5).</summary>
        public static readonly IReadOnlyDictionary<ushort, string> Codes = new Dictionary<ushort, string>
        {
            [0x0101] = "Firmware started",
            [0x0102] = "Reset: power-on / brown-out",
            [0x0103] = "Reset: NRST pin",
            [0x0104] = "Reset: software",
            [0x0105] = "Reset: watchdog",
            [0x0106] = "Reset: illegal low-power entry",
            [0x0107] = "Error handler ran since the previous boot",
            [0x0108] = "Log records dropped (buffer full)",
            [0x0201] = "Data file could not be created / opened",
            [0x0202] = "Write to a data file failed",
            [0x0203] = "Storage full",
            [0x0204] = "Storage flush failed",
            [0x0301] = "Battery reading not possible",
            [0x0302] = "USB power attached",
            [0x0303] = "USB power removed",
            [0x0401] = "MCU temperature reading not possible",
            [0x0402] = "Microphone clock lock re-armed",
            [0x0403] = "Microphone clock trimmed",
            [0x0404] = "RTC set by the host",
            [0x0405] = "RTC synchronised by the host",
            [0x0501] = "Recording session started",
            [0x0502] = "Recording session stopped",
            [0x0503] = "Schedule entry reached",
            [0x0504] = "Schedule finished",
            [0x0601] = "Audio window opened",
            [0x0602] = "Audio window closed",
            [0x0603] = "Audio samples missed (overrun)",
            [0x0604] = "Sound trigger event",
            [0x0701] = "Sensor started",
            [0x0702] = "Sensor stopped",
            [0x0703] = "Configured sensor not found",
            [0x0704] = "Sensor reading failed",
        };

        public static string CodeName(ushort code) => Codes.TryGetValue(code, out string? s) ? s : "unknown";

        // Bit = firmware driver type (SENSOR_Typedef), which differs per firmware family.
        private static readonly string[] KolSensors =
            { "DMIKE", "KOL", "EADC", "IMU", "EEG", "THCAMERA", "ALS", "AUX4", "TPRH", "LED", "LOGGER" };
        private static readonly string[] VesperSensors =
            { "GPS", "DMIKE", "MIC", "EADC", "IMU", "EEG", "THCAMERA", "ALS3001D", "AUX4", "TPRH31", "LED", "RADIO", "PROXTIT", "LOGGER" };

        public static string SensorNames(byte deviceType, uint bits)
        {
            string[] names = deviceType == 3 ? KolSensors : VesperSensors;     // 3 = KOL (GET_VER device type)
            var on = new List<string>();
            for (int b = 0; b < 32; b++)
            {
                if ((bits & (1u << b)) == 0) continue;
                on.Add(b < names.Length ? names[b] : $"bit{b}");
            }
            return on.Count == 0 ? "none" : string.Join("+", on);
        }

        public void WriteFile()
        {
            try
            {
                if (samples != null && samples.Count > 1) File.WriteAllLines(filename + ".csv", samples);
                if (events != null && events.Count > 1) File.WriteAllLines(filename + ".log.csv", events);
            }
            catch (IOException)
            {
            }
        }

        public void Dispose()
        {
            samples = null;
            events = null;
        }
    }
}
