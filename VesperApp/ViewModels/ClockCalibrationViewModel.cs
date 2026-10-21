using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using ReactiveUI;
using VesperApp.Models;
using VesperApp.Services.Time;

namespace VesperApp.ViewModels
{
    /// <summary>One row of the per-unit clock history.</summary>
    public sealed record SyncPointRow(
        string Time, string Offset, string Uncertainty, string Temperature,
        string Correction, int Segment, string Kind, string Reference);

    /// <summary>
    /// Clock page: precise RTC sync against NTP and per-unit crystal calibration for KOL, Vesper and
    /// Pipistrelle (VESPER_TIME_SYNC / RTC_SHIFT / RTC_CAL; asd-interfaces interfaces/rtc-time-sync.md).
    /// Workflow: Sync -> leave the unit at a stable temperature for 12-24 h -> Sync (or Measure) again
    /// -> Compute -> Apply. The calibration is stored on the unit and kept here as the master copy.
    /// </summary>
    public class ClockCalibrationViewModel : ViewModelBase
    {
        private readonly MainViewViewModel _main;
        private readonly RtcCalibrationStore _store = RtcCalibrationStore.Default;
        private ReferenceClock? _rc;
        private DateTime _rcAt;
        private CalibrationEstimate? _estimate;

        public ObservableCollection<SyncPointRow> History { get; } = new();

        private string _deviceText = "Select a connected KOL, Vesper or Pipistrelle in the device list.";
        public string DeviceText { get => _deviceText; set => this.RaiseAndSetIfChanged(ref _deviceText, value); }

        private string _referenceText = "Reference: NTP is queried on the first Sync or Measure (or via ··· > Refresh NTP reference).";
        public string ReferenceText { get => _referenceText; set => this.RaiseAndSetIfChanged(ref _referenceText, value); }

        private string _offsetText = "—";
        public string OffsetText { get => _offsetText; set => this.RaiseAndSetIfChanged(ref _offsetText, value); }

        private string _temperatureText = "—";
        public string TemperatureText { get => _temperatureText; set => this.RaiseAndSetIfChanged(ref _temperatureText, value); }

        private string _correctionText = "—";
        public string CorrectionText { get => _correctionText; set => this.RaiseAndSetIfChanged(ref _correctionText, value); }

        private string _calibrationText = "—";
        public string CalibrationText { get => _calibrationText; set => this.RaiseAndSetIfChanged(ref _calibrationText, value); }

        private string _tempCompText = "—";
        public string TempCompText { get => _tempCompText; set => this.RaiseAndSetIfChanged(ref _tempCompText, value); }

        private string _estimateText = "No estimate yet: sync, wait at least 12 h, then sync or measure again and press Compute.";
        public string EstimateText { get => _estimateText; set => this.RaiseAndSetIfChanged(ref _estimateText, value); }

        private string _statusText = "";
        public string StatusText { get => _statusText; set => this.RaiseAndSetIfChanged(ref _statusText, value); }

        private bool _isBusy;
        public bool IsBusy { get => _isBusy; set => this.RaiseAndSetIfChanged(ref _isBusy, value); }

        private bool _hasEstimate;
        public bool HasEstimate { get => _hasEstimate; set => this.RaiseAndSetIfChanged(ref _hasEstimate, value); }

        public ICommand RefreshReferenceCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand MeasureCommand { get; }
        public ICommand SyncCommand { get; }
        public ICommand ComputeCommand { get; }
        public ICommand ApplyCommand { get; }
        public ICommand ClearCommand { get; }
        public ICommand MeasureTemperatureCommand { get; }

        public ClockCalibrationViewModel(MainViewViewModel main)
        {
            _main = main;
            var idle = this.WhenAnyValue(x => x.IsBusy, b => !b);
            var canApply = this.WhenAnyValue(x => x.IsBusy, x => x.HasEstimate, (b, e) => !b && e);

            RefreshReferenceCommand = ReactiveCommand.CreateFromTask(() => Run("Querying NTP…", async () => { await EnsureReferenceAsync(true); }), idle);
            RefreshCommand = ReactiveCommand.CreateFromTask(() => Run("Reading the device…", RefreshDeviceAsync), idle);
            MeasureCommand = ReactiveCommand.CreateFromTask(() => Run("Measuring the device clock…", MeasureAsync), idle);
            SyncCommand = ReactiveCommand.CreateFromTask(() => Run("Synchronising the device clock…", SyncAsync), idle);
            ComputeCommand = ReactiveCommand.CreateFromTask(() => Run("Computing…", ComputeAsync), idle);
            ApplyCommand = ReactiveCommand.CreateFromTask(() => Run("Writing the calibration…", ApplyAsync), canApply);
            ClearCommand = ReactiveCommand.CreateFromTask(() => Run("Clearing the calibration…", ClearAsync), idle);
            MeasureTemperatureCommand = ReactiveCommand.CreateFromTask(() => Run("Measuring the device temperature…", MeasureTemperatureAsync), idle);

            if (_main?.SelectedLoggerDeviceModel != null)
                _main.SelectedLoggerDeviceModel.SelectionChanged += (s, e) =>
                    Dispatcher.UIThread.Post(() => { _estimate = null; HasEstimate = false; ShowHistory(); DescribeDevice(); });
            DescribeDevice();
        }

        // Parameterless ctor for the XAML designer / ViewLocator fallback.
        public ClockCalibrationViewModel() : this(null!) { }

        private bool DeviceUtc => _main?.IsClockUTC ?? true;

        private IRtcDevice? Device()
        {
            LoggerDevice? d = _main?.SelectedLoggerDevice;
            return (d != null && d.IsConsoleLogger && d.IsConnected) ? new LoggerRtcDevice(d) : null;
        }

        private void DescribeDevice()
        {
            LoggerDevice? d = _main?.SelectedLoggerDevice;
            DeviceText = d == null
                ? "Select a connected KOL, Vesper or Pipistrelle in the device list."
                : !d.IsConsoleLogger
                    ? $"{d.DeviceType} {d.SerialNumber}: clock calibration is available for KOL, Vesper and Pipistrelle only."
                    : $"{d.DeviceType} {d.SerialNumber} — firmware {d.FwId} — device clock in {(DeviceUtc ? "UTC" : "local time")}"
                      + (d.IsConnected ? "" : " (press Connect Device first)");
        }

        private async Task Run(string busy, Func<Task> action)
        {
            IsBusy = true;
            StatusText = busy;
            try { await action(); }
            catch (Exception ex) { StatusText = "Failed: " + ex.Message; }
            finally { IsBusy = false; }
        }

        private async Task<ReferenceClock> EnsureReferenceAsync(bool force = false)
        {
            if (force || _rc == null || (DateTime.UtcNow - _rcAt).TotalMinutes > 10)
            {
                _rc = await ReferenceClock.CreateAsync();
                _rcAt = DateTime.UtcNow;
                double pcFastMs = -_rc.PcOffsetSeconds * 1000.0;
                ReferenceText = _rc.IsNtp
                    ? $"Reference: {_rc.Source}, ± {_rc.UncertaintySeconds * 1000:0} ms. This PC's clock is {Math.Abs(pcFastMs):0} ms {(pcFastMs >= 0 ? "fast" : "slow")}."
                    : "Reference: no NTP server answered — using this PC's clock, which may be off by seconds. Check the network (UDP port 123).";
                StatusText = "";
            }
            return _rc;
        }

        private void ShowStatus(RtcCalStatus st)
        {
            TemperatureText = (st.Flags & RtcCalFlags.TemperatureFault) != 0
                ? "unavailable: the unit's ADC has no usable reference voltage, even from the internal buffer - temperature compensation cannot work on this unit"
                : st.TemperatureC.HasValue
                ? $"{st.TemperatureC:0.0} °C (internal sensor, {(st.TemperatureAgeSeconds >= 0 ? st.TemperatureAgeSeconds + " s ago" : "age unknown")})"
                : "not measured yet";
            CorrectionText = $"{st.AppliedPpb / 1000.0:+0.000;-0.000} ppm (RTC smooth calibration{(st.TemperatureCompensation ? ", time-averaged" : "")})";
            CalibrationText = st.HasUnitCalibration
                ? $"crystal {st.StoredErrorPpb / 1000.0:+0.000;-0.000} ppm at {(st.ReferenceTemperatureC.HasValue ? st.ReferenceTemperatureC.Value.ToString("0.0") + " °C" : "unknown temperature")}" +
                  ((st.Flags & RtcCalFlags.FromDisk) != 0 ? $" (loaded from {UnitDisk.CalibrationFile} on the unit's disk)" : "")
                : (st.Flags & RtcCalFlags.LegacyDrift) != 0
                    ? "none on the unit — the legacy config.json \"cdrift\" value applies"
                    : "none on the unit — no correction";
            TempCompText = st.TemperatureCompensation
                ? $"on: −{st.CurvaturePpbPerC2} ppb/°C² around {st.TurnoverC:0.0} °C, every {st.PeriodSeconds} s (config.json \"tcomp\")"
                : "off (enable \"Temperature compensation\" in the Configuration editor)";
        }

        private async Task RefreshDeviceAsync()
        {
            DescribeDevice();
            ShowHistory();
            IRtcDevice? dev = Device();
            if (dev == null) { StatusText = DeviceText; return; }
            RtcCalStatus? st = await dev.CalAsync(new byte[] { 0 });
            if (st == null)
            {
                StatusText = "The device does not support RTC calibration (needs KOL firmware 1.8 / Vesper firmware 1.21 or later).";
                return;
            }
            ShowStatus(st);
            StoredCalibration? master = _store.Get(dev.UnitKey).Calibration;
            StatusText = (!st.HasUnitCalibration && master != null)
                ? $"The unit has lost its calibration (no {UnitDisk.CalibrationFile} on its disk and the RTC was reset); the stored value {master.ErrorPpb / 1000.0:+0.000;-0.000} ppm is re-applied on the next Sync."
                : "";
        }

        private void ShowMeasurement(OffsetMeasurement m)
        {
            OffsetText = $"{m.OffsetSeconds * 1000:+0.0;-0.0} ms (device {(m.OffsetSeconds >= 0 ? "ahead" : "behind")}); " +
                         $"± {Math.Max(m.MinDelaySeconds / 2, 0.0005) * 1000:0.0} ms, best round trip {m.MinDelaySeconds * 1000:0.00} ms over {m.Samples} exchanges";
        }

        private async Task MeasureAsync()
        {
            IRtcDevice? dev = Device();
            if (dev == null) { StatusText = DeviceText; return; }
            ReferenceClock rc = await EnsureReferenceAsync();
            OffsetMeasurement? m = await RtcSyncService.MeasureAsync(dev, rc, DeviceUtc, 40);
            if (m == null) { StatusText = "The device does not answer the time-sync command (firmware too old?)."; return; }
            _store.Record(dev.UnitKey, m, SyncPointKind.Measured, rc);
            ShowMeasurement(m);
            ShowHistory();
            await RefreshDeviceAsync();
            StatusText = "Measured. " + (rc.IsNtp ? "" : "Warning: PC clock used as reference.");
        }

        private async Task SyncAsync()
        {
            IRtcDevice? dev = Device();
            if (dev == null) { StatusText = DeviceText; return; }
            ReferenceClock rc = await EnsureReferenceAsync();
            SyncResult r = await RtcSyncService.SyncAsync(dev, rc, DeviceUtc, _store);
            if (r.After != null) ShowMeasurement(r.After);
            ShowHistory();
            await RefreshDeviceAsync();
            StatusText = r.Message + (rc.IsNtp ? "" : " Warning: PC clock used as reference.");
        }

        private Task ComputeAsync()
        {
            IRtcDevice? dev = Device();
            string key = dev?.UnitKey ?? _main?.SelectedLoggerDevice?.SerialNumber ?? "";
            _estimate = string.IsNullOrEmpty(key) ? null : RtcSyncService.Estimate(_store.Get(key));
            HasEstimate = _estimate != null;
            EstimateText = _estimate == null
                ? $"Not enough data: needs two measurements at least {RtcSyncService.MinCalibrationHours:0} h apart with no sync in between " +
                  $"({RtcSyncService.RecommendedCalibrationHours:0} h or more recommended)."
                : $"Crystal error {_estimate.ErrorPpb / 1000.0:+0.000;-0.000} ppm ({_estimate.ErrorPpb / 1e9 * 86400 * 1000:+0;-0} ms/day uncorrected) " +
                  $"± {_estimate.UncertaintyPpb / 1000.0:0.000} ppm, from {_estimate.Points} measurements over {_estimate.SpanHours:0.0} h" +
                  (_estimate.ReferenceC.HasValue ? $" at {_estimate.ReferenceC:0.0} °C" : "") + "." +
                  (_estimate.Warning != null ? " Note: " + _estimate.Warning + "." : "");
            StatusText = "";
            return Task.CompletedTask;
        }

        private async Task ApplyAsync()
        {
            IRtcDevice? dev = Device();
            if (dev == null || _estimate == null) { StatusText = DeviceText; return; }
            CalibrationEstimate est = _estimate;
            RtcCalStatus? st = await RtcSyncService.ApplyAsync(dev, est, _store);
            if (st == null || !st.HasUnitCalibration) { StatusText = "The device did not accept the calibration."; return; }
            string disk = await Task.Run(() => RtcSyncService.PersistToDisk(dev.UnitKey, est, st));
            ShowStatus(st);
            _estimate = null;
            HasEstimate = false;
            StatusText = "Calibration applied on the unit and stored here. " + disk + " A new measurement period starts now.";
        }

        private async Task ClearAsync()
        {
            IRtcDevice? dev = Device();
            if (dev == null) { StatusText = DeviceText; return; }
            RtcCalStatus? st = await RtcSyncService.ClearAsync(dev, _store);
            if (st != null) ShowStatus(st);
            StatusText = st == null
                ? "The device did not answer."
                : "Calibration cleared on the unit and here. " + await Task.Run(() => RtcSyncService.RemoveFromDisk(dev.UnitKey));
        }

        private async Task MeasureTemperatureAsync()
        {
            IRtcDevice? dev = Device();
            if (dev == null) { StatusText = DeviceText; return; }
            await dev.CalAsync(new byte[] { 3 });
            await Task.Delay(400);
            RtcCalStatus? st = await dev.CalAsync(new byte[] { 0 });
            if (st != null) ShowStatus(st);
            StatusText = "";
        }

        private void ShowHistory()
        {
            History.Clear();
            string? key = _main?.SelectedLoggerDevice?.SerialNumber?.Trim();
            if (string.IsNullOrEmpty(key)) return;
            foreach (SyncPoint p in _store.Get(key).Points.OrderByDescending(p => p.TrueUtc).Take(200))
            {
                History.Add(new SyncPointRow(
                    p.TrueUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                    $"{p.OffsetSeconds * 1000:+0.0;-0.0}",
                    $"± {Math.Max(p.MinDelayMs / 2, 0.5):0.0}",
                    p.TemperatureC.HasValue ? $"{p.TemperatureC:0.0}" : "",
                    $"{p.AppliedPpb / 1000.0:+0.000;-0.000}",
                    p.Segment,
                    p.Kind == SyncPointKind.AfterSync ? "after sync" : "measured",
                    p.Reference));
            }
        }
    }
}
