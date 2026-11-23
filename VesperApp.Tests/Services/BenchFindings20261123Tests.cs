using VesperApp.Services;
using Xunit;

namespace VesperApp.Tests.Services;

/// <summary>
/// Findings of the VesperU5 bench pass 2026-11-23 (asd-interfaces backlog V12): Auto Decode left the DAQ24
/// windows (.SBN) undecoded because .SBN was not in the pipeline's decodable list.
/// </summary>
public class BenchFindings20261123Tests
{
    private const string Daq = "daq24-1s.S.BIN";

    [Fact]
    public async Task AutoDecode_Daq24RawFile_WritesTheCsv()
    {
        using var dir = new TempDir();
        string raw = Fixtures.CopyAs(Daq, dir);

        DecodeSummary sum = await RecordingPipeline.AutoDecodeAsync(new[] { raw });

        Assert.Equal(0, sum.Failed);
        Assert.Equal(1, sum.Csv);
        string sbn = Assert.Single(Directory.GetFiles(Path.Combine(dir.Path, "DAQ"), "*.SBN"));
        string[] lines = File.ReadAllLines(sbn + ".csv");
        Assert.Equal("Time,AIN0 (V),AIN1 (V),AIN2 (V),AIN3 (V)", lines[0]);
        Assert.Equal(1 + Fixtures.Json(Daq).GetProperty("records").GetInt32(), lines.Length);
        foreach (string l in lines.Skip(1))
        {
            string[] c = l.Split(',');
            Assert.Equal(5, c.Length);
            for (int ch = 1; ch <= 4; ch++)                     // open inputs on the bench: about 1.1 V, never empty
                Assert.InRange(double.Parse(c[ch], System.Globalization.CultureInfo.InvariantCulture), 0.5, 2.0);
        }
    }
}
