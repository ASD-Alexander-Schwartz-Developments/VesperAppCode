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

        /// <summary>The biopotential board from the probe that answered and the chip id: EEG4 (ADS1294/6/8 and R
        /// parts, 4/6/8 channels), EXG2 (ADS1291/2, no pre-amp), EEG2 / EMG2 / ECG2 (ADS1191/2, one DAQ behind
        /// three pre-amps - not told apart).</summary>
        public static string ExgPart(byte variant, ushort id = 0) => variant switch
        {
            1 => $"EEG2 / EMG2 / ECG2 ({(id == 0x50 ? "ADS1191" : "ADS1192")})",
            2 => $"EXG2 ({((id & 0x01) == 0 ? "ADS1291" : "ADS1292")}{((id & 0x20) != 0 ? "R" : "")})",
            3 => $"EEG4 ({(id & 0x03) switch { 0 => "ADS1294, 4", 1 => "ADS1296, 6", _ => "ADS1298, 8" }} channels{((id & 0x40) != 0 ? ", R part" : "")})",
            _ => "biopotential",
        };

        /// <summary>Thermal camera (console 70, op-based): STATUS powers the camera up if needed and reports
        /// it; POWER_OFF ends the session. Request: op, arg0, arg1, reserved.</summary>
        public const byte LeptonOpStatus = 0, LeptonOpPowerOff = 8;
        public static byte[] LeptonRequest(byte op) => new byte[] { op, 0, 0, 0 };

        /// <summary>Reply: status (0 ok, 0xFF camera board not there), Lepton 3.x flag, radiometric flag, feature
        /// flags, sensor (FPA) and housing temperature x100 °C (int16), spot K x100.</summary>
        public static (string status, IBrush brush, string detail) DescribeLepton(byte[]? r)
        {
            if (r == null || r.Length < 8)
                return ("No answer", Brushes.OrangeRed, "The unit did not answer - it may be recording (stop it first) or its firmware has no camera test.");
            if (r[0] == StatusNotPresent)
                return ("Not found", Brushes.Gray, "No thermal camera board answered on the connector.");
            double fpa = BitConverter.ToInt16(r, 4) / 100.0, aux = BitConverter.ToInt16(r, 6) / 100.0;
            string part = $"Lepton {(r[1] != 0 ? "3.x" : "2.x")}{(r[2] != 0 ? ", radiometric" : "")}, sensor {fpa:F1} °C, housing {aux:F1} °C";
            return r[0] == StatusOk && fpa > -20 && fpa < 80
                ? ("Pass", Brushes.SeaGreen, part)
                : ("Check", Brushes.Goldenrod, "Found, but it did not report a plausible temperature (it may still be booting - run again): " + part);
        }

        /// <summary>DAQ24: the four input voltages (AIN1:AIN0 in Sample0, AIN3:AIN2 in Sample1, int16 codes;
        /// Sample2 µV per code), "-" for an input whose conversion did not finish (SubStatus bit clear).</summary>
        public static string DaqPart(SensorBenchResult r)
        {
            double uv = r.Sample2 != 0 ? r.Sample2 : 125;
            short[] code = { (short)(r.Sample0 & 0xFFFF), (short)(r.Sample0 >> 16), (short)(r.Sample1 & 0xFFFF), (short)(r.Sample1 >> 16) };
            var parts = new string[4];
            for (int i = 0; i < 4; i++)
                parts[i] = $"AIN{i} " + ((r.SubStatus & (1 << i)) != 0 ? $"{code[i] * uv / 1e6:F3} V" : "-");
            return string.Join(", ", parts) + (r.IdFound != 0 ? $" (ADS1115 at 0x{r.IdFound:X2})" : "");
        }

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
                MessageTypes.VESPER_TEST_DAQ => DaqPart(r),
                MessageTypes.VESPER_TEST_EEG => $"{ExgPart(r.SubStatus, r.IdFound)}, device id 0x{r.IdFound:X2}" + (r.IdExpected != 0 ? $" (expected 0x{r.IdExpected:X2})" : ""),
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
