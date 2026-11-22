using VesperApp.Models;
using VesperApp.Services;
using Xunit;

namespace VesperApp.Tests.Services;

/// <summary>
/// Findings of the VesperU5 bench pass 2026-11-22 (asd-interfaces backlog V5-V9): EXG48 channel count with a record
/// read out of step, ExG FOOTER on an odd offset, ClockPpm of the approximate rates, the EXG48 rate list per mode,
/// the silicon-revision LOGGER code.
/// </summary>
public class BenchFindings20261122Tests
{
    private static readonly DateTime T0 = new(2026, 11, 22, 8, 47, 50);

    private static byte[] At(uint marker, double t)
    {
        long ticks = (long)Math.Round(t * 1024);
        return Raw.Block(marker, T0.AddSeconds(ticks / 1024), 1023, (ushort)(1023 - ticks % 1024));
    }

    /// <summary>A 4-channel ADS1294 record (CH5..CH8 zero); <paramref name="shifted"/>: bytes of the next frame in CH4..CH8,
    /// as record 5266 of the bench file 0E.BIN.</summary>
    private static byte[] Rec(bool shifted = false)
    {
        var r = new byte[EXG48Parser.RecordSize];
        r[0] = 0xC0;
        r[3] = 0xFF; r[4] = 0xCE; r[5] = 0x1D;           // CH1 negative, as the bench data
        if (shifted)
        {
            byte[] tail = { 0x00, 0xFE, 0x00, 0x00, 0x07, 0xFE, 0x71, 0x37, 0xFF, 0x0A, 0xE7, 0xFF, 0x5E, 0xB0, 0x00 };
            Array.Copy(tail, 0, r, 12, tail.Length);
        }
        return r;
    }

    [Fact]
    public void Exg48_OneRecordOutOfStep_KeepsFourChannels_AndHasNoValues()
    {
        var recs = Enumerable.Range(0, 1000).Select(k => Rec(k == 500)).ToArray();
        byte[] data = Raw.Concat(recs);
        using var dir = new TempDir();
        string f = dir.File("E2026_11_22_08_47_50_111-E2026_11_22_08_47_51_111.EBN");
        using (var p = new EXG48Parser(f, data, T0, 0, 1000.0))
        {
            Assert.Equal(4, p.Channels);
            Assert.Equal(1000, p.Rows);
            Assert.Equal(1, p.Shifted);
            p.WriteFile();
        }
        string[] lines = System.IO.File.ReadAllLines(f + ".csv");
        string row = lines[1 + 500];
        string sep = Utils.GetSeparator();
        string[] cells = row.Split(sep);
        Assert.Equal(2 + 4 * 3, cells.Length);
        Assert.All(cells.Skip(2), c => Assert.Equal("", c));
        Assert.NotEqual("", lines[1 + 499].Split(sep)[2]);
    }

    [Fact]
    public void Exg48_EightChannelBoard_StaysEight()
    {
        var r = Rec(); r[3 + 3 * 7 + 2] = 5;            // CH8 non-zero in every record
        var p = new EXG48Parser("x", Raw.Concat(Enumerable.Repeat(r, 200).ToArray()), T0, 0, 1000.0);
        Assert.Equal(8, p.Channels);
        Assert.Equal(0, p.Shifted);
    }

    [Fact]
    public void ExgCheck_FooterOnAnOddOffset_IsChecked()
    {
        // 9,743 records of 27 bytes: the FOOTER lands on an odd offset (bench 4E.BIN: 164,277)
        byte[] file = Raw.Concat(new[] { Raw.FileHeader("EXG48", 4, 0, 0, 0, 0, 0), At(0xA55AA55A, 0) }
            .Concat(Enumerable.Repeat(Rec(), 9743)).Append(At(0xA55A5AA5, 9.743)).ToArray());
        Assert.Equal(1, (file.Length - 16) % 2);
        var rep = new RecordingIntegrity.Report { Folder = "EXG" };
        double? prev = null;
        RecordingIntegrity.CheckExgFile(file, "4E.BIN", rep, ref prev);
        var f = Assert.Single(rep.Files);
        Assert.Equal(9743, f.Samples);
    }

    [Theory]
    [InlineData(11034u, 11034.4828)]                    // 11,025 setting: 4.8 MHz / 435
    [InlineData(22018u, 22018.3486)]                    // 22,050 / 44,100 family: / 218, / 109
    [InlineData(44037u, 44036.6972)]
    [InlineData(252632u, 252631.5789)]                  // 250 / 256 kHz: / 19
    [InlineData(369231u, 369230.7692)]                  // 384 kHz: / 13
    [InlineData(100000u, 100000.0)]
    [InlineData(48000u, 48000.0)]
    [InlineData(44100u, 44100.0)]                       // not 4.8 MHz / n: unchanged
    public void TrueRate_IsTheDividerRate(uint header, double expected)
    {
        Assert.Equal(expected, SampleClockFit.TrueRate(header), 3);
    }

    [Fact]
    public void ClockPpm_OfAnApproximateRate_IsAgainstTheDividerRate()
    {
        // bench R11025_12: locked, MeasuredRate 11,035.68 Hz on header 11,034 -> +108.5 ppm, not +152.5
        double rate = 11035.68;
        var pts = Enumerable.Range(0, 30).Select(k => ((long)(k * 32760), T0.AddSeconds(k * 32760 / rate))).ToList();
        var r = SampleClockFit.Fit(pts, 11034)!;
        Assert.InRange(r.Ppm, 108.0, 109.0);
    }

    [Fact]
    public void Exg48RateList_HighResolution_HasNo250()
    {
        Assert.Contains(EXGSampleRateOptions.ListFor(false), o => o.Value == 250);
        Assert.DoesNotContain(EXGSampleRateOptions.ListFor(true), o => o.Value == 250);
        var d = new ConfigEXG48Driver();
        var opts = ((IPropertyOptions)d).OptionsFor(nameof(ConfigEXG48Driver.SampleRate_1))!.Cast<EXGSampleRateOptions>();
        Assert.Contains(opts, o => o.Value == 250);
        bool rebuilt = false;
        ((IDynamicBrowsable)d).LayoutChanged += (_, _) => rebuilt = true;
        d.EnableHR = true;
        Assert.True(rebuilt);
        opts = ((IPropertyOptions)d).OptionsFor(nameof(ConfigEXG48Driver.SampleRate_1))!.Cast<EXGSampleRateOptions>();
        Assert.DoesNotContain(opts, o => o.Value == 250);
        Assert.Null(((IPropertyOptions)d).OptionsFor("EnableHR"));
    }

    [Theory]
    [InlineData((ushort)0x0A21, "MCU silicon revision X (REV_ID 0x2001)")]
    [InlineData((ushort)0x0A31, "MCU silicon revision W (REV_ID 0x3001)")]
    [InlineData((ushort)0x0A37, "MCU silicon revision U (REV_ID 0x3007)")]
    public void LoggerSiliconRevision_IsNamed(ushort code, string name)
    {
        Assert.Equal(name, LoggerParser.CodeName(code));
    }
}
