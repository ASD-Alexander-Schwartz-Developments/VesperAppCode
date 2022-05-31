using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Avalonia.Threading;


namespace VesperApp.Controls {
    public partial class PortSelector: ComboBox, IStyleable {
        Type IStyleable.StyleKey => typeof(ComboBox);

        public delegate void PortChangedEventHandler();
        public event PortChangedEventHandler PortChanged;

        bool _noalp = false;
        public void Port_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        public void Update() {
            if (!Dispatcher.UIThread.CheckAccess()) {
                Dispatcher.UIThread.InvokeAsync(() => UpdateSel(/*selected*/));
                return;
            }
            /*
            List<Launchpad> ports = new List<Launchpad>();//MIDI.UsableDevices;

            if (NoAbletonLaunchpads)
                ports = ports.Where(i => i.GetType() != typeof(AbletonLaunchpad)).ToList();

            if (selected?.Usable == false) ports.Add(selected);

            //ports.Add(MIDI.NoOutput);
            */
            Items = null;//ports;
            SelectedIndex = -1;
           // SelectedItem = selected;
        }

        void UpdateSel()
        {

        }

        public PortSelector() {
            AvaloniaXamlLoader.Load(this);

            Update();
            //MIDI.DevicesUpdated += Update;
        }

        void Unloaded(object sender, VisualTreeAttachmentEventArgs e) {
            //MIDI.DevicesUpdated -= Update;

            PortChanged = null;
        }

        //void Changed(object sender, SelectionChangedEventArgs e) => PortChanged?.Invoke((Launchpad)SelectedItem);
    }
}