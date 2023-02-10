using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using System;
using System.Collections.Generic;
using VesperApp.ViewModels;
using VesperApp.Views;


namespace VesperApp
{
    public partial class App : Application
    {
        static App? instance;

        public static Window? MainWindow => ((instance != null) ? ((ClassicDesktopStyleApplicationLifetime?)instance.ApplicationLifetime).MainWindow : null);
        public static IReadOnlyList<Window>? Windows => ((ClassicDesktopStyleApplicationLifetime?)instance?.ApplicationLifetime).Windows;
        public static void Shutdown() => ((ClassicDesktopStyleApplicationLifetime)instance.ApplicationLifetime).Shutdown();


        public override void Initialize()
        {
            instance = this;
            AvaloniaXamlLoader.Load(this);
            Styles.Add(new VesperApp.Themes.Light());
        }

        public static void WindowClosed(Window sender)
        {
            if (sender is SplashWindow)
            {
            }
        }


        public override void OnFrameworkInitializationCompleted()
        {
            MainViewViewModel mainViewModel = new();
            TopLevel? rootTopLevel = null;
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new MainWindow
                {
                    DataContext = mainViewModel
                };
                rootTopLevel = desktop.MainWindow;
            }
            else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
            {
                singleViewPlatform.MainView = new MainView
                {
                    DataContext = mainViewModel
                };

                //Getting TopLevel in SingleView - https://github.com/AvaloniaUI/Avalonia/discussions/8752
                rootTopLevel = (TopLevel?)singleViewPlatform.MainView.GetVisualRoot();
            }

            if (rootTopLevel == null)
                throw new NotImplementedException("Root TopLevel not found!");

            MainViewViewModel.RootTopLevel = rootTopLevel;

            base.OnFrameworkInitializationCompleted();
        }

    }
}
