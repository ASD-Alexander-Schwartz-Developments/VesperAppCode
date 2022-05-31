using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace VesperApp.Controls {
    public partial class ProjectButton: IconButton {
        void InitializeComponent() {
            AvaloniaXamlLoader.Load(this);

            path = this.Get<Path>("Path");
        }

        Path path;

        protected override IBrush Fill {
            get => path.Fill;
            set => path.Fill = value;
        }

        public ProjectButton() {
            InitializeComponent();

            base.MouseLeave(this, null);
        }

        //protected override void Click(PointerReleasedEventArgs e) => ProjectWindow.Create((Window)this.GetVisualRoot());
    }
}
