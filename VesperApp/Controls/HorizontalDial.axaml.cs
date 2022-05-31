using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Markup.Xaml;

namespace VesperApp.Controls {
    public partial class HorizontalDial: Dial {
        protected override void InitializeComponent() {
            AvaloniaXamlLoader.Load(this);

            arcCanvas = this.Get<Canvas>("ArcCanvas");
            arcBase = this.Get<Path>("ArcBase");
            arc = this.Get<Path>("Arc");

            display = this.Get<TextBlock>("Display");
            titleText = this.Get<TextBlock>("DialTitle");

            input = this.Get<TextBox>("Input");
        }

        public HorizontalDial() {}
    }
}
