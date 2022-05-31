using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace VesperApp.Controls
{
    public partial class Minimize: IconButton {
        void InitializeComponent() {
            AvaloniaXamlLoader.Load(this);

            path = this.Get<Path>("Path");
        }

        Path path;

        protected override IBrush Fill {
            get => path.Stroke;
            set => path.Stroke = value;
        }

        public Minimize() {
            InitializeComponent();

            base.MouseLeave(this, null);
        }
    }
}
