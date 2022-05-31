using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace VesperApp.Controls
{
    public partial class ConnectToggle : IconButton
    {
        public ConnectToggle()
        {
            InitializeComponent();

            base.MouseLeave(this, null);

            SetState(false);
        }

        private Canvas unlocked, locked;
        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            unlocked = this.Get<Canvas>("Unlocked");
            locked = this.Get<Canvas>("Locked");
        }

        protected override IBrush Fill
        {
            get => (IBrush)this.Resources["Brush"];
            set => this.Resources["Brush"] = value;
        }

        public void SetState(bool value) =>
            unlocked.IsVisible = !(locked.IsVisible = value);

    }
}
