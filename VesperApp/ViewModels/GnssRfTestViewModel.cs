using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using ReactiveUI;
using ASD.DeviceCore.Protocol;
using VesperApp.Models;
using VesperApp.Services;

namespace VesperApp.ViewModels
{
    /// <summary>
    /// GNSS RF / positioning test for VT04-VESPER, driven from the proven pluto-gnss
    /// rig. Two checks:
    ///   1. Tone go/no-go — transmit a CW tone (native pluto-gnss) and issue the
    ///      in-FW VESPER_GPS_SELFTEST; the device detects it in RAM and replies pass/fail.
    ///   2. Positioning — transmit a gps-sdr-sim scenario at the calibrated LO
    ///      (L1 + clock pre-comp), capture+download on the device (normal flow), then
    ///      decode the snaps via IGnssDecoder and check the fix against a reference.
    /// Mirrors MicHealthCheckViewModel. Requires the device in a shielded box with the
    /// Pluto TX coupled in. For positioning set the device snapSize to 256 ms (the
    /// decode-supported length) and use the per-Pluto calibrated LO.
    /// </summary>
    public class GnssRfTestViewModel : ViewModelBase
    {
        private readonly MainViewViewModel? _main;
        private readonly PlutoGnssTx _tx = new();

        public ObservableCollection<GnssTestRow> Results { get; } = new();

        // --- tone test params ---
        public double ToneOffsetKHz { get; set; } = 100;
        public double ToneGainDb { get; set; } = -50;
        public byte SnrThreshDb { get; set; } = 12;
        public byte SnapLenCode { get; set; } = 1;            // 0=64,1=128,2=256 ms

        // --- positioning params ---
        private readonly GnssScenarioService _scenario = new();
        public long CalibratedLoHz { get; set; } = PlutoGnssTx.L1Hz + 22_600;   // per-Pluto clock pre-comp
        public double GpsGainDb { get; set; } = -34;
        public double ReferenceLat { get; set; }
        public double ReferenceLon { get; set; }
        public double ToleranceMeters { get; set; } = 500;

        // Captures folder is chosen via a picker (no typed paths); shown read-only.
        private string _capturesFolder = string.Empty;
        public string CapturesFolder { get => _capturesFolder; set => this.RaiseAndSetIfChanged(ref _capturesFolder, value); }

        // The gps-sdr-sim scenario is an on-demand CDN asset cached locally.
        private bool _scenarioReady;
        public bool ScenarioReady { get => _scenarioReady; private set => this.RaiseAndSetIfChanged(ref _scenarioReady, value); }

        private string _scenarioStatus = string.Empty;
        public string ScenarioStatus { get => _scenarioStatus; private set => this.RaiseAndSetIfChanged(ref _scenarioStatus, value); }

        private bool _isDownloading;
        public bool IsDownloading { get => _isDownloading; private set => this.RaiseAndSetIfChanged(ref _isDownloading, value); }

        private double _downloadProgress;
        public double DownloadProgress { get => _downloadProgress; private set => this.RaiseAndSetIfChanged(ref _downloadProgress, value); }

        private bool _isRunning;
        public bool IsRunning { get => _isRunning; set => this.RaiseAndSetIfChanged(ref _isRunning, value); }

        // False when the selected product has no GNSS receiver (the device-side tone
        // self-test is then not applicable; the Pluto/offline actions stay available).
        private bool _isSupported;
        public bool IsSupported { get => _isSupported; private set => this.RaiseAndSetIfChanged(ref _isSupported, value); }

        private string _summary = "Connect the device (shielded box, Pluto TX coupled in), then run a test.";
        public string Summary { get => _summary; set => this.RaiseAndSetIfChanged(ref _summary, value); }

        public ICommand ToneSelfTestCommand { get; }
        public ICommand TransmitScenarioCommand { get; }
        public ICommand StopTransmitCommand { get; }
        public ICommand ValidatePositioningCommand { get; }
        public ICommand DownloadScenarioCommand { get; }
        public ICommand BrowseCapturesCommand { get; }

        public GnssRfTestViewModel(MainViewViewModel main)
        {
            _main = main;
            var canRun = this.WhenAnyValue(x => x.IsRunning, r => !r);
            // The tone self-test interrogates the device firmware, so it only applies
            // to GNSS-capable products; the Pluto-TX and offline-decode actions don't.
            var canDeviceTest = this.WhenAnyValue(x => x.IsRunning, x => x.IsSupported, (r, s) => !r && s);
            // Transmit needs the scenario asset present locally; download needs it absent.
            var canTransmit = this.WhenAnyValue(x => x.IsRunning, x => x.ScenarioReady, (r, ready) => !r && ready);
            var canDownload = this.WhenAnyValue(x => x.IsDownloading, x => x.ScenarioReady, (d, ready) => !d && !ready);
            ToneSelfTestCommand = ReactiveCommand.CreateFromTask(RunToneSelfTestAsync, canDeviceTest);
            TransmitScenarioCommand = ReactiveCommand.Create(StartTransmit, canTransmit);
            StopTransmitCommand = ReactiveCommand.Create(() => _tx.Stop());
            ValidatePositioningCommand = ReactiveCommand.CreateFromTask(RunValidateAsync, canRun);
            DownloadScenarioCommand = ReactiveCommand.CreateFromTask(DownloadScenarioAsync, canDownload);
            BrowseCapturesCommand = ReactiveCommand.CreateFromTask(BrowseCapturesAsync);
            UpdateScenarioStatus();

            if (_main?.SelectedLoggerDeviceModel != null)
                _main.SelectedLoggerDeviceModel.SelectionChanged += OnDeviceSelectionChanged;
            RefreshForDevice(_main?.SelectedLoggerDevice);
        }

        public GnssRfTestViewModel() : this(null!) { }

        private void OnDeviceSelectionChanged(object? sender,
            Avalonia.Controls.Selection.SelectionModelSelectionChangedEventArgs<LoggerDevice> e)
        {
            LoggerDevice? dev = (e.SelectedItems != null && e.SelectedItems.Count > 0) ? e.SelectedItems[0] : null;
            Dispatcher.UIThread.Post(() => RefreshForDevice(dev));
        }

        /// <summary>Enable the device-side tone self-test only for GNSS-capable products.</summary>
        public void RefreshForDevice(LoggerDevice? dev)
        {
            IsSupported = dev != null && DeviceCapabilities.HasGnss(dev.DeviceType);
            if (dev != null && !IsSupported)
                Summary = $"{dev.DeviceType} has no GNSS receiver — tone self-test disabled (Pluto TX / Validate still available).";
        }

        private LoggerDevice? Device()
        {
            LoggerDevice? dev = _main?.SelectedLoggerDevice;
            if (dev == null || dev.IsConnected == false || dev.IsComportDevice == false)
            {
                Summary = "No connected USB device. Connect the device and press Connect first.";
                return null;
            }
            if (!DeviceCapabilities.HasGnss(dev.DeviceType))
            {
                Summary = $"{dev.DeviceType} has no GNSS receiver to test.";
                return null;
            }
            return dev;
        }

        // ---- 1. Tone go/no-go (RF chain) ----
        private async Task RunToneSelfTestAsync()
        {
            LoggerDevice? dev = Device();
            if (dev == null) return;

            IsRunning = true;
            Results.Clear();
            var row = new GnssTestRow("RF chain (tone)") { Status = "Testing…" };
            Results.Add(row);
            try
            {
                Summary = $"Transmitting tone at L1 + {ToneOffsetKHz:0} kHz…";
                _tx.StartTone(ToneOffsetKHz * 1000.0, ToneGainDb);     // LO = L1; the FW finds the in-band tone
                await Task.Delay(400);                                  // let the tone settle

                uint snapMs = (SnapLenCode == 0) ? 64u : (SnapLenCode >= 2) ? 256u : 128u;
                byte[] req = GpsBenchTest.BuildRequest(snapMs, SnrThreshDb);
                byte[]? resp = await dev.SendCommandAsync(MessageTypes.VESPER_TEST_GPS, req, 8000);
                _tx.Stop();

                GpsTestResult? r = GpsBenchTest.ParseResponse(resp);
                await Dispatcher.UIThread.InvokeAsync(() => ApplyTone(row, resp, r));
                Summary = (r != null && r.Pass) ? "RF chain OK." : "RF chain check did not pass.";
            }
            finally { _tx.Stop(); IsRunning = false; }
        }

        private static void ApplyTone(GnssTestRow row, byte[]? resp, GpsTestResult? r)
        {
            if (resp == null || r == null)
            {
                row.Status = "No response"; row.StatusBrush = Brushes.OrangeRed;
                row.Detail = "Device didn't reply — check it's connected and not recording, and the FW supports VESPER_TEST_GPS.";
            }
            else if (r.Pass)
            {
                row.Status = "GO"; row.StatusBrush = Brushes.SeaGreen;
                row.Detail = $"front end {r.PeakCN0} dB · tone at {r.TcxoOffsetHz / 1000:+0;-0} kHz";
            }
            else if (!r.Ok)
            {
                row.Status = "Error"; row.StatusBrush = Brushes.OrangeRed;
                row.Detail = "Device reported the front-end capture failed (status 0xFF).";
            }
            else
            {
                row.Status = "NO-GO"; row.StatusBrush = Brushes.OrangeRed;
                row.Detail = $"front end {r.PeakCN0} dB — check coupling/attenuation into the box.";
            }
        }

        // ---- 2. Positioning: transmit scenario, then validate downloaded snaps ----
        private void StartTransmit()
        {
            if (!_scenario.Exists)
            { Summary = "Download the scenario first."; UpdateScenarioStatus(); return; }
            _tx.StartFile(_scenario.LocalPath, CalibratedLoHz, GpsGainDb);
            Summary = $"Transmitting {_scenario.FileName} at L1 {((CalibratedLoHz - PlutoGnssTx.L1Hz) / 1e3):+0.0} kHz. " +
                      "Capture a burst on the device, return it to disk mode, then Validate.";
        }

        private void UpdateScenarioStatus()
        {
            ScenarioReady = _scenario.Exists;
            ScenarioStatus = ScenarioReady
                ? $"Ready · {_scenario.FileName}"
                : $"Not downloaded · {_scenario.FileName}";
        }

        /// <summary>Fetch the scenario asset from the CDN into the local cache, with progress.</summary>
        private async Task DownloadScenarioAsync()
        {
            IsDownloading = true;
            DownloadProgress = 0;
            ScenarioStatus = "Downloading…";
            try
            {
                var progress = new Progress<double>(p =>
                {
                    DownloadProgress = p;
                    ScenarioStatus = $"Downloading… {p * 100:0}%";
                });
                await _scenario.DownloadAsync(progress);
                Summary = $"Scenario ready: {_scenario.LocalPath}";
            }
            catch (Exception ex)
            {
                Summary = "Scenario download failed: " + ex.Message;
            }
            finally
            {
                IsDownloading = false;
                UpdateScenarioStatus();
            }
        }

        /// <summary>Pick the device's *G.BIN capture folder (no typed paths).</summary>
        private async Task BrowseCapturesAsync()
        {
            IStorageProvider? sp = App.AppTopLevel?.StorageProvider;
            if (sp == null) { Summary = "No window available to open a folder picker."; return; }
            var folders = await sp.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select the device's capture folder (*G.BIN)",
                AllowMultiple = false,
            });
            string? path = folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
            if (!string.IsNullOrEmpty(path)) CapturesFolder = path!;
        }

        private async Task RunValidateAsync()
        {
            var dec = ASD.Platform.PlatformServices.Gnss;
            if (dec == null || !dec.IsAvailable)
            { Summary = "GNSS decoder plugin (ASD.Gnss) not installed."; return; }
            if (string.IsNullOrWhiteSpace(CapturesFolder) || !Directory.Exists(CapturesFolder))
            { Summary = "Set CapturesFolder to the device's *G.BIN folder."; return; }

            IsRunning = true;
            var row = new GnssTestRow("Positioning fix") { Status = "Decoding…" };
            Results.Clear(); Results.Add(row);
            try
            {
                string snapDir = Path.Combine(CapturesFolder, "_snaps");
                Directory.CreateDirectory(snapDir);
                string[] bins = Directory.GetFiles(CapturesFolder, "*G.BIN");
                foreach (string bin in bins)
                    await BinaryParser.ExtractVesperSnap(bin, snapDir, TimeSpan.Zero);

                var req = new ASD.Contracts.GnssDecodeRequest(snapDir, Path.Combine(snapDir, "decode"));
                ASD.Contracts.GnssDecodeResult res = await dec.DecodeAsync(req);

                var fixes = res.Fixes;
                await Dispatcher.UIThread.InvokeAsync(() => ApplyFix(row, res.Succeeded, fixes));
            }
            finally { IsRunning = false; }
        }

        private void ApplyFix(GnssTestRow row, bool ok, System.Collections.Generic.IReadOnlyList<ASD.Contracts.GnssFix> fixes)
        {
            if (!ok || fixes.Count == 0)
            {
                row.Status = "No fix"; row.StatusBrush = Brushes.OrangeRed;
                row.Detail = "No valid fixes — check the LO pre-comp, gain, and that snaps are 256 ms.";
                Summary = "Positioning: no fix.";
                return;
            }
            double mlat = fixes.Average(f => f.Latitude);
            double mlon = fixes.Average(f => f.Longitude);
            bool haveRef = ReferenceLat != 0 || ReferenceLon != 0;
            double err = haveRef ? GeoUtil.HaversineMeters(mlat, mlon, ReferenceLat, ReferenceLon) : 0;
            bool pass = haveRef && err <= ToleranceMeters;

            row.Status = !haveRef ? "Fixed" : pass ? "GO" : "NO-GO";
            row.StatusBrush = !haveRef ? Brushes.SeaGreen : pass ? Brushes.SeaGreen : Brushes.Goldenrod;
            row.Detail = haveRef
                ? $"{fixes.Count} fixes · mean {mlat:0.00000},{mlon:0.00000} · {err:0} m from reference"
                : $"{fixes.Count} fixes · mean {mlat:0.00000},{mlon:0.00000}";
            Summary = haveRef ? $"Positioning {(pass ? "GO" : "NO-GO")} — {err:0} m from reference." : "Positioning: fix obtained.";
        }
    }
}
