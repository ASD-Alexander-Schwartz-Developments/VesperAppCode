using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System;
using System.ComponentModel;
using VesperApp.ViewModels;
using ReactiveUI;
using Avalonia.ReactiveUI;
using VesperApp.Models;

namespace VesperApp.Views
{
    public partial class DockPickWindow : Window
    {
        public DockPickWindow()
        {
            InitializeComponent();

            //            this.WhenActivated(d => d(ViewModel!.CloseAndConnect.Subscribe<VesperApp.Models.DockDeviceInfo>(Close)));

            this.Closing += Unloading_DockPickWindow;
            this.Closed += Closed_DockPickWindow;
            this.btnChooseClose.Click += BtnChooseClose_Click;
            this.listDocks.DoubleTapped += ListDocks_DoubleTapped;
        }

        private void ListDocks_DoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
        {
            e.Handled = true;

            if (this.DataContext != null)
            {
                DockPickWindowViewModel dc = (DockPickWindowViewModel)this.DataContext;
                if (dc.SelectedDock != null)
                {
                    if (dc.SelectedDock.SelectedItem != null)
                    {
                        Close(dc.SelectedDock.SelectedItem);
                        return;
                    }
                }
            }

//            Close();

        }

        private void BtnChooseClose_Click(object? sender, RoutedEventArgs e)
        {
            if (this.DataContext != null)
            {
                DockPickWindowViewModel dc = (DockPickWindowViewModel)this.DataContext;
                if (dc.SelectedDock != null)
                {
                    if (dc.SelectedDock.SelectedItem != null)
                    {
                        Close(dc.SelectedDock.SelectedItem);
                        return;
                    }
                }
            }

//            Close();
        }

        /*
        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }*/



        public void Closed_DockPickWindow(object? sender, EventArgs e)
        {/*
            if (this.DataContext != null)
            {
                DockPickWindowViewModel dc = (DockPickWindowViewModel)this.DataContext;
                if(dc.SelectedDock != null)
                {
                    if (dc.SelectedDock.SelectedItem != null)
                    {
                        Close(dc.SelectedDock.SelectedItem);
                        return;
                    }
                }
            }

            Close();*/
        }

        public void Unloading_DockPickWindow(object? sender, CancelEventArgs? e)
        {
            if (this.DataContext != null)
            {
                DockPickWindowViewModel dc = (DockPickWindowViewModel)this.DataContext;
                dc.TerminateScan();
            }

            if(e is not  null)
            {
                e.Cancel = false;
            }
            
        }
    }
}
