using Avalonia.Controls;
using Avalonia.Markup.Xaml;

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
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
