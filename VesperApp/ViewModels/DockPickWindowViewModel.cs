using Avalonia.Controls.Selection;
using Avalonia.Threading;
using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reactive;
using System.Windows.Input;
using VesperApp.Models;
using VesperApp.Services;

namespace VesperApp.ViewModels
{
    public class DockPickWindowViewModel : ViewModelBase
    {
        public ObservableCollection<DockDeviceInfo> Docks { get; }

        //public ReactiveCommand<Unit, DockDeviceInfo?> ? CloseAndConnect { get; }
        public SelectionModel<DockDeviceInfo?> ? SelectedDock { get; }

        public bool IsBusy
        {
            get => _isBusy;
            set => this.RaiseAndSetIfChanged(ref _isBusy, value);
        }
        private bool _isBusy;


        private System.Timers.Timer? scanTimer;
        private DockAdapter? _adapter;
        private DockDeviceInfo? _dockDeviceInfo;

        public DockPickWindowViewModel()
        {
            Docks = new ObservableCollection<DockDeviceInfo>();
            _dockDeviceInfo = null;
            _adapter = null;
            scanTimer = null;
            SelectedDock = null;
            _isBusy = false;
            //CloseAndConnect = null;
        }

        public DockPickWindowViewModel(DockAdapter adapter)
        {
            Docks = new ObservableCollection<DockDeviceInfo>();
            SelectedDock = new SelectionModel<DockDeviceInfo?>();
            SelectedDock.SelectionChanged += SelectedDock_SelectionChanged;
            _dockDeviceInfo = null;
            _isBusy = false;
            _adapter = adapter;
            scanTimer = new System.Timers.Timer();
            scanTimer.Elapsed += ScanTimer_Elapsed;
            scanTimer.Interval = 2500;
            scanTimer.Start();
        }

        private void SelectedDock_SelectionChanged(object? sender, SelectionModelSelectionChangedEventArgs<DockDeviceInfo?> e)
        {
            if(e.SelectedItems != null && e.SelectedItems.Count > 0)
                _dockDeviceInfo = e.SelectedItems[0];
        }

        public void TerminateScan()
        {
            if(scanTimer != null)
            {
                scanTimer.Stop();
            }
        }
        private async void ScanTimer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            if (_adapter != null)
            {

                IsBusy = true;
                scanTimer?.Stop();
                var docks = await _adapter.ScanDocksAsync();

                //ObservableCollection<DockDeviceInfo> _discovered = new ObservableCollection<DockDeviceInfo>();
                await System.Threading.Tasks.Task.Delay(150);

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    //Docks.Clear();
                    foreach (DockDevice dockDevice in docks)
                    {
                        // Match by Id: each scan creates fresh DockDeviceInfo instances,
                        // so reference equality would re-add the same dock every tick.
                        bool known = false;
                        foreach (DockDeviceInfo dockinfo in Docks)
                        {
                            if (dockinfo.Id == dockDevice.Info.Id) known = true;
                        }
                        if (known == false)
                            Docks.Add(dockDevice.Info);
                    }

                    DockDeviceInfo[] list = new DockDeviceInfo[Docks.Count];
                    Docks.CopyTo(list, 0);

                    foreach (DockDeviceInfo dockinfo in list)
                    {
                        bool f = false;
                        foreach (DockDevice dockDevice in docks)
                        {
                            if (dockinfo.Id == dockDevice.Info.Id) f = true;
                        }

                        if (f == false) Docks.Remove(dockinfo);
                    }


                });

                //            Docks = _discovered;
                IsBusy = false;
                scanTimer?.Start();
            }
        }
    }
}
