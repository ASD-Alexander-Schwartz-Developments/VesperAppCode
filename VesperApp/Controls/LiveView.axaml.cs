using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using VesperApp.Controls.LiveAudio;
using VesperApp.ViewModels;

namespace VesperApp.Controls
{
    /// <summary>Live View page: real-time microphone visualisation (KOL 4-channel USB stream or PC input).</summary>
    public partial class LiveView : UserControl
    {
        public LiveView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Open one instrument in its own resizable window (second monitor / projector). The
        /// window shares this page's view-model, so it follows the same source, channels and
        /// display settings live; closing it leaves the page untouched.
        /// </summary>
        private void PopOut_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is not Button b || b.Tag is not string kind || DataContext is not LiveViewViewModel vm) return;

            LiveAudioView view;
            switch (kind)
            {
                case "Spectrum":
                    var sp = new SpectrumView { Title = "Spectrum" };
                    sp.Bind(LiveAudioView.ChannelMaskProperty, new Binding(nameof(vm.ChannelMask)));
                    sp.Bind(SpectrumView.FftSizeProperty, new Binding(nameof(vm.FftSize)));
                    sp.Bind(SpectrumView.LogFrequencyProperty, new Binding(nameof(vm.LogFrequency)));
                    view = sp; break;
                case "Spectrogram":
                    var sg = new SpectrogramView { Title = "Spectrogram" };
                    sg.Bind(LiveAudioView.SelectedChannelProperty, new Binding(nameof(vm.SelectedChannel)));
                    sg.Bind(SpectrogramView.SecondsProperty, new Binding(nameof(vm.SpectrogramSeconds)));
                    sg.Bind(SpectrogramView.MinDbProperty, new Binding(nameof(vm.SpectrogramFloorDb)));
                    view = sg; break;
                default:
                    var sc = new ScopeView { Title = "Time series" };
                    sc.Bind(LiveAudioView.ChannelMaskProperty, new Binding(nameof(vm.ChannelMask)));
                    sc.Bind(ScopeView.WindowMsProperty, new Binding(nameof(vm.ScopeWindowMs)));
                    view = sc; break;
            }
            view.Bind(LiveAudioView.SourceProperty, new Binding(nameof(vm.DisplayBuffer)));
            view.Margin = new Thickness(8);

            var win = new Window
            {
                Title = $"Live View — {view.Title}",
                Width = 1100,
                Height = 640,
                MinWidth = 480,
                MinHeight = 300,
                Background = new SolidColorBrush(Color.Parse("#0B1220")),
                DataContext = vm,
                Content = view,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
            };
            if (TopLevel.GetTopLevel(this) is Window owner) win.Show(owner); else win.Show();
        }
    }
}
