using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;


namespace VesperApp.Controls {
    public partial class ClearButton: IconButton {
        void InitializeComponent() {
            AvaloniaXamlLoader.Load(this);

            path = this.Get<Path>("Path");
        }

        Path path;

        protected override IBrush Fill {
            get => path.Fill;
            set => path.Fill = value;
        }

        public ClearButton() {
            InitializeComponent();

            base.MouseLeave(this, null);
        }

        //protected override void Click(PointerReleasedEventArgs e) => MIDI.ClearState(force: e.KeyModifiers == KeyModifiers.Shift);
    }
}
