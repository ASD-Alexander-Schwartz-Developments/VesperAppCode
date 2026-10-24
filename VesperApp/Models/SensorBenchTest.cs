using System;
using Avalonia.Media;
using ReactiveUI;
using VesperApp.Services;

namespace VesperApp.Models
{
    /// <summary>
    /// Live sensor test (console 71 IMU, 72 EXG, 73 temperature / humidity, 74 light) - the firmware
    /// probes the add-on itself (Idle only, no configuration needed) and answers with presence, device
    /// id and one live sample. Contract: asd-interfaces interfaces/sensor-test.md; VesperU5
    /// bench_test.c bench_sensor_run, KOL sensor_test.c (IMU and TPRH only).
    /// </summary>
    public static class SensorBenchTest
    {
        public const byte StatusOk = 0, StatusIdFail = 1, StatusSampleFail = 2, StatusSubFail = 3, StatusNotPresent = 0xFF;

        /// <summary>Request: op 0, arg0 = variant (IMU 1 IMU10 / 2 IMU10HT, EXG 1 ADS1192 / 2 ADS1292 / 3 ADS1298, 0 = probe each).</summary>
        public static byte[] BuildRequest(byte variant = 0) => new byte[] { 0, variant, 0, 0 };

        public static SensorBenchResult? ParseResponse(byte[]? r)
        {
            if (r == null || r.Length < 20) return null;
            return new SensorBenchResult
            {
                Status = r[0],
                SubStatus = r[1],
                IdFound = BitConverter.ToUInt16(r, 2),
                IdExpected = BitConverter.ToUInt16(r, 4),
                Sample0 = BitConverter.ToInt32(r, 8),
                Sample1 = BitConverter.ToInt32(r, 12),
                Sample2 = BitConverter.ToInt32(r, 16),
            };
        }

        public static string ImuPart(ushort id) => id switch
        {
            0x6A => "LSM6DSM",
            0x69 => "LSM6DS3",
            0x6C => "LSM6DSO",
            0xFA => "ICM-20680 (IMU10HT)",
            _ => $"id 0x{id:X2}",
        };

        public static string ExgPart(byte variant) => variant switch
        {
            1 => "ADS1192 (EAG2)",
            2 => "ADS1292 (EXG2)",
            3 => "ADS1298 (EXG48)",
            _ => "EXG",
        };

        /// <summary>Verdict and a readable detail line for one sensor.</summary>
        public static (string status, IBrush brush, string detail) Describe(MessageTypes test, SensorBenchResult? r)
        {
            if (r == null)
                return ("No answer", Brushes.OrangeRed, "The unit did not answer - it may be recording (stop it first) or its firmware has no sensor test.");

            string sample = test switch
            {
                MessageTypes.VESPER_TEST_IMU => $"{ImuPart(r.IdFound)}, acceleration {r.Sample0 / 1000.0:F2} g (about 1 g at rest)",
                MessageTypes.VESPER_TEST_TPRH => $"{r.Sample0 / 100.0:F2} °C, {r.Sample1 / 100.0:F1} %RH" + (r.Sample2 == 0 ? ", checksum error" : ""),
                MessageTypes.VESPER_TEST_ALS => $"{r.Sample0 / 100.0:F2} lux (device id 0x{r.IdFound:X4})",
                MessageTypes.VESPER_TEST_EEG => $"{ExgPart(r.SubStatus)}, device id 0x{r.IdFound:X2}" + (r.IdExpected != 0 ? $" (expected 0x{r.IdExpected:X2})" : ""),
                _ => "",
            };

            return r.Status switch
            {
                StatusOk => ("Pass", Brushes.SeaGreen, sample),
                StatusSampleFail => ("Check", Brushes.Goldenrod, "Found, but the live reading is out of range: " + sample),
                StatusSubFail => ("Partial", Brushes.Goldenrod, "Found, but a sub-sensor (magnetometer / barometer) did not answer: " + sample),
                StatusIdFail => ("Not found", Brushes.OrangeRed,
                    r.IdFound != 0 ? $"Wrong device id 0x{r.IdFound:X2} - another add-on, or a damaged part." : "No add-on of this type answered on the connector."),
                StatusNotPresent => ("Not found", Brushes.Gray, "No add-on of this type answered (or the bus could not start)."),
                _ => ($"Status {r.Status}", Brushes.OrangeRed, sample),
            };
        }
    }

    public class SensorBenchResult
    {
        public byte Status { get; init; }
        public byte SubStatus { get; init; }
        public ushort IdFound { get; init; }
        public ushort IdExpected { get; init; }
        public int Sample0 { get; init; }
        public int Sample1 { get; init; }
        public int Sample2 { get; init; }
    }

    /// <summary>One row of the Sensors tab.</summary>
    public class SensorTestRow : ReactiveObject
    {
        public MessageTypes Test { get; }
        public string Label { get; }
        public string Hint { get; }

        public SensorTestRow(MessageTypes test, string label, string hint) { Test = test; Label = label; Hint = hint; }

        private string _status = "Not tested";
        public string Status { get => _status; set => this.RaiseAndSetIfChanged(ref _status, value); }

        private string _detail = string.Empty;
        public string Detail { get => _detail; set => this.RaiseAndSetIfChanged(ref _detail, value); }

        private IBrush _statusBrush = Brushes.Gray;
        public IBrush StatusBrush { get => _statusBrush; set => this.RaiseAndSetIfChanged(ref _statusBrush, value); }
    }
}
