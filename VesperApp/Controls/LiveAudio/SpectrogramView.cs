using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using VesperApp.Services.Audio;

namespace VesperApp.Controls.LiveAudio
{
    /// <summary>Scrolling waterfall of one channel (or the mono mix): time on X, frequency on Y,
    /// level as colour. New columns are computed only for samples that arrived since the last frame.</summary>
    public sealed class SpectrogramView : LiveAudioView
    {
        public static readonly StyledProperty<int> FftSizeProperty =
            AvaloniaProperty.Register<SpectrogramView, int>(nameof(FftSize), 1024);
        public static readonly StyledProperty<double> MinDbProperty =
            AvaloniaProperty.Register<SpectrogramView, double>(nameof(MinDb), -100);
        public static readonly StyledProperty<double> MaxDbProperty =
            AvaloniaProperty.Register<SpectrogramView, double>(nameof(MaxDb), -10);
        /// <summary>Seconds of history across the width.</summary>
        public static readonly StyledProperty<double> SecondsProperty =
            AvaloniaProperty.Register<SpectrogramView, double>(nameof(Seconds), 8);

        public int FftSize { get => GetValue(FftSizeProperty); set => SetValue(FftSizeProperty, value); }
        public double MinDb { get => GetValue(MinDbProperty); set => SetValue(MinDbProperty, value); }
        public double MaxDb { get => GetValue(MaxDbProperty); set => SetValue(MaxDbProperty, value); }
        public double Seconds { get => GetValue(SecondsProperty); set => SetValue(SecondsProperty, value); }

        private const int W = 1024, H = 384;
        private WriteableBitmap? _bmp;
        private readonly int[] _pix = new int[W * H];
        private readonly int[] _lut = BuildLut();
        private float[] _x = new float[0], _re = new float[0], _im = new float[0], _db = new float[0];
        private long _lastTotal = -1;
        private LiveAudioBuffer? _lastSrc;
        private int _lastChannel = int.MinValue;

        private static int[] BuildLut()
        {
            var lut = new int[256];
            for (int i = 0; i < 256; i++) { Color c = Heat(i / 255.0); lut[i] = (255 << 24) | (c.R << 16) | (c.G << 8) | c.B; }
            return lut;
        }

        private void Reset()
        {
            Array.Fill(_pix, _lut[0]);
            _lastTotal = -1;
        }

        protected override void RenderPlot(DrawingContext ctx, Rect plot, LiveAudioBuffer src)
        {
            int n = FftSize; if (n < 256) n = 256; if ((n & (n - 1)) != 0) n = 1024;
            if (_x.Length != n) { _x = new float[n]; _re = new float[n]; _im = new float[n]; _db = new float[n / 2]; Reset(); }
            if (!ReferenceEquals(src, _lastSrc) || SelectedChannel != _lastChannel) { _lastSrc = src; _lastChannel = SelectedChannel; Reset(); }
            _bmp ??= new WriteableBitmap(new PixelSize(W, H), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);

            // how many new columns since the last frame: one column per hop
            double secondsPerCol = Seconds / W;
            int hop = Math.Max(64, (int)(src.SampleRate * secondsPerCol));
            long total = src.TotalFrames;
            if (_lastTotal < 0) _lastTotal = total - hop;
            int newCols = (int)Math.Min(W, (total - _lastTotal) / hop);
            if (newCols > 0)
            {
                // shift left by newCols
                Array.Copy(_pix, newCols, _pix, 0, _pix.Length - newCols);   // row-major shift: fix per row below
                // Array.Copy above shifts the flat buffer; correct the wrap at row ends by re-drawing the last newCols columns of each row.
                // Simpler and exact: do the shift per row.
                for (int row = 0; row < H; row++)
                {
                    int rb = row * W;
                    // (already shifted flat; the last newCols of each row now hold the next row's first pixels - overwrite them)
                    for (int c = W - newCols; c < W; c++) _pix[rb + c] = _lut[0];
                }
                float range = (float)(MaxDb - MinDb);
                // Compute columns for the newest newCols hops (oldest of them first)
                float[] col = new float[H];
                for (int j = newCols - 1; j >= 0; j--)
                {
                    // window ending j hops before the newest sample
                    int back = j * hop;
                    // copy latest n+back, take first n of the tail
                    float[] tmp = back == 0 ? _x : new float[n + back];
                    if (back == 0)
                    {
                        if (SelectedChannel < 0) src.CopyLatestMix(_x); else src.CopyLatest(SelectedChannel, _x);
                    }
                    else
                    {
                        if (SelectedChannel < 0) src.CopyLatestMix(tmp); else src.CopyLatest(SelectedChannel, tmp);
                        Array.Copy(tmp, 0, _x, 0, n);
                    }
                    Dsp.SpectrumDb(_x, _db, _re, _im);
                    // map bins (0..n/2) to rows (bottom = 0 Hz), max-pool
                    int bins = _db.Length;
                    for (int row = 0; row < H; row++)
                    {
                        int k0 = (int)((long)row * bins / H), k1 = Math.Max(k0 + 1, (int)((long)(row + 1) * bins / H));
                        float m = -200f; for (int k = k0; k < k1 && k < bins; k++) if (_db[k] > m) m = _db[k];
                        col[row] = m;
                    }
                    int x = W - 1 - j;
                    for (int row = 0; row < H; row++)
                    {
                        float t = (col[row] - (float)MinDb) / range;
                        int idx = t <= 0 ? 0 : t >= 1 ? 255 : (int)(t * 255);
                        _pix[(H - 1 - row) * W + x] = _lut[idx];
                    }
                }
                _lastTotal += (long)newCols * hop;
                using (ILockedFramebuffer fb = _bmp.Lock())
                {
                    Marshal.Copy(_pix, 0, fb.Address, _pix.Length);
                }
            }
            ctx.DrawImage(_bmp, new Rect(0, 0, W, H), plot);
        }

        protected override void RenderAxes(DrawingContext ctx, Rect plot, LiveAudioBuffer src)
        {
            double fMax = src.SampleRate / 2.0;
            for (int i = 0; i <= 4; i++)
            {
                double f = fMax * i / 4, y = plot.Bottom - plot.Height * i / 4;
                DrawTextRight(ctx, f >= 1000 ? $"{f / 1000:0.#}k" : $"{f:0}", new Point(plot.X - 4, y - (i == 4 ? 0 : i == 0 ? 12 : 6)), TextBrush, 9);
            }
            for (int i = 0; i <= 4; i++)
            {
                double x = plot.X + plot.Width * i / 4, s = -Seconds * (4 - i) / 4;
                DrawText(ctx, i == 4 ? "now" : $"{s:0.#} s", new Point(x - 10, plot.Bottom + 4), TextBrush, 9);
            }
            string chLabel = SelectedChannel < 0 ? "mix" : $"ch{SelectedChannel}";
            DrawText(ctx, $"{chLabel}  {MinDb:0}…{MaxDb:0} dBFS", new Point(plot.Right - 120, plot.Y - 16), TextBrush, 9);
        }
    }
}
