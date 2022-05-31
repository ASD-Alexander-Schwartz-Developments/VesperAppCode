using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace VesperApp.Controls
{
    public partial class SelectDeviceDrivers : UserControl
    {
        public SelectDeviceDrivers()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
