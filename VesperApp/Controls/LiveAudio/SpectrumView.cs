using System;
using Avalonia;
using Avalonia.Media;
using VesperApp.Services.Audio;

namespace VesperApp.Controls.LiveAudio
{
    /// <summary>Real-time magnitude spectrum (dBFS) per enabled channel with slow peak hold.</summary>
    public sealed class SpectrumView : LiveAudioView
    {
        public static readonly StyledProperty<int> FftSizeProperty =
            AvaloniaProperty.Register<SpectrumView, int>(nameof(FftSize), 4096);
        public static readonly StyledProperty<bool> LogFrequencyProperty =
            AvaloniaProperty.Register<SpectrumView, bool>(nameof(LogFrequency), false);
        public static readonly StyledProperty<double> MinDbProperty =
            AvaloniaProperty.Register<SpectrumView, double>(nameof(MinDb), -110);
        public static readonly StyledProperty<double> MaxDbProperty =
            AvaloniaProperty.Register<SpectrumView, double>(nameof(MaxDb), 0);
        public static readonly StyledProperty<bool> PeakHoldProperty =
            AvaloniaProperty.Register<SpectrumView, bool>(nameof(PeakHold), true);

        public int FftSize { get => GetValue(FftSizeProperty); set => SetValue(FftSizeProperty, value); }
        public bool LogFrequency { get => GetValue(LogFrequencyProperty); set => SetValue(LogFrequencyProperty, value); }
        public double MinDb { get => GetValue(MinDbProperty); set => SetValue(MinDbProperty, value); }
        public double MaxDb { get => GetValue(MaxDbProperty); set => SetValue(MaxDbProperty, value); }
        public bool PeakHold { get => GetValue(PeakHoldProperty); set => SetValue(PeakHoldProperty, value); }

        private float[] _x = new float[0], _re = new float[0], _im = new float[0];
        private float[][] _db = new float[0][], _smooth = new float[0][], _peak = new float[0][];
        private static readonly IPen[] PeakPens = BuildPeakPens();
        private static IPen[] BuildPeakPens()
        {
            var p = new IPen[ChannelColors.Length];
            for (int i = 0; i < p.Length; i++) p[i] = new Pen(new SolidColorBrush(ChannelColors[i], 0.35), 1);
            return p;
        }

        private void Ensure(int n, int channels)
        {
            if (_x.Length != n) { _x = new float[n]; _re = new float[n]; _im = new float[n]; }
            if (_db.Length != channels || (_db.Length > 0 && _db[0].Length != n / 2))
            {
                _db = new float[channels][]; _smooth = new float[channels][]; _peak = new float[channels][];
                for (int c = 0; c < channels; c++)
                {
                    _db[c] = new float[n / 2]; _smooth[c] = new float[n / 2]; _peak[c] = new float[n / 2];
                    Array.Fill(_smooth[c], -140f); Array.Fill(_peak[c], -140f);
                }
            }
        }

        private double FreqToX(double f, Rect plot, double fMax)
        {
            if (LogFrequency)
            {
                double fMin = 20;
                double u = Math.Log(Math.Max(f, fMin) / fMin) / Math.Log(fMax / fMin);
                return plot.X + u * plot.Width;
            }
            return plot.X + f / fMax * plot.Width;
        }

        protected override void RenderPlot(DrawingContext ctx, Rect plot, LiveAudioBuffer src)
        {
            int n = FftSize; if (n < 256) n = 256; if ((n & (n - 1)) != 0) n = 4096;
            Ensure(n, src.Channels);
            double fMax = src.SampleRate / 2.0, range = MaxDb - MinDb;

            // grid: dB lines every 20 dB, frequency ticks
            for (double db = MinDb; db <= MaxDb; db += 20)
            {
                double y = plot.Bottom - (db - MinDb) / range * plot.Height;
                ctx.DrawLine(GridPen, new Point(plot.X, y), new Point(plot.Right, y));
            }
            foreach (double f in FreqTicks(fMax))
            {
                double x = FreqToX(f, plot, fMax);
                ctx.DrawLine(GridPen, new Point(x, plot.Y), new Point(x, plot.Bottom));
            }

            for (int ch = 0; ch < src.Channels; ch++)
            {
                if (!ChannelOn(ch)) continue;
                src.CopyLatest(ch, _x);
                Dsp.SpectrumDb(_x, _db[ch], _re, _im);
                float[] s = _smooth[ch], p = _peak[ch], d = _db[ch];
                for (int k = 0; k < d.Length; k++)
                {
                    s[k] = s[k] * 0.6f + d[k] * 0.4f;
                    p[k] = d[k] > p[k] ? d[k] : p[k] - 0.15f;   // slow decay
                }
                if (PeakHold) ctx.DrawGeometry(null, PeakPens[ch % PeakPens.Length], Curve(p, plot, fMax, src.SampleRate, n));
                ctx.DrawGeometry(null, ChannelPens[ch % ChannelPens.Length], Curve(s, plot, fMax, src.SampleRate, n));
            }
        }

        private StreamGeometry Curve(float[] db, Rect plot, double fMax, int fs, int n)
        {
            var geo = new StreamGeometry();
            double range = MaxDb - MinDb;
            int px = Math.Max(1, (int)plot.Width);
            using (var g = geo.Open())
            {
                bool first = true;
                double binHz = fs / (double)n;
                double lastX = double.NegativeInfinity; float acc = -200; int count = 0;
                int kStart = LogFrequency ? Math.Max(1, (int)(20 / binHz)) : 1;
                for (int k = kStart; k < db.Length; k++)
                {
                    double x = Math.Floor(FreqToX(k * binHz, plot, fMax));
                    if (x != lastX && count > 0)
                    {
                        double y = plot.Bottom - Math.Clamp((acc - MinDb) / range, 0, 1.02) * plot.Height;
                        if (first) { g.BeginFigure(new Point(lastX, y), false); first = false; } else g.LineTo(new Point(lastX, y));
                        acc = -200; count = 0;
                    }
                    lastX = x; if (db[k] > acc) acc = db[k]; count++;   // max per pixel column
                }
                if (count > 0)
                {
                    double y = plot.Bottom - Math.Clamp((acc - MinDb) / range, 0, 1.02) * plot.Height;
                    if (first) g.BeginFigure(new Point(lastX, y), false); else g.LineTo(new Point(lastX, y));
                }
                g.EndFigure(false);
            }
            return geo;
        }

        private static double[] FreqTicks(double fMax)
        {
            double[] all = { 20, 50, 100, 200, 500, 1000, 2000, 5000, 10000, 20000, 50000 };
            var list = new System.Collections.Generic.List<double>();
            foreach (double f in all) if (f < fMax) list.Add(f);
            return list.ToArray();
        }

        protected override void RenderAxes(DrawingContext ctx, Rect plot, LiveAudioBuffer src)
        {
            double fMax = src.SampleRate / 2.0, range = MaxDb - MinDb;
            for (double db = MinDb; db <= MaxDb; db += 20)
            {
                double y = plot.Bottom - (db - MinDb) / range * plot.Height;
                DrawTextRight(ctx, $"{db:0}", new Point(plot.X - 4, y - 6), TextBrush, 9);
            }
            DrawTextRight(ctx, "dBFS", new Point(plot.X - 4, plot.Y - 16), TextBrush, 9);
            if (LogFrequency)
            {
                foreach (double f in FreqTicks(fMax))
                {
                    double x = FreqToX(f, plot, fMax);
                    DrawText(ctx, f >= 1000 ? $"{f / 1000:0.#}k" : $"{f:0}", new Point(x - 8, plot.Bottom + 4), TextBrush, 9);
                }
            }
            else
            {
                for (int i = 0; i <= 8; i++)
                {
                    double f = fMax * i / 8, x = plot.X + plot.Width * i / 8;
                    DrawText(ctx, f >= 1000 ? $"{f / 1000:0.#} kHz" : $"{f:0} Hz", new Point(x - 14, plot.Bottom + 4), TextBrush, 9);
                }
            }
        }
    }
}
