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

        // The stimulus from this PC's speaker (on by default): with no tone a healthy microphone is "Weak".
        private bool _playTones = true;
        public bool PlayTones { get => _playTones; set => this.RaiseAndSetIfChanged(ref _playTones, value); }

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

            var q = _spec.Request;
            if (PlayTones)
            {
                int seconds = 2 + 2 * micsToTest;              // estimate: the sound check + ~2 s per microphone (not yet timed on a unit)
                if (!await AskAsync($"A test sound will play for about {seconds} seconds",
                        "VesperApp plays a steady 1 kHz + 4 kHz tone from this computer's speaker while it tests the microphone(s).\n\n" +
                        "Set the speaker volume to 50–80 % and hold the device close to the speaker.\n\nPlay the sound and run the check?"))
                {
                    Summary = "Check cancelled - nothing was played.";
                    return;
                }
            }

            IsRunning = true;
            Results.Clear();
            for (int i = 0; i < micsToTest; i++)
                Results.Add(new MicHealthResult(i) { Status = "Testing…", StatusBrush = Brushes.Gray });

            var replies = new System.Collections.Generic.List<AudioBenchResult?>();
            int healthy = 0;
            string toneNote = "";
            using var tones = new Services.Audio.TonePlayer();
            try
            {
                if (PlayTones)
                {
                    if (!await Task.Run(() => tones.Start(q.TonesHz)))
                    {
                        await Dispatcher.UIThread.InvokeAsync(() => Results.Clear());
                        Summary = $"The test sound could not be played on this computer ({tones.LastError}). Untick \"Play test tones on this PC\" and play them from a phone instead.";
                        return;
                    }
                    Summary = "Playing the test sound - checking that it really comes out…";
                    await Task.Delay(400);                     // output latency + fade-in

                    // Feedback loop: this PC's own microphone listens for the tones (no speaker, muted, volume 0
                    // would otherwise read as a weak unit microphone). Some PC microphones filter out steady
                    // tones, so when it cannot confirm them the user is asked instead of failing the check.
                    var heard = await Task.Run(() => Services.Audio.TonePlayer.CheckHeard(q.TonesHz));
                    if (heard.Heard)
                    {
                        toneNote = " Test sound confirmed by this PC's microphone.";
                    }
                    else
                    {
                        string why = heard.Microphone == null
                            ? "This computer has no microphone of its own to confirm that the sound is playing."
                            : "This computer's microphone could not confirm the sound (it may filter out steady tones).";
                        if (!await AskAsync("Do you hear the test sound now?",
                                why + "\n\nYes: run the check. No: stop - make sure the speaker is on, not muted, volume 50–80 %."))
                        {
                            tones.Stop();
                            await Dispatcher.UIThread.InvokeAsync(() => Results.Clear());
                            Summary = "No test sound, the check was not run: make sure the speaker is on and not muted (volume 50–80 %), " +
                                      "or untick \"Play test tones on this PC\" and play them from a phone.";
                            return;
                        }
                        toneNote = " Test sound confirmed by you.";
                    }
                    Summary = $"Playing the test sound on {tones.DeviceName} - keep the device close to the speaker…";
                }
                else
                {
                    Summary = "Keep the test tones playing near the device while the check runs…";
                }

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
                    Summary = $"{healthy} of {micsToTest} microphone(s) healthy." + (spread != null ? " " + spread : "") + toneNote +
                              (_spec.Validated ? "" : " (Thresholds not yet validated on hardware.)"));
            }
            finally
            {
                tones.Stop();
                IsRunning = false;
            }
        }

        private static Task<bool> AskAsync(string header, string message) =>
            Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var box = MsBox.Avalonia.MessageBoxManager.GetMessageBoxStandard(new MsBox.Avalonia.Dto.MessageBoxStandardParams
                {
                    ButtonDefinitions = MsBox.Avalonia.Enums.ButtonEnum.YesNo,
                    ContentTitle = "Microphone check",
                    ContentHeader = header,
                    ContentMessage = message,
                    Icon = MsBox.Avalonia.Enums.Icon.Question,
                    WindowIcon = App.MainWindow?.Icon,
                });
                return await box.ShowWindowDialogAsync(App.MainWindow) == MsBox.Avalonia.Enums.ButtonResult.Yes;
            });

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
