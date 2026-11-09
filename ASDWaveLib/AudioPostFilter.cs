using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;

namespace ASDWaveLib
{
    /// <summary>What the post filters should do with one recording.</summary>
    /// <param name="PortEq">Remove the board sound-port resonance (Vesper / Pipistrelle footprint only).</param>
    /// <param name="LseNotch">Notch the 32.768 kHz RTC-crystal line (and its harmonics below Nyquist).</param>
    public sealed record PostFilterOptions(bool PortEq, bool LseNotch);

    /// <summary>What the post filters did, for the sidecar note and the decode summary.</summary>
    public sealed class PostFilterReport
    {
        public bool PortEqApplied { get; set; }
        public double PortEqF0Hz { get; set; }
        public double PortEqCutDb { get; set; }
        public double PortEqQ { get; set; }

        /// <summary>The crystal line found in this recording (recorded scale), or null when none stood out.</summary>
        public double? LseHz { get; set; }
        public double LseProminenceDb { get; set; }
        /// <summary>Frequencies notched: the line and the harmonics that stood out below Nyquist.</summary>
        public List<double> NotchedHz { get; } = new();

        public bool AnyApplied => PortEqApplied || NotchedHz.Count > 0;

        public string Describe()
        {
            var ci = CultureInfo.InvariantCulture;
            var parts = new List<string>();
            if (PortEqApplied)
                parts.Add(string.Format(ci, "PortEQ: board sound-port resonance removed ({0:0.0} dB at {1:0} Hz, Q {2:0.00})", PortEqCutDb, PortEqF0Hz, PortEqQ));
            if (NotchedHz.Count > 0)
                parts.Add("LseNotch: " + string.Join(", ", NotchedHz.ConvertAll(f => f.ToString("0.0", ci) + " Hz")) +
                          string.Format(ci, " (line {0:+0.0} dB over the local floor)", LseProminenceDb));
            else if (LseHz is null)
                parts.Add("LseNotch: no 32.768 kHz line stood out, nothing notched");
            return string.Join(Environment.NewLine, parts);
        }
    }

    /// <summary>
    /// Optional post-processing for microphone recordings, applied on export next to the untouched raw WAV.
    /// <para>
    /// <b>Port EQ</b> — the SPH0641 behind the PCB sound hole of the Vesper / Pipistrelle footprint (0.8 mm board,
    /// Ø0.5 mm hole) forms an acoustic resonance: a +14.0 dB peak, Q 1.40 (fitted to the Knowles SPH0641LU4H-1 typical
    /// ultrasonic response within 0.11 dB rms, 12–36 kHz), centred at 26.5 kHz on our boards (25.0 kHz on Knowles'
    /// fixture). The EQ is the exact inverse of that peak — a cut-only, linear-phase FIR — and leaves the microphone's
    /// own slow rise alone. KOL is excluded: its boards (1.55 mm main, 0.4 mm external) put the resonance elsewhere.
    /// Bench: asd-interfaces projects/u585-bench-2026-11-08.md.
    /// </para>
    /// <para>
    /// <b>32.768 kHz notch</b> — the RTC crystal shows up as a narrow line 9–33 dB over the floor. It is located per
    /// recording (it sits a little off 32 768 Hz in the recorded scale and moves ~0.5 % when the microphone clock loses
    /// its lock) and removed with a narrow zero-phase notch (≈ 65 Hz wide), together with its harmonics that stand out
    /// below Nyquist.
    /// </para>
    /// </summary>
    public static class AudioPostFilter
    {
        public const double BoardPortF0Hz = 26500.0;
        public const double BoardPortPeakDb = 14.0;
        public const double BoardPortQ = 1.40;

        public const double LseNominalHz = 32768.0;
        /// <summary>Search window around the nominal line (covers rate offset and a lost microphone-clock lock).</summary>
        public const double LseSearchFraction = 0.015;
        /// <summary>The line is notched only when it stands this far over the surrounding floor.</summary>
        public const double LseMinProminenceDb = 6.0;
        public const double LseNotchQ = 500.0;

        /// <summary>The board-port resonance (dB) at <paramref name="f"/>: an analog peaking section.</summary>
        public static double PortResonanceDb(double f, double f0 = BoardPortF0Hz, double peakDb = BoardPortPeakDb, double q = BoardPortQ)
        {
            if (f <= 0) return 0;
            var s = new Complex(0, f / f0);
            double a = Math.Pow(10, peakDb / 40.0);
            Complex h = (s * s + s * (a / q) + 1) / (s * s + s / (a * q) + 1);
            return 20 * Math.Log10(h.Magnitude);
        }

        /// <summary>Applies the selected filters; the input is not modified.</summary>
        public static short[] Process(short[] pcm, double sampleRate, PostFilterOptions options, out PostFilterReport report)
        {
            report = new PostFilterReport();
            if (pcm.Length == 0 || sampleRate <= 0) return (short[])pcm.Clone();

            double[] x = new double[pcm.Length];
            for (int i = 0; i < x.Length; i++) x[i] = pcm[i];

            if (options.PortEq)
            {
                x = ApplyFir(x, DesignPortEq(sampleRate));
                report.PortEqApplied = true;
                report.PortEqF0Hz = BoardPortF0Hz;
                report.PortEqCutDb = -BoardPortPeakDb;
                report.PortEqQ = BoardPortQ;
            }

            if (options.LseNotch && LseNominalHz * (1 + LseSearchFraction) < sampleRate / 2)
            {
                double? line = FindLine(x, sampleRate, LseNominalHz, LseSearchFraction, out double prom);
                if (line is double f1 && prom >= LseMinProminenceDb)
                {
                    report.LseHz = f1;
                    report.LseProminenceDb = prom;
                    x = NotchZeroPhase(x, sampleRate, f1, LseNotchQ);
                    report.NotchedHz.Add(f1);
                    for (int k = 2; k * f1 < 0.45 * sampleRate; k++)
                    {
                        double? h = FindLine(x, sampleRate, k * f1, 0.002, out double ph);
                        if (h is double fh && ph >= LseMinProminenceDb)
                        {
                            x = NotchZeroPhase(x, sampleRate, fh, LseNotchQ);
                            report.NotchedHz.Add(fh);
                        }
                    }
                }
            }

            var y = new short[x.Length];
            for (int i = 0; i < y.Length; i++)
                y[i] = (short)Math.Clamp(Math.Round(x[i]), short.MinValue, short.MaxValue);
            return y;
        }

        // ───────────────────────────── port EQ ─────────────────────────────

        /// <summary>Linear-phase FIR (odd length, ~10 ms) whose magnitude is the inverse of the port resonance.</summary>
        public static double[] DesignPortEq(double sampleRate)
        {
            int taps = (int)Math.Round(sampleRate * 0.010) | 1;
            taps = Math.Clamp(taps, 255, 4095);
            int m = 1;
            while (m < 8 * taps) m <<= 1;
            int half = m / 2;

            // zero-phase frequency samples -> real even impulse response (cosine series)
            var g = new double[half + 1];
            for (int k = 0; k <= half; k++)
                g[k] = Math.Pow(10, -PortResonanceDb(k * sampleRate / m) / 20.0);

            int c = taps / 2;
            var h = new double[taps];
            for (int n = -c; n <= c; n++)
            {
                double acc = g[0] + g[half] * Math.Cos(Math.PI * n);
                for (int k = 1; k < half; k++) acc += 2 * g[k] * Math.Cos(2 * Math.PI * k * n / m);
                double w = 0.5 * (1 + Math.Cos(Math.PI * n / (c + 1)));    // Hann
                h[n + c] = acc / m * w;
            }
            return h;
        }

        /// <summary>FFT overlap-add convolution, output aligned with the input (the FIR delay removed).</summary>
        public static double[] ApplyFir(double[] x, double[] h)
        {
            int n = h.Length, d = n / 2;
            int fft = 1;
            while (fft < 4 * n) fft <<= 1;
            int block = fft - n + 1;

            var hf = new Complex[fft];
            for (int i = 0; i < n; i++) hf[i] = h[i];
            Fft(hf, false);

            var y = new double[x.Length + n - 1];
            var buf = new Complex[fft];
            for (int start = 0; start < x.Length; start += block)
            {
                int len = Math.Min(block, x.Length - start);
                Array.Clear(buf);
                for (int i = 0; i < len; i++) buf[i] = x[start + i];
                Fft(buf, false);
                for (int i = 0; i < fft; i++) buf[i] *= hf[i];
                Fft(buf, true);
                int lim = Math.Min(fft, y.Length - start);
                for (int i = 0; i < lim; i++) y[start + i] += buf[i].Real;
            }
            var outp = new double[x.Length];
            Array.Copy(y, d, outp, 0, x.Length);
            return outp;
        }

        // ───────────────────────────── 32.768 kHz notch ─────────────────────────────

        /// <summary>
        /// The strongest narrow line within <paramref name="centerHz"/> × (1 ± <paramref name="fraction"/>) in the
        /// Welch-averaged spectrum (≈ 3 Hz bins), with its prominence over the median of the surrounding ±1.5 kHz.
        /// </summary>
        public static double? FindLine(double[] x, double sampleRate, double centerHz, double fraction, out double prominenceDb)
        {
            prominenceDb = 0;
            int nfft = 1;
            while (nfft < sampleRate / 3.0) nfft <<= 1;          // ~3 Hz resolution
            if (x.Length < nfft) { nfft = 1; while (nfft * 2 <= x.Length) nfft <<= 1; }
            if (nfft < 1024) return null;

            var win = new double[nfft];
            for (int i = 0; i < nfft; i++) win[i] = 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / nfft);
            var p = new double[nfft / 2 + 1];
            var buf = new Complex[nfft];
            int hop = nfft / 2, segs = 0;
            int maxSegs = 64;                                     // first ~30 s at 100 kHz is plenty
            for (int s = 0; s + nfft <= x.Length && segs < maxSegs; s += hop, segs++)
            {
                for (int i = 0; i < nfft; i++) buf[i] = x[s + i] * win[i];
                Fft(buf, false);
                for (int k = 0; k < p.Length; k++) p[k] += buf[k].Magnitude * buf[k].Magnitude;
            }
            if (segs == 0) return null;

            double bin = sampleRate / nfft;
            int lo = (int)Math.Floor(centerHz * (1 - fraction) / bin), hi = (int)Math.Ceiling(centerHz * (1 + fraction) / bin);
            lo = Math.Max(lo, 1); hi = Math.Min(hi, p.Length - 2);
            if (hi <= lo) return null;
            int best = lo;
            for (int k = lo; k <= hi; k++) if (p[k] > p[best]) best = k;

            int span = (int)(1500 / bin), guard = (int)Math.Ceiling(50 / bin);
            var floor = new List<double>();
            for (int k = Math.Max(1, best - span); k <= Math.Min(p.Length - 1, best + span); k++)
                if (Math.Abs(k - best) > guard) floor.Add(p[k]);
            if (floor.Count == 0) return null;
            floor.Sort();
            double med = floor[floor.Count / 2];
            prominenceDb = 10 * Math.Log10(p[best] / Math.Max(med, 1e-30));

            // parabolic interpolation on the log spectrum for a sub-bin frequency
            double a = Math.Log(p[best - 1] + 1e-30), b = Math.Log(p[best] + 1e-30), cc = Math.Log(p[best + 1] + 1e-30);
            double den = a - 2 * b + cc;
            double off = den != 0 ? 0.5 * (a - cc) / den : 0;
            return (best + Math.Clamp(off, -0.5, 0.5)) * bin;
        }

        /// <summary>Second-order notch (RBJ) run forward and backward: zero phase, double depth.</summary>
        public static double[] NotchZeroPhase(double[] x, double sampleRate, double f0, double q)
        {
            double w0 = 2 * Math.PI * f0 / sampleRate, alpha = Math.Sin(w0) / (2 * q), cw = Math.Cos(w0);
            double a0 = 1 + alpha;
            double b0 = 1 / a0, b1 = -2 * cw / a0, b2 = 1 / a0, a1 = -2 * cw / a0, a2 = (1 - alpha) / a0;

            double[] Run(double[] v, bool reverse)
            {
                var o = new double[v.Length];
                double x1 = 0, x2 = 0, y1 = 0, y2 = 0;
                for (int j = 0; j < v.Length; j++)
                {
                    int i = reverse ? v.Length - 1 - j : j;
                    double xi = v[i];
                    double yi = b0 * xi + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
                    x2 = x1; x1 = xi; y2 = y1; y1 = yi;
                    o[i] = yi;
                }
                return o;
            }
            return Run(Run(x, false), true);
        }

        // ───────────────────────────── FFT ─────────────────────────────

        /// <summary>In-place iterative radix-2 FFT; <paramref name="inverse"/> scales by 1/N.</summary>
        public static void Fft(Complex[] a, bool inverse)
        {
            int n = a.Length;
            for (int i = 1, j = 0; i < n; i++)
            {
                int bit = n >> 1;
                for (; (j & bit) != 0; bit >>= 1) j ^= bit;
                j ^= bit;
                if (i < j) (a[i], a[j]) = (a[j], a[i]);
            }
            for (int len = 2; len <= n; len <<= 1)
            {
                double ang = 2 * Math.PI / len * (inverse ? 1 : -1);
                var wl = new Complex(Math.Cos(ang), Math.Sin(ang));
                for (int i = 0; i < n; i += len)
                {
                    Complex w = Complex.One;
                    for (int k = 0; k < len / 2; k++)
                    {
                        Complex u = a[i + k], v = a[i + k + len / 2] * w;
                        a[i + k] = u + v;
                        a[i + k + len / 2] = u - v;
                        w *= wl;
                    }
                }
            }
            if (inverse) for (int i = 0; i < n; i++) a[i] /= n;
        }
    }
}
