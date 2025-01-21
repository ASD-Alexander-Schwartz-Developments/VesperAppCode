using Avalonia.Controls;

namespace VesperApp.Services;

public static class ClipboardService
{
    public static TopLevel Owner { get; set; }

    public static System.Threading.Tasks.Task SetTextAsync(string text) =>
        Owner.Clipboard.SetTextAsync(text);
}
