using System.Globalization;
using VesperApp.Services;
using Xunit;

namespace VesperApp.Tests.Decoders;

/// <summary>
/// A record that is not a sample (no 1100 status start: the firmware read the converter out of step while it wrote
/// to storage, backlog F3) gives no CSV row, but it still stands for one sample period: the rows after it keep their
/// time. Before 2026-11-20 the EXG2 and ADS1192 decoders closed the gap, so later rows were early (twin, EXG2 1 kSPS:
/// 1.7 % of records, up to 0.5 s by the end of a 30 s file).
/// </summary>
public class ExgRowTimeTests
{
    private static readonly DateTime Start = new(2026, 11, 20, 9, 30, 15);
    private const string Name = "E2026_11_20_09_30_15_000-E2026_11_20_09_31_15_000.EBN";

    private static double Ms(string line) =>
        (TimeSpan.ParseExact(line.Split(',')[1], @"hh\:mm\:ss\.FFF", CultureInfo.InvariantCulture) - Start.TimeOfDay).TotalMilliseconds;

    [Fact]
    public void Exg2_RowAfterABadRecord_KeepsItsTime()
    {
        byte[] good = { 0xC0, 0, 0, 0, 0, 1, 0, 0, 2 }, bad = { 0x55, 0x55, 0x55, 0, 0, 1, 0, 0, 2 };
        byte[] data = Raw.Concat(good, good, bad, good, new byte[] { 0 });
        using var dir = new TempDir();
        string f = dir.File(Name);
        using (var p = new EXG1292Parser(f, data, Start, 1, 2000)) p.WriteFile();       // 500 SPS
        string[] lines = File.ReadAllLines(f + ".csv");
        Assert.Equal(4, lines.Length);
        Assert.Equal(2.0, Ms(lines[1]), 3);
        Assert.Equal(4.0, Ms(lines[2]), 3);
        Assert.Equal(8.0, Ms(lines[3]), 3);                                             // the 4th period, not the 3rd
    }

    [Fact]
    public void Ads1192_RowAfterABadRecord_KeepsItsTime()
    {
        var parts = new List<byte[]>();
        for (int k = 0; k < 20; k++)
            parts.Add(k == 10 ? new byte[] { 0x11, 0x22, 0x33, 0x44, 0x55, 0x66 } : new byte[] { 0xC0, 0x00, 0x01, (byte)k, 0x02, 0x03 });
        using var dir = new TempDir();
        string f = dir.File(Name);
        using (var p = new EXG1192Parser(f, Raw.Concat(parts.ToArray()), Start, 1, 1, 1, 2000)) p.WriteFile();
        string[] lines = File.ReadAllLines(f + ".csv");
        Assert.Equal(20, lines.Length);                                                 // 19 rows
        Assert.Equal(20.0, Ms(lines[10]), 3);                                           // record 9
        Assert.Equal(24.0, Ms(lines[11]), 3);                                           // record 11: 12th period
        Assert.Equal(40.0, Ms(lines[19]), 3);
    }
}
