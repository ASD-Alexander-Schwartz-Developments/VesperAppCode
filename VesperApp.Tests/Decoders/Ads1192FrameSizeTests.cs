using System.Globalization;
using VesperApp.Services;
using Xunit;

namespace VesperApp.Tests.Decoders;

/// <summary>
/// ADS1192 (EEG2 / EMG2 / ECG2) records: VesperU5 1.22 writes 9 bytes per sample (the 6-byte frame + its first 3
/// bytes again); firmware after 1.22 writes the 6-byte frame only (backlog F9). The parser tells the layout from the
/// data.
/// </summary>
public class Ads1192FrameSizeTests
{
    private const string Name = "E2026_11_20_09_30_15_000-E2026_11_20_09_31_15_000.EBN";

    private static byte[] Frame(short ch1, short ch2, byte s0 = 0xC0, byte s1 = 0x20) =>
        new byte[] { s0, s1, (byte)(ch1 >> 8), (byte)ch1, (byte)(ch2 >> 8), (byte)ch2 };

    private static byte[] Record9(short ch1, short ch2) { var f = Frame(ch1, ch2); return Raw.Concat(f, new[] { f[0], f[1], f[2] }); }

    private static double Col(string line, int col) => double.Parse(line.Split(',')[col], CultureInfo.InvariantCulture);

    private static byte[] Stream(int n, bool nine, Func<int, short> ch1, Func<int, short> ch2)
    {
        var parts = new List<byte[]>();
        for (int i = 0; i < n; i++) parts.Add(nine ? Record9(ch1(i), ch2(i)) : Frame(ch1(i), ch2(i)));
        return Raw.Concat(parts.ToArray());
    }

    [Fact]
    public void Detects_TheLayout()
    {
        Assert.Equal(9, EXG1192Parser.DetectRecordSize(Stream(50, true, i => (short)(i * 37), i => (short)(-i * 11))));
        Assert.Equal(6, EXG1192Parser.DetectRecordSize(Stream(50, false, i => (short)(i * 37), i => (short)(-i * 11))));
    }

    /// <summary>A slow signal: CH1's high byte stays the same, so in a 6-byte stream bytes 6..8 of every frame repeat
    /// bytes 0..2 (they look like the 1.22 filler). Still 6 bytes per record, every sample kept.</summary>
    [Fact]
    public void SixByteStream_SlowSignal_IsNotTakenFor1_22()
    {
        byte[] data = Stream(200, false, i => (short)(0x0100 + i % 7), i => (short)(i * 3));
        Assert.Equal(6, EXG1192Parser.DetectRecordSize(data));
        using var dir = new TempDir();
        string f = dir.File(Name);
        using var p = new EXG1192Parser(f, data, new DateTime(2026, 11, 20), 1, 1, 2000);
        p.WriteFile();
        Assert.Equal(200, p.Rows);
        string[] lines = File.ReadAllLines(f + ".csv");
        double k = 2420000.0 / 32768;
        Assert.InRange(Col(lines[1], 2), 0x0100 * k - 0.01, 0x0100 * k + 0.01);
        Assert.InRange(Col(lines[200], 5), 199 * 3 * k - 0.01, 199 * 3 * k + 0.01);
    }

    [Fact]
    public void NineByteStream_From1_22_DecodesAsBefore()
    {
        byte[] data = Stream(120, true, i => (short)(i * 100), i => (short)(-i));
        using var dir = new TempDir();
        string f = dir.File(Name);
        using var p = new EXG1192Parser(f, data, new DateTime(2026, 11, 20), 1, 1, 2000);
        p.WriteFile();
        Assert.Equal(120, p.Rows);
    }
}
