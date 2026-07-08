using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace VesperApp.Controls
{
    public partial class PluginUpdates : UserControl
    {
        public PluginUpdates()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
