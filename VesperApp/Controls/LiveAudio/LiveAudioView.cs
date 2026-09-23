using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using VesperApp.Services.Audio;

namespace VesperApp.Controls.LiveAudio
{
    /// <summary>
    /// Base for the Live View visualisers: a dark instrument panel that redraws itself
    /// ~30x/s from a shared <see cref="LiveAudioBuffer"/> while attached to the visual
    /// tree. Subclasses implement <see cref="RenderPlot"/> inside the plot rectangle.
    /// </summary>
    public abstract class LiveAudioView : Control
    {
        public static readonly StyledProperty<LiveAudioBuffer?> SourceProperty =
            AvaloniaProperty.Register<LiveAudioView, LiveAudioBuffer?>(nameof(Source));

        /// <summary>Bit mask of channels to draw (bit n = channel n).</summary>
        public static readonly StyledProperty<int> ChannelMaskProperty =
            AvaloniaProperty.Register<LiveAudioView, int>(nameof(ChannelMask), 0xF);

        /// <summary>Channel for single-channel views (-1 = mono mix of all channels).</summary>
        public static readonly StyledProperty<int> SelectedChannelProperty =
            AvaloniaProperty.Register<LiveAudioView, int>(nameof(SelectedChannel), 0);

        public static readonly StyledProperty<string?> TitleProperty =
            AvaloniaProperty.Register<LiveAudioView, string?>(nameof(Title));

        public LiveAudioBuffer? Source { get => GetValue(SourceProperty); set => SetValue(SourceProperty, value); }
        public int ChannelMask { get => GetValue(ChannelMaskProperty); set => SetValue(ChannelMaskProperty, value); }
        public int SelectedChannel { get => GetValue(SelectedChannelProperty); set => SetValue(SelectedChannelProperty, value); }
        public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

        // ---- palette (instrument look, theme independent) ----
        protected static readonly IBrush PanelBrush = new SolidColorBrush(Color.Parse("#0B1220"));
        protected static readonly IBrush PlotBrush = new SolidColorBrush(Color.Parse("#0F172A"));
        protected static readonly IPen GridPen = new Pen(new SolidColorBrush(Color.Parse("#1E293B")), 1);
        protected static readonly IPen AxisPen = new Pen(new SolidColorBrush(Color.Parse("#334155")), 1);
        protected static readonly IBrush TextBrush = new SolidColorBrush(Color.Parse("#94A3B8"));
        protected static readonly IBrush TitleBrush = new SolidColorBrush(Color.Parse("#E2E8F0"));
        protected static readonly IBrush IdleBrush = new SolidColorBrush(Color.Parse("#475569"));
        protected static readonly Typeface Mono = new("Consolas, Menlo, monospace");
        protected static readonly Typeface Sans = new("Segoe UI, Inter, sans-serif");

        public static readonly Color[] ChannelColors =
        {
            Color.Parse("#22D3EE"), // ch0 cyan
            Color.Parse("#A3E635"), // ch1 lime
            Color.Parse("#FBBF24"), // ch2 amber
            Color.Parse("#F472B6"), // ch3 pink
            Color.Parse("#818CF8"), Color.Parse("#34D399"), Color.Parse("#F87171"), Color.Parse("#E879F9"),
        };
        protected static readonly IPen[] ChannelPens = BuildPens(1.5);
        protected static readonly IBrush[] ChannelBrushes = BuildBrushes();

        private static IPen[] BuildPens(double w)
        {
            var pens = new IPen[ChannelColors.Length];
            for (int i = 0; i < pens.Length; i++) pens[i] = new Pen(new SolidColorBrush(ChannelColors[i]), w, lineJoin: PenLineJoin.Round);
            return pens;
        }
        private static IBrush[] BuildBrushes()
        {
            var b = new IBrush[ChannelColors.Length];
            for (int i = 0; i < b.Length; i++) b[i] = new SolidColorBrush(ChannelColors[i]);
            return b;
        }

        private DispatcherTimer? _timer;

        protected LiveAudioView()
        {
            ClipToBounds = true;
            MinHeight = 120;
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(33), DispatcherPriority.Render, (_, _) => InvalidateVisual());
            _timer.Start();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            _timer?.Stop();
            _timer = null;
            base.OnDetachedFromVisualTree(e);
        }

        protected const double PadLeft = 52, PadRight = 12, PadTop = 26, PadBottom = 22;

        public sealed override void Render(DrawingContext ctx)
        {
            Rect b = new(Bounds.Size);
            ctx.FillRectangle(PanelBrush, b, 6);
            Rect plot = new(PadLeft, PadTop, Math.Max(10, b.Width - PadLeft - PadRight), Math.Max(10, b.Height - PadTop - PadBottom));
            ctx.FillRectangle(PlotBrush, plot, 3);

            if (!string.IsNullOrEmpty(Title))
                DrawText(ctx, Title!, new Point(PadLeft, 5), TitleBrush, 12, Sans, FontWeight.SemiBold);

            LiveAudioBuffer? src = Source;
            if (src == null || src.TotalFrames == 0)
            {
                DrawText(ctx, "no signal", new Point(plot.Center.X - 24, plot.Center.Y - 8), IdleBrush, 12, Sans);
                return;
            }
            using (ctx.PushClip(plot.Inflate(new Thickness(0, 0, 0, 0))))
            {
                RenderPlot(ctx, plot, src);
            }
            RenderAxes(ctx, plot, src);
        }

        protected abstract void RenderPlot(DrawingContext ctx, Rect plot, LiveAudioBuffer src);
        protected virtual void RenderAxes(DrawingContext ctx, Rect plot, LiveAudioBuffer src) { }

        protected static void DrawText(DrawingContext ctx, string text, Point at, IBrush brush, double size = 10, Typeface? face = null, FontWeight weight = FontWeight.Normal)
        {
            var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface((face ?? Mono).FontFamily, FontStyle.Normal, weight), size, brush);
            ctx.DrawText(ft, at);
        }

        protected static void DrawTextRight(DrawingContext ctx, string text, Point rightAt, IBrush brush, double size = 10)
        {
            var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Mono, size, brush);
            ctx.DrawText(ft, new Point(rightAt.X - ft.Width, rightAt.Y));
        }

        protected bool ChannelOn(int ch) => (ChannelMask & (1 << ch)) != 0;

        /// <summary>Viridis-like colormap, t in [0,1].</summary>
        public static Color Heat(double t)
        {
            t = t < 0 ? 0 : t > 1 ? 1 : t;
            // piecewise through: #0B1220 -> #1E3A8A -> #0EA5E9 -> #A3E635 -> #FDE047 -> #FFFFFF
            (double p, Color c)[] stops =
            {
                (0.00, Color.Parse("#0B1220")), (0.25, Color.Parse("#1E3A8A")), (0.50, Color.Parse("#0EA5E9")),
                (0.70, Color.Parse("#A3E635")), (0.88, Color.Parse("#FDE047")), (1.00, Color.Parse("#FFFFFF")),
            };
            for (int i = 1; i < stops.Length; i++)
            {
                if (t <= stops[i].p)
                {
                    double u = (t - stops[i - 1].p) / (stops[i].p - stops[i - 1].p);
                    Color a = stops[i - 1].c, bb = stops[i].c;
                    return Color.FromArgb(255, (byte)(a.R + (bb.R - a.R) * u), (byte)(a.G + (bb.G - a.G) * u), (byte)(a.B + (bb.B - a.B) * u));
                }
            }
            return stops[^1].c;
        }
    }
}
