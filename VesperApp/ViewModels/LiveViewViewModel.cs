using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ASD.Contracts;
using Avalonia.Threading;
using ReactiveUI;
using VesperApp.Models;
using VesperApp.Services.Audio;

namespace VesperApp.ViewModels
{
    /// <summary>One ranked classifier label for the recognition panel.</summary>
    public sealed class ClassLabelRow : ReactiveObject
    {
        private string _label = ""; private double _confidence;
        public string Label { get => _label; set => this.RaiseAndSetIfChanged(ref _label, value); }
        public double Confidence { get => _confidence; set { this.RaiseAndSetIfChanged(ref _confidence, value); this.RaisePropertyChanged(nameof(PercentText)); } }
        public string PercentText => $"{Confidence * 100:0} %";
    }

    /// <summary>
    /// Live View: real-time microphone visualisation (scope, spectrum, spectrogram) from
    /// either the selected KOL streaming as a 4-channel USB microphone (Live Audio mode,
    /// microphone parameters set live over the console) or a PC input, with optional local
    /// processing (filters, post gain) and a pluggable <see cref="ISoundClassifier"/>.
    /// </summary>
    public sealed class LiveViewViewModel : ViewModelBase
    {
        private readonly MainViewViewModel _main;
        private readonly AudioCaptureService _capture = new();
        private readonly ISoundClassifier _classifier;
        private readonly DispatcherTimer _classifyTimer;
        private int _classifyBusy;

        public LiveViewViewModel(MainViewViewModel main)
        {
            _main = main;
            _classifier = ASD.Platform.PlatformServices.SoundClassifier ?? new HeuristicSoundClassifier();
            ClassifierName = _classifier.Name;
            ClassifierIsDemo = ASD.Platform.PlatformServices.SoundClassifier == null;

            StartCommand = ReactiveCommand.CreateFromTask(StartAsync);
            StopCommand = ReactiveCommand.Create(Stop);
            ApplyKolParamsCommand = ReactiveCommand.CreateFromTask(ApplyKolParamsAsync);
            ReadKolParamsCommand = ReactiveCommand.CreateFromTask(ReadKolParamsAsync);
            RefreshDevicesCommand = ReactiveCommand.Create(RefreshDevices);
            KolToIdleCommand = ReactiveCommand.CreateFromTask(KolToIdleAsync);
            FocusCommand = ReactiveCommand.Create<string>(ToggleFocus);
            ToggleControlsCommand = ReactiveCommand.Create(() => ShowControls = !ShowControls);
            ClearDetectionsCommand = ReactiveCommand.Create(() => DetectionLog.Clear());

            _classifyTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(600), DispatcherPriority.Background, (_, _) => ClassifyTick());

            if (main != null) main.PropertyChanged += OnMainPropertyChanged;
            RefreshDevices();
            UpdateKolAvailability();
            UpdateLayout();
        }

        public LiveViewViewModel() : this(null!) { }

        // ------------------------------------------------------------------ source
        private bool _isKolSource = true;
        public bool IsKolSource { get => _isKolSource; set { this.RaiseAndSetIfChanged(ref _isKolSource, value); this.RaisePropertyChanged(nameof(IsPcSource)); } }
        public bool IsPcSource { get => !_isKolSource; set => IsKolSource = !value; }

        public ObservableCollection<AudioInputDevice> PcDevices { get; } = new();
        private AudioInputDevice? _selectedPcDevice;
        public AudioInputDevice? SelectedPcDevice { get => _selectedPcDevice; set => this.RaiseAndSetIfChanged(ref _selectedPcDevice, value); }
        public int[] PcChannelOptions { get; } = { 1, 2 };
        private int _pcChannels = 1;
        public int PcChannels { get => _pcChannels; set => this.RaiseAndSetIfChanged(ref _pcChannels, value); }

        private string _kolAvailabilityText = "";
        public string KolAvailabilityText { get => _kolAvailabilityText; private set => this.RaiseAndSetIfChanged(ref _kolAvailabilityText, value); }
        private bool _isKolAvailable;
        public bool IsKolAvailable { get => _isKolAvailable; private set => this.RaiseAndSetIfChanged(ref _isKolAvailable, value); }

        public string? AudioBackendProblem => AudioCaptureService.UnavailableReason;

        // ------------------------------------------------------------------ KOL microphone parameters
        public int[] SampleRates { get; } = { 8000, 16000, 32000, 48000, 96000 };
        private int _selectedSampleRate = 48000;
        public int SelectedSampleRate { get => _selectedSampleRate; set { this.RaiseAndSetIfChanged(ref _selectedSampleRate, value); this.RaisePropertyChanged(nameof(RateNoteText)); } }

        /// <summary>The MDF decimation is integer: 4.8 MHz PDM / (CIC ratio × reshape); some
        /// nominal rates are not exactly reachable (96 kHz runs at 100 kHz).</summary>
        public string RateNoteText
        {
            get
            {
                double pdm = 4_800_000.0; int ratio = (int)(pdm / _selectedSampleRate);
                bool rs4 = ReshapeEnabled && !ReshapeRatioOne;
                if (rs4) ratio /= 4;
                double actual = pdm / (ratio * (rs4 ? 4 : 1));
                return Math.Abs(actual - _selectedSampleRate) < 1 ? $"exact ({_selectedSampleRate / 1000.0:0.#} kHz)" : $"MDF delivers {actual / 1000.0:0.##} kHz for this setting";
            }
        }

        private double _gainStep = 12;
        public double GainStep { get => _gainStep; set { this.RaiseAndSetIfChanged(ref _gainStep, Math.Round(value)); this.RaisePropertyChanged(nameof(GainText)); } }
        public string GainText => $"{GainStep * 3:+0;-0;0} dB  (step {GainStep:0})";

        public string[] HpfOptions { get; } = KolMicParams.HpfLabels;
        private int _hpfIndex = 1;
        public int HpfIndex { get => _hpfIndex; set => this.RaiseAndSetIfChanged(ref _hpfIndex, value); }

        private bool _reshapeEnabled = true;
        public bool ReshapeEnabled { get => _reshapeEnabled; set { this.RaiseAndSetIfChanged(ref _reshapeEnabled, value); this.RaisePropertyChanged(nameof(RateNoteText)); } }
        private bool _reshapeRatioOne;
        public bool ReshapeRatioOne { get => _reshapeRatioOne; set { this.RaiseAndSetIfChanged(ref _reshapeRatioOne, value); this.RaisePropertyChanged(nameof(RateNoteText)); } }
        private bool _cic4;
        public bool Cic4 { get => _cic4; set => this.RaiseAndSetIfChanged(ref _cic4, value); }

        public KolMicParams CurrentKolParams => new((int)GainStep, HpfIndex, ReshapeEnabled, ReshapeRatioOne, Cic4);

        private void LoadKolParams(KolMicParams p)
        {
            GainStep = p.GainStep; HpfIndex = Math.Clamp(p.Hpf, 0, HpfOptions.Length - 1);
            ReshapeEnabled = p.ReshapeEnabled; ReshapeRatioOne = p.ReshapeRatioOne; Cic4 = p.Cic4;
        }

        // ------------------------------------------------------------------ local processing (host side)
        private bool _processingEnabled;
        public bool ProcessingEnabled { get => _processingEnabled; set { this.RaiseAndSetIfChanged(ref _processingEnabled, value); UpdateDisplayBuffer(); } }
        private double _postGainDb;
        public double PostGainDb { get => _postGainDb; set { this.RaiseAndSetIfChanged(ref _postGainDb, Math.Round(value)); this.RaisePropertyChanged(nameof(PostGainText)); RebuildProcessing(); } }
        public string PostGainText => $"{PostGainDb:+0;-0;0} dB";
        private bool _hpEnabled;
        public bool HpEnabled { get => _hpEnabled; set { this.RaiseAndSetIfChanged(ref _hpEnabled, value); RebuildProcessing(); } }
        private double _hpHz = 100;
        public double HpHz { get => _hpHz; set { this.RaiseAndSetIfChanged(ref _hpHz, Math.Max(1, value)); RebuildProcessing(); } }
        private bool _lpEnabled;
        public bool LpEnabled { get => _lpEnabled; set { this.RaiseAndSetIfChanged(ref _lpEnabled, value); RebuildProcessing(); } }
        private double _lpHz = 20000;
        public double LpHz { get => _lpHz; set { this.RaiseAndSetIfChanged(ref _lpHz, Math.Max(10, value)); RebuildProcessing(); } }
        public int[] FilterOrders { get; } = { 2, 4, 6, 8 };
        private int _hpOrder = 2;
        public int HpOrder { get => _hpOrder; set { this.RaiseAndSetIfChanged(ref _hpOrder, value); RebuildProcessing(); } }
        private int _lpOrder = 4;
        public int LpOrder { get => _lpOrder; set { this.RaiseAndSetIfChanged(ref _lpOrder, value); RebuildProcessing(); } }
        private bool _notchEnabled;
        public bool NotchEnabled { get => _notchEnabled; set { this.RaiseAndSetIfChanged(ref _notchEnabled, value); RebuildProcessing(); } }
        public double[] NotchFrequencies { get; } = { 50, 60 };
        private double _notchHz = 50;
        public double NotchHz { get => _notchHz; set { this.RaiseAndSetIfChanged(ref _notchHz, value); RebuildProcessing(); } }
        public int[] NotchHarmonicOptions { get; } = { 1, 2, 3, 5, 8 };
        private int _notchHarmonics = 3;
        public int NotchHarmonics { get => _notchHarmonics; set { this.RaiseAndSetIfChanged(ref _notchHarmonics, value); RebuildProcessing(); } }
        private string _processingText = "pass-through";
        public string ProcessingText { get => _processingText; private set => this.RaiseAndSetIfChanged(ref _processingText, value); }

        private ProcessingSettings CurrentProcessing => new(PostGainDb, HpEnabled, HpHz, HpOrder, LpEnabled, LpHz, LpOrder, NotchEnabled, NotchHz, NotchHarmonics, 30);

        private void RebuildProcessing()
        {
            LiveAudioBuffer? raw = _capture.Buffer;
            if (raw == null) { ProcessingText = new ProcessingChain(CurrentProcessing, 48000, 1).Describe(); return; }
            var chain = new ProcessingChain(CurrentProcessing, raw.SampleRate, raw.Channels);
            _capture.Processor = chain;
            ProcessingText = chain.Describe();
        }

        // ------------------------------------------------------------------ display options
        private bool _ch0 = true, _ch1 = true, _ch2 = true, _ch3 = true;
        public bool Ch0On { get => _ch0; set { this.RaiseAndSetIfChanged(ref _ch0, value); this.RaisePropertyChanged(nameof(ChannelMask)); } }
        public bool Ch1On { get => _ch1; set { this.RaiseAndSetIfChanged(ref _ch1, value); this.RaisePropertyChanged(nameof(ChannelMask)); } }
        public bool Ch2On { get => _ch2; set { this.RaiseAndSetIfChanged(ref _ch2, value); this.RaisePropertyChanged(nameof(ChannelMask)); } }
        public bool Ch3On { get => _ch3; set { this.RaiseAndSetIfChanged(ref _ch3, value); this.RaisePropertyChanged(nameof(ChannelMask)); } }
        public int ChannelMask => (_ch0 ? 1 : 0) | (_ch1 ? 2 : 0) | (_ch2 ? 4 : 0) | (_ch3 ? 8 : 0);

        public string[] ChannelOptions { get; } = { "Mix of all channels", "Channel 0 (WE, rising edge)", "Channel 1 (WE, falling edge)", "Channel 2 (NS, rising edge)", "Channel 3 (NS, falling edge)" };
        private int _channelOptionIndex = 1;
        public int ChannelOptionIndex { get => _channelOptionIndex; set { this.RaiseAndSetIfChanged(ref _channelOptionIndex, value); this.RaisePropertyChanged(nameof(SelectedChannel)); } }
        public int SelectedChannel => _channelOptionIndex - 1;   // -1 = mix

        public double[] ScopeWindowOptions { get; } = { 5, 10, 20, 50, 100, 200, 500, 1000 };
        private double _scopeWindowMs = 50;
        public double ScopeWindowMs { get => _scopeWindowMs; set => this.RaiseAndSetIfChanged(ref _scopeWindowMs, value); }

        public int[] FftSizeOptions { get; } = { 1024, 2048, 4096, 8192 };
        private int _fftSize = 4096;
        public int FftSize { get => _fftSize; set => this.RaiseAndSetIfChanged(ref _fftSize, value); }

        private bool _logFrequency;
        public bool LogFrequency { get => _logFrequency; set => this.RaiseAndSetIfChanged(ref _logFrequency, value); }

        public double[] SpectrogramSecondsOptions { get; } = { 4, 8, 15, 30, 60 };
        private double _spectrogramSeconds = 8;
        public double SpectrogramSeconds { get => _spectrogramSeconds; set => this.RaiseAndSetIfChanged(ref _spectrogramSeconds, value); }

        private double _spectrogramFloorDb = -100;
        public double SpectrogramFloorDb { get => _spectrogramFloorDb; set { this.RaiseAndSetIfChanged(ref _spectrogramFloorDb, Math.Round(value)); this.RaisePropertyChanged(nameof(SpectrogramFloorText)); } }
        public string SpectrogramFloorText => $"{SpectrogramFloorDb:0} dBFS";

        // ------------------------------------------------------------------ layout: focus / controls
        private bool _showControls = true;
        public bool ShowControls { get => _showControls; set => this.RaiseAndSetIfChanged(ref _showControls, value); }

        private string _focusedView = "";
        /// <summary>"" = all instruments; "Scope" / "Spectrum" / "Spectrogram" = that one full size.</summary>
        public string FocusedView { get => _focusedView; private set { this.RaiseAndSetIfChanged(ref _focusedView, value); UpdateLayout(); } }
        public bool IsFocused => _focusedView.Length > 0;

        private void ToggleFocus(string view) => FocusedView = _focusedView == view ? "" : view;

        public bool ScopeVisible { get; private set; } = true;
        public bool SpectrumVisible { get; private set; } = true;
        public bool SpectrogramVisible { get; private set; } = true;
        public int ScopeRow { get; private set; } public int ScopeCol { get; private set; } public int ScopeRowSpan { get; private set; } = 1; public int ScopeColSpan { get; private set; } = 1;
        public int SpectrumRow { get; private set; } public int SpectrumCol { get; private set; } = 1; public int SpectrumRowSpan { get; private set; } = 1; public int SpectrumColSpan { get; private set; } = 1;
        public int SpectrogramRow { get; private set; } = 1; public int SpectrogramCol { get; private set; } public int SpectrogramRowSpan { get; private set; } = 1; public int SpectrogramColSpan { get; private set; } = 2;

        private void UpdateLayout()
        {
            string f = _focusedView;
            ScopeVisible = f == "" || f == "Scope"; SpectrumVisible = f == "" || f == "Spectrum"; SpectrogramVisible = f == "" || f == "Spectrogram";
            if (f == "")
            {
                ScopeRow = 0; ScopeCol = 0; ScopeRowSpan = 1; ScopeColSpan = 1;
                SpectrumRow = 0; SpectrumCol = 1; SpectrumRowSpan = 1; SpectrumColSpan = 1;
                SpectrogramRow = 1; SpectrogramCol = 0; SpectrogramRowSpan = 1; SpectrogramColSpan = 2;
            }
            else
            {
                ScopeRow = SpectrumRow = SpectrogramRow = 0; ScopeCol = SpectrumCol = SpectrogramCol = 0;
                ScopeRowSpan = SpectrumRowSpan = SpectrogramRowSpan = 2; ScopeColSpan = SpectrumColSpan = SpectrogramColSpan = 2;
            }
            foreach (string n in new[] { nameof(ScopeVisible), nameof(SpectrumVisible), nameof(SpectrogramVisible), nameof(IsFocused),
                                          nameof(ScopeRow), nameof(ScopeCol), nameof(ScopeRowSpan), nameof(ScopeColSpan),
                                          nameof(SpectrumRow), nameof(SpectrumCol), nameof(SpectrumRowSpan), nameof(SpectrumColSpan),
                                          nameof(SpectrogramRow), nameof(SpectrogramCol), nameof(SpectrogramRowSpan), nameof(SpectrogramColSpan) })
                this.RaisePropertyChanged(n);
        }

        // ------------------------------------------------------------------ run state
        private LiveAudioBuffer? _displayBuffer;
        /// <summary>What the instruments and the classifier read: the processed stream when local
        /// processing is enabled, otherwise the raw capture.</summary>
        public LiveAudioBuffer? DisplayBuffer { get => _displayBuffer; private set => this.RaiseAndSetIfChanged(ref _displayBuffer, value); }
        private void UpdateDisplayBuffer() => DisplayBuffer = ProcessingEnabled ? _capture.ProcessedBuffer ?? _capture.Buffer : _capture.Buffer;

        private bool _isRunning;
        public bool IsRunning { get => _isRunning; private set { this.RaiseAndSetIfChanged(ref _isRunning, value); this.RaisePropertyChanged(nameof(IsIdle)); } }
        public bool IsIdle => !_isRunning;
        private string _statusText = "Select a source and press Start.";
        public string StatusText { get => _statusText; private set => this.RaiseAndSetIfChanged(ref _statusText, value); }
        private string _streamInfoText = "";
        public string StreamInfoText { get => _streamInfoText; private set => this.RaiseAndSetIfChanged(ref _streamInfoText, value); }

        // ------------------------------------------------------------------ recognition
        public string ClassifierName { get; }
        public bool ClassifierIsDemo { get; }
        private bool _classifierEnabled = true;
        public bool ClassifierEnabled { get => _classifierEnabled; set => this.RaiseAndSetIfChanged(ref _classifierEnabled, value); }
        public ObservableCollection<ClassLabelRow> ClassifierLabels { get; } = new();
        public ObservableCollection<string> DetectionLog { get; } = new();
        private string _bestLabel = "—";
        public string BestLabel { get => _bestLabel; private set => this.RaiseAndSetIfChanged(ref _bestLabel, value); }
        private string _lastLogged = "";

        // ------------------------------------------------------------------ commands
        public ICommand StartCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand ApplyKolParamsCommand { get; }
        public ICommand ReadKolParamsCommand { get; }
        public ICommand RefreshDevicesCommand { get; }
        public ICommand KolToIdleCommand { get; }
        public ICommand FocusCommand { get; }
        public ICommand ToggleControlsCommand { get; }
        public ICommand ClearDetectionsCommand { get; }

        private void OnMainPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(MainViewViewModel.SelectedLoggerDevice) or nameof(MainViewViewModel.IsDeviceConnected)
                or nameof(MainViewViewModel.KolUsbMode) or nameof(MainViewViewModel.IsKolSelected))
                Dispatcher.UIThread.Post(UpdateKolAvailability);
        }

        private LoggerDevice? Kol
        {
            get { LoggerDevice? d = _main?.SelectedLoggerDevice; return d?.IsKol == true && d.IsConnected ? d : null; }
        }

        private void UpdateKolAvailability()
        {
            LoggerDevice? k = Kol;
            IsKolAvailable = k != null;
            KolAvailabilityText = k == null
                ? "No KOL connected — connect a KOL on its USB console (Idle or Live Audio) to stream its 4 microphones."
                : $"KOL {k.SerialNumber} — {KolUsbModeInfo.Label(_main.KolUsbMode)}";
        }

        private void RefreshDevices()
        {
            if (IsRunning) return;
            AudioCaptureService.Rescan();
            var inputs = AudioCaptureService.ListInputs();
            var keep = SelectedPcDevice?.Name;
            PcDevices.Clear();
            foreach (var d in inputs.Where(d => !d.Name.Contains("KOL", StringComparison.OrdinalIgnoreCase))
                                     .GroupBy(d => d.Name).Select(g => g.OrderByDescending(x => x.HostApi).First()))
                PcDevices.Add(d);
            SelectedPcDevice = PcDevices.FirstOrDefault(d => d.Name == keep) ?? PcDevices.FirstOrDefault();
            this.RaisePropertyChanged(nameof(AudioBackendProblem));
        }

        private async Task StartAsync()
        {
            if (IsRunning) return;
            if (AudioCaptureService.UnavailableReason is string why) { StatusText = "Audio backend unavailable: " + why; return; }
            try
            {
                if (IsKolSource) await StartKolAsync();
                else StartPc();
            }
            catch (Exception ex) { StatusText = "Start failed: " + ex.Message; }
        }

        private async Task StartKolAsync()
        {
            LoggerDevice? dev = Kol;
            if (dev == null) { StatusText = "Connect a KOL first (Device console → Connect Device)."; return; }
            int rate = SelectedSampleRate;

            StatusText = "Sending microphone parameters…";
            bool ack = await dev.SetMicParamsAsync(CurrentKolParams);
            if (!ack) StatusText = "Firmware did not accept the microphone parameters (older firmware?) — continuing with the device's config.json values.";

            if (_main.KolUsbMode != Models.KolUsbMode.LiveAudio)
            {
                StatusText = "Switching the KOL to Live Audio (it re-enumerates as a USB microphone)…";
                await _main.SwitchKolUsbModeAsync(Models.KolUsbMode.LiveAudio);
                if (_main.KolUsbMode != Models.KolUsbMode.LiveAudio) { StatusText = "The KOL did not enter Live Audio. " + _main.KolStatusText; return; }
            }

            StatusText = "Waiting for the KOL microphone to appear…";
            IReadOnlyList<AudioInputDevice> cands = Array.Empty<AudioInputDevice>();
            for (int i = 0; i < 16 && cands.Count == 0; i++)
            {
                AudioCaptureService.Rescan();
                cands = AudioCaptureService.FindKolCandidates(rate);
                if (cands.Count == 0) await Task.Delay(500);
            }
            if (cands.Count == 0) { StatusText = "The KOL USB microphone did not appear on this computer."; return; }

            foreach (AudioInputDevice c in cands)
                if (_capture.Start(c, 4, rate)) { Started($"KOL {dev.SerialNumber} · 4 ch · {rate / 1000.0:0.#} kHz nominal · {c.Name}"); return; }
            StatusText = "Could not open the KOL microphone: " + (_capture.LastError ?? "unknown error") + ". On Windows make sure no other app holds it exclusively.";
        }

        private void StartPc()
        {
            AudioInputDevice? d = SelectedPcDevice;
            if (d == null) { StatusText = "No PC input device selected."; return; }
            int rate = (int)Math.Round(d.DefaultSampleRate > 0 ? d.DefaultSampleRate : 48000);
            int ch = Math.Min(PcChannels, d.MaxChannels);
            if (_capture.Start(d, ch, rate)) Started($"{d.Name} · {ch} ch · {rate / 1000.0:0.#} kHz");
            else StatusText = "Could not open the input: " + (_capture.LastError ?? "unknown error");
        }

        private void Started(string info)
        {
            RebuildProcessing();
            UpdateDisplayBuffer();
            IsRunning = true;
            StreamInfoText = info;
            StatusText = "Streaming.";
            _classifyTimer.Start();
        }

        private void Stop()
        {
            _classifyTimer.Stop();
            _capture.Stop();
            IsRunning = false;
            StatusText = "Stopped (display frozen on the last samples).";
        }

        private async Task ApplyKolParamsAsync()
        {
            LoggerDevice? dev = Kol;
            if (dev == null) { StatusText = "No KOL connected."; return; }
            bool ack = await dev.SetMicParamsAsync(CurrentKolParams);
            StatusText = ack ? $"Microphone parameters applied: {GainText}, HPF {HpfOptions[HpfIndex]}, reshape {(ReshapeEnabled ? (ReshapeRatioOne ? "/1" : "/4") : "off")}, {(Cic4 ? "SINC4" : "SINC5")}."
                             : "The device did not acknowledge the parameters (firmware without VESPER_SET_MIC_PARAMS?).";
        }

        private async Task ReadKolParamsAsync()
        {
            LoggerDevice? dev = Kol;
            if (dev == null) { StatusText = "No KOL connected."; return; }
            KolMicStatus? st = await dev.GetMicParamsAsync();
            if (st == null) { StatusText = "No reply to GET_MIC_PARAMS (older firmware?)."; return; }
            LoadKolParams(st.Params);
            StatusText = $"Read from device: {GainText}, HPF {HpfOptions[HpfIndex]}; stream {(st.Streaming ? "on" : "off")}, capture {(st.Capturing ? "on" : "off")}, fs {st.SampleRate} Hz{(st.Saturated ? ", SATURATING" : "")}.";
        }

        private async Task KolToIdleAsync()
        {
            if (IsRunning) Stop();
            if (Kol == null) return;
            await _main.SwitchKolUsbModeAsync(Models.KolUsbMode.Idle);
            StatusText = _main.KolStatusText;
        }

        // ------------------------------------------------------------------ classification
        private float[] _clsBuf = new float[0];

        private void ClassifyTick()
        {
            LiveAudioBuffer? buf = DisplayBuffer;
            if (!ClassifierEnabled || buf == null || Interlocked.Exchange(ref _classifyBusy, 1) == 1) return;
            int n = (int)(buf.SampleRate * Math.Max(0.5, _classifier.WindowSeconds));
            if (_clsBuf.Length != n) _clsBuf = new float[n];
            if (SelectedChannel < 0) buf.CopyLatestMix(_clsBuf); else buf.CopyLatest(Math.Min(SelectedChannel, buf.Channels - 1), _clsBuf);
            float[] window = _clsBuf; int fs = buf.SampleRate;
            Task.Run(() =>
            {
                SoundClassification result;
                try { result = _classifier.Classify(window, fs); } catch { result = SoundClassification.Empty; }
                Dispatcher.UIThread.Post(() =>
                {
                    try
                    {
                        var top = result.Top;
                        while (ClassifierLabels.Count > top.Count) ClassifierLabels.RemoveAt(ClassifierLabels.Count - 1);
                        for (int i = 0; i < top.Count; i++)
                        {
                            if (i >= ClassifierLabels.Count) ClassifierLabels.Add(new ClassLabelRow());
                            ClassifierLabels[i].Label = top[i].Label;
                            ClassifierLabels[i].Confidence = top[i].Confidence;
                        }
                        SoundLabel? best = result.Best;
                        BestLabel = best?.Label ?? "—";
                        if (best != null && best.Confidence >= 0.35 && best.Label != _lastLogged && best.Label != "Quiet" && best.Label != "Unclassified sound")
                        {
                            _lastLogged = best.Label;
                            DetectionLog.Insert(0, $"{DateTime.Now:HH:mm:ss}  {best.Label}  ({best.Confidence * 100:0} %)");
                            while (DetectionLog.Count > 12) DetectionLog.RemoveAt(DetectionLog.Count - 1);
                        }
                        else if (best == null || best.Label == "Quiet") _lastLogged = "";
                    }
                    finally { Interlocked.Exchange(ref _classifyBusy, 0); }
                });
            });
        }
    }
}
