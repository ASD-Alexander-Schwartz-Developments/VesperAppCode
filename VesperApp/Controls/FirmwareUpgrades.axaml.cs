using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using VesperApp.Services;
using VesperApp.ViewModels;

namespace VesperApp.Controls;

public partial class FirmwareUpgrades : UserControl
{
    public FirmwareUpgrades()
    {
        InitializeComponent();

        // Belt-and-braces selection sync: the SelectedItem TwoWay binding has a history
        // of not reaching the view model on this DataGrid (Flash then claims nothing is
        // selected), so mirror the selection explicitly.
        listReleasesList.SelectionChanged += (_, _) =>
        {
            if (DataContext is FirmwareUpgradesViewModel vm)
                vm.SelectedFirmwareRelease = listReleasesList.SelectedItem as ReleaseEntry;
        };
    }
}