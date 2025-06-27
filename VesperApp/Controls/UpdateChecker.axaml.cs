using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace VesperApp.Controls
{
    public partial class UpdateChecker : UserControl
    {
        public UpdateChecker()
        {
            InitializeComponent();
        }
        
        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}