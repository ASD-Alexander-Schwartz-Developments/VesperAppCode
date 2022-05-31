using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.VisualTree;


namespace VesperApp.Controls {
    public partial class PreferencesButton: IconButton {
        void InitializeComponent() {
            AvaloniaXamlLoader.Load(this);
   
            hole = this.Get<Ellipse>("Hole");
        }

        Ellipse hole;

        public IBrush HoleFill {
            get => hole.Fill;
            set => hole.Fill = value;
        }

        protected override IBrush Fill {
            get => (IBrush)this.Resources["Brush"];
            set => this.Resources["Brush"] = value;
        }

        public PreferencesButton() {
            InitializeComponent();

            base.MouseLeave(this, null);

            hole.Fill = (SolidColorBrush)Application.Current.FindResource("ThemeBorderMidBrush");
        }

        //protected override void Click(PointerReleasedEventArgs e) => PreferencesWindow.Create((Window)this.GetVisualRoot());
    }
}
