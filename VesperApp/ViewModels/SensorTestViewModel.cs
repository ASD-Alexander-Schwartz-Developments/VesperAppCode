using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Media;
using Avalonia.Threading;
using ReactiveUI;
using VesperApp.Models;
using VesperApp.Services;

namespace VesperApp.ViewModels
{
    /// <summary>
    /// Live test of the add-on sensors before a deployment: one request per sensor type (console 71-74),
    /// answered by the unit in Idle with presence, device id and a live reading. KOL tests the IMU
    /// (IMU10 / IMU10HT) and temperature / humidity add-ons from firmware 1.8; Vesper and Pipistrelle
    /// also light and EXG. The unit probes every variant itself, so no configuration is needed.
    /// </summary>
    public class SensorTestViewModel : ViewModelBase
    {
        private readonly MainViewViewModel? _main;

        public ObservableCollection<SensorTestRow> Rows { get; } = new();

        private bool _isSupported;
        public bool IsSupported { get => _isSupported; private set => this.RaiseAndSetIfChanged(ref _isSupported, value); }

        private bool _isRunning;
        public bool IsRunning { get => _isRunning; set => this.RaiseAndSetIfChanged(ref _isRunning, value); }

        private string _summary = "Connect your device over USB, then run the test.";
        public string Summary { get => _summary; set => this.RaiseAndSetIfChanged(ref _summary, value); }

        public ICommand RunCommand { get; }

        public SensorTestViewModel(MainViewViewModel main)
        {
            _main = main;
            var canRun = this.WhenAnyValue(x => x.IsRunning, x => x.IsSupported, (running, supported) => !running && supported);
            RunCommand = ReactiveCommand.CreateFromTask(RunAsync, canRun);

            if (_main?.SelectedLoggerDeviceModel != null)
                _main.SelectedLoggerDeviceModel.SelectionChanged += (s, e) =>
                {
                    LoggerDevice? dev = (e.SelectedItems != null && e.SelectedItems.Count > 0) ? e.SelectedItems[0] : null;
                    Dispatcher.UIThread.Post(() => RefreshForDevice(dev));
                };
            RefreshForDevice(_main?.SelectedLoggerDevice);
        }

        public SensorTestViewModel() : this(null!) { }

        public void RefreshForDevice(LoggerDevice? dev)
        {
            Rows.Clear();
            if (dev == null)
            {
                IsSupported = false;
                Summary = "Select a connected device, then run the test.";
                return;
            }
            if (!DeviceCapabilities.SupportsSensorTest(dev.DeviceType, dev.FirmwareVersion))
            {
                IsSupported = false;
                Summary = DeviceCapabilities.HasAddonSensors(dev.DeviceType)
                    ? $"The sensor test needs newer firmware on this {DeviceCapabilities.DisplayName(dev.DeviceType)} (KOL {DeviceCapabilities.KolAudioSelftestFirmware}, Vesper / Pipistrelle {DeviceCapabilities.VesperUsbModesFirmware} or later)."
                    : $"{DeviceCapabilities.DisplayName(dev.DeviceType)} has no add-on sensors to test.";
                return;
            }

            Rows.Add(new SensorTestRow(MessageTypes.VESPER_TEST_IMU, "Motion (IMU10 / IMU10HT)", "Keep the unit still"));
            Rows.Add(new SensorTestRow(MessageTypes.VESPER_TEST_TPRH, "Temperature / humidity (TPRH31)", ""));
            if (dev.DeviceType != DeviceTypes.Kol)
            {
                Rows.Add(new SensorTestRow(MessageTypes.VESPER_TEST_ALS, "Light (ALS3001D)", ""));
                Rows.Add(new SensorTestRow(MessageTypes.VESPER_TEST_EEG, "Biopotential (EXG)", ""));
            }
            IsSupported = true;
            Summary = $"{DeviceCapabilities.DisplayName(dev.DeviceType)}: fit the add-on board(s), keep the unit still and not recording, then run the test.";
        }

        private async Task RunAsync()
        {
            LoggerDevice? dev = _main?.SelectedLoggerDevice;
            if (dev == null || !dev.IsConnected || !dev.IsComportDevice)
            {
                Summary = "No connected USB device found. Connect a device and press Connect first.";
                return;
            }

            IsRunning = true;
            int found = 0;
            try
            {
                foreach (SensorTestRow row in Rows)
                {
                    await Dispatcher.UIThread.InvokeAsync(() => { row.Status = "Testing…"; row.StatusBrush = Brushes.Gray; row.Detail = ""; });
                    byte[]? resp = await dev.SendCommandAsync(row.Test, SensorBenchTest.BuildRequest(0), 5000);
                    SensorBenchResult? r = SensorBenchTest.ParseResponse(resp);
                    var (status, brush, detail) = SensorBenchTest.Describe(row.Test, r);
                    if (r != null && r.Status != SensorBenchTest.StatusIdFail && r.Status != SensorBenchTest.StatusNotPresent) found++;
                    await Dispatcher.UIThread.InvokeAsync(() => { row.Status = status; row.StatusBrush = brush; row.Detail = detail; });
                }
                await Dispatcher.UIThread.InvokeAsync(() =>
                    Summary = found == 0 ? "No add-on sensor answered." : $"{found} add-on sensor(s) found - see each result.");
            }
            finally
            {
                IsRunning = false;
            }
        }
    }
}
