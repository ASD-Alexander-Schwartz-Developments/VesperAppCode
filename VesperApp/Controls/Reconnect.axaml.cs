using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace VesperApp.Controls {
    public partial class Reconnect: IconButton {
        void InitializeComponent() => AvaloniaXamlLoader.Load(this);

        protected override IBrush Fill {
            get => (IBrush)this.Resources["Brush"];
            set => this.Resources["Brush"] = value;
        }

        public Reconnect() {
            InitializeComponent();

            base.MouseLeave(this, null);
        }
    }
}
