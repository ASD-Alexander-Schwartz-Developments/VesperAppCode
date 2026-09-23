using System;
using Avalonia;
using Avalonia.Media;
using VesperApp.Services.Audio;

namespace VesperApp.Controls.LiveAudio
{
    /// <summary>Oscilloscope: the last <see cref="WindowMs"/> of every enabled channel, min/max
    /// decimated per pixel column, with level meters on the right.</summary>
    public sealed class ScopeView : LiveAudioView
    {
        public static readonly StyledProperty<double> WindowMsProperty =
            AvaloniaProperty.Register<ScopeView, double>(nameof(WindowMs), 50);

        /// <summary>Vertical full scale (1 = +-1.0 = full scale). 0 = auto (fits the peak).</summary>
        public static readonly StyledProperty<double> ScaleProperty =
            AvaloniaProperty.Register<ScopeView, double>(nameof(Scale), 0);

        public double WindowMs { get => GetValue(WindowMsProperty); set => SetValue(WindowMsProperty, value); }
        public double Scale { get => GetValue(ScaleProperty); set => SetValue(ScaleProperty, value); }

        private float[] _buf = new float[0];
        private double _autoScale = 0.1;

        protected override void RenderPlot(DrawingContext ctx, Rect plot, LiveAudioBuffer src)
        {
            int n = Math.Max(16, (int)(src.SampleRate * WindowMs / 1000.0));
            if (_buf.Length != n) _buf = new float[n];

            // grid: 10 vertical divisions, 8 horizontal
            for (int i = 1; i < 10; i++) { double x = plot.X + plot.Width * i / 10; ctx.DrawLine(GridPen, new Point(x, plot.Y), new Point(x, plot.Bottom)); }
            for (int i = 1; i < 8; i++) { double y = plot.Y + plot.Height * i / 8; ctx.DrawLine(i == 4 ? AxisPen : GridPen, new Point(plot.X, y), new Point(plot.Right, y)); }

            // scale
            double peak = 0;
            for (int ch = 0; ch < src.Channels; ch++)
            {
                if (!ChannelOn(ch)) continue;
                src.CopyLatest(ch, _buf);
                for (int i = 0; i < n; i++) { float a = Math.Abs(_buf[i]); if (a > peak) peak = a; }
            }
            double scale;
            if (Scale > 0) scale = Scale;
            else
            {
                double target = Math.Max(0.002, Math.Min(1.0, peak * 1.25));
                _autoScale = target > _autoScale ? target : _autoScale * 0.96 + target * 0.04;  // fast attack, slow release
                scale = _autoScale;
            }

            double midY = plot.Y + plot.Height / 2, half = plot.Height / 2;
            int px = Math.Max(1, (int)plot.Width);
            for (int ch = 0; ch < src.Channels; ch++)
            {
                if (!ChannelOn(ch)) continue;
                src.CopyLatest(ch, _buf);
                var geo = new StreamGeometry();
                using (var g = geo.Open())
                {
                    bool first = true;
                    double spp = n / (double)px;   // samples per pixel
                    for (int x = 0; x < px; x++)
                    {
                        int a = (int)(x * spp), b = Math.Min(n, (int)((x + 1) * spp) + 1);
                        float lo = float.MaxValue, hi = float.MinValue;
                        for (int i = a; i < b; i++) { float v = _buf[i]; if (v < lo) lo = v; if (v > hi) hi = v; }
                        if (lo == float.MaxValue) continue;
                        double yHi = midY - Math.Clamp(hi / scale, -1, 1) * half;
                        double yLo = midY - Math.Clamp(lo / scale, -1, 1) * half;
                        double X = plot.X + x;
                        if (first) { g.BeginFigure(new Point(X, yHi), false); first = false; } else g.LineTo(new Point(X, yHi));
                        if (yLo - yHi > 1) g.LineTo(new Point(X, yLo));
                    }
                    g.EndFigure(false);
                }
                ctx.DrawGeometry(null, ChannelPens[ch % ChannelPens.Length], geo);
            }

            // level meters (block peak) on the right edge inside the plot
            double mw = 6, mx = plot.Right - 4 - mw * src.Channels;
            for (int ch = 0; ch < src.Channels; ch++)
            {
                double lvl = Math.Clamp(src.BlockPeak[Math.Min(ch, src.BlockPeak.Length - 1)], 0, 1);
                double h = Math.Max(1, plot.Height * Math.Pow(lvl, 0.5));
                var r = new Rect(mx + ch * mw, plot.Bottom - h, mw - 1, h);
                ctx.FillRectangle(ChannelOn(ch) ? ChannelBrushes[ch % ChannelBrushes.Length] : IdleBrush, r);
            }

            DrawText(ctx, $"±{FormatScale(scale)}", new Point(plot.X + 4, plot.Y + 3), TextBrush, 10);
        }

        protected override void RenderAxes(DrawingContext ctx, Rect plot, LiveAudioBuffer src)
        {
            for (int i = 0; i <= 10; i += 2)
            {
                double x = plot.X + plot.Width * i / 10;
                double ms = -WindowMs * (10 - i) / 10;
                DrawText(ctx, i == 10 ? "now" : $"{ms:0.#} ms", new Point(x - 12, plot.Bottom + 4), TextBrush, 9);
            }
            DrawTextRight(ctx, "+FS", new Point(plot.X - 4, plot.Y - 2), TextBrush, 9);
            DrawTextRight(ctx, "0", new Point(plot.X - 4, plot.Y + plot.Height / 2 - 6), TextBrush, 9);
            DrawTextRight(ctx, "−FS", new Point(plot.X - 4, plot.Bottom - 12), TextBrush, 9);
        }

        private static string FormatScale(double s) => s >= 0.999 ? "FS" : s >= 0.01 ? $"{s * 100:0.#}% FS" : $"{20 * Math.Log10(s):0} dBFS";
    }
}
