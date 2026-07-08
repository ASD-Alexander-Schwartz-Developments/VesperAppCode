using Avalonia.Data.Converters;

namespace VesperApp.Services
{
    /// <summary>Small value converters for the Decoding Progress panel.</summary>
    public static class DecodeJobConverters
    {
        /// <summary>Bool IsExpanded → button label ("Hide output" / "Show output").</summary>
        public static readonly IValueConverter ExpandLabel =
            new FuncValueConverter<bool, string>(expanded => expanded ? "Hide output" : "Show output");
    }
}
