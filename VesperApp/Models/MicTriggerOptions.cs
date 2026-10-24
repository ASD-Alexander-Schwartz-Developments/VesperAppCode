using System.Globalization;
using System.Linq;
using VesperApp.Services;

namespace VesperApp.Models
{
    /// <summary>A labelled integer choice for the driver grid's ComboBox (matched on <see cref="Value"/>).</summary>
    public abstract class MicChoice
    {
        protected MicChoice(int value, string text) { Value = value; Text = text; }
        public int Value { get; }
        public string Text { get; }
        public override string ToString() => Text;
    }

    public sealed class DetectorChoice : MicChoice
    {
        public const int Software = 0, Hardware = 1;
        private DetectorChoice(int v, string t) : base(v, t) { }
        public static DetectorChoice[] ListOfOptions { get; } =
        {
            new(Software, "Software — pre-trigger, ultrasound option"),
            new(Hardware, "Hardware — longest battery life"),
        };
        public static DetectorChoice From(int v) => ListOfOptions.First(o => o.Value == v);
    }

    public sealed class LevelTypeChoice : MicChoice
    {
        public const int AboveBackground = 0, Absolute = 1;
        private LevelTypeChoice(int v, string t) : base(v, t) { }
        public static LevelTypeChoice[] ListOfOptions { get; } =
        {
            new(AboveBackground, "dB above the background noise"),
            new(Absolute, "Absolute level (dB SPL)"),
        };
        public static LevelTypeChoice From(int v) => ListOfOptions.First(o => o.Value == v);
    }

    public sealed class RelativeLevelChoice : MicChoice
    {
        public const string OffText = "Off — record continuously";
        private RelativeLevelChoice(int v) : base(v, v == 0 ? OffText : $"{v} dB above background") { }
        public static RelativeLevelChoice[] ListOfOptions { get; } =
            new[] { new RelativeLevelChoice(0) }.Concat(MicTrigger.RelativeLevelsDb.Select(v => new RelativeLevelChoice(v))).ToArray();
        public static RelativeLevelChoice From(int v) => v <= 0 ? ListOfOptions[0] : ListOfOptions.Skip(1).OrderBy(o => System.Math.Abs(o.Value - v)).First();
    }

    public sealed class AbsoluteLevelChoice : MicChoice
    {
        private AbsoluteLevelChoice(int v) : base(v, v == 0 ? RelativeLevelChoice.OffText : $"{v} dB SPL") { }
        public static AbsoluteLevelChoice[] ListOfOptions { get; } =
            new[] { new AbsoluteLevelChoice(0) }.Concat(MicTrigger.AbsoluteLevelsDb.Select(v => new AbsoluteLevelChoice(v))).ToArray();
        public static AbsoluteLevelChoice From(int v) => v <= 0 ? ListOfOptions[0] : ListOfOptions.Skip(1).OrderBy(o => System.Math.Abs(o.Value - v)).First();
    }

    public sealed class HysteresisChoice : MicChoice
    {
        private HysteresisChoice(int v) : base(v, v == 0 ? "3 dB below the start level (default)" : $"{v} dB below the start level") { }
        public static HysteresisChoice[] ListOfOptions { get; } = MicTrigger.HysteresisDb.Select(v => new HysteresisChoice(v)).ToArray();
        public static HysteresisChoice From(int v) => ListOfOptions.FirstOrDefault(o => o.Value == v) ?? ListOfOptions[0];
    }

    public sealed class HoldChoice : MicChoice
    {
        private HoldChoice(int v) : base(v, string.Create(CultureInfo.InvariantCulture, $"{v / 1000.0:0.#} s")) { }
        public static HoldChoice[] ListOfOptions { get; } = MicTrigger.HoldMs.Select(v => new HoldChoice((int)v)).ToArray();
        public static HoldChoice From(uint v) => ListOfOptions.OrderBy(o => System.Math.Abs(o.Value - (long)v)).First();
    }

    public sealed class PreTriggerChoice : MicChoice
    {
        private PreTriggerChoice(int v) : base(v, v == 0 ? "None" : $"{v} ms") { }
        public static PreTriggerChoice[] ListOfOptions { get; } = MicTrigger.PreTriggerMs.Select(v => new PreTriggerChoice((int)v)).ToArray();
        public static PreTriggerChoice From(uint v) => ListOfOptions.OrderBy(o => System.Math.Abs(o.Value - (long)v)).First();
    }
}
