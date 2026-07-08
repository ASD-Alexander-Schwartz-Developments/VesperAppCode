using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
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

        // Registered in code with handledEventsToo so right-click works even though the
        // themed TreeViewItem marks pointer events handled before they bubble to the tree.
        dataTree.AddHandler(PointerPressedEvent, OnTreePointerPressed,
            RoutingStrategies.Tunnel, handledEventsToo: true);
        dataTree.AddHandler(PointerReleasedEvent, OnTreePointerReleased,
            RoutingStrategies.Tunnel, handledEventsToo: true);

    }

    // Double-click: folders expand/collapse (TreeViewItem's own behaviour); files are
    // activated — recognised configuration .json offers the editor, others OS-open.
    private void OnDataDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is RecordingParsingViewModel vm && vm.ActivateSelectedCommand?.CanExecute(null) == true)
            vm.ActivateSelectedCommand.Execute(null);
    }

    // Right-click selects the row under the pointer (unless it is already part of the
    // selection), so the context menu always acts on what was clicked.
    private void OnTreePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not TreeView tree) return;
        if (!e.GetCurrentPoint(tree).Properties.IsRightButtonPressed) return;

        if ((e.Source as Control)?.DataContext is RecordingDataNode node &&
            tree.SelectedItems?.Contains(node) != true)
        {
            tree.SelectedItems?.Clear();
            tree.SelectedItem = node;
        }
    }

    // Open the context menu explicitly at the pointer on right-button release: the
    // themed TreeViewItem swallows the native context request, so don't rely on it.
    // Show the context flyout at the pointer on right-button release. Done explicitly
    // (not via the native context-request) because the themed TreeViewItem swallows
    // the pointer events before Avalonia's own context-flyout trigger sees them.
    private void OnTreePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton != MouseButton.Right) return;
        if (dataTree.ContextFlyout is not Avalonia.Controls.Primitives.PopupFlyoutBase flyout) return;

        e.Handled = true;
        Avalonia.Threading.Dispatcher.UIThread.Post(
            () => flyout.ShowAt(dataTree, showAtPointer: true),
            Avalonia.Threading.DispatcherPriority.Background);
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
