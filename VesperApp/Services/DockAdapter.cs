using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using ASD.Devices;
using ASD.Devices.Dock;
using ASDLibUSBWrapper;
using VesperApp.Models;

namespace VesperApp.Services
{
    public class DockAdapter : IDockInterface<Models.DockDevice>, IDisposable
    {
        private readonly IDockController _controller;
        private readonly DeviceManager? _deviceManager; // non-Windows only

        private DockDevice? _connectedDock;
        private bool _ven, _boot0, _nrst;

        private readonly CancellationTokenSource _cts = new();
        private readonly Task _watchTask;

        private readonly object _eventLock = new();
        private EventHandler<DockConnectionEventArgs>? _connectEvent;

        public DockAdapter()
        {
            if (OperatingSystem.IsWindows())
            {
                _controller = DockControllerFactory.Create();
                _deviceManager = null;
            }
            else
            {
                var usbCtx = new UsbContext();
                _controller = DockControllerFactory.Create(usbCtx);
                _deviceManager = new DeviceManager(usbContext: usbCtx);
            }

            _watchTask = Task.Run(() => WatchAsync(_cts.Token));
        }

        public bool IsConnected => _controller.IsOpen;

        /// <summary>Detail of the last failed connect attempt, or null.</summary>
        public string? LastConnectError => _controller.LastError;

        public event EventHandler<DockConnectionEventArgs> ConnectionEvent
        {
            add { lock (_eventLock) _connectEvent += value; }
            remove { lock (_eventLock) _connectEvent -= value; }
        }

        private async Task WatchAsync(CancellationToken ct)
        {
            bool prev = IsConnected;
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    bool cur = IsConnected;
                    if (cur != prev)
                    {
                        EventHandler<DockConnectionEventArgs>? h;
                        lock (_eventLock) h = _connectEvent;
                        h?.Invoke(this, new DockConnectionEventArgs(cur, "Dock connection changed", _connectedDock));
                        prev = cur;
                    }
                }
                catch { }

                try { await Task.Delay(250, ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
            }
        }

        public async Task<IEnumerable<DockDevice>> ScanDocksAsync(bool forceRefresh = false)
        {
            if (OperatingSystem.IsWindows())
                return ScanDocksViaD2xx();

            var result = new List<DockDevice>();
            if (_deviceManager != null)
            {
                _deviceManager.ScanOnce();
                foreach (DiscoveredDevice d in _deviceManager.Current)
                {
                    if (d.Kind != AsdDeviceKind.Dock) continue;
                    result.Add(new DockDevice(new DockDeviceInfo
                    {
                        Id = d.SerialNumber ?? d.Key,
                        Text = d.Description,
                        Description = "FTDI Dock",
                        SerialNumber = d.SerialNumber, // null: libusb discovery doesn't open devices
                    }));
                }
            }
            return await Task.FromResult(result);
        }

        [SupportedOSPlatform("windows")]
        private static List<DockDevice> ScanDocksViaD2xx()
        {
            var result = new List<DockDevice>();
            var ftdi = new FTD2XX_NET.FTDI();

            uint count = 0;
            if (ftdi.GetNumberOfDevices(ref count) != FTD2XX_NET.FTDI.FT_STATUS.FT_OK || count == 0)
                return result;

            var list = new FTD2XX_NET.FTDI.FT_DEVICE_INFO_NODE[count];
            if (ftdi.GetDeviceList(list) != FTD2XX_NET.FTDI.FT_STATUS.FT_OK)
                return result;

            for (int i = 0; i < count; i++)
            {
                if (list[i].SerialNumber?.Length > 0)
                    result.Add(new DockDevice(new DockDeviceInfo
                    {
                        Id = list[i].SerialNumber,
                        Text = list[i].Description,
                        Description = "FTDI",
                        SerialNumber = list[i].SerialNumber,
                    }));
            }
            return result;
        }

        public Task StopDocksScanAsync() => Task.CompletedTask;

        public async Task<DockDevice?> GetDockBySerialNumberAsync(string serialnum)
        {
            foreach (DockDevice d in await ScanDocksAsync())
                if (string.Equals(d.Info.Id, serialnum, StringComparison.OrdinalIgnoreCase))
                    return d;
            return null;
        }

        public async Task<bool> DockConnect(DockDevice d)
        {
            if (_controller.IsOpen) return true;
            // Filter the open on the real FTDI serial only. On Linux discovery cannot read
            // it (no open), Info.Id is a synthetic key like "usb:Dock:0403:6001", and passing
            // that as a serial would reject every device — open the first matching dock instead.
            bool ok = await _controller.OpenAsync(d.Info.SerialNumber).ConfigureAwait(false);
            if (ok)
            {
                _connectedDock = d;
                _ven = _boot0 = _nrst = false;
                await _controller.SetLinesAsync(false, false, false).ConfigureAwait(false);
            }
            return ok;
        }

        public async Task<bool> DockDisconnect()
        {
            _connectedDock = null;
            await _controller.CloseAsync().ConfigureAwait(false);
            return true;
        }

        public Task<string> GetManufacturerName() => Task.FromResult("FTDI");

        public Task<string> GetSerialNumber() =>
            Task.FromResult(_connectedDock?.Info?.Id ?? string.Empty);

        public async Task<bool> SetBits(bool ven, bool boot0, bool nrst)
        {
            bool ok = await _controller.SetLinesAsync(ven, boot0, nrst).ConfigureAwait(false);
            if (ok) { _ven = ven; _boot0 = boot0; _nrst = nrst; }
            return ok;
        }

        public async Task<bool> SetEnableDevice(bool ven)
        {
            bool ok = await _controller.SetLinesAsync(ven, _boot0, _nrst).ConfigureAwait(false);
            if (ok) _ven = ven;
            return ok;
        }

        public async Task<bool> SetNReset(bool nrst)
        {
            bool ok = await _controller.SetLinesAsync(_ven, _boot0, nrst).ConfigureAwait(false);
            if (ok) _nrst = nrst;
            return ok;
        }

        public async Task<bool> SetBoot0Mode(bool boot0)
        {
            bool ok = await _controller.SetLinesAsync(_ven, boot0, _nrst).ConfigureAwait(false);
            if (ok) _boot0 = boot0;
            return ok;
        }

        public Task<bool> ResetDevice() => _controller.ResetAsync();

        public void Dispose()
        {
            _cts.Cancel();
            try { _watchTask.Wait(500); } catch { }
            _cts.Dispose();
            _controller.Dispose();
            _deviceManager?.Dispose();
        }
    }

    public class DockConnectionEventArgs : EventArgs
    {
        public string DebugMessage { get; set; }
        public bool IsConnected { get; set; }
        public DockDevice? Dock { get; set; }

        public DockConnectionEventArgs(bool connection, string debugmsg, DockDevice? dockDevice) : base()
        {
            DebugMessage = debugmsg;
            IsConnected = connection;
            Dock = dockDevice;
        }
    }
}
