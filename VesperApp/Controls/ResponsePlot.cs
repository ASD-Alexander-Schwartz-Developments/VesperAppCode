using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using VesperApp.Services;

namespace VesperApp.Controls
{
    /// <summary>
    /// Small log-frequency plot of the microphone chain's gain or phase (<see cref="MicChainSolver.ResponseCurve"/>),
    /// one curve per configured sample rate. Drawn directly — the app has no charting library and this needs none.
    /// </summary>
    public class ResponsePlot : Control
    {
        public static readonly StyledProperty<IReadOnlyList<MicChainSolver.ResponseCurve>?> CurvesProperty =
            AvaloniaProperty.Register<ResponsePlot, IReadOnlyList<MicChainSolver.ResponseCurve>?>(nameof(Curves));
        public static readonly StyledProperty<bool> ShowPhaseProperty =
            AvaloniaProperty.Register<ResponsePlot, bool>(nameof(ShowPhase));
        public static readonly StyledProperty<IBrush?> GridBrushProperty =
            AvaloniaProperty.Register<ResponsePlot, IBrush?>(nameof(GridBrush));
        public static readonly StyledProperty<IBrush?> TextBrushProperty =
            AvaloniaProperty.Register<ResponsePlot, IBrush?>(nameof(TextBrush));
        public static readonly StyledProperty<IBrush?> Curve1BrushProperty =
            AvaloniaProperty.Register<ResponsePlot, IBrush?>(nameof(Curve1Brush));
        public static readonly StyledProperty<IBrush?> Curve2BrushProperty =
            AvaloniaProperty.Register<ResponsePlot, IBrush?>(nameof(Curve2Brush));

        static ResponsePlot()
        {
            AffectsRender<ResponsePlot>(CurvesProperty, ShowPhaseProperty, GridBrushProperty, TextBrushProperty,
                                        Curve1BrushProperty, Curve2BrushProperty);
        }

        public IReadOnlyList<MicChainSolver.ResponseCurve>? Curves { get => GetValue(CurvesProperty); set => SetValue(CurvesProperty, value); }
        public bool ShowPhase { get => GetValue(ShowPhaseProperty); set => SetValue(ShowPhaseProperty, value); }
        public IBrush? GridBrush { get => GetValue(GridBrushProperty); set => SetValue(GridBrushProperty, value); }
        public IBrush? TextBrush { get => GetValue(TextBrushProperty); set => SetValue(TextBrushProperty, value); }
        public IBrush? Curve1Brush { get => GetValue(Curve1BrushProperty); set => SetValue(Curve1BrushProperty, value); }
        public IBrush? Curve2Brush { get => GetValue(Curve2BrushProperty); set => SetValue(Curve2BrushProperty, value); }

        private const double Left = 44, Bottom = 20, Top = 6, Right = 8;

        public override void Render(DrawingContext ctx)
        {
            base.Render(ctx);
            var curves = Curves;
            if (curves == null || curves.Count == 0 || Bounds.Width < 120 || Bounds.Height < 60) return;

            double w = Bounds.Width - Left - Right, h = Bounds.Height - Top - Bottom;
            double fmin = curves.Min(c => c.FreqHz[0]), fmax = curves.Max(c => c.FreqHz[^1]);
            double lmin = Math.Log10(fmin), lmax = Math.Log10(fmax);
            double ymin = ShowPhase ? 0 : -40, ymax = ShowPhase ? 90 : 5;
            double X(double f) => Left + (Math.Log10(f) - lmin) / (lmax - lmin) * w;
            double Y(double v) => Top + (ymax - Math.Clamp(v, ymin, ymax)) / (ymax - ymin) * h;

            var grid = new Pen(GridBrush ?? Brushes.Gray, 1);
            var axis = new Pen(GridBrush ?? Brushes.Gray, 1.5);
            var text = TextBrush ?? Brushes.Gray;
            var tf = new Typeface(FontFamily.Default);

            void Label(string s, double x, double y, bool right = false)
            {
                var ft = new FormattedText(s, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, tf, 10.5, text);
                ctx.DrawText(ft, new Point(right ? x - ft.Width : x - ft.Width / 2, y));
            }

            // horizontal grid + y labels
            double step = ShowPhase ? 30 : 10;
            for (double v = ymin; v <= ymax + 1e-9; v += step)
            {
                ctx.DrawLine(v == 0 && !ShowPhase ? axis : grid, new Point(Left, Y(v)), new Point(Left + w, Y(v)));
                Label(ShowPhase ? $"{v:0}°" : $"{v:0} dB", Left - 4, Y(v) - 7, right: true);
            }
            // decade grid + x labels
            for (int d = (int)Math.Ceiling(lmin); d <= (int)Math.Floor(lmax); d++)
            {
                double f = Math.Pow(10, d), x = X(f);
                ctx.DrawLine(grid, new Point(x, Top), new Point(x, Top + h));
                Label(f >= 1000 ? $"{f / 1000:0} kHz" : $"{f:0} Hz", x, Top + h + 3);
            }
            ctx.DrawRectangle(null, axis, new Rect(Left, Top, w, h));

            // curves
            IBrush?[] brushes = { Curve1Brush ?? Brushes.CornflowerBlue, Curve2Brush ?? Brushes.Orange };
            for (int ci = 0; ci < curves.Count; ci++)
            {
                var c = curves[ci];
                double[] vals = ShowPhase ? c.PhaseDeg : c.GainDb;
                var geo = new StreamGeometry();
                using (var g = geo.Open())
                {
                    g.BeginFigure(new Point(X(c.FreqHz[0]), Y(vals[0])), false);
                    for (int i = 1; i < vals.Length; i++) g.LineTo(new Point(X(c.FreqHz[i]), Y(vals[i])));
                    g.EndFigure(false);
                }
                ctx.DrawGeometry(null, new Pen(brushes[ci % brushes.Length], 2), geo);
            }
        }
    }
}
