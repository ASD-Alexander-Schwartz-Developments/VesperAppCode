using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using System;
using System.Collections.Generic;
using VesperApp.ViewModels;
using VesperApp.Views;


namespace VesperApp
{
    public partial class App : Application
    {
        static App instance;

        public static Window MainWindow => ((ClassicDesktopStyleApplicationLifetime)instance.ApplicationLifetime).MainWindow;
        public static IReadOnlyList<Window> Windows => ((ClassicDesktopStyleApplicationLifetime)instance.ApplicationLifetime).Windows;
        public static void Shutdown() => ((ClassicDesktopStyleApplicationLifetime)instance.ApplicationLifetime).Shutdown();


        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
            instance = this;
            Styles.Add(new VesperApp.Themes.Light());
        }

        public static void WindowClosed(Window sender)
        {
            if (sender is SplashWindow)
            {
                //Preferences.Window?.Close();

                //foreach (Launchpad lp in MIDI.Devices)
                    //lp.Window?.Close();
            }
        }


        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var mainWindow = new MainWindow();//SplashWindow();
                mainWindow.DataContext = new MainWindowViewModel(mainWindow);

                desktop.MainWindow = mainWindow;
            }

            //lifetime.MainWindow = new SplashWindow();
            base.OnFrameworkInitializationCompleted();
        }

    }
}
