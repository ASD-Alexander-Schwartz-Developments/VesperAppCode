using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using System.Linq;
using VesperApp.Models;
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

    // Mirror the (multi-)selection into the view model so the context-menu actions
    // (Decode / Parse / Delete …) operate on everything that is selected.
    private void OnDataSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is RecordingParsingViewModel vm && sender is TreeView tree)
            vm.SetSelection(tree.SelectedItems?.OfType<RecordingDataNode>().ToList()
                            ?? new System.Collections.Generic.List<RecordingDataNode>());
    }
}
