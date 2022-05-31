using System;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;


namespace VesperApp.Controls {
    public partial class DeviceHead: UserControl {
        void InitializeComponent() {
            AvaloniaXamlLoader.Load(this);

            border = this.Get<Border>("Border");
            header = this.Get<Border>("Header");
        }

        //DeviceViewer Owner;
        public Border border, header;

        //public DeviceHead() => throw new InvalidOperationException();

        public DeviceHead(/*Device owner, DeviceViewer ownerviewer*/) {
            InitializeComponent();

            //Owner = ownerviewer;

            //this.Resources["TitleBrush"] = Owner.Header.Background/*?? Owner.Resources["TitleBrush"]*/;

            //Owner.DragDrop.Subscribe(this);
            
            SetEnabled(/*owner.Enabled*/true);
        }

        //void Unloaded(object sender, VisualTreeAttachmentEventArgs e) => Owner = null;

        public void SetEnabled(bool value) {
            border.Background = (IBrush)Application.Current.FindResource(value? "ThemeControlHighBrush" : "ThemeControlMidBrush");
            border.BorderBrush = (IBrush)Application.Current.FindResource(value? "ThemeBorderMidBrush" : "ThemeBorderLowBrush");
        }

        private void DoNothing() { }

        void Drag(object sender, PointerPressedEventArgs e) => DoNothing();//Owner.Drag(sender, e);
    }
}
