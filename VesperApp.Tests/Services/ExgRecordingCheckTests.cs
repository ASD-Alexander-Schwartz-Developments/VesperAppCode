using VesperApp.Services;
using Xunit;

namespace VesperApp.Tests.Services;

/// <summary>
/// The recording check for ExG runs (backlog V1): data lost at a file change (F1), records that are not samples
/// (the EXG2 family's merged reads, F3), and the recorded samples per second against the board's clock tolerance.
/// Synthetic E.BIN files: HEADER, records, FOOTER.
/// </summary>
public class ExgRecordingCheckTests
{
    private static readonly DateTime T0 = new(2026, 11, 20, 12, 0, 0);

    private static byte[] At(uint marker, double t)
    {
        long ticks = (long)Math.Round(t * 1024);
        return Raw.Block(marker, T0.AddSeconds(ticks / 1024), 1023, (ushort)(1023 - ticks % 1024));
    }

    private static byte[] Exg48Rec(bool valid = true)
    {
        var r = new byte[EXG48Parser.RecordSize];
        r[0] = valid ? (byte)0xC0 : (byte)0x12;
        r[5] = 7;                                        // CH1 = 7
        return r;
    }

    private static byte[] Exg2Rec(bool valid = true) => valid
        ? new byte[] { 0xC0, 0x00, 0x00, 0, 0, 7, 0, 0, 9 }
        : new byte[] { 0x55, 0x55, 0x55, 0, 0, 7, 0, 0, 9 };

    /// <summary>EXG48 at 500 SPS low power (code 5, Config0 HR bit 0), <paramref name="n"/> records from
    /// <paramref name="start"/> s, the FOOTER at start + clockS.</summary>
    private static byte[] Exg48File(double start, int n, double clockS, int badEvery = 0)
    {
        var parts = new List<byte[]> { Raw.FileHeader("EXG48", 5, 0, 0, 0, 0, 0), At(0xA55AA55A, start) };
        for (int k = 0; k < n; k++) parts.Add(Exg48Rec(badEvery == 0 || k % badEvery != 0));
        parts.Add(At(0xA55A5AA5, start + clockS));
        return Raw.Concat(parts.ToArray());
    }

    private static RecordingIntegrity.Report Check(string letter, params byte[][] files)
    {
        var rep = new RecordingIntegrity.Report { Folder = "EXG" };
        double? prev = null;
        for (int i = 0; i < files.Length; i++) RecordingIntegrity.CheckExgFile(files[i], $"{i}{letter}.BIN", rep, ref prev);
        RecordingIntegrity.Finish(rep);
        return rep;
    }

    [Fact]
    public void CleanRun_NoProblems_AndTheRecordedRate()
    {
        // 20 s at 500 SPS, the next file one sample period after the FOOTER
        var rep = Check("E", Exg48File(0, 10000, 20), Exg48File(20.002, 10000, 20));
        Assert.False(rep.AnyProblem, string.Join("; ", rep.Findings.Select(f => f.Text)));
        Assert.Equal(2, rep.Files.Count);
        Assert.Equal(500u, rep.Files[0].Rate);
        Assert.InRange(rep.Files[0].RecordedRate!.Value, 499.9, 500.1);
        Assert.Contains("No problems found", RecordingIntegrity.Format(rep, "S"));
    }

    [Fact]
    public void FileChangeGap_IsReportedAsLostData()
    {
        // firmware 1.22: ~0.21-0.28 s between the FOOTER and the next HEADER (bench 5e-5g)
        var rep = Check("E", Exg48File(0, 10000, 20), Exg48File(20.240, 10000, 20));
        Assert.Equal(1, rep.LostBetweenFiles);
        var f = Assert.Single(rep.Findings);
        Assert.Equal("1E.BIN", f.File);
        Assert.Matches(@"^23[78] ms of data lost before this file \(about 11[89] samples\)", f.Text);
        Assert.Contains("ms lost between files", rep.Headline());
    }

    [Fact]
    public void SamplesMissing_BeyondTheClockTolerance_IsReported_WithinIsNot()
    {
        // F3 1 kSPS: -1.7 % is inside the converter oscillator's +-2 %: shown in the table only
        var ok = Check("E", Exg48File(0, 9830, 20));
        Assert.False(ok.AnyProblem);
        Assert.InRange(ok.Files[0].RecordedRate!.Value, 491, 492);
        Assert.Contains("-1.70", RecordingIntegrity.Format(ok, "S"));
        // 5 % short: reported
        var bad = Check("E", Exg48File(0, 9500, 20));
        Assert.Equal(1, bad.RateOffFiles);
        Assert.Contains("samples were lost", Assert.Single(bad.Findings).Text);
    }

    [Fact]
    public void Exg2_RecordsWithoutStatus_AreCounted()
    {
        var parts = new List<byte[]> { Raw.FileHeader("EXG2", 3, 0, 0, 0, 0, 0), At(0xA55AA55A, 0) };   // code 3 = 500 SPS
        for (int k = 0; k < 10000; k++) parts.Add(Exg2Rec(k % 100 != 50));                                // 1 % not samples
        parts.Add(At(0xA55A5AA5, 20));
        var rep = Check("E", Raw.Concat(parts.ToArray()));
        Assert.Equal(500u, rep.Files[0].Rate);
        Assert.Equal(100, rep.UnreliableRecords);
        Assert.Contains("100 of 10000 records (1.00 %) are not valid samples", Assert.Single(rep.Findings).Text);
    }

    [Fact]
    public void Ads1192_SixByteFrames_AreCountedAsSamples()
    {
        var parts = new List<byte[]> { Raw.FileHeader("EEG2", 3, 0, 0, 0, 0, 0), At(0xA55AA55A, 0) };
        for (int k = 0; k < 10000; k++) parts.Add(new byte[] { 0xC0, 0x00, 0x01, (byte)k, 0x02, (byte)(k >> 3) });
        parts.Add(At(0xA55A5AA5, 20));
        var rep = Check("E", Raw.Concat(parts.ToArray()));
        Assert.Equal(10000, rep.Files[0].Samples);
        Assert.False(rep.AnyProblem, string.Join("; ", rep.Findings.Select(f => f.Text)));
    }

    [Fact]
    public void CheckAndWrite_PutsTheExgReportInTheExgFolder_AndTheSummaryNamesIt()
    {
        using var dir = new TempDir();
        string a = dir.File("0E.BIN"), b = dir.File("1E.BIN");
        System.IO.File.WriteAllBytes(a, Exg48File(0, 10000, 20));
        System.IO.File.WriteAllBytes(b, Exg48File(20.240, 10000, 20));
        var checks = RecordingIntegrity.CheckAndWrite(new[] { a, b });
        var (folder, rep) = Assert.Single(checks);
        Assert.True(rep.IsExg);
        string report = System.IO.File.ReadAllText(Path.Combine(dir.Path, "EXG", RecordingIntegrity.ReportName));
        Assert.Contains("Recording check (ExG)", report);
        Assert.Contains("1E.BIN: 23", report);
        var sum = new DecodeSummary { Checks = checks };
        Assert.Contains(@"see EXG\RECORDING-CHECK.TXT", sum.CheckLine());
    }

    [Fact]
    public void Exg2Rate_FollowsTheCode()
    {
        Assert.Equal(125.0, ExgRate.Exg2SamplesPerSecond(1));
        Assert.Equal(500.0, ExgRate.Exg2SamplesPerSecond(3));
        Assert.Equal(8000.0, ExgRate.Exg2SamplesPerSecond(7));
        Assert.Equal(0.0, ExgRate.Exg2SamplesPerSecond(0));
        Assert.Equal(0.0, ExgRate.Exg2SamplesPerSecond(8));
    }
}
