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

    /// <summary>µV of a 24-bit code as the decoders scale it (full scale 2^24 - 1 over the reference), divided by the gain.</summary>
    private static double MicroVolts(int code, double vrefMicroVolts, double gain) => code / (double)((1 << 24) - 1) * vrefMicroVolts / gain;

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
