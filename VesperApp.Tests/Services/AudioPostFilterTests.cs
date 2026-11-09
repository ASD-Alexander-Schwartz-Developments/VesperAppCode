using System.Numerics;
using ASDWaveLib;
using VesperApp.Services;
using Xunit;

namespace VesperApp.Tests.Services;

/// <summary>Port EQ and 32.768 kHz notch (asd-interfaces projects/u585-bench-2026-11-08.md, "Post-processing proposal").</summary>
public class AudioPostFilterTests
{
    private const double Fs = 100000;

    /// <summary>Magnitude (dB) of an FIR at frequency f.</summary>
    private static double FirDb(double[] h, double f, double fs)
    {
        Complex acc = 0;
        int c = h.Length / 2;
        for (int n = 0; n < h.Length; n++) acc += h[n] * Complex.Exp(new Complex(0, -2 * Math.PI * f * (n - c) / fs));
        return 20 * Math.Log10(acc.Magnitude);
    }

    private static double RmsDb(double[] x, int from, int to)
    {
        double s = 0;
        for (int i = from; i < to; i++) s += x[i] * x[i];
        return 10 * Math.Log10(s / (to - from));
    }

    [Fact]
    public void PortResonance_ModelPeaksAt26k5()
    {
        Assert.Equal(14.0, AudioPostFilter.PortResonanceDb(26500), 1);
        Assert.InRange(AudioPostFilter.PortResonanceDb(1000), -0.1, 0.1);
        Assert.True(AudioPostFilter.PortResonanceDb(20000) > 8);   // the skirt is broad (Q 1.4)
    }

    [Theory]
    [InlineData(100000)]
    [InlineData(192000)]
    [InlineData(48000)]
    public void PortEq_IsTheInverseOfTheResonance(int fs)
    {
        double[] h = AudioPostFilter.DesignPortEq(fs);
        Assert.Equal(1, h.Length % 2);
        foreach (double f in new[] { 1000.0, 10000, 20000, 26500, 30000, 40000 })
        {
            if (f > 0.45 * fs) continue;
            double want = -AudioPostFilter.PortResonanceDb(f);
            Assert.InRange(FirDb(h, f, fs), want - 0.3, want + 0.3);
        }
    }

    [Fact]
    public void PortEq_CutsTheResonanceAndKeepsTheBand()
    {
        int n = (int)Fs;
        double[] tone(double f) => Enumerable.Range(0, n).Select(i => 1000 * Math.Sin(2 * Math.PI * f * i / Fs)).ToArray();
        double[] h = AudioPostFilter.DesignPortEq(Fs);
        double at26 = RmsDb(AudioPostFilter.ApplyFir(tone(26500), h), 5000, n - 5000) - RmsDb(tone(26500), 5000, n - 5000);
        double at2 = RmsDb(AudioPostFilter.ApplyFir(tone(2000), h), 5000, n - 5000) - RmsDb(tone(2000), 5000, n - 5000);
        Assert.InRange(at26, -14.3, -13.7);
        Assert.InRange(at2, -0.2, 0.05);
    }

    [Fact]
    public void ApplyFir_KeepsTimingAligned()
    {
        var x = new double[20000]; x[10000] = 1000;
        double[] y = AudioPostFilter.ApplyFir(x, AudioPostFilter.DesignPortEq(Fs));
        int peak = Array.IndexOf(y, y.Max());
        Assert.Equal(10000, peak);
    }

    private static short[] NoisePlusLine(double lineHz, double lineAmp, int seconds = 4, int seed = 3)
    {
        var rnd = new Random(seed);
        var x = new short[(int)Fs * seconds];
        for (int i = 0; i < x.Length; i++)
            x[i] = (short)Math.Round(200 * (rnd.NextDouble() * 2 - 1) + lineAmp * Math.Sin(2 * Math.PI * lineHz * i / Fs));
        return x;
    }

    [Fact]
    public void Notch_FindsAndRemovesTheCrystalLine()
    {
        short[] x = NoisePlusLine(32765.2, 60);   // about the strength seen on the bench (+10…+30 dB over the floor)
        short[] y = AudioPostFilter.Process(x, Fs, new PostFilterOptions(false, true), out var rep);
        Assert.NotNull(rep.LseHz);
        Assert.InRange(rep.LseHz!.Value, 32760, 32770);
        Assert.True(rep.LseProminenceDb > 10);
        Assert.Single(rep.NotchedHz);

        double[] xd = x.Select(v => (double)v).ToArray(), yd = y.Select(v => (double)v).ToArray();
        double? before = AudioPostFilter.FindLine(xd, Fs, 32768, 0.015, out double pb);
        double? after = AudioPostFilter.FindLine(yd, Fs, 32768, 0.015, out double pa);
        Assert.True(pb - pa > 20, $"prominence {pb:0.0} -> {pa:0.0} dB");
    }

    [Fact]
    public void Notch_FollowsAShiftedLine()
    {
        // microphone clock unlocked: +0.5 % fast -> the line reads 0.5 % low in the recording
        short[] x = NoisePlusLine(32768 / 1.005, 60);
        AudioPostFilter.Process(x, Fs, new PostFilterOptions(false, true), out var rep);
        Assert.InRange(rep.LseHz!.Value, 32600, 32610);
    }

    [Fact]
    public void Notch_LeavesARecordingWithoutTheLineAlone()
    {
        short[] x = NoisePlusLine(32765, 0);
        short[] y = AudioPostFilter.Process(x, Fs, new PostFilterOptions(false, true), out var rep);
        Assert.Null(rep.LseHz);
        Assert.Empty(rep.NotchedHz);
        Assert.False(rep.AnyApplied);
        Assert.Equal(x, y);
    }

    [Fact]
    public void Notch_TakesHarmonicsBelowNyquist()
    {
        double fs = 200000;
        var rnd = new Random(5);
        var x = new short[(int)fs * 4];
        for (int i = 0; i < x.Length; i++)
            x[i] = (short)Math.Round(200 * (rnd.NextDouble() * 2 - 1) + 60 * Math.Sin(2 * Math.PI * 32765 * i / fs)
                                     + 40 * Math.Sin(2 * Math.PI * 65530 * i / fs));
        AudioPostFilter.Process(x, fs, new PostFilterOptions(false, true), out var rep);
        Assert.Equal(2, rep.NotchedHz.Count);
        Assert.InRange(rep.NotchedHz[1], 65520, 65540);
    }

    [Fact]
    public void Notch_SkippedWhenTheLineIsAboveNyquist()
    {
        short[] x = new short[48000 * 2];
        AudioPostFilter.Process(x, 48000, new PostFilterOptions(false, true), out var rep);
        Assert.False(rep.AnyApplied);
    }

    [Theory]
    [InlineData(@"C:\d\KOL-AUD\2026_11_08-0000_U0_.UBN", true)]
    [InlineData(@"C:\d\AUD\2026_11_08-0000_U1_.UBN", true)]
    [InlineData(@"C:\d\AUD\2026_11_08_12_35_07.000-2026_11_08_12_35_49.000.UBN", false)]
    public void KolAudio_IsRecognised(string path, bool kol) => Assert.Equal(kol, RecordingPipeline.IsKolAudio(path));
}
