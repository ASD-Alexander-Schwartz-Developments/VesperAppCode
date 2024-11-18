using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System;
using System.Diagnostics;
using System.Reflection;
using VesperApp.ViewModels;
using FluentAvalonia.UI.Windowing;
using Velopack;
using Microsoft.Extensions.Logging;
using System.Text;
using Avalonia.Threading;

namespace VesperApp.Views
{
    public partial class MainWindow : AppWindow
    {
        public MainWindow()
        {
            InitializeComponent();

            System.Version? v = Assembly.GetExecutingAssembly().GetName().Version;

            string version = (v == null) ? "Unknown Version" : v.ToString();

            this.Title += " - v" + version;

            TitleBar.ExtendsContentIntoTitleBar = false;
            TitleBar.TitleBarHitTestType = TitleBarHitTestType.Complex;

            //this.mainView.textLogWindow.Text= version;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            //Setting up the minimum size based on the initial size of the form elements
            //https://stackoverflow.com/questions/9319248/how-to-avoid-having-a-window-smaller-than-the-minimum-size-of-a-usercontrol-in-w

            // We know longer need to size to the contents.
            ClearValue(SizeToContentProperty);
            // We want our control to shrink/expand with the window.
            /*_MyControlName.ClearValue(WidthProperty);
            _MyControlName.ClearValue(HeightProperty);*/
            // Don't want our window to be able to get any smaller than this.
            SetValue(MinWidthProperty, this.Width);
            SetValue(MinHeightProperty, this.Height);        }

        private void Window_Closed(object? sender, System.EventArgs e)
        {
            Trace.Flush();
        }

        /*
        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }*/
    }
}
