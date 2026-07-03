using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using VesperApp.ViewModels;

namespace VesperApp.Controls;

public partial class RecordingsParsing : UserControl
{
    public RecordingsParsing()
    {
        InitializeComponent();
    }

    // Double-click a file node in the data browser to open it with the OS default app.
    private void OnDataDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is RecordingParsingViewModel vm && vm.OpenSelectedCommand?.CanExecute(null) == true)
            vm.OpenSelectedCommand.Execute(null);
    }
}
