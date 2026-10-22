using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using VesperApp.ViewModels;
using VesperApp.Views;

namespace VesperApp.Controls
{
    /// <summary>
    /// Displays and edits device-driver properties in a grid. The editor for each
    /// row (ComboBox / NumberBox / CheckBox / TextBox) is chosen entirely from the
    /// <see cref="VesperApp.ViewModels.DriverPropertyViewModel"/> metadata, so no
    /// per-type code lives here — adding a new sensor option type needs no change to
    /// this control. Interactions should occur on the UI thread.
    /// </summary>
    public partial class DeviceDriverPropertyGrid : UserControl
    {
        public DeviceDriverPropertyGrid()
        {
            InitializeComponent();
            /* The microphone "Solve" buttons live in cell templates; one routed handler opens the
               Solve window as a modal over the editor (a flyout was clipped by the grid). */
            AddHandler(Button.ClickEvent, OnButtonClick);
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private async void OnButtonClick(object? sender, RoutedEventArgs e)
        {
            if (e.Source is not Button { Tag: "micSolve" } b || b.DataContext is not DriverPropertyViewModel { Solvable: { } driver })
                return;
            e.Handled = true;
            var window = new MicChainSolverWindow { DataContext = new MicChainSolverViewModel(driver) };
            if (TopLevel.GetTopLevel(this) is Window owner)
                await window.ShowDialog(owner);
            else
                window.Show();
        }
    }
}
