using VesperApp.Services.Audio;
using Xunit;

namespace VesperApp.Tests.Services;

/// <summary>The mic check's feedback loop: this PC's microphone must hear the test tones before the unit is
/// tested, so "no speaker / muted" is reported as such and not as a weak unit microphone.</summary>
public class ToneDetectorTests
{
    private static readonly int[] Tones = { 1000, 4000 };

    private static float[] Signal(int rate, double toneAmp, double noiseAmp, double humHz = 0, int seed = 1)
    {
        var rnd = new Random(seed);
        var gen = new ToneGenerator(rate, Tones, (float)toneAmp, fadeSeconds: 0.0001);
        var x = new float[rate / 2];
        gen.Fill(x, x.Length, 1);
        for (int i = 0; i < x.Length; i++)
        {
            x[i] += (float)((rnd.NextDouble() * 2 - 1) * noiseAmp);
            if (humHz > 0) x[i] += (float)(0.05 * Math.Sin(2 * Math.PI * humHz * i / rate));
        }
        return x;
    }

    [Theory]
    [InlineData(48000)]
    [InlineData(44100)]
    [InlineData(16000)]
    public void QuietTonesInNoise_Heard(int rate)
    {
        double[] snr = ToneDetector.SnrDb(Signal(rate, 0.01, 0.02), rate, Tones);   // tones 6 dB below the noise peak
        Assert.True(ToneDetector.Heard(snr), string.Join(", ", snr));
    }

    [Fact]
    public void NoiseOnly_NotHeard()
    {
        for (int seed = 1; seed <= 20; seed++)
        {
            double[] snr = ToneDetector.SnrDb(Signal(48000, 0, 0.05, seed: seed), 48000, Tones);
            Assert.False(ToneDetector.Heard(snr), $"seed {seed}: {string.Join(", ", snr)}");
        }
    }

    [Fact]
    public void SilenceAndHum_NotHeard()
    {
        Assert.False(ToneDetector.Heard(ToneDetector.SnrDb(new float[24000], 48000, Tones)));
        Assert.False(ToneDetector.Heard(ToneDetector.SnrDb(Signal(48000, 0, 0.001, humHz: 50), 48000, Tones)));
    }

    [Fact]
    public void OnlyOneTone_NotHeard()
    {
        var x = new float[24000];
        new ToneGenerator(48000, new[] { 1000 }, 0.1f, 0.0001).Fill(x, x.Length, 1);
        Assert.False(ToneDetector.Heard(ToneDetector.SnrDb(x, 48000, Tones)));
    }
}
