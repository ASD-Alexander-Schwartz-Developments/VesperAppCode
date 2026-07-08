using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Styling;
using Avalonia.Markup.Xaml;
using System;
using System.Diagnostics;
using System.Reflection;
using FluentAvalonia.UI.Windowing;
using Velopack;
using System.Text;
using Avalonia.Threading;
using FluentAvalonia.Styling;
using FluentAvalonia.UI.Media;

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
            TitleBar.TitleBarHitTestType = TitleBarHitTestType.Simple;
#if DEBUG
            this.AttachDevTools();
#endif

            //SplashScreen = new MainAppSplashScreen(this);

            Application.Current.ActualThemeVariantChanged += OnActualThemeVariantChanged;

            //this.mainView.textLogWindow.Text= version;
        }


        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);

            var thm = ActualThemeVariant;
            if (IsWindows11 && thm != FluentAvaloniaTheme.HighContrastTheme)
            {
                TryEnableMicaEffect();
            }
        }

        private void OnActualThemeVariantChanged(object? sender, EventArgs e)
        {
            if (IsWindows11)
            {
                if (ActualThemeVariant != FluentAvaloniaTheme.HighContrastTheme)
                {
                    TryEnableMicaEffect();
                }
                else
                {
                    ClearValue(BackgroundProperty);
                    ClearValue(TransparencyBackgroundFallbackProperty);
                }
            }
        }

        private void TryEnableMicaEffect()
        {
            return;
            // TransparencyBackgroundFallback = Brushes.Transparent;
            // TransparencyLevelHint = WindowTransparencyLevel.Mica;

            // The background colors for the Mica brush are still based around SolidBackgroundFillColorBase resource
            // BUT since we can't control the actual Mica brush color, we have to use the window background to create
            // the same effect. However, we can't use SolidBackgroundFillColorBase directly since its opaque, and if
            // we set the opacity the color become lighter than we want. So we take the normal color, darken it and 
            // apply the opacity until we get the roughly the correct color
            // NOTE that the effect still doesn't look right, but it suffices. Ideally we need access to the Mica
            // CompositionBrush to properly change the color but I don't know if we can do that or not
            if (ActualThemeVariant == ThemeVariant.Dark)
            {
                var color = this.TryFindResource("SolidBackgroundFillColorBase",
                    ThemeVariant.Dark, out var value) ? (Color2)(Color)value : new Color2(32, 32, 32);

                color = color.LightenPercent(-0.8f);

                Background = new ImmutableSolidColorBrush(color, 0.9);
            }
            else if (ActualThemeVariant == ThemeVariant.Light)
            {
                // Similar effect here
                var color = this.TryFindResource("SolidBackgroundFillColorBase",
                    ThemeVariant.Light, out var value) ? (Color2)(Color)value : new Color2(243, 243, 243);

                color = color.LightenPercent(0.5f);

                Background = new ImmutableSolidColorBrush(color, 0.9);
            }
        } 

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Window opens at an explicit 1280x820 (set in XAML) and is freely
            // resizable down to MinWidth/MinHeight. We no longer size-to-content
            // (which produced a tall, narrow window) or pin the min to the initial
            // size (which prevented shrinking).
            ClearValue(SizeToContentProperty);
        }

        private void Window_Closed(object? sender, System.EventArgs e)
        {
            Trace.Flush();
        }
    }
}
