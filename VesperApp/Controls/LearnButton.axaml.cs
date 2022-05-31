using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace VesperApp.Controls {
    public partial class LearnButton: UserControl {
        void InitializeComponent() {
            AvaloniaXamlLoader.Load(this);
            
            grid = this.Get<Grid>("Grid");
            textBlock = this.Get<TextBlock>("TextBlock");
        }

        public delegate void ClickEventHandler();
        public event ClickEventHandler Click;

        Grid grid;
        TextBlock textBlock;

        public Canvas Icon {
            get => (grid.Children.Count > 1)? (Canvas)grid.Children[1] : null;
            set {
                Grid.SetColumn(value, 1);
                
                if (grid.Children.Count > 1) grid.Children[1] = value;
                else grid.Children.Add(value);
            }
        }

        public string Text {
            get => textBlock.Text;
            set => textBlock.Text = value;
        }

        public LearnButton() => InitializeComponent();

        void Clicked(object sender, RoutedEventArgs e) => Click?.Invoke();
    }
}
