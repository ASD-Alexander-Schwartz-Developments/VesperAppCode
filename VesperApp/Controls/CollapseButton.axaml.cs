using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace VesperApp.Controls {
    public partial class CollapseButton: IconButton {
        void InitializeComponent() {
            AvaloniaXamlLoader.Load(this);

            path = this.Get<Path>("Path");
            rotation = (RotateTransform)this.Get<LayoutTransformControl>("Layout").LayoutTransform;
        }

        Path path;
        RotateTransform rotation;

        protected override IBrush Fill {
            get => path.Stroke;
            set => path.Stroke = value;
        }

        public bool Showing {
            get => rotation.Angle == 180;
            set {
                rotation.Angle = value? 180 : 0;
                base.MouseLeave(this, null);
            }
        }

        public CollapseButton() {
            InitializeComponent();

            base.MouseLeave(this, null);
        }
    }
}
