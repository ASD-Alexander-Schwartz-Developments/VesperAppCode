using Avalonia.Controls;
using Avalonia.ReactiveUI;
using ReactiveUI;
using System;
using System.Threading.Tasks;
using VesperApp.Models;
using VesperApp.ViewModels;
using VesperApp.Controls;
using Avalonia.Markup.Xaml;
using System.Reflection;

namespace VesperApp.Views
{
    public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
    {
        
//        private ConnectToggle _connectToggle;
        public MainWindow()
        {
            InitializeComponent();
//            _connectToggle = this.Get<ConnectToggle>("ConnectToggle");
            this.WhenActivated(d => d(ViewModel!.ShowDockPickDialog.RegisterHandler(DoShowDockPickDialogAsync)));
        }

        void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);

            Version? v = Assembly.GetExecutingAssembly().GetName().Version;

            string version = (v == null) ? "Unknown Version" : v.ToString();

            this.Title += " - v" + version;
        }



        private bool _toggle = false;
        void Launchpad_LockToggle()
        {
//            _connectToggle.SetState(_toggle);
//            _toggle = !_toggle;
        }

        private async Task DoShowDockPickDialogAsync(InteractionContext<DockPickWindowViewModel, DockDeviceInfo?> interaction)
        {
            var dialog = new DockPickWindow();
            dialog.DataContext = interaction.Input;

            var result = await dialog.ShowDialog<DockDeviceInfo?>(this);
            interaction.SetOutput(result);
        }
    }
}
