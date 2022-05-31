using System.Collections.Generic;
using System.Linq;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

using VesperApp.Models;


namespace VesperApp.Controls {
    public partial class Indicator: UserControl {
        void InitializeComponent() {
            AvaloniaXamlLoader.Load(this);

            display = this.Get<Ellipse>("IndicatorDisplay");
        }

        public bool ChainKind { get; set; } = false;

        Ellipse display;
        Courier Timer;
        object locker = new object();
        
        bool Disposed = false;

        void SetIndicator(double state) => Dispatcher.UIThread.InvokeAsync(() => display.Opacity = state);

        public Indicator() {
            InitializeComponent();
            
            Timer = new Courier(200, _ => SetIndicator(0), false);
        }

        void Unloaded(object sender, VisualTreeAttachmentEventArgs e) {
            Disposed = true;

            lock (locker) {
                Timer?.Dispose();
                Timer = null;
            }
        }

        public void Trigger(List<Signal> triggering) {
            if (Disposed) return;
            //if (ChainKind? !Preferences.ChainSignalIndicators : !Preferences.DeviceSignalIndicators) return;

            SetIndicator(triggering.Any(i => i.Color.Lit)? 1 : 0.5);

            lock (locker)
                Timer?.Restart();
        }
    }
}
