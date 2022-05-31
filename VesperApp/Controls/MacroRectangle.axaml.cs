using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace VesperApp.Controls {
    public partial class MacroRectangle: UserControl {
        void InitializeComponent() {
            AvaloniaXamlLoader.Load(this);

            rect = this.Get<Grid>("Rect");
            text = this.Get<TextBlock>("Text");
        }

        Grid rect;
        TextBlock text;

        public IBrush Fill {
            get => rect.Background;
            set => rect.Background = value;
        }

        public int Index {
            set => text.Text = value.ToString();
        }

        public MacroRectangle() => InitializeComponent();
    }
}
