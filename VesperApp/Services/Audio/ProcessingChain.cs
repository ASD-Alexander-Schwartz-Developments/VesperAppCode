using System;
using System.Collections.Generic;
using System.Linq;

namespace VesperApp.Services.Audio
{
    /// <summary>User-selectable local processing applied to the captured stream before display / classification.</summary>
    public sealed record ProcessingSettings(
        double GainDb,
        bool HighPass, double HighPassHz, int HighPassOrder,
        bool LowPass, double LowPassHz, int LowPassOrder,
        bool Notch, double NotchHz, int NotchHarmonics, double NotchQ)
    {
        public static ProcessingSettings Default => new(0, false, 100, 2, false, 20000, 4, false, 50, 3, 30);
    }

    /// <summary>Direct-form-II transposed biquad (RBJ cookbook coefficients).</summary>
    public sealed class Biquad
    {
        private readonly double _b0, _b1, _b2, _a1, _a2;
        private double _z1, _z2;

        private Biquad(double b0, double b1, double b2, double a0, double a1, double a2)
        {
            _b0 = b0 / a0; _b1 = b1 / a0; _b2 = b2 / a0; _a1 = a1 / a0; _a2 = a2 / a0;
        }

        public static Biquad LowPass(double fs, double fc, double q)
        {
            double w = 2 * Math.PI * Math.Min(fc, fs * 0.49) / fs, c = Math.Cos(w), s = Math.Sin(w), a = s / (2 * q);
            return new Biquad((1 - c) / 2, 1 - c, (1 - c) / 2, 1 + a, -2 * c, 1 - a);
        }
        public static Biquad HighPass(double fs, double fc, double q)
        {
            double w = 2 * Math.PI * Math.Max(1, Math.Min(fc, fs * 0.49)) / fs, c = Math.Cos(w), s = Math.Sin(w), a = s / (2 * q);
            return new Biquad((1 + c) / 2, -(1 + c), (1 + c) / 2, 1 + a, -2 * c, 1 - a);
        }
        public static Biquad Notch(double fs, double f0, double q)
        {
            double w = 2 * Math.PI * Math.Min(f0, fs * 0.49) / fs, c = Math.Cos(w), s = Math.Sin(w), a = s / (2 * q);
            return new Biquad(1, -2 * c, 1, 1 + a, -2 * c, 1 - a);
        }

        public float Process(float x)
        {
            double y = _b0 * x + _z1;
            _z1 = _b1 * x - _a1 * y + _z2;
            _z2 = _b2 * x - _a2 * y;
            return (float)y;
        }
    }

    /// <summary>
    /// Per-channel filter cascade + post gain. Built once per settings change (immutable
    /// coefficients, per-channel state) and swapped into the capture callback atomically.
    /// Butterworth responses are realised as cascaded second-order sections.
    /// </summary>
    public sealed class ProcessingChain
    {
        private readonly Biquad[][] _sections;   // [channel][section]
        private readonly float _gain;

        public ProcessingSettings Settings { get; }
        public int SampleRate { get; }
        public int Channels { get; }

        public ProcessingChain(ProcessingSettings s, int sampleRate, int channels)
        {
            Settings = s; SampleRate = sampleRate; Channels = channels;
            _gain = (float)Math.Pow(10, s.GainDb / 20);
            _sections = new Biquad[channels][];
            for (int c = 0; c < channels; c++) _sections[c] = Build(s, sampleRate).ToArray();
        }

        private static IEnumerable<Biquad> Build(ProcessingSettings s, int fs)
        {
            if (s.HighPass) foreach (double q in ButterworthQ(s.HighPassOrder)) yield return Biquad.HighPass(fs, s.HighPassHz, q);
            if (s.LowPass) foreach (double q in ButterworthQ(s.LowPassOrder)) yield return Biquad.LowPass(fs, s.LowPassHz, q);
            if (s.Notch)
                for (int h = 1; h <= Math.Max(1, s.NotchHarmonics); h++)
                {
                    double f = s.NotchHz * h; if (f >= fs * 0.49) break;
                    yield return Biquad.Notch(fs, f, s.NotchQ);
                }
        }

        /// <summary>Q values of the second-order sections of an even-order Butterworth filter.</summary>
        public static double[] ButterworthQ(int order)
        {
            order = Math.Clamp(order / 2 * 2, 2, 8);
            var q = new double[order / 2];
            for (int k = 0; k < q.Length; k++) q[k] = 1.0 / (2 * Math.Cos(Math.PI * (2 * k + 1) / (2.0 * order)));
            return q;
        }

        /// <summary>Process interleaved Int16 frames into interleaved float [-1,1].</summary>
        public void Process(ReadOnlySpan<short> input, Span<float> output, int frames)
        {
            int ch = Channels;
            for (int i = 0; i < frames; i++)
            {
                for (int c = 0; c < ch; c++)
                {
                    float x = input[i * ch + c] * (1f / 32768f);
                    Biquad[] sec = _sections[c];
                    for (int k = 0; k < sec.Length; k++) x = sec[k].Process(x);
                    x *= _gain;
                    output[i * ch + c] = x > 1f ? 1f : x < -1f ? -1f : x;
                }
            }
        }

        public string Describe()
        {
            var parts = new List<string>();
            if (Settings.HighPass) parts.Add($"HP {Settings.HighPassHz:0} Hz (order {Settings.HighPassOrder})");
            if (Settings.LowPass) parts.Add($"LP {Settings.LowPassHz:0} Hz (order {Settings.LowPassOrder})");
            if (Settings.Notch) parts.Add($"notch {Settings.NotchHz:0} Hz ×{Settings.NotchHarmonics}");
            if (Math.Abs(Settings.GainDb) > 0.01) parts.Add($"gain {Settings.GainDb:+0.#;-0.#} dB");
            return parts.Count == 0 ? "pass-through" : string.Join(" → ", parts);
        }
    }
}
