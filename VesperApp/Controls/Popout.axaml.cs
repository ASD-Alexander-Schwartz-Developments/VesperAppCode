using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace VesperApp.Controls {
    public partial class Popout: IconButton {
        void InitializeComponent() => AvaloniaXamlLoader.Load(this);

        protected override IBrush Fill {
            get => (IBrush)this.Resources["Brush"];
            set => this.Resources["Brush"] = value;
        }

        public Popout() {
            InitializeComponent();

            base.MouseLeave(this, null);
        }
    }
}
