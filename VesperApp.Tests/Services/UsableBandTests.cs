using System.Text;
using ASDWaveLib;
using VesperApp.Services;
using Xunit;

namespace VesperApp.Tests.Services;

/// <summary>The WAV low-pass marks the usable band: 0.44 x rate with the reshape anti-aliasing filter, 0.33 x rate on
/// CIC-only chains (asd-interfaces projects/u585-bench-2026-11-08.md).</summary>
public class UsableBandTests
{
    [Theory]
    [InlineData(100000u, 48000u, 200000u)]
    public void FullFilterRates_KeepTheUsualLowPass(uint a, uint b, uint c)
    {
        foreach (uint fs in new[] { a, b, c })
        {
            var band = MicChainSolver.UsableBand(fs);
            Assert.True(band.FullFilter);
            Assert.Equal(fs / 2.2, band.LowPassHz, 6);
            Assert.Equal(0.44 * fs, band.UsableHz, 6);
            Assert.Null(band.BetterRate);
        }
    }

    [Theory]
    [InlineData(96000u, 100000u)]
    [InlineData(192000u, 200000u)]
    [InlineData(250000u, 300000u)]
    public void CicOnlyRates_LowPassAtAThird_WhereImagesAreWellDown(uint fs, uint better)
    {
        var band = MicChainSolver.UsableBand(fs);
        Assert.False(band.FullFilter);
        Assert.Equal(0.33 * fs, band.LowPassHz, 6);
        Assert.Equal(0.33 * fs, band.UsableHz, 6);
        Assert.InRange(band.AliasDbAtEdge!.Value, -40, -22);       // images 22-40 dB down at the edge (SINC4 / SINC5)
        Assert.Equal(better, band.BetterRate);
    }

    [Fact]
    public void Note_SaysWhatTheBandIs()
    {
        string full = MicChainSolver.UsableBandNote(100000);
        Assert.Contains("UsableBand: 0-44000 Hz (full anti-aliasing filter", full);
        Assert.Contains("LowPass: 45455 Hz", full);

        string cic = MicChainSolver.UsableBandNote(96000);
        Assert.Contains("UsableBand: 0-31680 Hz (no anti-aliasing filter", cic);
        Assert.Contains("Advice: record at 100 kHz", cic);
    }

    [Fact]
    public void Butterworth_FiltersTheLastSampleToo()
    {
        var x = new double[2000];
        x[^1] = 1.0;                                     // a spike on the very last sample
        double[] y = ButterworthFilter.Butterworth((double[])x.Clone(), 100000, 100000 / 2.2);
        Assert.True(y[^1] < 0.9, $"last sample {y[^1]}");   // was left untouched (1.0) before the fix

        var dc = Enumerable.Repeat(0.5, 2000).ToArray();
        double[] z = ButterworthFilter.Butterworth(dc, 100000, 30000);
        Assert.All(z, v => Assert.Equal(0.5, v, 6));       // unity at DC, every sample
    }

    [Fact]
    public void LowPassHz_MovesTheCorner()
    {
        // 32 kHz tone at 96 kHz: kept by the usual corner (43.6 kHz), cut by the CIC-only corner (31.7 kHz)
        double Pass(double fc)
        {
            int n = 96000;
            var x = Enumerable.Range(0, n).Select(i => 0.3 * Math.Sin(2 * Math.PI * 32000 * i / 96000.0)).ToArray();
            double[] y = ButterworthFilter.Butterworth(x, 96000, fc);
            double r(double[] v) => Math.Sqrt(v.Skip(5000).Take(n - 10000).Select(s => s * s).Average());
            return 20 * Math.Log10(r(y) / 0.3 * Math.Sqrt(2));
        }
        Assert.InRange(Pass(96000 / 2.2), -1.0, 0.1);
        Assert.InRange(Pass(0.33 * 96000), -7.5, -5.0);
    }

    [Fact]
    public async Task Decode_WritesTheSidecarWithTheBand()
    {
        using var dir = new TempDir();
        string ubn = Path.Combine(dir.Path, "U2026_11_08_12_00_00_000-U2026_11_08_12_00_10_000.UBN");
        var pcm = new byte[96000 * 2];
        File.WriteAllBytes(ubn, pcm);
        File.WriteAllText(ubn + ".txt", "Sensor:SPH0641" + Environment.NewLine + "SampleRate:96000" + Environment.NewLine, Encoding.UTF8);

        var sum = await RecordingPipeline.DecodeFilesAsync(new[] { ubn });

        Assert.Equal(1, sum.Wav);
        Assert.True(File.Exists(ubn + ".WAV"));
        string note = File.ReadAllText(ubn + ".WAV.txt");
        Assert.Contains("SampleRate: 96000", note);
        Assert.Contains("UsableBand: 0-31680 Hz", note);
        Assert.Contains("MicChain: CIC 50 SINC4 (basic filter only)", note);
    }
}
