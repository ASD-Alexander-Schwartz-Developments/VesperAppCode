using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace VesperApp.Controls
{
    public partial class UpdateButton : IconButton
    {
        public UpdateButton()
        {
            InitializeComponent();

            base.MouseLeave(this, null);
        }

        private TextBlock messageBlock;

        protected override IBrush Fill { get; set; }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);

            messageBlock = this.Get<TextBlock>("Message");
        }


        public void Enable(string message)
        {
            messageBlock.Text = message;
            IsVisible = true;
        }

    }
}
