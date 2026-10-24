using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using VesperApp.Models;

namespace VesperApp.Controls
{
    /// <summary>
    /// Sound-trigger meter for console 79 (<see cref="MicLevels"/>): one horizontal bar per channel on a
    /// −100..0 dBFS scale - the current level (filled), the background (thin dark mark), the peak since the
    /// last read (tick), and the start / stop levels as vertical lines when a trigger is configured.
    /// </summary>
    public class MicLevelMeter : Control
    {
        public static readonly StyledProperty<MicLevels?> LevelsProperty =
            AvaloniaProperty.Register<MicLevelMeter, MicLevels?>(nameof(Levels));

        static MicLevelMeter()
        {
            AffectsRender<MicLevelMeter>(LevelsProperty);
            AffectsMeasure<MicLevelMeter>(LevelsProperty);   // row count follows the channel count
        }

        public MicLevels? Levels { get => GetValue(LevelsProperty); set => SetValue(LevelsProperty, value); }

        private const double Left = 22, Right = 6, Top = 2, Bottom = 14, MinDb = -100, MaxDb = 0;
        private static readonly IBrush[] ChannelBrushes =
        {
            new SolidColorBrush(Color.Parse("#22D3EE")), new SolidColorBrush(Color.Parse("#A3E635")),
            new SolidColorBrush(Color.Parse("#FBBF24")), new SolidColorBrush(Color.Parse("#F472B6")),
        };
        private static readonly IBrush Track = new SolidColorBrush(Color.FromArgb(40, 128, 128, 128));
        private static readonly IPen BackgroundPen = new Pen(new SolidColorBrush(Color.FromArgb(220, 30, 30, 30)), 2);
        private static readonly IPen PeakPen = new Pen(Brushes.White, 2);
        private static readonly IPen StartPen = new Pen(new SolidColorBrush(Color.Parse("#EF4444")), 1.5);
        private static readonly IPen StopPen = new Pen(new SolidColorBrush(Color.Parse("#EF4444")), 1, new DashStyle(new double[] { 3, 2 }, 0));
        private static readonly IBrush TextBrush = new SolidColorBrush(Color.FromArgb(200, 128, 128, 128));

        protected override Size MeasureOverride(Size availableSize)
        {
            int n = Math.Max(1, Levels?.Channels ?? 1);
            return new Size(double.IsInfinity(availableSize.Width) ? 300 : availableSize.Width, Top + Bottom + n * 16);
        }

        public override void Render(DrawingContext ctx)
        {
            base.Render(ctx);
            MicLevels? m = Levels;
            if (m == null || m.Channels == 0 || Bounds.Width < 80) return;

            double w = Bounds.Width - Left - Right;
            double X(double db) => Left + (Math.Clamp(db, MinDb, MaxDb) - MinDb) / (MaxDb - MinDb) * w;
            double rowH = (Bounds.Height - Top - Bottom) / m.Channels;
            var ci = CultureInfo.InvariantCulture;

            for (int c = 0; c < m.Channels; c++)
            {
                double y = Top + c * rowH, h = Math.Max(4, rowH - 4);
                ctx.DrawText(Text(c.ToString(ci)), new Point(4, y + (h - 12) / 2));
                ctx.FillRectangle(Track, new Rect(Left, y, w, h));
                if (m.Level[c] is double lv)
                    ctx.FillRectangle(ChannelBrushes[c % ChannelBrushes.Length], new Rect(Left, y, Math.Max(0, X(lv) - Left), h));
                if (m.Background[c] is double bg)
                    ctx.DrawLine(BackgroundPen, new Point(X(bg), y), new Point(X(bg), y + h));
                if (m.Peak[c] is double pk)
                    ctx.DrawLine(PeakPen, new Point(X(pk), y), new Point(X(pk), y + h));
                if (m.StartDbfs(c) is double st)
                    ctx.DrawLine(StartPen, new Point(X(st), y - 1), new Point(X(st), y + h + 1));
                if (m.StopDbfs(c) is double sp)
                    ctx.DrawLine(StopPen, new Point(X(sp), y), new Point(X(sp), y + h));
            }

            double yAxis = Bounds.Height - Bottom + 1;
            for (int db = (int)MinDb; db <= MaxDb; db += 20)
                ctx.DrawText(Text(db.ToString(ci)), new Point(X(db) - (db == 0 ? 8 : 10), yAxis));
        }

        private static FormattedText Text(string s) =>
            new(s, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default, 10, TextBrush);
    }
}
