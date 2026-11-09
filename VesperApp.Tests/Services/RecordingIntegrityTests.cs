using System.Text;
using VesperApp.Services;
using Xunit;

namespace VesperApp.Tests.Services;

/// <summary>Per-run recording check (RecordingIntegrity): sample clock vs the crystal time in HEADER / SYNC / FOOTER,
/// crystal stalls, audio lost between files. Synthetic U.BIN files at 1 kHz with a SYNC every second.</summary>
public class RecordingIntegrityTests
{
    private const uint Fs = 1000;
    private static readonly DateTime T0 = new(2026, 11, 8, 12, 0, 0);

    /// <summary>A block whose RTC time is <paramref name="t"/> seconds after T0 (1/256 s sub-seconds, as the RTC).</summary>
    private static byte[] At(uint marker, double t)
    {
        int whole = (int)Math.Floor(t);
        int ticks = (int)Math.Round((t - whole) * 256);
        if (ticks == 256) { whole++; ticks = 0; }
        return Raw.Block(marker, T0.AddSeconds(whole), 255, (ushort)(255 - ticks));
    }

    /// <summary>One file: HEADER at <paramref name="start"/>, <paramref name="seconds"/> of audio in 1 s chunks each
    /// followed by a SYNC (the last by the FOOTER); <paramref name="rtcStep"/> gives the crystal time each chunk took.</summary>
    private static byte[] File(double start, int seconds, Func<int, double> rtcStep)
    {
        var parts = new List<byte[]> { Raw.FileHeader("SPH0641", Fs, 0, 0, 0, 0, 0), At(0xA55AA55A, start) };
        double t = start;
        for (int k = 0; k < seconds; k++)
        {
            parts.Add(new byte[2 * Fs]);                                     // silence: no magic inside
            t += rtcStep(k);
            parts.Add(At(k == seconds - 1 ? 0xA55A5AA5u : 0xABCDEFEFu, t));
        }
        return Raw.Concat(parts.ToArray());
    }

    private static RecordingIntegrity.Report Check(params byte[][] files)
    {
        var rep = new RecordingIntegrity.Report();
        double? prev = null;
        for (int i = 0; i < files.Length; i++) RecordingIntegrity.CheckFile(files[i], $"{i}U.BIN", rep, ref prev);
        RecordingIntegrity.Finish(rep);
        return rep;
    }

    [Fact]
    public void LockedRun_NoProblems()
    {
        var rep = Check(File(0, 60, _ => 1.0));
        Assert.False(rep.AnyProblem, string.Join("; ", rep.Findings.Select(f => f.Text)));
        Assert.All(rep.Files[0].WindowPpm, p => Assert.InRange(p, -500, 500));
        Assert.Equal(60.0, rep.Files[0].AudioS, 3);
    }

    [Fact]
    public void LockLostMidFile_ReportsWhereAndHowFast()
    {
        // From 30 s the sample clock runs +0.5 %: each 1000 samples take 1/1.005 s of crystal time.
        var rep = Check(File(0, 60, k => k < 30 ? 1.0 : 1 / 1.005));
        Assert.Equal(1, rep.ClockOffFiles);
        var f = Assert.Single(rep.Findings);
        Assert.InRange(f.AtS, 25, 35);
        Assert.Matches(@"fast by 0\.(49|50|51) %", f.Text);   // 1/256 s stamps: +-~400 ppm per 10 s window
        Assert.Matches(@"about 100[45] Hz", f.Text);
        Assert.Equal(0, rep.Stalls);
    }

    [Fact]
    public void LockLostForTheRestOfTheRun_IsOneFindingAcrossFiles()
    {
        double e0 = 15 + 15 / 1.005, s1 = e0 + 0.019, s2 = s1 + 30 / 1.005 + 0.019;      // fast clock: files end early on the crystal
        var rep = Check(File(0, 30, k => k < 15 ? 1.0 : 1 / 1.005), File(s1, 30, _ => 1 / 1.005), File(s2, 30, _ => 1 / 1.005));
        Assert.Equal(3, rep.ClockOffFiles);
        var f = Assert.Single(rep.Findings);
        Assert.Equal("0U.BIN", f.File);
        Assert.Contains("to the end of 2U.BIN (the end of the run)", f.Text);
        Assert.Matches(@"fast by 0\.(49|50|51) %", f.Text);
    }

    [Fact]
    public void CrystalStall_IsReportedWithTheTimeLost()
    {
        // At 20 s the crystal loses 300 ms over one SYNC interval.
        var rep = Check(File(0, 60, k => k == 20 ? 0.7 : 1.0));
        Assert.Equal(1, rep.Stalls);
        Assert.InRange(rep.StallTotalMs, 290, 310);
        Assert.Equal(0, rep.ClockOffFiles);                  // the stall window is not read as a clock offset
        Assert.Matches("fell behind by (29[0-9]|30[0-9]) ms", rep.Findings.Single().Text);
    }

    [Fact]
    public void FileChanges_NormalGapIsQuiet_LongGapIsReported()
    {
        var a = File(0, 30, _ => 1.0);
        var b = File(30.019, 30, _ => 1.0);                  // a normal 19 ms file change
        var c = File(60.019 + 0.116, 30, _ => 1.0);          // 116 ms: audio lost
        var rep = Check(a, b, c);
        Assert.Equal(1, rep.LostBetweenFiles);
        Assert.InRange(rep.Files[1].GapBeforeMs!.Value, 15, 23);
        var f = Assert.Single(rep.Findings);
        Assert.Equal("2U.BIN", f.File);
        Assert.Contains("ms of audio lost before this file", f.Text);
    }

    [Fact]
    public void PauseBetweenWindows_IsNotALoss()
    {
        var rep = Check(File(0, 20, _ => 1.0), File(80, 20, _ => 1.0));   // a 60 s schedule pause
        Assert.False(rep.AnyProblem);
        Assert.Null(rep.Files[1].GapBeforeMs);
    }

    [Fact]
    public void CheckAndWrite_WritesTheReportNextToTheAudio_AndTheSummarySaysSo()
    {
        using var dir = new TempDir();
        System.IO.File.WriteAllBytes(Path.Combine(dir.Path, "0U.BIN"), File(0, 60, k => k < 30 ? 1.0 : 1 / 1.005));
        System.IO.File.WriteAllBytes(Path.Combine(dir.Path, "0M.BIN"), new byte[16]);      // not a microphone raw

        var runs = RecordingIntegrity.CheckAndWrite(Directory.GetFiles(dir.Path));

        var run = Assert.Single(runs);
        string report = System.IO.File.ReadAllText(Path.Combine(dir.Path, "AUD", RecordingIntegrity.ReportName), Encoding.UTF8);
        Assert.Matches(@"sample clock fast by 0\.(49|50|51) %", report);
        var sum = new DecodeSummary { RawParsed = 1, Checks = runs };
        Assert.Contains("Recording check: sample clock off in 1 file(s)", sum.ToString());
    }

    [Fact]
    public void KolChannelFiles_AreNotCheckedHere()
    {
        Assert.True(RecordingIntegrity.IsMicRaw(@"C:\x\12U.BIN"));
        Assert.False(RecordingIntegrity.IsMicRaw(@"C:\x\12U0.BIN"));
        Assert.False(RecordingIntegrity.IsMicRaw(@"C:\x\12M.BIN"));
    }
}
