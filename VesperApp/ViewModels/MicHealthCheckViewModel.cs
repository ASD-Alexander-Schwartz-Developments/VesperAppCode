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
    /// Microphone health check, the same feature on KOL, Vesper and Pipistrelle: one self-test request
    /// (console 60, VESPER_TEST_AUDIO) per microphone, analysed on the unit by the shared firmware engine,
    /// judged here with the thresholds of the embedded <see cref="AudioSelftestSpec"/>. Mic count and
    /// applicability follow the product and firmware (<see cref="DeviceCapabilities"/>): KOL 4 from 1.8,
    /// Vesper / Pipistrelle 1 from the feature release; older firmware is not sent the request.
    /// </summary>
    public class MicHealthCheckViewModel : ViewModelBase
    {
        private readonly AudioSelftestSpec _spec = AudioSelftestSpec.Default;

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
            if (dev != null && DeviceCapabilities.HasMicrophones(dev.DeviceType)
                && DeviceCapabilities.SupportsAudioSelftest(dev.DeviceType, dev.FirmwareVersion))
            {
                MicCountOptions = DeviceCapabilities.MicCountOptions(dev.DeviceType);
                MicCount = DeviceCapabilities.MicCount(dev.DeviceType);
                IsSupported = true;
                Summary = $"{DeviceCapabilities.DisplayName(dev.DeviceType)}: {MicCount} microphone(s). {_spec.Stimulus.Description} Then start the check.";
            }
            else if (dev != null && DeviceCapabilities.HasMicrophones(dev.DeviceType))
            {
                MicCountOptions = System.Array.Empty<int>();
                IsSupported = false;
                Summary = $"The microphone check needs newer firmware on this {DeviceCapabilities.DisplayName(dev.DeviceType)} " +
                          $"(KOL {DeviceCapabilities.KolAudioSelftestFirmware}, Vesper / Pipistrelle {DeviceCapabilities.VesperUsbModesFirmware} or later).";
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
            if (!DeviceCapabilities.SupportsAudioSelftest(dev.DeviceType, dev.FirmwareVersion))
            {
                RefreshForDevice(dev);
                return;
            }

            // Clamp to the product's mic count in case the UI default lagged a selection change.
            int micsToTest = System.Math.Min(MicCount, DeviceCapabilities.MicCount(dev.DeviceType));

            IsRunning = true;
            Results.Clear();
            for (int i = 0; i < micsToTest; i++)
                Results.Add(new MicHealthResult(i) { Status = "Testing…", StatusBrush = Brushes.Gray });

            Summary = "Keep the test tones playing near the device while the check runs…";

            var q = _spec.Request;
            var replies = new System.Collections.Generic.List<AudioBenchResult?>();
            int healthy = 0;
            try
            {
                for (int i = 0; i < micsToTest; i++)
                {
                    MicHealthResult row = Results[i];
                    byte[] req = AudioBenchTest.BuildRequest(q.SampleRateHz, q.DurationMs, q.SnrMarginDb, q.TonesHz, (byte)i);
                    byte[]? resp = await dev.SendCommandAsync(MessageTypes.VESPER_TEST_AUDIO, req, 8000);
                    AudioBenchResult? parsed = AudioBenchTest.ParseResponse(resp);
                    replies.Add(parsed);

                    var (verdict, detail) = AudioSelftestEvaluator.Evaluate(parsed, _spec.Pass);
                    await Dispatcher.UIThread.InvokeAsync(() => Apply(row, verdict, detail));
                    if (verdict == MicVerdict.Healthy) healthy++;
                }

                string? spread = AudioSelftestEvaluator.SpreadWarning(replies, _spec.Pass);
                await Dispatcher.UIThread.InvokeAsync(() =>
                    Summary = $"{healthy} of {micsToTest} microphone(s) healthy." + (spread != null ? " " + spread : "") +
                              (_spec.Validated ? "" : " (Thresholds not yet validated on hardware.)"));
            }
            finally
            {
                IsRunning = false;
            }
        }

        private static void Apply(MicHealthResult row, MicVerdict verdict, string detail)
        {
            row.Detail = detail;
            (row.Status, row.StatusBrush) = verdict switch
            {
                MicVerdict.Healthy    => ("Healthy", (IBrush)Brushes.SeaGreen),
                MicVerdict.Weak       => ("Weak", Brushes.Goldenrod),
                MicVerdict.TooQuiet   => ("Too quiet", Brushes.Goldenrod),
                MicVerdict.Overloaded => ("Too loud", Brushes.Goldenrod),
                MicVerdict.Busy       => ("Busy", Brushes.Goldenrod),
                MicVerdict.NotPresent => ("Not fitted", Brushes.Gray),
                MicVerdict.NoSignal   => ("No signal", Brushes.OrangeRed),
                MicVerdict.Offset     => ("Offset", Brushes.OrangeRed),
                MicVerdict.NoResponse => ("No response", Brushes.OrangeRed),
                _                     => ("Failed", Brushes.OrangeRed),
            };
        }
    }
}
