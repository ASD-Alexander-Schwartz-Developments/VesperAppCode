using System;
using System.Collections.ObjectModel;
using System.Reactive.Linq;
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
    /// User-facing microphone health check: capture each mic in turn over the USB
    /// console (VESPER_TEST_AUDIO) and report a friendly per-mic OK / Problem
    /// verdict. Aimed at end users assessing device health, not factory go/no-go.
    /// Mic count, options and applicability follow the detected product via
    /// <see cref="DeviceCapabilities"/> (KOL has 4, Vesper/Pipistrelle 1, Nanotag none).
    /// </summary>
    public class MicHealthCheckViewModel : ViewModelBase
    {
        // A health check needs an acoustic stimulus. Two well-separated in-band
        // tones; a mic is "healthy" if both clear a lenient SNR margin.
        private const uint   SampleRate  = 16000;
        private const ushort DurationMs  = 200;
        private const byte   SnrThreshDb = 12;
        private static readonly int[] Tones = { 1000, 4000 };

        private readonly MainViewViewModel _main;

        public ObservableCollection<MicHealthResult> Results { get; } = new();

        // Both follow the detected product (see RefreshForDevice); 1-mic fallback.
        private int[] _micCountOptions = { 1 };
        public int[] MicCountOptions { get => _micCountOptions; private set => this.RaiseAndSetIfChanged(ref _micCountOptions, value); }

        private int _micCount = 1;
        public int MicCount { get => _micCount; set => this.RaiseAndSetIfChanged(ref _micCount, value); }

        // False when the selected product has no microphones (test not applicable).
        private bool _isSupported;
        public bool IsSupported { get => _isSupported; private set => this.RaiseAndSetIfChanged(ref _isSupported, value); }

        private bool _isRunning;
        public bool IsRunning { get => _isRunning; set => this.RaiseAndSetIfChanged(ref _isRunning, value); }

        private string _summary = "Connect your device over USB, then start the check.";
        public string Summary { get => _summary; set => this.RaiseAndSetIfChanged(ref _summary, value); }

        public ICommand RunCommand { get; }

        public MicHealthCheckViewModel(MainViewViewModel main)
        {
            _main = main;
            var canRun = this.WhenAnyValue(x => x.IsRunning, x => x.IsSupported,
                                           (running, supported) => !running && supported);
            RunCommand = ReactiveCommand.CreateFromTask(RunAsync, canRun);

            // Track the device selected in the main list so the mic count, options and
            // applicability always match the connected product.
            if (_main?.SelectedLoggerDeviceModel != null)
                _main.SelectedLoggerDeviceModel.SelectionChanged += OnDeviceSelectionChanged;
            RefreshForDevice(_main?.SelectedLoggerDevice);
        }

        // Parameterless ctor for the XAML designer / ViewLocator fallback.
        public MicHealthCheckViewModel() : this(null!) { }

        private void OnDeviceSelectionChanged(object? sender,
            Avalonia.Controls.Selection.SelectionModelSelectionChangedEventArgs<LoggerDevice> e)
        {
            LoggerDevice? dev = (e.SelectedItems != null && e.SelectedItems.Count > 0) ? e.SelectedItems[0] : null;
            Dispatcher.UIThread.Post(() => RefreshForDevice(dev));
        }

        /// <summary>Align the mic-count options/default and applicability with the product.</summary>
        public void RefreshForDevice(LoggerDevice? dev)
        {
            if (dev != null && DeviceCapabilities.HasMicrophones(dev.DeviceType))
            {
                MicCountOptions = DeviceCapabilities.MicCountOptions(dev.DeviceType);
                MicCount = DeviceCapabilities.MicCount(dev.DeviceType);
                IsSupported = true;
                Summary = $"{dev.DeviceType}: {MicCount} microphone(s) expected. Connect over USB, then start the check.";
            }
            else
            {
                MicCountOptions = System.Array.Empty<int>();
                IsSupported = false;
                Summary = dev != null
                    ? $"{dev.DeviceType} has no microphones to test."
                    : "Select a connected device with microphones, then start the check.";
            }
        }

        private async Task RunAsync()
        {
            LoggerDevice? dev = _main?.SelectedLoggerDevice;

            if (dev == null || dev.IsConnected == false || dev.IsComportDevice == false)
            {
                Summary = "No connected USB device found. Connect a device and press Connect first.";
                return;
            }
            if (!DeviceCapabilities.HasMicrophones(dev.DeviceType))
            {
                Summary = $"{dev.DeviceType} has no microphones to test.";
                return;
            }

            // Clamp to the product's mic count in case the UI default lagged a selection change.
            int micsToTest = System.Math.Min(MicCount, DeviceCapabilities.MicCount(dev.DeviceType));

            IsRunning = true;
            Results.Clear();
            for (int i = 0; i < micsToTest; i++)
                Results.Add(new MicHealthResult(i) { Status = "Testing…", StatusBrush = Brushes.Gray });

            Summary = "Play a steady tone near the device (e.g. from your phone) while the check runs…";

            int healthy = 0;
            try
            {
                for (int i = 0; i < micsToTest; i++)
                {
                    MicHealthResult row = Results[i];
                    byte[] req = AudioBenchTest.BuildRequest(SampleRate, DurationMs, SnrThreshDb, Tones, (byte)i);
                    byte[]? resp = await dev.SendCommandAsync(MessageTypes.VESPER_TEST_AUDIO, req, 8000);
                    AudioBenchResult? parsed = AudioBenchTest.ParseResponse(resp);

                    await Dispatcher.UIThread.InvokeAsync(() => Apply(row, resp, parsed));
                    if (parsed != null && parsed.Pass) healthy++;
                }

                await Dispatcher.UIThread.InvokeAsync(() =>
                    Summary = $"{healthy} of {micsToTest} microphone(s) healthy.");
            }
            finally
            {
                IsRunning = false;
            }
        }

        private static void Apply(MicHealthResult row, byte[]? resp, AudioBenchResult? parsed)
        {
            if (resp == null || parsed == null)
            {
                row.Status = "No response";
                row.Detail = "The device didn't reply. Make sure it's on and not currently recording.";
                row.StatusBrush = Brushes.OrangeRed;
            }
            else if (parsed.Pass)
            {
                row.Status = "Healthy";
                row.Detail = $"SNR {string.Join("/", parsed.SnrDb)} dB · noise floor {parsed.NoiseFloorDb} dB";
                row.StatusBrush = Brushes.SeaGreen;
            }
            else if (parsed.NoSignal)
            {
                row.Status = "No signal";
                row.Detail = "No audio captured — this microphone may be faulty or disconnected.";
                row.StatusBrush = Brushes.OrangeRed;
            }
            else
            {
                row.Status = "Weak";
                row.Detail = $"Low tone SNR {string.Join("/", parsed.SnrDb)} dB — make sure a tone is playing close to the device.";
                row.StatusBrush = Brushes.Goldenrod;
            }
        }
    }
}
