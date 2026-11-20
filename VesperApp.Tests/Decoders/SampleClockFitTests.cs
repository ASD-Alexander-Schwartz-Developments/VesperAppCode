using System.Globalization;
using VesperApp.Models;
using VesperApp.Services;
using Xunit;

namespace VesperApp.Tests.Decoders;

/// <summary>
/// Audio files: the true sample rate from the SYNC stamps (backlog F6). The header holds the nominal rate; the
/// microphone clock runs +106.7 ppm plus the crystal error. Stamps are 1/1024 s steps, read back to whole ms.
/// </summary>
public class SampleClockFitTests
{
    private static readonly DateTime Start = new(2026, 11, 20, 10, 0, 0);

    /// <summary>SYNC points every <paramref name="every"/> samples of a clock running at <paramref name="trueHz"/>, the
    /// times rounded to whole ms as the parser reads them.</summary>
    private static List<(long, DateTime)> Points(double trueHz, long every, double seconds)
    {
        var p = new List<(long, DateTime)>();
        for (long n = every; n / trueHz <= seconds; n += every)
            p.Add((n, Start.AddMilliseconds(Math.Round(n / trueHz * 1000))));
        return p;
    }

    [Fact]
    public void TenMinutes_GivesTheRate_WithinAPpm()
    {
        double trueHz = 100000 * 1.0001067;
        var r = SampleClockFit.Fit(Points(trueHz, 32760, 600), 100000);
        Assert.NotNull(r);
        Assert.InRange(r!.Ppm, 106.7 - 1, 106.7 + 1);
        Assert.InRange(r.RateHz, trueHz - 0.1, trueHz + 0.1);
        Assert.True(r.MaxDeviationMs < 1.0);
    }

    [Fact]
    public void SlowCrystal_GivesANegativeOffset()
    {
        // KOL 47715041: crystal -204 ppm -> +106.7 - 204 = about -97 ppm
        var r = SampleClockFit.Fit(Points(48000 * (1 - 97.3e-6), 32760, 900), 48000);
        Assert.InRange(r!.Ppm, -97.3 - 2, -97.3 + 2);
    }

    [Fact]
    public void ShortFile_IsNotMeasured()
    {
        Assert.Null(SampleClockFit.Fit(Points(100010.67, 32760, 8), 100000));       // 8 s: under 10 s
        Assert.Null(SampleClockFit.Fit(Points(100010.67, 32760, 0.5), 100000));     // one point
    }

    [Fact]
    public void MetadataLines_InvariantAndSigned()
    {
        var prev = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");                   // decimal comma must not leak in
            string s = SampleClockFit.MetadataLines(new SampleClockFit.Result(100010.6712, 106.712, 1831, 599.8, 0.61));
            Assert.Contains("MeasuredRate:100010.67", s);
            Assert.Contains("ClockPpm:+106.7", s);
            Assert.Contains("ClockFit:1831 SYNC stamps over 599.8 s, largest deviation 0.6 ms", s);
            Assert.DoesNotContain("uneven", s);
            Assert.Contains("ClockPpm:-97.0", SampleClockFit.MetadataLines(new SampleClockFit.Result(47995.3, -97.0, 50, 30, 25)));
            Assert.Contains("uneven", SampleClockFit.MetadataLines(new SampleClockFit.Result(47995.3, -97.0, 50, 30, 25)));
        }
        finally { CultureInfo.CurrentCulture = prev; }
    }

    /// <summary>A stamp block at <paramref name="s"/> seconds after <see cref="Start"/> (PREDIV_S 1023).</summary>
    private static byte[] Stamp(uint magic, double s)
    {
        long ticks = (long)Math.Round(s * 1024);
        DateTime whole = Start.AddSeconds(ticks / 1024);
        ushort ssr = (ushort)(1023 - ticks % 1024);
        return Raw.Block(magic, whole, 1023, ssr);
    }

    [Fact]
    public async Task AudioFile_SyncLinesAreWavSamples_AndTheRateIsMeasured()
    {
        using var dir = new TempDir();
        const uint nominal = 1000;
        const double trueHz = 1000 * (1 + 100e-6);
        const int every = 500;                                  // samples between SYNCs
        var parts = new List<byte[]> { Raw.FileHeader("SPH0641", nominal, 0, 0, 0, 0, 0), Stamp(BinHeader.PreambleHeader, 0) };
        int syncs = 60;                                         // 30 s
        for (int k = 1; k <= syncs; k++)
        {
            parts.Add(new byte[every * 2]);
            parts.Add(Stamp(BinHeader.PreambleSync, k * every / trueHz));
        }
        parts.Add(new byte[every * 2]);
        parts.Add(Stamp(BinHeader.PreambleFooter, (syncs + 1) * every / trueHz));
        string raw = dir.File("4U.BIN");
        File.WriteAllBytes(raw, Raw.Concat(parts.ToArray()));

        await BinaryParser.StripSplit(raw, dir.Path, 0);

        string ubn = Assert.Single(Directory.GetFiles(dir.Path, "*.UBN"));
        Assert.Equal((syncs + 1) * every * 2, new FileInfo(ubn).Length);
        string[] lines = File.ReadAllLines(ubn + ".txt");
        var sync = lines.Where(l => l.StartsWith("Sync:")).ToList();
        Assert.Equal(syncs, sync.Count);
        Assert.EndsWith(":500", sync[0]);                       // the sample number in the WAV, not the raw position
        Assert.EndsWith(":30000", sync[^1]);
        Assert.Contains("SampleRate:1000", lines);              // the header rate stays nominal
        double ppm = double.Parse(lines.Single(l => l.StartsWith("ClockPpm:")).Substring(9), CultureInfo.InvariantCulture);
        Assert.InRange(ppm, 100 - 15, 100 + 15);
        Assert.StartsWith("MeasuredRate:1000.1", lines.Single(l => l.StartsWith("MeasuredRate:")));

        string note = RecordingPipeline.ClockNote(ubn);
        Assert.Contains("MeasuredRate: 1000.1", note);
        Assert.Contains("ClockPpm: +", note);
        Assert.Contains("ClockFit: 60 SYNC stamps", note);
    }

    [Fact]
    public async Task NonAudioFile_GetsNoMeasuredRate()
    {
        using var dir = new TempDir();
        var parts = new List<byte[]> { Raw.FileHeader("EXG48", 250, 0, 0, 0, 0, 0), Stamp(BinHeader.PreambleHeader, 0) };
        for (int k = 1; k <= 20; k++) { parts.Add(new byte[1000]); parts.Add(Stamp(BinHeader.PreambleSync, k)); }
        parts.Add(new byte[8]);
        parts.Add(Stamp(BinHeader.PreambleFooter, 21));
        string raw = dir.File("9E.BIN");
        File.WriteAllBytes(raw, Raw.Concat(parts.ToArray()));

        await BinaryParser.StripSplit(raw, dir.Path, 0);

        string ebn = Assert.Single(Directory.GetFiles(dir.Path, "*.EBN"));
        Assert.DoesNotContain(File.ReadAllLines(ebn + ".txt"), l => l.StartsWith("MeasuredRate"));
    }
}
