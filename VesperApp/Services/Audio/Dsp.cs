using System;
using System.Collections.Generic;

namespace VesperApp.Services.Audio
{
    /// <summary>Small self-contained DSP helpers for the Live View (no external deps).</summary>
    public static class Dsp
    {
        private static readonly Dictionary<int, float[]> HannCache = new();

        public static float[] Hann(int n)
        {
            lock (HannCache)
            {
                if (HannCache.TryGetValue(n, out float[]? w)) return w;
                w = new float[n];
                for (int i = 0; i < n; i++) w[i] = (float)(0.5 - 0.5 * Math.Cos(2 * Math.PI * i / (n - 1)));
                HannCache[n] = w;
                return w;
            }
        }

        /// <summary>In-place iterative radix-2 FFT. Lengths must be a power of two.</summary>
        public static void Fft(Span<float> re, Span<float> im)
        {
            int n = re.Length;
            for (int i = 1, j = 0; i < n; i++)
            {
                int bit = n >> 1;
                for (; (j & bit) != 0; bit >>= 1) j ^= bit;
                j ^= bit;
                if (i < j) { (re[i], re[j]) = (re[j], re[i]); (im[i], im[j]) = (im[j], im[i]); }
            }
            for (int len = 2; len <= n; len <<= 1)
            {
                double ang = -2 * Math.PI / len;
                float wr = (float)Math.Cos(ang), wi = (float)Math.Sin(ang);
                for (int i = 0; i < n; i += len)
                {
                    float cr = 1, ci = 0;
                    int half = len >> 1;
                    for (int k = 0; k < half; k++)
                    {
                        int a = i + k, b = a + half;
                        float tr = re[b] * cr - im[b] * ci;
                        float ti = re[b] * ci + im[b] * cr;
                        re[b] = re[a] - tr; im[b] = im[a] - ti;
                        re[a] += tr; im[a] += ti;
                        float ncr = cr * wr - ci * wi;
                        ci = cr * wi + ci * wr; cr = ncr;
                    }
                }
            }
        }

        /// <summary>Windowed power spectrum in dBFS (0 dB = full-scale sine), <c>n/2</c> bins.
        /// <paramref name="x"/> must hold n samples; <paramref name="dbOut"/> n/2 values.</summary>
        public static void SpectrumDb(ReadOnlySpan<float> x, Span<float> dbOut, float[]? re = null, float[]? im = null)
        {
            int n = x.Length;
            re ??= new float[n]; im ??= new float[n];
            float[] w = Hann(n);
            double wsum = 0; for (int i = 0; i < n; i++) wsum += w[i];
            for (int i = 0; i < n; i++) { re[i] = x[i] * w[i]; im[i] = 0f; }
            Fft(re, im);
            float norm = (float)(2.0 / wsum);                    // sine amplitude normalisation
            int bins = Math.Min(dbOut.Length, n / 2);
            for (int k = 0; k < bins; k++)
            {
                float mag = MathF.Sqrt(re[k] * re[k] + im[k] * im[k]) * norm;
                dbOut[k] = 20f * MathF.Log10(mag + 1e-9f);
            }
        }

        public static float RmsDb(ReadOnlySpan<float> x)
        {
            double s = 0; for (int i = 0; i < x.Length; i++) s += x[i] * x[i];
            return 20f * (float)Math.Log10(Math.Sqrt(s / Math.Max(1, x.Length)) + 1e-9);
        }

        /// <summary>
        /// First-order sigma-delta re-modulation of PCM into a 1-bit "PDM" stream, for the
        /// PDM view. This is a reconstruction (the true PDM lives between the microphone and
        /// the MDF and never reaches the host); it shows what the bitstream looks like for the
        /// captured signal. Returns the number of bits written (pcm.Length * oversample).
        /// </summary>
        public static int SigmaDelta(ReadOnlySpan<float> pcm, int oversample, Span<byte> bits, ref float integrator)
        {
            int n = 0;
            float prev = pcm.Length > 0 ? pcm[0] : 0f;
            for (int i = 0; i < pcm.Length && n + oversample <= bits.Length; i++)
            {
                float cur = pcm[i];
                for (int k = 0; k < oversample; k++)
                {
                    float x = prev + (cur - prev) * (k / (float)oversample);   // linear interpolation
                    float y = integrator >= 0f ? 1f : -1f;
                    integrator += x - y;
                    bits[n++] = (byte)(y > 0 ? 1 : 0);
                }
                prev = cur;
            }
            return n;
        }
    }
}
