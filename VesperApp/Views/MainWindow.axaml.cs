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
            TitleBar.
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
