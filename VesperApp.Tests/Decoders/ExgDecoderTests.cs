using System.Globalization;
using VesperApp.Models;
using VesperApp.Services;
using Xunit;

namespace VesperApp.Tests.Decoders;

/// <summary>
/// ExG (.EBN) decoding uses the gain the unit recorded with: the header's Config0 (= config.json eegconf1, copied
/// by VesperU5 _StoreFileHeader) carries the ADS129x gain code at the bits the firmware reads it (EXG48 / ADS1298
/// eeg_params.ggain bits 16..18, EXG2 / ADS1292 exg2_params.ggain bits 3..5). Synthetic records follow the parsers'
/// layouts: EXG2 = 0xC0|status, status, pad, CH1 (24-bit MSB first), CH2; EXG48 = 0xC0, LOFFP, LOFFN, CH1..CH8.
/// </summary>
public class ExgDecoderTests
{
    private const string Name = "E2026_10_25_09_30_15_000-E2026_10_25_09_31_15_000.EBN";

    private static byte[] S24(int code) => new[] { (byte)(code >> 16), (byte)(code >> 8), (byte)code };

    private static byte[] Exg2Record(int ch1, int ch2) => Raw.Concat(new byte[] { 0xC0, 0x00, 0x00 }, S24(ch1), S24(ch2));

    private static byte[] Exg48Record(params int[] ch)
        => Raw.Concat(new byte[] { 0xC0, 0x00, 0x00 }, Raw.Concat(ch.Select(S24).ToArray()));

    /// <summary>µV of a 24-bit ADS1292 / ADS129x code: full scale = VREF / gain at code 2^23 (the datasheets' ideal output
    /// code). Before item 25 (release-bringup-report 5h) the decoders divided by 2^24: half the true value.</summary>
    private static double MicroVolts(int code, double vrefMicroVolts, double gain) => code / 8388608.0 * vrefMicroVolts / gain;

    /// <summary>ADS1192 record as VesperU5 1.22 writes it: 6-byte frame (status 2 bytes, CH1, CH2 16-bit) + the frame's
    /// first 3 bytes again (the driver reads 9).</summary>
    private static byte[] Ads1192Record(short ch1, short ch2, byte s0 = 0xC0, byte s1 = 0x20)
    {
        var f = new byte[] { s0, s1, (byte)(ch1 >> 8), (byte)ch1, (byte)(ch2 >> 8), (byte)ch2 };
        return Raw.Concat(f, new[] { f[0], f[1], f[2] });
    }

    /// <summary>The CSV keeps 4 decimals.</summary>
    private static void Near(double expected, double actual)
        => Assert.True(Math.Abs(expected - actual) <= 0.00051, $"expected {expected}, got {actual}");

    private static double Col(string line, int col) => double.Parse(line.Split(',')[col], CultureInfo.InvariantCulture);

    [Theory]
    [InlineData(0u, 0x00000000u, 0)]        // eegconf1 = 0: the firmware runs x6
    [InlineData(0u, 0x00000008u, 1)]        // EXG2 ggain bits 3..5 = 001: x1
    [InlineData(0u, 0x00000030u, 6)]        // 110: x12
    [InlineData(1u, 0x00060000u, 6)]        // EXG48 ggain bits 16..18 = 110: x12
    [InlineData(1u, 0x00050001u, 5)]        // 101 (x8), High Resolution bit 0 does not count
    [InlineData(1u, 0x00000007u, 0)]        // bits 0..2 are not the EXG48 gain
    public void GainCode_FromConfig0_AtTheFirmwareBits(uint exg48, uint config0, uint expected)
        => Assert.Equal(expected, ExgGain.CodeFromConfig0(exg48 == 0, config0));

    [Theory]
    [InlineData((byte)0, 6.0)]
    [InlineData((byte)1, 1.0)]
    [InlineData((byte)5, 8.0)]
    [InlineData((byte)6, 12.0)]
    [InlineData((byte)7, 1.0)]              // reserved code: no exception, x1
    public void GainMultiplier(byte code, double gain) => Assert.Equal(gain, EXGGainOptions.ToMultiplier(code));

    [Fact]
    public void ConfigDrivers_WriteTheGainWhereTheDecoderAndFirmwareReadIt()
    {
        var exg2 = new ConfigEXG2Driver { GlobalGain = EXGGainOptions.CreateFromValue(6) };
        Assert.Equal(6u << 3, exg2.EegConf1 & (7u << 3));
        Assert.Equal(6u, ExgGain.CodeFromConfig0(true, exg2.EegConf1));
        Assert.Equal(6, exg2.GlobalGain.Value);

        var exg48 = new ConfigEXG48Driver { GlobalGain = EXGGainOptions.CreateFromValue(5) };
        Assert.Equal(5u << 16, exg48.EegConf1 & (7u << 16));
        Assert.Equal(0u, exg48.EegConf1 & 0x0Fu);                 // High Resolution / sr untouched
        Assert.Equal(5u, ExgGain.CodeFromConfig0(false, exg48.EegConf1));
    }

    [Fact]
    public void Exg48WctSwitches_WriteEegconf4_AndLeaveTheGainAlone()
    {
        var exg48 = new ConfigEXG48Driver { GlobalGain = EXGGainOptions.CreateFromValue(6) };
        exg48.EnWCTB = true;
        Assert.True(exg48.EnWCTB);
        Assert.Equal(1u << 16, exg48.EegConf4 & (1u << 16));
        exg48.EnWCTB = false;
        Assert.False(exg48.EnWCTB);
        Assert.Equal(6, exg48.GlobalGain.Value);
    }

    [Theory]
    [InlineData("Config0:30", 12.0)]        // gain code 6 at bits 3..5
    [InlineData("Config0:10", 2.0)]         // code 2
    [InlineData("Config0:0", 6.0)]          // default eegconf1: the unit ran x6
    [InlineData(null, 1.0)]                 // no Config0 in the sidecar: unknown, x1 as before
    public async Task Pipeline_Exg2_DividesByTheRecordedGain(string? config0Line, double gain)
    {
        using var dir = new TempDir();
        string f = dir.File(Name);
        File.WriteAllBytes(f, Raw.Concat(Exg2Record(0x100000, -0x123456), Exg2Record(0x7FFFFF, 1), new byte[] { 0 }));
        var meta = new List<string> { "Sensor:EXG2", "SampleRate:2" };
        if (config0Line != null) meta.Add(config0Line);
        File.WriteAllLines(f + ".txt", meta);

        DecodeSummary sum = await RecordingPipeline.DecodeFilesAsync(new[] { f });

        Assert.Equal(1, sum.Csv);
        string[] lines = Fixtures.WaitForLines(f + ".csv", 3);
        Assert.Equal(3, lines.Length);
        Near(MicroVolts(0x100000, 2420000, gain), Col(lines[1], 2));
        Near(MicroVolts(-0x123456, 2420000, gain), Col(lines[1], 5));
        Near(MicroVolts(0x7FFFFF, 2420000, gain), Col(lines[2], 2));
    }

    [Theory]
    [InlineData("Config0:60001", 12.0)]     // code 6 at bits 16..18 (+ High Resolution)
    [InlineData("Config0:40000", 4.0)]
    [InlineData("Config0:1", 6.0)]          // ggain 000: x6
    public async Task Pipeline_Exg48_DividesByTheRecordedGain_WithFullResolution(string config0Line, double gain)
    {
        using var dir = new TempDir();
        string f = dir.File(Name);
        int[] ch = { 0x123456, -0x000101, 0x7FFFFF, -0x800000, 1, -1, 0x00ABCD, 0 };
        File.WriteAllBytes(f, Raw.Concat(Exg48Record(ch), new byte[] { 0 }));
        File.WriteAllLines(f + ".txt", new[] { "Sensor:EXG48", "SampleRate:250", config0Line });

        DecodeSummary sum = await RecordingPipeline.DecodeFilesAsync(new[] { f });

        Assert.Equal(1, sum.Csv);
        string[] lines = Fixtures.WaitForLines(f + ".csv", 2);
        Assert.Equal(2, lines.Length);
        for (int i = 0; i < ch.Length; i++)
            Near(MicroVolts(ch[i], 2400000, gain), Col(lines[1], 2 + 3 * i));
    }

    /// <summary>EXG48 rate code -> samples per second: CONFIG1 DR[2:0] with the HR bit (eegconf1 bit 0); item 22,
    /// asd-interfaces release-bringup-report 5g. Before, both decoders used 1000 / code µs.</summary>
    [Theory]
    [InlineData(5u, false, 500.0)]          // the bench recording E01 (low power)
    [InlineData(4u, false, 1000.0)]
    [InlineData(6u, false, 250.0)]
    [InlineData(0u, false, 16000.0)]
    [InlineData(5u, true, 1000.0)]          // VesperApp SR_1K_HR
    [InlineData(6u, true, 500.0)]
    [InlineData(0u, true, 32000.0)]
    [InlineData(250u, false, 250.0)]        // a value in SPS already (125 and up)
    [InlineData(7u, false, 0.0)]            // DR 111 is not a rate
    public void Exg48Rate_FollowsTheAds129xTable(uint code, bool hr, double sps)
    {
        Assert.Equal(sps, ExgRate.Exg48SamplesPerSecond(code, hr));
        Assert.Equal(sps > 0 ? 1e6 / sps : 0.0, ExgRate.Exg48UsPerSample(code, hr), 6);
    }

    [Fact]
    public void Exg48HighResolution_IsConfig0Bit0_FromTheSidecar()
    {
        using var dir = new TempDir();
        string s = dir.File("a.EBN.txt");
        Assert.False(ExgRate.Exg48HighResolutionFromSidecar(s));                  // no sidecar
        File.WriteAllLines(s, new[] { "Sensor:EXG48", "SampleRate:5", "Config0:60001" });
        Assert.True(ExgRate.Exg48HighResolutionFromSidecar(s));
        File.WriteAllLines(s, new[] { "Sensor:EXG48", "SampleRate:5", "Config0:60000" });
        Assert.False(ExgRate.Exg48HighResolutionFromSidecar(s));
    }

    [Theory]
    [InlineData("Config0:0", 2.0)]          // code 5, low power: 500 SPS
    [InlineData("Config0:1", 1.0)]          // code 5, high resolution: 1 kSPS
    public async Task Pipeline_Exg48_RowTimesStepByTheRecordedRate(string config0Line, double stepMs)
    {
        using var dir = new TempDir();
        string f = dir.File(Name);
        int[] ch = { 1, 2, 3, 4, 0, 0, 0, 0 };
        File.WriteAllBytes(f, Raw.Concat(Exg48Record(ch), Exg48Record(ch), Exg48Record(ch), new byte[] { 0 }));
        File.WriteAllLines(f + ".txt", new[] { "Sensor:EXG48", "SampleRate:5", config0Line });

        await RecordingPipeline.DecodeFilesAsync(new[] { f });

        string[] lines = Fixtures.WaitForLines(f + ".csv", 3);
        var start = new TimeSpan(9, 30, 15);
        for (int i = 1; i < lines.Length; i++)
        {
            // the date column follows the PC's culture; the time column is HH:mm:ss.fff
            TimeSpan ts = TimeSpan.ParseExact(lines[i].Split(',')[1], @"hh\:mm\:ss\.FFF", CultureInfo.InvariantCulture);
            Assert.Equal(i * stepMs, (ts - start).TotalMilliseconds, 3);
        }
    }

    [Fact]
    public void FullScale24_IsVrefOverGain()
    {
        Assert.Equal(2420000.0 * 8388607 / 8388608, ExgGain.MicroVolts24(0x7FFFFF, 2420000, 1), 6);
        Assert.Equal(-2400000.0 / 6, ExgGain.MicroVolts24(-0x800000, 2400000, 6), 6);
    }

    [Theory]
    [InlineData("EEG2", 2001.0)]
    [InlineData("emg2", 999.0)]
    [InlineData(" ECG2 ", 201.0)]
    [InlineData("EAG2", 1.0)]               // v1.20 config name: variant unknown, values at the ADC input
    [InlineData("EXG2", 0.0)]
    [InlineData("EXG48", 0.0)]
    [InlineData(null, 0.0)]
    public void Ads1192PreampGain_FromTheSensorName(string? sensor, double gain)
    {
        Assert.Equal(gain, ExgGain.Ads1192PreampGain(sensor));
        Assert.Equal(gain > 0, ExgGain.IsAds1192(sensor));
    }

    /// <summary>µV at the electrodes = code x 2.42 V / (2^15 x PGA x pre-amp gain); PGA from Config0 bits 3..5, pre-amp
    /// gain from the Sensor name (asd-interfaces release-bringup-report 5h, DS-VT04-EEG2 rev 1.2).</summary>
    [Theory]
    [InlineData("EEG2", "Config0:8", 1.0, 2001.0)]      // PGA x1
    [InlineData("EMG2", "Config0:0", 6.0, 999.0)]       // default eegconf1: PGA x6
    [InlineData("ECG2", "Config0:30", 12.0, 201.0)]     // PGA x12
    [InlineData("EAG2", "Config0:8", 1.0, 1.0)]
    public async Task Pipeline_Ads1192_ScalesWithPgaAndPreampGain(string sensor, string config0Line, double pga, double preamp)
    {
        using var dir = new TempDir();
        string f = dir.File(Name);
        File.WriteAllBytes(f, Raw.Concat(Ads1192Record(32767, -32768), Ads1192Record(-14, 1234)));
        File.WriteAllLines(f + ".txt", new[] { "Sensor:" + sensor, "SampleRate:3", config0Line });

        DecodeSummary sum = await RecordingPipeline.DecodeFilesAsync(new[] { f });

        Assert.Equal(1, sum.Csv);
        string[] lines = Fixtures.WaitForLines(f + ".csv", 3);
        Assert.Equal(3, lines.Length);
        double k = 2420000.0 / 32768 / (pga * preamp);
        Near(32767 * k, Col(lines[1], 2));
        Near(-32768 * k, Col(lines[1], 5));
        Near(-14 * k, Col(lines[2], 2));
        Near(1234 * k, Col(lines[2], 5));
        var start = new TimeSpan(9, 30, 15);                   // rate code 3 = 500 SPS: 2 ms per row
        TimeSpan t2 = TimeSpan.ParseExact(lines[2].Split(',')[1], @"hh\:mm\:ss\.FFF", CultureInfo.InvariantCulture);
        Assert.Equal(4.0, (t2 - start).TotalMilliseconds, 3);
    }

    [Fact]
    public void Ads1192Parser_LeadOffBits_AndResyncAfterAStrayByte()
    {
        using var dir = new TempDir();
        string f = dir.File(Name);
        // LOFF_STAT = RLD, IN2N, IN2P, IN1N (byte 0 low nibble) and IN1P (byte 1 bit 7); a stray byte between records
        byte[] data = Raw.Concat(Ads1192Record(100, 200, 0xCF, 0xA0), new byte[] { 0x55 }, Ads1192Record(1, 2));
        using var p = new EXG1192Parser(f, data, new DateTime(2026, 11, 11), 1, 2001, 2000);
        p.WriteFile();
        Assert.Equal(2, p.Rows);
        string[] lines = File.ReadAllLines(f + ".csv");
        string[] c = lines[1].Split(',');
        Assert.Equal(new[] { "True", "True" }, new[] { c[3], c[4] });     // CH1+ / CH1- lead-off
        Assert.Equal(new[] { "True", "True", "True" }, new[] { c[6], c[7], c[8] });
        Assert.Equal("False", lines[2].Split(',')[3]);
    }

    [Fact]
    public void Ads1192Config_UsesTheExg2LayoutUnderTheProductName()
    {
        var eeg2 = new ConfigEEG2Driver { GlobalGain = EXGGainOptions.CreateFromValue(1) };
        Assert.Equal("EEG2", eeg2.Name);
        Assert.Equal("EMG2", new ConfigEMG2Driver().Name);
        Assert.Equal("ECG2", new ConfigECG2Driver().Name);
        Assert.Equal(1u, ExgGain.CodeFromConfig0(true, eeg2.EegConf1));
        Assert.Contains(Vesper.SupportedDeviceDrivers, d => d is ConfigEEG2Driver);
        Assert.Contains(Pipistrelle.SupportedDeviceDrivers, d => d is ConfigECG2Driver);
        Assert.DoesNotContain(Vesper.SupportedDeviceDrivers, d => d.Name == "EAG2");
    }

    /// <summary>A stream that starts at a filler (the last 3 bytes of a record, which repeat the frame start) must
    /// not be decoded 6 bytes late: bench T03, 2026-11-11.</summary>
    [Fact]
    public void Ads1192Parser_DoesNotLockOntoTheFiller()
    {
        using var dir = new TempDir();
        string f = dir.File(Name);
        byte[] r1 = Ads1192Record(-86, -86), r2 = Ads1192Record(77, 78);
        byte[] data = Raw.Concat(r1.Skip(6).ToArray(), r1, r2, r1, r2);      // filler first, as after a cut
        using var p = new EXG1192Parser(f, data, new DateTime(2026, 11, 11), 1, 1, 2000);
        p.WriteFile();
        Assert.Equal(4, p.Rows);
        string[] lines = File.ReadAllLines(f + ".csv");
        double k = 2420000.0 / 32768;
        Near(-86 * k, Col(lines[1], 2));
        Near(78 * k, Col(lines[2], 5));
    }

    /// <summary>A channel on an internal input (test signal mux 0101, short, ...) does not pass the pre-amp.</summary>
    [Theory]
    [InlineData("EEG2", 0x00000000u, 1, 2001.0)]
    [InlineData("EEG2", 0x00550080u, 1, 1.0)]         // both channels on the test signal
    [InlineData("EEG2", 0x00500000u, 1, 2001.0)]      // only CH2 on the test signal
    [InlineData("EEG2", 0x00500000u, 2, 1.0)]
    [InlineData("ECG2", 0x00010000u, 1, 1.0)]         // CH1 shorted
    [InlineData("EAG2", 0x00000000u, 1, 1.0)]
    public void Ads1192ChannelPreamp_FollowsTheMux(string sensor, uint config0, int ch, double gain)
        => Assert.Equal(gain, ExgGain.Ads1192ChannelPreampGain(sensor, config0, ch));

    /// <summary>The bench recording T03 (2026-11-11, EEG2 board, PGA x6, test signal on both channels): the step of the
    /// +/- 1 mV square wave decodes to 2 mV at the ADC input, not 2 mV / 2001.</summary>
    [Fact]
    public async Task Pipeline_Ads1192_TestSignal_DecodesWithoutThePreamp()
    {
        using var dir = new TempDir();
        string f = dir.File(Name);
        File.WriteAllBytes(f, Raw.Concat(Ads1192Record(77, 78), Ads1192Record(-86, -86)));
        File.WriteAllLines(f + ".txt", new[] { "Sensor:EEG2", "SampleRate:3", "Config0:5500C0" });

        await RecordingPipeline.DecodeFilesAsync(new[] { f });

        string[] lines = Fixtures.WaitForLines(f + ".csv", 3);
        double k = 2420000.0 / 32768 / 6;                  // PGA x6, no pre-amp
        Near(77 * k, Col(lines[1], 2));
        Near(-86 * k, Col(lines[2], 5));
        Assert.InRange(Col(lines[1], 2) - Col(lines[2], 2), 1900, 2100);   // ~2 mV step in µV
    }

    /// <summary>Device Tests verdict of the console 72 op 1 reply, with the bench numbers of 2026-11-11 (EEG2 board):
    /// PGA x6, steps 163.125 / 163.8125 codes x16.</summary>
    [Fact]
    public void DeviceTest_ExgTestSignal_Ads1192AtX6_Passes()
    {
        var r = new SensorBenchResult { Status = 0, SubStatus = 1, IdFound = 0x51, Sample0 = 2610, Sample1 = 2621, Sample2 = (int)(0u | (2u << 4) | (16u << 8) | (824u << 16)) };
        var d = SensorBenchTest.DescribeExgTestSignal(r);
        Assert.NotNull(d);
        Assert.True(d!.Value.pass, d.Value.text);
        Assert.Contains("2.008", d.Value.text);
        Assert.Contains("2.016", d.Value.text);
    }

    [Fact]
    public void DeviceTest_ExgTestSignal_HalfScaleOrNoSignalFails_ProbeReplyIsNotATestReply()
    {
        // 24-bit ADS1292 at x12: expected step 2 x 2^23 x 12 / 2420 = 83192.8 codes; half of it must fail
        int full = (int)Math.Round(83192.8 * 16);
        var ok = new SensorBenchResult { Status = 0, SubStatus = 2, Sample0 = full, Sample1 = full, Sample2 = (int)(6u | (2u << 4) | (24u << 8)) };
        Assert.True(SensorBenchTest.DescribeExgTestSignal(ok)!.Value.pass);
        var half = new SensorBenchResult { Status = 2, SubStatus = 2, Sample0 = full / 2, Sample1 = full / 2, Sample2 = ok.Sample2 };
        Assert.False(SensorBenchTest.DescribeExgTestSignal(half)!.Value.pass);
        var flat = new SensorBenchResult { Status = 2, SubStatus = 1, Sample0 = -14, Sample1 = 10, Sample2 = (int)(1u | 0x08u | (2u << 4) | (16u << 8)) };
        Assert.False(SensorBenchTest.DescribeExgTestSignal(flat)!.Value.pass);
        var probe = new SensorBenchResult { Status = 0, SubStatus = 1, Sample0 = 2, Sample1 = 128, Sample2 = 0 };   // older firmware: probe reply
        Assert.Null(SensorBenchTest.DescribeExgTestSignal(probe));
    }

    [Fact]
    public void Sidecar_MissingOrWithoutConfig0_IsUnknown()
    {
        using var dir = new TempDir();
        Assert.Equal(ExgGain.Unknown, ExgGain.CodeFromSidecar(dir.File("none.EBN.txt")));
        string s = dir.File("a.EBN.txt");
        File.WriteAllLines(s, new[] { "Sensor:EXG2", "SampleRate:2" });
        Assert.Equal(ExgGain.Unknown, ExgGain.CodeFromSidecar(s));
        File.WriteAllLines(s, new[] { "Sensor:EXG2", "Config0:28" });
        Assert.Equal(5u, ExgGain.CodeFromSidecar(s));             // Sensor: decides the bit position
        File.WriteAllLines(s, new[] { "Sensor:EXG48", "Config0:28" });
        Assert.Equal(0u, ExgGain.CodeFromSidecar(s));
    }
}
