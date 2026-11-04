using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using ReactiveUI;
using ASD.DeviceCore.Protocol;
using VesperApp.Models;
using VesperApp.Services;

namespace VesperApp.ViewModels
{
    /// <summary>
    /// Device Tests › Thermal Camera: live image of a docked Vesper / Pipistrelle's Lepton add-on.
    /// LIVE_START (VESPER_TEST_LEPTON op 9) makes the unit stream LVF1 frames on the console port
    /// (asd-interfaces interfaces/lepton-live.md); LIVE_STOP (op 10) ends it and powers the camera down.
    /// The unit reports the camera type in each frame (3.x 160x120 / 2.x 80x60), so nothing is assumed here.
    /// </summary>
    public class LiveThermalViewModel : ViewModelBase
    {
        public const byte OpLiveStart = 9, OpLiveStop = 10, OpFfc = 1;

        private readonly MainViewViewModel? _main;
        private LoggerDevice? _dev;
        private ThermalFrame? _last;
        private int _renderPending;
        private readonly Stopwatch _fpsClock = new();
        private int _fpsFrames;

        public IReadOnlyList<LeptonFilterType> Palettes { get; } =
            new[] { LeptonFilterType.LEPTON_IRONBLACK, LeptonFilterType.LEPTON_RAINBOW, LeptonFilterType.LEPTON_GRAYSCALE };

        private LeptonFilterType _palette = LeptonFilterType.LEPTON_IRONBLACK;
        public LeptonFilterType Palette
        {
            get => _palette;
            set { this.RaiseAndSetIfChanged(ref _palette, value); if (_last != null) Render(_last); }
        }

        private WriteableBitmap? _image;
        public WriteableBitmap? Image { get => _image; private set => this.RaiseAndSetIfChanged(ref _image, value); }

        private bool _isStreaming;
        public bool IsStreaming { get => _isStreaming; private set => this.RaiseAndSetIfChanged(ref _isStreaming, value); }

        private bool _isBusy;
        public bool IsBusy { get => _isBusy; private set => this.RaiseAndSetIfChanged(ref _isBusy, value); }

        private string _summary = "Fit the Lepton board, connect the unit over USB (not recording), then press Start.";
        public string Summary { get => _summary; set => this.RaiseAndSetIfChanged(ref _summary, value); }

        private string _camera = "";
        public string Camera { get => _camera; private set => this.RaiseAndSetIfChanged(ref _camera, value); }

        private string _minText = "–", _maxText = "–", _centreText = "–", _hoverText = "", _sensorText = "–", _rateText = "–";
        public string MinText { get => _minText; private set => this.RaiseAndSetIfChanged(ref _minText, value); }
        public string MaxText { get => _maxText; private set => this.RaiseAndSetIfChanged(ref _maxText, value); }
        public string CentreText { get => _centreText; private set => this.RaiseAndSetIfChanged(ref _centreText, value); }
        public string HoverText { get => _hoverText; private set => this.RaiseAndSetIfChanged(ref _hoverText, value); }
        public string SensorText { get => _sensorText; private set => this.RaiseAndSetIfChanged(ref _sensorText, value); }
        public string RateText { get => _rateText; private set => this.RaiseAndSetIfChanged(ref _rateText, value); }

        public ICommand StartCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand FfcCommand { get; }
        public ICommand SnapshotCommand { get; }

        public LiveThermalViewModel(MainViewViewModel main)
        {
            _main = main;
            var idle = this.WhenAnyValue(x => x.IsStreaming, x => x.IsBusy, (s, b) => !s && !b);
            var live = this.WhenAnyValue(x => x.IsStreaming, x => x.IsBusy, (s, b) => s && !b);
            StartCommand = ReactiveCommand.CreateFromTask(StartAsync, idle);
            StopCommand = ReactiveCommand.CreateFromTask(StopAsync, live);
            FfcCommand = ReactiveCommand.CreateFromTask(FfcAsync, live);
            SnapshotCommand = ReactiveCommand.CreateFromTask(SnapshotAsync,
                this.WhenAnyValue(x => x.Image).Select(i => i != null));

            if (_main?.SelectedLoggerDeviceModel != null)
                _main.SelectedLoggerDeviceModel.SelectionChanged += (s, e) =>
                {
                    if (IsStreaming) _ = StopAsync();
                };
        }

        public LiveThermalViewModel() : this(null!) { }

        private LoggerDevice? Device()
        {
            LoggerDevice? dev = _main?.SelectedLoggerDevice;
            return dev != null && dev.IsConnected && dev.IsComportDevice ? dev : null;
        }

        private async Task StartAsync()
        {
            LoggerDevice? dev = Device();
            if (dev == null) { Summary = "No connected USB device. Connect the unit and press Connect first."; return; }
            if (!DeviceCapabilities.HasAddonSensors(dev.DeviceType) || dev.DeviceType == DeviceTypes.Kol)
            { Summary = $"{DeviceCapabilities.DisplayName(dev.DeviceType)} has no thermal camera add-on."; return; }

            IsBusy = true;
            try
            {
                Summary = "Starting the camera (boot takes up to 2 s)…";
                dev.ThermalFrameReceived += OnFrame;
                _dev = dev;
                byte[]? r = await dev.SendCommandAsync(MessageTypes.VESPER_TEST_LEPTON, new byte[] { OpLiveStart, 0, 0, 0 }, 8000);
                if (r == null || r.Length < 1 || r[0] != 0)
                {
                    dev.ThermalFrameReceived -= OnFrame;
                    _dev = null;
                    Summary = r == null ? "No answer - this firmware has no live thermal view (needs VesperU5 with Lepton live, 2026-11-04 or later)."
                            : r[0] == 0xFF ? "No thermal camera found - check the Lepton board."
                            : r[0] == 0xFE ? "The unit is armed - stop the recording first."
                            : $"The unit refused the live view (status 0x{r[0]:X2}).";
                    return;
                }
                Camera = $"Lepton {(r.Length > 1 && r[1] != 0 ? "3.x" : "2.x")}{(r.Length > 2 && r[2] != 0 ? ", radiometric" : ", not radiometric (raw counts)")}";
                _fpsFrames = 0;
                _fpsClock.Restart();
                IsStreaming = true;
                Summary = "Live. Hover over the image for a pixel's value.";
            }
            finally { IsBusy = false; }
        }

        public async Task StopAsync()
        {
            LoggerDevice? dev = _dev;
            if (dev == null) { IsStreaming = false; return; }
            IsBusy = true;
            try
            {
                await dev.SendCommandAsync(MessageTypes.VESPER_TEST_LEPTON, new byte[] { OpLiveStop, 0, 0, 0 }, 4000);
            }
            finally
            {
                dev.ThermalFrameReceived -= OnFrame;
                _dev = null;
                IsStreaming = false;
                IsBusy = false;
                RateText = "–";
                Summary = "Stopped; the camera is powered down.";
            }
        }

        private async Task FfcAsync()
        {
            if (_dev == null) return;
            await _dev.SendCommandAsync(MessageTypes.VESPER_TEST_LEPTON, new byte[] { OpFfc, 0, 0, 0 }, 3000);
        }

        private void OnFrame(object? sender, ThermalFrame f)
        {
            // reader task: drop frames while the UI is still drawing the previous one
            if (Interlocked.Exchange(ref _renderPending, 1) == 1) return;
            Dispatcher.UIThread.Post(() =>
            {
                try { Render(f); }
                finally { Interlocked.Exchange(ref _renderPending, 0); }
            });
        }

        private void Render(ThermalFrame f)
        {
            _last = f;
            var (lo, hi) = ThermalRenderer.Range(f.Pixels);
            var bmp = new WriteableBitmap(new PixelSize(f.Width, f.Height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
            var bgra = new byte[f.Width * f.Height * 4];
            ThermalRenderer.ToBgra(f.Pixels, lo, hi, Palette, bgra);
            using (ILockedFramebuffer fb = bmp.Lock())
            {
                for (int y = 0; y < f.Height; y++)
                    System.Runtime.InteropServices.Marshal.Copy(bgra, y * f.Width * 4, fb.Address + y * fb.RowBytes, f.Width * 4);
            }
            WriteableBitmap? old = Image;
            Image = bmp;
            old?.Dispose();

            MinText = ThermalRenderer.Format(f, lo);
            MaxText = ThermalRenderer.Format(f, hi);
            CentreText = ThermalRenderer.Format(f, f.At(f.Width / 2, f.Height / 2));
            SensorText = f.FpaC.HasValue ? $"{f.FpaC.Value:F1} °C" + (f.HousingC.HasValue ? $" (housing {f.HousingC.Value:F1} °C)" : "") : "–";
            if (f.FfcRan) Summary = "Flat-field correction done.";

            _fpsFrames++;
            double s = _fpsClock.Elapsed.TotalSeconds;
            if (s >= 1.0)
            {
                RateText = $"{_fpsFrames / s:F1} fps, frame {f.FrameNumber}";
                _fpsFrames = 0;
                _fpsClock.Restart();
            }
        }

        /// <summary>Pointer over the image, in image pixels (null when it leaves).</summary>
        public void SetHover(int? x, int? y)
        {
            ThermalFrame? f = _last;
            if (f == null || x == null || y == null || x < 0 || y < 0 || x >= f.Width || y >= f.Height) { HoverText = ""; return; }
            HoverText = $"({x}, {y}) {ThermalRenderer.Format(f, f.At(x.Value, y.Value))}";
        }

        private async Task SnapshotAsync()
        {
            ThermalFrame? f = _last;
            WriteableBitmap? img = Image;
            if (f == null || img == null) return;
            string unit = string.IsNullOrWhiteSpace(_main?.SelectedLoggerDevice?.SerialNumber) ? "unit" : _main!.SelectedLoggerDevice!.SerialNumber!.Trim();
            string dir = Path.Combine(SettingsService.Instance.ResolveWorkingDirectory(), "Thermal", unit);
            string stem = Path.Combine(dir, DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));
            try
            {
                Directory.CreateDirectory(dir);
                img.Save(stem + ".png");
                await File.WriteAllTextAsync(stem + ".csv", ThermalRenderer.ToCsv(f));
                Summary = $"Saved {stem}.png and .csv";
            }
            catch (Exception ex) { Summary = "Snapshot failed: " + ex.Message; }
        }
    }
}
