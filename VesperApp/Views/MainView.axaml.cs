using Avalonia.Controls;
using Avalonia.ReactiveUI;
using ReactiveUI;
using System;
using System.Threading.Tasks;
using VesperApp.Models;
using VesperApp.ViewModels;
using Avalonia.Markup.Xaml;
using System.Reflection;
using Avalonia;

namespace VesperApp.Views
{
    public partial class MainView : UserControl
    {
        
//        private ConnectToggle _connectToggle;
        public MainView()
        {
            InitializeComponent();
//            _connectToggle = this.Get<ConnectToggle>("ConnectToggle");
            //this.WhenActivated(d => d(ViewModel!.ShowDockPickDialog.RegisterHandler(DoShowDockPickDialogAsync)));
        }
        /*
        void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }*/
    }
}
