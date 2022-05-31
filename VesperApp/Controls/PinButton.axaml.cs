using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.VisualTree;


namespace VesperApp.Controls {
    public partial class PinButton: IconButton {
        void InitializeComponent() {
            AvaloniaXamlLoader.Load(this);

            pathUnpinned = this.Get<Path>("PathUnpinned");
            pathPinned = this.Get<Path>("PathPinned");
        }

        Path pathUnpinned, pathPinned;
        Path CurrentPath => /*Preferences.AlwaysOnTop? PathPinned : */pathUnpinned;

        protected override IBrush Fill {
            get => CurrentPath.Stroke;
            set => pathUnpinned.Fill = pathUnpinned.Stroke = pathPinned.Fill = pathPinned.Stroke = value;
        }

        void UpdateTopmost(bool value) {
            (value? pathUnpinned : pathPinned).Opacity = 0;
            (value? pathPinned : pathUnpinned).Opacity = 1;
        }

        public PinButton() {
            InitializeComponent();

            base.MouseLeave(this, null);

            CurrentPath.Opacity = 1;

            //Preferences.AlwaysOnTopChanged += UpdateTopmost;
        }

        protected override void Unloaded(object sender, VisualTreeAttachmentEventArgs e) {
            base.Unloaded(sender, e);

            //Preferences.AlwaysOnTopChanged -= UpdateTopmost;
        }
        
        protected override void Click(PointerReleasedEventArgs e) {
            //Preferences.AlwaysOnTop = !Preferences.AlwaysOnTop;
            ((Window)this.GetVisualRoot()).Activate();
        }
    }
}
