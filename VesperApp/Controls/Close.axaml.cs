using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;


namespace VesperApp.Controls {
    public partial class Close: IconButton {
        void InitializeComponent() {
            AvaloniaXamlLoader.Load(this);

            path = this.Get<Path>("Path");
        }

        public new delegate void ClickedEventHandler();
        public new event ClickedEventHandler Clicked;

        public delegate Task ForceEventHandler(bool force);
        public event ForceEventHandler ClickedWithForce;

        Path path;

        protected override IBrush Fill {
            get => path.Stroke;
            set => path.Stroke = value;
        }

        public Close() {
            InitializeComponent();

            base.MouseLeave(this, null);
        }

        protected override void Unloaded(object sender, VisualTreeAttachmentEventArgs e) {
            base.Unloaded(sender, e);
            Clicked = null;
        }

        protected override void Click(PointerReleasedEventArgs e) {
            Clicked?.Invoke();
            ClickedWithForce?.Invoke(e.KeyModifiers == KeyModifiers.Control);
        }
    }
}
