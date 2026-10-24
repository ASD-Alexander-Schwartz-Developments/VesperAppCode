using System;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Serialization;
using VesperApp.Services;

namespace VesperApp.Models
{
    /// <summary>
    /// What the KOL and the Vesper / Pipistrelle (SPH0641) microphone drivers share: the automatic filter chain
    /// (<see cref="MicChainSolver"/>), the predefined sample rates, the Solve window, the legacy-config migration
    /// and sound-triggered recording (<see cref="MicTrigger"/>). The trigger reuses existing config.json fields: a
    /// non-zero start level makes a config sound-triggered (no separate mode switch), and the grid then shows the
    /// duty-cycle window fields as hold / pre-trigger instead.
    /// </summary>
    public abstract class MicDriverBase : ConfigurationDeviceDriver, IJsonOnDeserialized, IMicChainSolvable, IDynamicBrowsable
    {
        protected uint thresholdup, thresholddown;
        protected bool cic4;
        protected ushort digitalFilter, gain, hpf;

        protected MicDriverBase(string name, string description) : base(name, description)
        {
            FileSize = 8 * 1024 * 1024;
        }

        /// <summary>Product the driver configures (firmware version, DMA ring for the pre-trigger).</summary>
        [JsonIgnore, Browsable(false)]
        public abstract MicChainSolver.Product Product { get; }

        /// <summary>Microphones that record (storage estimate).</summary>
        [JsonIgnore, Browsable(false)]
        public abstract int MicCount { get; }

        /// <summary>Raised when rows appear or disappear (recording mode, detector, level type changed).</summary>
        public event EventHandler? LayoutChanged;

        public override void Load(ConfigurationDeviceDriver ldrv)
        {
            base.Load(ldrv);
            if (ldrv is MicDriverBase m)
            {
                thresholdup = m.thresholdup; thresholddown = m.thresholddown; cic4 = m.cic4;
                digitalFilter = m.digitalFilter; gain = m.gain; hpf = m.hpf; MigrationNote = m.MigrationNote;
            }
        }

        // ───────────────────────── config.json fields ─────────────────────────

        [JsonPropertyName("thresup"), JsonPropertyOrder(21), Browsable(false)]
        public uint ThresholdUp { get => thresholdup; set => thresholdup = Math.Min(value, 65535u); }

        [JsonPropertyName("thresdn"), JsonPropertyOrder(22), Browsable(false)]
        public uint ThresholdDown { get => thresholddown; set => thresholddown = Math.Min(value, 65535u); }

        [JsonPropertyName("cic4"), JsonPropertyOrder(23), Browsable(false)]   // chosen from the rate; kept for the JSON field
        public bool UseCic4Filter { get => cic4; set => cic4 = value; }

        [JsonPropertyName("filter"), JsonPropertyOrder(24), Browsable(false)]
        public ushort DFilter
        {
            /* always written in auto mode, with the reshape bits older firmware needs (MicChainSolver.FilterValue) */
            get => MicChainSolver.FilterValue(RefRate);
            set => digitalFilter = value;
        }

        [JsonPropertyName("hpf"), JsonPropertyOrder(25), Browsable(false)]
        public ushort HPF { get => hpf; set => hpf = value; }

        [JsonPropertyName("gain"), JsonPropertyOrder(26), Browsable(false)]
        public ushort GAIN { get => gain; set => gain = value; }

        // ───────────────────────── migration, Solve ─────────────────────────

        /// <summary>Set when an old config was converted on load; shown by the solver.</summary>
        [JsonIgnore, Browsable(false)]
        public string? MigrationNote { get; private set; }

        protected uint RefRate => MicChainSolver.RefRate(SampleRate[1], SampleRate[2], 0);

        /// <summary>Old configs: automatic chain at the same level (MicChainSolver.MigrateLegacy); raw thresholds
        /// from before the trigger existed were never implemented and are cleared.</summary>
        public void OnDeserialized()
        {
            bool legacy = (digitalFilter & MicChainSolver.AutoBit) == 0;   /* not written by this VesperApp */
            var m = MicChainSolver.MigrateLegacy(SampleRate[1], SampleRate[2], digitalFilter, cic4, gain);
            gain = m.Gain;
            SampleRate[1] = m.Rate1;
            SampleRate[2] = m.Rate2;
            MigrationNote = m.Note;
            digitalFilter |= MicChainSolver.AutoBit;
            cic4 = false;
            if (legacy && (thresholdup | thresholddown) != 0)
            {
                MigrationNote = (MigrationNote == null ? "" : MigrationNote + " ") +
                                $"The old raw threshold values ({thresholdup} / {thresholddown}) were never implemented in the firmware and were cleared; set up sound-triggered recording with the start level or the Recording helper.";
                thresholdup = thresholddown = 0;
            }
        }

        public MicChainSolver.SolverReport MicChainReport()
        {
            var report = MicChainSolver.BuildReport(SampleRate[1], SampleRate[2], gain, Product, MigrationNote, hpf);
            var t = Trigger;
            if (!t.On || RefRate == 0) return report;
            var sections = report.Sections.ToList();
            sections.Insert(Math.Max(0, sections.Count - 1), MicTrigger.SolveSection(t, SampleRate[1], SampleRate[2], gain, Product, MicCount));
            return report with { Sections = sections };
        }

        public void ApplyRecommendedGain()
        {
            if (RefRate == 0) return;
            gain = MicChainSolver.GainValue(MicChainSolver.Recommended(RefRate));
            OnPropertyChanged("Gain");
        }

        public void ApplyRecommendedHpf()
        {
            if (RefRate == 0) return;
            hpf = (ushort)MicChainSolver.RecommendedHpf(RefRate);
            OnPropertyChanged("HighPassFilter");
        }

        // ───────────────────────── sample rates ─────────────────────────

        [CategoryAttribute("Standard configuration"),
        DescriptionAttribute("Microphone sample rate while the schedule runs Config 1 (Off = microphones not used). The unit picks the best filter for it; press Solve for details."),
        DisplayName("Sample Rate [1]")]
        [JsonIgnore, Browsable(true), MicSolver]
        public KOLSampleRate Rate1
        {
            get => KOLSampleRate.CreateFromValue(SampleRate[1]);
            set { bool layout = (SampleRate[1] == 0) != (value.Value == 0); SampleRate[1] = value.Value; RecapTrigger(); OnPropertyChanged(); if (layout) LayoutChanged?.Invoke(this, EventArgs.Empty); }
        }

        [CategoryAttribute("Standard configuration"),
        DescriptionAttribute("Microphone sample rate while the schedule runs Config 2 (Off = microphones not used). The unit picks the best filter for it; press Solve for details."),
        DisplayName("Sample Rate [2]")]
        [JsonIgnore, Browsable(true), MicSolver]
        public KOLSampleRate Rate2
        {
            get => KOLSampleRate.CreateFromValue(SampleRate[2]);
            set { bool layout = (SampleRate[2] == 0) != (value.Value == 0); SampleRate[2] = value.Value; RecapTrigger(); OnPropertyChanged(); if (layout) LayoutChanged?.Invoke(this, EventArgs.Empty); }
        }

        [DisplayName("Sample Rate [1]"), JsonIgnore, Browsable(false)]   // edited as Rate1 (predefined rates)
        public override uint SampleRate1 { get => SampleRate[1]; set { SampleRate[1] = value; OnPropertyChanged(); } }

        [DisplayName("Sample Rate [2]"), JsonIgnore, Browsable(false)]   // edited as Rate2 (predefined rates)
        public override uint SampleRate2 { get => SampleRate[2]; set { SampleRate[2] = value; OnPropertyChanged(); } }

        // ───────────────────────── sound-triggered recording ─────────────────────────

        [JsonIgnore, Browsable(false)]
        public MicTrigger.Settings Trigger
        {
            get => MicTrigger.Read(bitmask, thresholdup, thresholddown, WindowLength, WindowRate);
            set
            {
                value = Fit(value);
                bool layout = value.On != Trigger.On || value.Hardware != Trigger.Hardware || value.Absolute != Trigger.Absolute;
                if (!value.On && Trigger.On)
                {
                    /* back to continuous: the window fields are duty-cycle windows again - none */
                    WindowLength[1] = WindowLength[2] = WindowRate[1] = WindowRate[2] = 0;
                }
                (bitmask, thresholdup, thresholddown) = MicTrigger.Write(value, bitmask, WindowLength, WindowRate);
                OnPropertyChanged(nameof(Trigger));
                if (layout) LayoutChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>What this product can do: no hardware detector where the board lacks it, and a pre-trigger the
        /// DMA ring holds at each config's rate.</summary>
        private MicTrigger.Settings Fit(MicTrigger.Settings s) => !s.On ? s : s with
        {
            Hardware = s.Hardware && Product.HasHardwareDetector,
            Pre1 = MicTrigger.CapPreTrigger(Product, SampleRate[1], s.Pre1),
            Pre2 = MicTrigger.CapPreTrigger(Product, SampleRate[2], s.Pre2),
        };

        /// <summary>A rate change can shorten the pre-trigger the ring holds.</summary>
        private void RecapTrigger()
        {
            if (Trigger is { On: true } t && Fit(t) != t)
            {
                Trigger = t;
                OnPropertyChanged(nameof(PreTrigger1));
                OnPropertyChanged(nameof(PreTrigger2));
            }
        }

        [CategoryAttribute("Sound trigger"), DisplayName("Start level"),
        DescriptionAttribute("Off records continuously. Any level makes the recording sound-triggered: a sound this much louder than the background noise starts a recording. Press Helper to set up rate, filter, gain and trigger for what you are recording in one place.")]
        [JsonIgnore, Browsable(true), MicHelper]
        public RelativeLevelChoice StartLevelRelative
        {
            get => RelativeLevelChoice.From(Trigger.On ? Trigger.StartDb : 0);
            set { SetStartLevel(value.Value); OnPropertyChanged(); }
        }

        [CategoryAttribute("Sound trigger"), DisplayName("Start level"),
        DescriptionAttribute("The sound level that starts a recording (dB SPL at the microphone): quiet forest at night ≈ 30, suburb ≈ 45, near a road ≈ 60. Off records continuously.")]
        [JsonIgnore, Browsable(true), MicHelper]
        public AbsoluteLevelChoice StartLevelAbsolute
        {
            get => AbsoluteLevelChoice.From(Trigger.On ? Trigger.StartDb : 0);
            set { SetStartLevel(value.Value); OnPropertyChanged(); }
        }

        /// <summary>The start level is the switch: 0 = continuous, any level = sound-triggered.</summary>
        private void SetStartLevel(int db)
        {
            var t = Trigger;
            if (db <= 0) Trigger = MicTrigger.Settings.Default;
            else Trigger = t.On ? t with { StartDb = db } : MicTrigger.Settings.Default with { On = true, StartDb = db, Absolute = t.Absolute };
        }

        [CategoryAttribute("Sound trigger"), DisplayName("Stop level"),
        DescriptionAttribute("A recording ends after the sound has dropped this far below the start level for the hold time. A gap avoids rapid start/stop on sounds hovering at the start level.")]
        [JsonIgnore, Browsable(true)]
        public HysteresisChoice StopLevel
        {
            get => HysteresisChoice.From(Trigger.HysteresisDb);
            set { Trigger = Trigger with { HysteresisDb = value.Value }; OnPropertyChanged(); }
        }

        [CategoryAttribute("Sound trigger"), DisplayName("Trigger level type"),
        DescriptionAttribute("Above the background: the unit keeps tracking the ambient noise, robust when wind or insects change it (recommended). Absolute: a fixed sound level in dB SPL.")]
        [JsonIgnore, Browsable(true)]
        public LevelTypeChoice LevelType
        {
            get => LevelTypeChoice.From(Trigger.Absolute ? LevelTypeChoice.Absolute : LevelTypeChoice.AboveBackground);
            set
            {
                bool abs = value.Value == LevelTypeChoice.Absolute;
                if (abs == Trigger.Absolute) return;
                int moderate = MicTrigger.Sites[1].BackgroundDbSpl;
                int start = abs ? Trigger.StartDb + moderate : Math.Max(3, Trigger.StartDb - moderate);
                Trigger = Trigger with { Absolute = abs, StartDb = start };
                OnPropertyChanged();
            }
        }

        [CategoryAttribute("Sound trigger"), DisplayName("Trigger detector"),
        DescriptionAttribute("Software: pre-trigger and the ultrasound-only option, more power. Hardware: the unit sleeps until a sound arrives — longest battery life, no pre-trigger, no ultrasound-only option.")]
        [JsonIgnore, Browsable(true)]
        public DetectorChoice Detector
        {
            get => DetectorChoice.From(Trigger.Hardware ? DetectorChoice.Hardware : DetectorChoice.Software);
            set { Trigger = Trigger with { Hardware = value.Value == DetectorChoice.Hardware }; OnPropertyChanged(); }
        }

        [CategoryAttribute("Sound trigger"), DisplayName("Ultrasound only"),
        DescriptionAttribute("Only sound above 15 kHz can start a recording: bats and insects trigger, wind, rain and birdsong do not (software detector).")]
        [JsonIgnore, Browsable(true)]
        public bool UltrasoundOnly
        {
            get => Trigger.Ultrasound;
            set { Trigger = Trigger with { Ultrasound = value }; OnPropertyChanged(); }
        }

        [CategoryAttribute("Standard configuration"), DisplayName("Hold after sound [1]"),
        DescriptionAttribute("Config 1: keep recording this long after the sound has dropped below the stop level, so calls and songs are not cut into pieces.")]
        [JsonIgnore, Browsable(true)]
        public HoldChoice HoldAfterSound1
        {
            get => HoldChoice.From(Trigger.Hold1);
            set { Trigger = Trigger with { Hold1 = (uint)value.Value }; OnPropertyChanged(); }
        }

        [CategoryAttribute("Standard configuration"), DisplayName("Hold after sound [2]"),
        DescriptionAttribute("Config 2: keep recording this long after the sound has dropped below the stop level.")]
        [JsonIgnore, Browsable(true)]
        public HoldChoice HoldAfterSound2
        {
            get => HoldChoice.From(Trigger.Hold2);
            set { Trigger = Trigger with { Hold2 = (uint)value.Value }; OnPropertyChanged(); }
        }

        [CategoryAttribute("Standard configuration"), DisplayName("Pre-trigger [1]"),
        DescriptionAttribute("Config 1: also keep the moments just before the trigger, so the start of a call is not lost (software detector; limited by the unit's audio buffer at high sample rates).")]
        [JsonIgnore, Browsable(true)]
        public PreTriggerChoice PreTrigger1
        {
            get => PreTriggerChoice.From(Trigger.Pre1);
            set { Trigger = Trigger with { Pre1 = (uint)value.Value }; OnPropertyChanged(); }
        }

        [CategoryAttribute("Standard configuration"), DisplayName("Pre-trigger [2]"),
        DescriptionAttribute("Config 2: also keep the moments just before the trigger (software detector).")]
        [JsonIgnore, Browsable(true)]
        public PreTriggerChoice PreTrigger2
        {
            get => PreTriggerChoice.From(Trigger.Pre2);
            set { Trigger = Trigger with { Pre2 = (uint)value.Value }; OnPropertyChanged(); }
        }

        /// <summary>Rows shown for the current recording mode: the window fields double as hold / pre-trigger.</summary>
        public bool IsPropertyVisible(string name)
        {
            var t = Trigger;
            return name switch
            {
                nameof(WindowLength1) or nameof(WindowLength2) or nameof(WindowRate1) or nameof(WindowRate2) => !t.On,
                nameof(Detector) => t.On && Product.HasHardwareDetector,
                nameof(LevelType) or nameof(StopLevel) => t.On,
                nameof(HoldAfterSound1) => t.On && SampleRate[1] != 0,
                nameof(HoldAfterSound2) => t.On && SampleRate[2] != 0,
                nameof(StartLevelRelative) => !t.On || !t.Absolute,
                nameof(StartLevelAbsolute) => t.On && t.Absolute,
                nameof(UltrasoundOnly) => t.On && !t.Hardware,
                nameof(PreTrigger1) => t.On && !t.Hardware && SampleRate[1] != 0,
                nameof(PreTrigger2) => t.On && !t.Hardware && SampleRate[2] != 0,
                _ => true,
            };
        }

        /// <summary>Apply the Recording helper's choices: rates, trigger, and the recommended gain and high-pass.</summary>
        public void ApplyHelper(uint rate, bool config1, bool config2, MicTrigger.Settings settings)
        {
            if (config1) SampleRate[1] = rate;
            if (config2) SampleRate[2] = rate;
            var s = settings;
            if (!config1) s = s with { Hold1 = Trigger.Hold1, Pre1 = Trigger.Pre1 };
            if (!config2) s = s with { Hold2 = Trigger.Hold2, Pre2 = Trigger.Pre2 };
            Trigger = s;
            ApplyRecommendedGain();
            ApplyRecommendedHpf();
            OnPropertyChanged(nameof(Rate1));
            OnPropertyChanged(nameof(Rate2));
            OnPropertyChanged(nameof(StartLevelRelative));
            OnPropertyChanged(nameof(StartLevelAbsolute));
            LayoutChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
