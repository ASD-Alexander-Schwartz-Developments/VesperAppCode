using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System;
using System.ComponentModel;
using VesperApp.ViewModels;
using ReactiveUI;
using Avalonia.ReactiveUI;

using VesperApp.ViewModels;

namespace VesperApp.Views
{
    public partial class DockPickWindow : ReactiveWindow<DockPickWindowViewModel>
    {
        public DockPickWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif

            this.WhenActivated(d => d(ViewModel!.CloseAndConnect.Subscribe<VesperApp.Models.DockDeviceInfo>(Close)));
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }


        public void Unloading_DockPickWindow(object sender, CancelEventArgs e)
        {
            if (this.DataContext != null)
            {
                DockPickWindowViewModel dc = (DockPickWindowViewModel)this.DataContext;
                dc.TerminateScan();
            }

            e.Cancel = false;
        }
    }
}
