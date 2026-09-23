using System;
using System.Collections.Generic;
using System.Linq;
using ASD.Contracts;

namespace VesperApp.Services.Audio
{
    /// <summary>
    /// Built-in demo classifier for Live View: a handful of spectral/temporal features
    /// (level, centroid, band ratios, tonality, modulation rate) mapped to broad sound
    /// classes by rules. Deliberately simple and honest about it (its Name says "demo");
    /// a real species model plugs in through <see cref="ISoundClassifier"/> and replaces
    /// it without UI changes.
    /// </summary>
    public sealed class HeuristicSoundClassifier : ISoundClassifier
    {
        public string Name => "Demo heuristic (rules)";
        public int PreferredSampleRate => 0;
        public double WindowSeconds => 1.0;

        private float[] _re = new float[0], _im = new float[0], _db = new float[0];

        public SoundClassification Classify(ReadOnlySpan<float> mono, int sampleRate)
        {
            if (mono.Length < 1024 || sampleRate <= 0) return SoundClassification.Empty;

            // ---- level ----
            float rmsDb = Dsp.RmsDb(mono);
            if (rmsDb < -70f)
                return new SoundClassification(new[] { new SoundLabel("Quiet", 0.9) });

            // ---- spectrum (largest power-of-two window <= 8192) ----
            int n = 1024; while (n * 2 <= Math.Min(mono.Length, 8192)) n *= 2;
            if (_re.Length != n) { _re = new float[n]; _im = new float[n]; _db = new float[n / 2]; }
            Dsp.SpectrumDb(mono.Slice(mono.Length - n), _db, _re, _im);
            double binHz = sampleRate / (double)n;

            double total = 0, cen = 0, low = 0, mid = 0, high = 0, ultra = 0, geo = 0;
            int kMin = Math.Max(1, (int)(20 / binHz));
            for (int k = kMin; k < _db.Length; k++)
            {
                double p = Math.Pow(10, _db[k] / 10.0); double f = k * binHz;
                total += p; cen += p * f; geo += Math.Log(p + 1e-20);
                if (f < 300) low += p; else if (f < 3000) mid += p; else if (f < 12000) high += p; else ultra += p;
            }
            if (total <= 0) return SoundClassification.Empty;
            cen /= total;
            double flatness = Math.Exp(geo / (_db.Length - kMin)) / (total / (_db.Length - kMin));   // 1 = white, ->0 tonal
            double lowR = low / total, midR = mid / total, highR = high / total, ultraR = ultra / total;

            // mains hum: dominant line at 50/60 Hz (or harmonic) and low band dominant
            int kPeak = kMin; for (int k = kMin; k < _db.Length; k++) if (_db[k] > _db[kPeak]) kPeak = k;
            double fPeak = kPeak * binHz;
            bool humLine = Near(fPeak, 50, 4) || Near(fPeak, 60, 4) || Near(fPeak, 100, 4) || Near(fPeak, 120, 4);

            // ---- temporal: envelope modulation rate (syllables / pulses per second) ----
            int hop = Math.Max(1, sampleRate / 200);                 // 5 ms envelope
            int m = mono.Length / hop; if (m < 8) m = 8;
            double[] env = new double[m];
            for (int i = 0; i < m; i++)
            {
                int a = i * hop, b = Math.Min(mono.Length, a + hop); double acc = 0;
                for (int j = a; j < b; j++) acc += mono[j] * mono[j];
                env[i] = Math.Sqrt(acc / Math.Max(1, b - a));
            }
            double mean = env.Average(); double sd = Math.Sqrt(env.Select(e => (e - mean) * (e - mean)).Average());
            double modDepth = mean > 0 ? sd / mean : 0;              // 0 steady .. >1 very pulsed
            int crossings = 0; for (int i = 1; i < m; i++) if ((env[i] > mean) != (env[i - 1] > mean)) crossings++;
            double modRate = crossings / 2.0 / (mono.Length / (double)sampleRate);   // Hz

            // ---- rules -> scores ----
            var s = new Dictionary<string, double>();
            s["Mains hum / electrical"] = (humLine ? 0.6 : 0) + Clamp01((lowR - 0.4) * 2) * 0.4;
            s["Bat echolocation"] = Clamp01((ultraR - 0.3) * 2.5) * (modDepth > 0.6 ? 1.0 : 0.5);
            s["Ultrasonic tone / electronics"] = Clamp01((ultraR - 0.3) * 2.5) * (modDepth <= 0.6 ? 0.9 : 0.2);
            s["Bird song"] = Clamp01(1 - Math.Abs(cen - 4500) / 3500) * Clamp01((0.35 - flatness) * 4) * Clamp01(modDepth * 1.5) * (modRate >= 2 && modRate <= 25 ? 1 : 0.4);
            s["Insect stridulation"] = Clamp01((cen - 3000) / 6000) * Clamp01(highR * 1.5) * (modRate > 8 ? 1 : 0.5) * Clamp01(1 - modDepth);
            s["Human speech"] = Clamp01(1 - Math.Abs(cen - 1200) / 1500) * Clamp01(midR * 1.4) * (modRate >= 2 && modRate <= 8 ? 1 : 0.3) * Clamp01(modDepth * 2);
            s["Mechanical / engine"] = Clamp01(1 - Math.Abs(cen - 500) / 900) * Clamp01((0.4 - flatness) * 3) * Clamp01(1 - modDepth * 1.5) * (humLine ? 0.3 : 1);
            s["Wind / rain / broadband noise"] = Clamp01((flatness - 0.3) * 2.5) * Clamp01(1 - modDepth);
            s["Impact / click"] = Clamp01((modDepth - 1.0) * 1.5) * Clamp01(flatness * 2);

            double sum = s.Values.Sum() + 1e-6;
            var top = s.OrderByDescending(kv => kv.Value).Take(4)
                .Select(kv => new SoundLabel(kv.Key, Math.Round(kv.Value / sum, 3))).ToList();
            if (top.Count == 0 || top[0].Confidence < 0.15)
                top.Insert(0, new SoundLabel("Unclassified sound", 0.5));
            return new SoundClassification(top);
        }

        private static bool Near(double f, double target, double tol) => Math.Abs(f - target) <= tol;
        private static double Clamp01(double v) => v < 0 ? 0 : v > 1 ? 1 : v;
    }
}
