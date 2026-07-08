using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using VesperApp.Services;

namespace VesperApp.Views
{
    /// <summary>
    /// Non-modal panel listing all decode/parse jobs (Windows-copy-dialog style). A single instance
    /// is reused: <see cref="ShowSingleton"/> creates it on first use and re-activates it afterwards,
    /// so jobs keep running whether or not it is open.
    /// </summary>
    public partial class DecodingProgressWindow : Window
    {
        private static DecodingProgressWindow? _instance;

        public DecodingProgressWindow()
        {
            InitializeComponent();
            DataContext = DecodeJobManager.Instance;
            AddHandler(TextBox.TextChangedEvent, OnLogTextChanged, RoutingStrategies.Bubble);
        }

        /// <summary>Show or foreground the shared panel. Safe to call repeatedly, from any decode start.</summary>
        public static void ShowSingleton()
        {
            if (_instance is null)
            {
                _instance = new DecodingProgressWindow();
                _instance.Closed += (_, _) => _instance = null;
                if (App.MainWindow is { } owner) _instance.Show(owner);
                else _instance.Show();
                return;
            }

            if (_instance.WindowState == WindowState.Minimized)
                _instance.WindowState = WindowState.Normal;
            _instance.Activate();
        }

        private void OnLogTextChanged(object? sender, TextChangedEventArgs e)
        {
            if (e.Source is TextBox tb && tb.Classes.Contains("log"))
                tb.CaretIndex = tb.Text?.Length ?? 0;
        }
    }
}
