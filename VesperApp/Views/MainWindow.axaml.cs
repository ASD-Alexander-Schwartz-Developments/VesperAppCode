using Avalonia.Controls;
using Avalonia.ReactiveUI;
using ReactiveUI;
using System;
using System.Threading.Tasks;
using VesperApp.Models;
using VesperApp.ViewModels;
using VesperApp.Controls;
using Avalonia.Markup.Xaml;

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
