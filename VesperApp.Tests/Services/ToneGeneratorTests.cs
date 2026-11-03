using VesperApp.Services.Audio;
using Xunit;

namespace VesperApp.Tests.Services;

/// <summary>The microphone check's stimulus (1 kHz + 4 kHz from this PC, TonePlayer): the tones are where the
/// unit looks for them, it starts without a click and stays below full scale.</summary>
public class ToneGeneratorTests
{
    private static double Goertzel(float[] x, int start, int n, double f, int rate)
    {
        double w = 2 * Math.PI * f / rate, c = 2 * Math.Cos(w), s1 = 0, s2 = 0;
        for (int i = start; i < start + n; i++) { double s0 = x[i] + c * s1 - s2; s2 = s1; s1 = s0; }
        return Math.Sqrt(s1 * s1 + s2 * s2 - c * s1 * s2) / (n / 2.0);   // ≈ amplitude of the tone
    }

    [Fact]
    public void TwoTones_AtTheirFrequencies_FadeIn_BelowFullScale()
    {
        const int rate = 48000;
        var gen = new ToneGenerator(rate, new[] { 1000, 4000 }, 0.35f);
        var x = new float[rate];
        for (int k = 0; k < x.Length; k += 480)                      // in callback-sized blocks
        {
            var block = new float[480];
            gen.Fill(block, 480, 1);
            Array.Copy(block, 0, x, k, 480);
        }

        Assert.Equal(0f, x[0]);                                       // fade-in: no click
        Assert.True(Math.Abs(x[10]) < 0.02f);
        Assert.InRange(x.Max(v => Math.Abs(v)), 0.6f, 0.7001f);       // 2 × 0.35, never clipping

        int n = 4800, at = 24000;                                     // 100 ms, after the fade
        Assert.InRange(Goertzel(x, at, n, 1000, rate), 0.33, 0.37);
        Assert.InRange(Goertzel(x, at, n, 4000, rate), 0.33, 0.37);
        Assert.True(Goertzel(x, at, n, 2000, rate) < 0.01);
        Assert.True(Goertzel(x, at, n, 8000, rate) < 0.01);
    }

    [Fact]
    public void Stereo_SameSignalInBothChannels()
    {
        var gen = new ToneGenerator(48000, new[] { 1000 }, 0.5f);
        var b = new float[2 * 2000];
        gen.Fill(b, 2000, 2);
        for (int i = 0; i < 2000; i++) Assert.Equal(b[2 * i], b[2 * i + 1]);
    }
}
