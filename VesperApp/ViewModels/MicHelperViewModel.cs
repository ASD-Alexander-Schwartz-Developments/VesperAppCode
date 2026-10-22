using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ReactiveUI;
using VesperApp.Models;
using VesperApp.Services;

namespace VesperApp.ViewModels
{
    /// <summary>
    /// The Recording helper: what you record (preset), how noisy the site is, the sample rate and which configs, and
    /// the trigger — with a plain-language summary. Nothing changes on the driver until Apply
    /// (<see cref="MicDriverBase.ApplyHelper"/>, which also applies the recommended gain and high-pass).
    /// </summary>
    public class MicHelperViewModel : ViewModelBase
    {
        private readonly MicDriverBase _driver;

        public sealed class PresetItem : ReactiveObject
        {
            public PresetItem(MicTrigger.Preset? p) { Preset = p; }
            public MicTrigger.Preset? Preset { get; }
            public string Name => Preset?.Name ?? "Custom";
            public string Description => Preset?.Description ?? "Keep the current rate and set the trigger by hand below.";
        }

        public sealed class SiteItem
        {
            public SiteItem(MicTrigger.Site s) { Site = s; }
            public MicTrigger.Site Site { get; }
            public override string ToString() => $"{Site.Name} — about {Site.BackgroundDbSpl} dB SPL ({Site.Example})";
        }

        public sealed class RateItem
        {
            public RateItem(uint r) { Rate = r; }
            public uint Rate { get; }
            public override string ToString() => KOLSampleRate.CreateFromValue(Rate).ToString();
        }

        public sealed class TargetItem
        {
            public TargetItem(string text, bool c1, bool c2) { Text = text; Config1 = c1; Config2 = c2; }
            public string Text { get; }
            public bool Config1 { get; }
            public bool Config2 { get; }
            public override string ToString() => Text;
        }

        public MicHelperViewModel(MicDriverBase driver)
        {
            _driver = driver;
            Presets = MicTrigger.Presets.Select(p => new PresetItem(p)).Append(new PresetItem(null)).ToList();
            Sites = MicTrigger.Sites.Select(s => new SiteItem(s)).ToList();
            Rates = MicChainSolver.CommonRates.Where(r => r != 0).Select(r => new RateItem(r)).ToList();
            Targets = new List<TargetItem>
            {
                new("Config 1 (Sample Rate [1])", true, false),
                new("Config 2 (Sample Rate [2])", false, true),
                new("Both configs", true, true),
            };

            var t = driver.Trigger;
            _site = Sites[1];
            _target = Targets[0];
            _rate = Rates.FirstOrDefault(r => r.Rate == driver.SampleRate[1]) ?? Rates.First(r => r.Rate == 48000);
            _hardware = t.Hardware && driver.Product.HasHardwareDetector;
            _absolute = t.Absolute;
            _ultrasound = t.Ultrasound;
            _relative = RelativeLevelChoice.From(!t.On ? 0 : t.Absolute ? 12 : t.StartDb);
            _absoluteLevel = AbsoluteLevelChoice.From(!t.On ? 0 : t.Absolute ? t.StartDb : 57);
            _stop = HysteresisChoice.From(t.HysteresisDb);
            _hold = HoldChoice.From(t.Hold1);
            _pre = PreTriggerChoice.From(t.Pre1);
            _preset = Presets[^1];   // Custom: start from what the driver has
        }

        public IReadOnlyList<PresetItem> Presets { get; }
        public IReadOnlyList<SiteItem> Sites { get; }
        public IReadOnlyList<RateItem> Rates { get; }
        public IReadOnlyList<TargetItem> Targets { get; }
        public IReadOnlyList<RelativeLevelChoice> RelativeLevels => RelativeLevelChoice.ListOfOptions;
        public IReadOnlyList<AbsoluteLevelChoice> AbsoluteLevels => AbsoluteLevelChoice.ListOfOptions;
        public IReadOnlyList<HysteresisChoice> StopLevels => HysteresisChoice.ListOfOptions;
        public IReadOnlyList<HoldChoice> Holds => HoldChoice.ListOfOptions;
        public IReadOnlyList<PreTriggerChoice> PreTriggers => PreTriggerChoice.ListOfOptions;

        private PresetItem _preset;
        public PresetItem SelectedPreset
        {
            get => _preset;
            set
            {
                this.RaiseAndSetIfChanged(ref _preset, value);
                if (value?.Preset is { } p)
                {
                    SelectedRate = Rates.FirstOrDefault(r => r.Rate == p.Rate) ?? SelectedRate;
                    Ultrasound = p.Ultrasound;
                    Hardware = false;
                    Absolute = false;
                    SelectedRelative = RelativeLevelChoice.From(p.Triggered ? p.StartDb : 0);
                    SelectedHold = HoldChoice.From(p.HoldMs);
                    SelectedPre = PreTriggerChoice.From(p.PreMs);
                }
                Changed();
            }
        }

        private SiteItem _site;
        public SiteItem SelectedSite { get => _site; set { this.RaiseAndSetIfChanged(ref _site, value); Changed(); } }

        private RateItem _rate;
        public RateItem SelectedRate { get => _rate; set { this.RaiseAndSetIfChanged(ref _rate, value); Changed(); } }

        private TargetItem _target;
        public TargetItem SelectedTarget { get => _target; set { this.RaiseAndSetIfChanged(ref _target, value); Changed(); } }

        /// <summary>Sound-triggered when a start level is chosen ("Off" = continuous) - there is no separate mode.</summary>
        public bool Triggered => Absolute ? (SelectedAbsolute?.Value ?? 0) != 0 : (SelectedRelative?.Value ?? 0) != 0;

        /// <summary>False on the current KOL / Vesper boards (the sound-activity detector is not wired to the mics).</summary>
        public bool HardwareAvailable => _driver.Product.HasHardwareDetector;

        private bool _hardware;
        public bool Hardware
        {
            get => _hardware;
            set { this.RaiseAndSetIfChanged(ref _hardware, value && HardwareAvailable); this.RaisePropertyChanged(nameof(Software)); Changed(); }
        }
        public bool Software { get => !_hardware; set => Hardware = !value; }

        private bool _absolute;
        public bool Absolute
        {
            get => _absolute;
            set
            {
                bool on = Triggered;
                this.RaiseAndSetIfChanged(ref _absolute, value);
                this.RaisePropertyChanged(nameof(Relative));
                /* keep on/off when switching the level type */
                if (value && on && SelectedAbsolute.Value == 0) SelectedAbsolute = AbsoluteLevelChoice.From(57);
                if (!value && on && SelectedRelative.Value == 0) SelectedRelative = RelativeLevelChoice.From(12);
                if (!on) { SelectedAbsolute = AbsoluteLevelChoice.From(0); SelectedRelative = RelativeLevelChoice.From(0); }
                Changed();
            }
        }
        public bool Relative { get => !_absolute; set => Absolute = !value; }

        private bool _ultrasound;
        public bool Ultrasound { get => _ultrasound; set { this.RaiseAndSetIfChanged(ref _ultrasound, value); Changed(); } }

        private RelativeLevelChoice _relative;
        public RelativeLevelChoice SelectedRelative { get => _relative; set { this.RaiseAndSetIfChanged(ref _relative, value); Changed(); } }

        private AbsoluteLevelChoice _absoluteLevel;
        public AbsoluteLevelChoice SelectedAbsolute { get => _absoluteLevel; set { this.RaiseAndSetIfChanged(ref _absoluteLevel, value); Changed(); } }

        private HysteresisChoice _stop;
        public HysteresisChoice SelectedStop { get => _stop; set { this.RaiseAndSetIfChanged(ref _stop, value); Changed(); } }

        private HoldChoice _hold;
        public HoldChoice SelectedHold { get => _hold; set { this.RaiseAndSetIfChanged(ref _hold, value); Changed(); } }

        private PreTriggerChoice _pre;
        public PreTriggerChoice SelectedPre { get => _pre; set { this.RaiseAndSetIfChanged(ref _pre, value); Changed(); } }

        /// <summary>The settings as they would be applied.</summary>
        public MicTrigger.Settings Settings
        {
            get
            {
                uint pre = (uint)(SelectedPre?.Value ?? 0), hold = (uint)(SelectedHold?.Value ?? 2000);
                return new MicTrigger.Settings(Triggered, Hardware, Absolute, Ultrasound && !Hardware,
                    Absolute ? SelectedAbsolute?.Value ?? 57 : SelectedRelative?.Value ?? 12, SelectedStop?.Value ?? 0,
                    hold, pre, hold, pre);
            }
        }

        public string Summary
        {
            get
            {
                /* the rates as they will be after Apply */
                uint r1 = SelectedTarget.Config1 ? SelectedRate.Rate : _driver.SampleRate[1];
                uint r2 = SelectedTarget.Config2 ? SelectedRate.Rate : _driver.SampleRate[2];
                return MicTrigger.Summary(Settings, r1, r2, _driver.Product, _driver.MicCount, SelectedSite.Site);
            }
        }

        public string PreTriggerNote
        {
            get
            {
                uint max = MicTrigger.PreTriggerMaxMs(_driver.Product, SelectedRate.Rate);
                return max < MicTrigger.MaxPreTriggerMs
                    ? string.Create(CultureInfo.InvariantCulture, $"At {MicChainSolver.Hz(SelectedRate.Rate)} the unit's audio buffer holds up to {max} ms before the trigger.")
                    : "";
            }
        }

        public string FirmwareNote => MicTrigger.FirmwareNote(_driver.Product);
        public string ApplyNote => "Apply also sets the recommended gain and high-pass filter for the chosen rate (see Solve).";

        private void Changed()
        {
            this.RaisePropertyChanged(nameof(Triggered));
            this.RaisePropertyChanged(nameof(Summary));
            this.RaisePropertyChanged(nameof(PreTriggerNote));
        }

        public void Apply() => _driver.ApplyHelper(SelectedRate.Rate, SelectedTarget.Config1, SelectedTarget.Config2, Settings);
    }
}
