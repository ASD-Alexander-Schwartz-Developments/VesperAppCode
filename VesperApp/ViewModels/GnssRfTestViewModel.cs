using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using ReactiveUI;
using ASD.Contracts;
using ASD.DeviceCore.Protocol;
using ASD.Platform;
using VesperApp.Models;
using VesperApp.Services;

namespace VesperApp.ViewModels
{
    /// <summary>
    /// GNSS RF / positioning test for VT04-VESPER. The RF generator is a plugin
    /// (<see cref="IGnssSignalSource"/>, installed from the Plugins page); this panel only asks it to
    /// transmit. Two checks:
    ///   1. Tone go/no-go — the generator transmits a CW tone and the host issues the in-FW
    ///      VESPER_TEST_GPS (61); the device detects it in RAM and replies pass/fail plus the offset it
    ///      measured, which can be fed back into the generator's station calibration.
    ///   2. Positioning — the generator transmits a simulated scenario, the device captures and is
    ///      downloaded the normal way, then the snaps are decoded via <see cref="IGnssDecoder"/> and
    ///      every fix is checked against the scenario's reference (a point, or a circle around it).
    /// Mirrors MicHealthCheckViewModel. Requires the device in a shielded box with the generator
    /// coupled in. For positioning use a decode-supported snap length (cg-gnss accepts
    /// 64/128/256/440/512 ms; 512 ms is decoded whole) and do not truncate snaps.
    /// </summary>
    public class GnssRfTestViewModel : ViewModelBase
    {
        private readonly MainViewViewModel? _main;
        private readonly IGnssSignalSource? _source = PlatformServices.GnssSignalSource;

        public ObservableCollection<GnssTestRow> Results { get; } = new();

        // --- tone test params ---
        public double ToneOffsetKHz { get; set; } = 100;
        public double ToneGainDb { get; set; } = -50;
        public byte SnrThreshDb { get; set; } = 12;
        public byte SnapLenCode { get; set; } = 1;            // 0=64,1=128,2=256 ms

        // --- positioning params ---
        public double ScenarioGainDb { get; set; } = -34;
        public double ToleranceMeters { get; set; } = 500;

        /// <summary>True when an RF generator plugin is installed.</summary>
        public bool HasSource => _source is { IsAvailable: true };
        public string SourceName => _source?.Name ?? "No RF generator";

        private string _sourceStatus = string.Empty;
        public string SourceStatus { get => _sourceStatus; private set => this.RaiseAndSetIfChanged(ref _sourceStatus, value); }

        private IReadOnlyList<GnssScenarioInfo> _scenarios = Array.Empty<GnssScenarioInfo>();
        public IReadOnlyList<GnssScenarioInfo> Scenarios { get => _scenarios; private set => this.RaiseAndSetIfChanged(ref _scenarios, value); }

        private GnssScenarioInfo? _selectedScenario;
        public GnssScenarioInfo? SelectedScenario
        {
            get => _selectedScenario;
            set { this.RaiseAndSetIfChanged(ref _selectedScenario, value); UpdateScenarioStatus(); }
        }

        // Captures folder is chosen via a picker (no typed paths); shown read-only.
        private string _capturesFolder = string.Empty;
        public string CapturesFolder { get => _capturesFolder; set => this.RaiseAndSetIfChanged(ref _capturesFolder, value); }

        private bool _scenarioReady;
        public bool ScenarioReady { get => _scenarioReady; private set => this.RaiseAndSetIfChanged(ref _scenarioReady, value); }

        private string _scenarioStatus = string.Empty;
        public string ScenarioStatus { get => _scenarioStatus; private set => this.RaiseAndSetIfChanged(ref _scenarioStatus, value); }

        private bool _isPreparing;
        public bool IsPreparing { get => _isPreparing; private set => this.RaiseAndSetIfChanged(ref _isPreparing, value); }

        private double _prepareProgress;
        public double PrepareProgress { get => _prepareProgress; private set => this.RaiseAndSetIfChanged(ref _prepareProgress, value); }

        private bool _isRunning;
        public bool IsRunning { get => _isRunning; set => this.RaiseAndSetIfChanged(ref _isRunning, value); }

        // False when the selected product has no GNSS receiver (the device-side tone
        // self-test is then not applicable; transmit and offline validation stay available).
        private bool _isSupported;
        public bool IsSupported { get => _isSupported; private set => this.RaiseAndSetIfChanged(ref _isSupported, value); }

        /// <summary>Device-measured minus transmitted tone offset from the last tone test, Hz.</summary>
        private double? _residualHz;
        public double? ResidualHz { get => _residualHz; private set => this.RaiseAndSetIfChanged(ref _residualHz, value); }

        private string _summary = "Connect the device (shielded box, RF generator coupled in), then run a test.";
        public string Summary { get => _summary; set => this.RaiseAndSetIfChanged(ref _summary, value); }

        public ICommand ProbeCommand { get; }
        public ICommand ToneSelfTestCommand { get; }
        public ICommand ApplyCalibrationCommand { get; }
        public ICommand TransmitScenarioCommand { get; }
        public ICommand StopTransmitCommand { get; }
        public ICommand ValidatePositioningCommand { get; }
        public ICommand PrepareScenarioCommand { get; }
        public ICommand BrowseCapturesCommand { get; }

        public GnssRfTestViewModel(MainViewViewModel main)
        {
            _main = main;
            var canRun = this.WhenAnyValue(x => x.IsRunning, r => !r);
            var canSource = this.WhenAnyValue(x => x.IsRunning, r => !r && HasSource);
            // The tone self-test interrogates the device firmware, so it only applies
            // to GNSS-capable products; transmit and offline decode don't.
            var canDeviceTest = this.WhenAnyValue(x => x.IsRunning, x => x.IsSupported, (r, s) => !r && s && HasSource);
            var canTransmit = this.WhenAnyValue(x => x.IsRunning, x => x.ScenarioReady, (r, ready) => !r && ready);
            var canPrepare = this.WhenAnyValue(x => x.IsPreparing, x => x.ScenarioReady, x => x.SelectedScenario,
                (p, ready, s) => !p && !ready && s != null);
            var canCalibrate = this.WhenAnyValue(x => x.IsRunning, x => x.ResidualHz, (r, res) => !r && res.HasValue);

            ProbeCommand = ReactiveCommand.CreateFromTask(ProbeAsync, canSource);
            ToneSelfTestCommand = ReactiveCommand.CreateFromTask(RunToneSelfTestAsync, canDeviceTest);
            ApplyCalibrationCommand = ReactiveCommand.CreateFromTask(ApplyCalibrationAsync, canCalibrate);
            TransmitScenarioCommand = ReactiveCommand.CreateFromTask(StartTransmitAsync, canTransmit);
            StopTransmitCommand = ReactiveCommand.Create(() => _source?.Stop());
            ValidatePositioningCommand = ReactiveCommand.CreateFromTask(RunValidateAsync, canRun);
            PrepareScenarioCommand = ReactiveCommand.CreateFromTask(PrepareScenarioAsync, canPrepare);
            BrowseCapturesCommand = ReactiveCommand.CreateFromTask(BrowseCapturesAsync);

            if (HasSource)
            {
                SourceStatus = "Press Check radio to look for the transmitter and its calibration.";
                RefreshScenarios();
            }
            else
            {
                SourceStatus = "No RF generator plugin is installed. Install the GNSS RF test plugin from the Plugins page "
                             + "to run the tone self-test and transmit scenarios; Validate Fix works on downloaded captures without it.";
                UpdateScenarioStatus();
            }

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
                Summary = $"{dev.DeviceType} has no GNSS receiver — tone self-test disabled (transmit / Validate still available).";
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

        private async Task ProbeAsync()
        {
            if (_source == null) return;
            IsRunning = true;
            try { SourceStatus = Describe(await _source.ProbeAsync()); }
            catch (Exception ex) { SourceStatus = "Check failed: " + ex.Message; }
            finally { IsRunning = false; }
        }

        private static string Describe(GnssSourceStatus s)
        {
            string cal = s.CalibrationHz is double hz
                ? $"station calibration {hz:+0;-0} Hz" + (s.CalibratedOn is DateTimeOffset on ? $" ({on:yyyy-MM-dd})" : "")
                : "no station calibration — run the tone self-test with a reference unit and apply the offset";
            return (s.RadioFound ? "Radio found" : "Radio not found") + $" · {cal}. {s.Message}".TrimEnd();
        }

        // ---- 1. Tone go/no-go (RF chain) ----
        private async Task RunToneSelfTestAsync()
        {
            LoggerDevice? dev = Device();
            if (dev == null || _source == null) return;

            IsRunning = true;
            ResidualHz = null;
            Results.Clear();
            var row = new GnssTestRow("RF chain (tone)") { Status = "Testing…" };
            Results.Add(row);
            try
            {
                Summary = $"Transmitting tone at L1 + {ToneOffsetKHz:0} kHz…";
                double sentHz = await _source.StartToneAsync(ToneOffsetKHz * 1000.0, ToneGainDb);
                await Task.Delay(400);                                  // let the tone settle

                uint snapMs = (SnapLenCode == 0) ? 64u : (SnapLenCode >= 2) ? 256u : 128u;
                byte[] req = GpsBenchTest.BuildRequest(snapMs, SnrThreshDb);
                byte[]? resp = await dev.SendCommandAsync(MessageTypes.VESPER_TEST_GPS, req, 8000);
                _source.Stop();

                GpsTestResult? r = GpsBenchTest.ParseResponse(resp);
                if (r is { Pass: true }) ResidualHz = r.TcxoOffsetHz - sentHz;
                await Dispatcher.UIThread.InvokeAsync(() => ApplyTone(row, resp, r, ResidualHz));
                Summary = (r != null && r.Pass) ? "RF chain OK." : "RF chain check did not pass.";
            }
            catch (Exception ex)
            {
                row.Status = "Error"; row.StatusBrush = Brushes.OrangeRed;
                row.Detail = ex.Message;
                Summary = "Tone test could not run: " + ex.Message;
            }
            finally { _source.Stop(); IsRunning = false; }
        }

        private static void ApplyTone(GnssTestRow row, byte[]? resp, GpsTestResult? r, double? residualHz)
        {
            if (resp == null || r == null)
            {
                row.Status = "No response"; row.StatusBrush = Brushes.OrangeRed;
                row.Detail = "Device didn't reply — check it's connected and not recording, and the FW supports VESPER_TEST_GPS.";
            }
            else if (r.Pass)
            {
                row.Status = "GO"; row.StatusBrush = Brushes.SeaGreen;
                row.Detail = $"front end {r.PeakCN0} dB · tone at {r.TcxoOffsetHz / 1000.0:+0.0;-0.0} kHz"
                           + (residualHz is double res ? $" · {res:+0;-0} Hz from the transmitted offset" : "");
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

        /// <summary>Fold the last measured residual into the generator's station calibration. Use a
        /// reference unit for this, not the unit under test.</summary>
        private async Task ApplyCalibrationAsync()
        {
            if (_source == null || ResidualHz is not double res) return;
            IsRunning = true;
            try
            {
                SourceStatus = Describe(await _source.AdjustCalibrationAsync(res));
                Summary = $"Station calibration adjusted by {res:+0;-0} Hz. Repeat the tone test until the residual is small.";
                ResidualHz = null;
            }
            catch (Exception ex) { Summary = "Calibration failed: " + ex.Message; }
            finally { IsRunning = false; }
        }

        // ---- 2. Positioning: transmit scenario, then validate downloaded snaps ----
        private async Task StartTransmitAsync()
        {
            if (_source == null || SelectedScenario is not { } s) return;
            IsRunning = true;
            try
            {
                Summary = $"Starting {s.Name}…";
                await _source.StartScenarioAsync(s.Id, ScenarioGainDb);
                Summary = $"Transmitting {s.Name}. Capture a burst on the device, return it to disk mode, then Validate.";
            }
            catch (Exception ex) { Summary = "Transmit failed: " + ex.Message; }
            finally { IsRunning = false; }
        }

        private void RefreshScenarios()
        {
            string? id = SelectedScenario?.Id;
            Scenarios = _source?.GetScenarios() ?? Array.Empty<GnssScenarioInfo>();
            SelectedScenario = Scenarios.FirstOrDefault(x => x.Id == id) ?? Scenarios.FirstOrDefault();
        }

        private void UpdateScenarioStatus()
        {
            ScenarioReady = HasSource && SelectedScenario is { IsReady: true };
            ScenarioStatus = !HasSource ? "—"
                : SelectedScenario is not { } s ? "The generator offers no scenarios."
                : s.IsReady ? $"Ready · {s.Description}"
                : $"Not prepared · {s.Description}";
        }

        /// <summary>Have the generator prepare the selected scenario into its local cache, with progress.</summary>
        private async Task PrepareScenarioAsync()
        {
            if (_source == null || SelectedScenario is not { } s) return;
            IsPreparing = true;
            PrepareProgress = 0;
            ScenarioStatus = "Preparing…";
            try
            {
                var progress = new Progress<double>(p =>
                {
                    PrepareProgress = p;
                    ScenarioStatus = $"Preparing… {p * 100:0}%";
                });
                bool ok = await _source.PrepareScenarioAsync(s.Id, progress);
                Summary = ok ? $"Scenario ready: {s.Name}" : $"Could not prepare {s.Name}.";
            }
            catch (Exception ex)
            {
                Summary = "Scenario preparation failed: " + ex.Message;
            }
            finally
            {
                IsPreparing = false;
                RefreshScenarios();
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
            var dec = PlatformServices.Gnss;
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
                string[] bins = Directory.GetFiles(CapturesFolder, "*G.BIN",
                    new EnumerationOptions { MatchCasing = MatchCasing.CaseInsensitive });
                foreach (string bin in bins)
                    await BinaryParser.ExtractVesperSnap(bin, snapDir, TimeSpan.Zero);

                var req = new GnssDecodeRequest(snapDir, Path.Combine(snapDir, "decode"));
                GnssDecodeResult res = await dec.DecodeAsync(req);

                var fixes = res.Fixes;
                await Dispatcher.UIThread.InvokeAsync(() => ApplyFix(row, res.Succeeded, fixes, SelectedScenario));
            }
            finally { IsRunning = false; }
        }

        /// <summary>Each fix's distance from where the scenario puts it: the reference point, or the
        /// circle of <see cref="GnssScenarioInfo.PathRadiusMeters"/> around it. Passes on the median.</summary>
        private void ApplyFix(GnssTestRow row, bool ok, IReadOnlyList<GnssFix> fixes, GnssScenarioInfo? scenario)
        {
            if (!ok || fixes.Count == 0)
            {
                row.Status = "No fix"; row.StatusBrush = Brushes.OrangeRed;
                row.Detail = "No valid fixes — check the station calibration, gain, and that snaps are 256 ms.";
                Summary = "Positioning: no fix.";
                return;
            }
            double mlat = fixes.Average(f => f.Latitude);
            double mlon = fixes.Average(f => f.Longitude);
            if (scenario == null)
            {
                row.Status = "Fixed"; row.StatusBrush = Brushes.SeaGreen;
                row.Detail = $"{fixes.Count} fixes · mean {mlat:0.00000},{mlon:0.00000} (no scenario reference to compare with)";
                Summary = "Positioning: fix obtained.";
                return;
            }
            double[] err = fixes
                .Select(f => Math.Abs(GeoUtil.HaversineMeters(f.Latitude, f.Longitude,
                    scenario.ReferenceLatitude, scenario.ReferenceLongitude) - scenario.PathRadiusMeters))
                .OrderBy(e => e).ToArray();
            double median = err[err.Length / 2];
            bool pass = median <= ToleranceMeters;

            row.Status = pass ? "GO" : "NO-GO";
            row.StatusBrush = pass ? Brushes.SeaGreen : Brushes.Goldenrod;
            row.Detail = $"{fixes.Count} fixes · median {median:0} m off the {scenario.Name} path (max {err[^1]:0} m)";
            Summary = $"Positioning {(pass ? "GO" : "NO-GO")} — median {median:0} m from the scenario.";
        }
    }
}
