using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.VisualTree;
using FluentAvalonia.Styling;
using System;
using System.Collections.Generic;
using VesperApp.ViewModels;
using VesperApp.Views;


namespace VesperApp
{
    public partial class App : Application
    {
        static App? instance;

        public static Window? MainWindow => ((instance != null) ? ((ClassicDesktopStyleApplicationLifetime?)instance?.ApplicationLifetime!).MainWindow : null);
        public static IReadOnlyList<Window>? Windows => ((ClassicDesktopStyleApplicationLifetime?)instance?.ApplicationLifetime!).Windows;
        public static void Shutdown() => ((ClassicDesktopStyleApplicationLifetime)instance?.ApplicationLifetime!).Shutdown();

        public static TopLevel? AppTopLevel;

        public override void Initialize()
        {
            instance = this;
            AvaloniaXamlLoader.Load(this);

            // Drive the whole Fluent palette (nav selection, buttons, focus rings)
            // from one brand accent so the app reads as a single cohesive identity.
            foreach (var style in Styles)
            {
                if (style is FluentAvaloniaTheme faTheme)
                {
                    faTheme.PreferSystemTheme = false;
                    faTheme.PreferUserAccentColor = false;
                    faTheme.CustomAccentColor = Color.Parse("#6366F1");
                    break;
                }
            }
        }

        public static void WindowClosed(Window sender)
        {
        }


        public override void OnFrameworkInitializationCompleted()
        {
            // Open-core platform seam: discover proprietary plugins (e.g. ASD.Gnss over
            // cg-gnss, ASD.PlmClient) from the gitignored plugins/ folder and bind them
            // before the main view model builds its navigation. With none present, the
            // stub services stand and those features are unavailable. See docs/ARCHITECTURE.md.
            ASD.Platform.PluginLoader.LoadFrom();
            ASD.Platform.PlatformServices.Initialize();
            ASD.Modules.ModuleHost.Instance.UseContext(ASD.Platform.PlatformServices.Context);

            // Let the decode-job manager (Services, no view refs) pop the unified, non-modal
            // Decoding Progress panel when a job starts, without Services depending on Views.
            VesperApp.Services.DecodeJobManager.PanelOpener = DecodingProgressWindow.ShowSingleton;

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

            //if (rootTopLevel == null)
            //    throw new NotImplementedException("Root TopLevel not found!");

            MainViewViewModel.RootTopLevel = rootTopLevel;
            AppTopLevel = rootTopLevel;

            base.OnFrameworkInitializationCompleted();
        }

    }
}
