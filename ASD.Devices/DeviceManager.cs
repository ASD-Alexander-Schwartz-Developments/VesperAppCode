using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ASD.DeviceCore.Transport;
using ASDLibUSBWrapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ASD.Devices
{
    /// <summary>An ASD device seen by discovery (not yet opened).</summary>
    public sealed record DiscoveredDevice(
        AsdDeviceKind Kind,
        AsdBus Bus,
        string Key,
        int? Vid = null,
        int? Pid = null,
        string? PortName = null,
        string? SerialNumber = null,
        string? Description = null);

    /// <summary>
    /// Unified, cross-platform device discovery. Replaces the legacy "open and probe
    /// every COM port every 2.15 s" loop with:
    /// <list type="bullet">
    /// <item>CDC loggers: the OS serial enumerator (VID/PID, <b>no port opened</b>),
    /// filtered to ASD ids.</item>
    /// <item>Nanotag / Dock: libusb VID/PID match.</item>
    /// </list>
    /// A debounced background scan diffs against the last snapshot and raises
    /// <see cref="Arrived"/>/<see cref="Removed"/> — so the UI updates on change rather
    /// than re-probing constantly. Resolving a CDC logger's exact kind + serial is a
    /// single GET_VER probe of just the matched port, done at connect time, not here.
    /// </summary>
    public sealed class DeviceManager : IDisposable
    {
        private readonly ILogger _log;
        private readonly UsbContext? _usb;
        private readonly object _gate = new();
        private Dictionary<string, DiscoveredDevice> _known = new();

        private CancellationTokenSource? _cts;
        private Task? _loop;

        /// <param name="usbContext">libusb context for Nanotag/Dock discovery; null = serial only.</param>
        public DeviceManager(ILogger? log = null, UsbContext? usbContext = null)
        {
            _log = log ?? NullLogger.Instance;
            _usb = usbContext;
        }

        public event Action<DiscoveredDevice>? Arrived;
        public event Action<DiscoveredDevice>? Removed;

        /// <summary>The current set of discovered devices.</summary>
        public IReadOnlyCollection<DiscoveredDevice> Current
        {
            get { lock (_gate) return _known.Values.ToArray(); }
        }

        /// <summary>Start the debounced background scan (cheap enumeration only).</summary>
        public void Start(int intervalMs = 1500)
        {
            if (_loop is { IsCompleted: false }) return;
            _cts = new CancellationTokenSource();
            _loop = Task.Run(() => LoopAsync(intervalMs, _cts.Token));
        }

        public void Stop() => _cts?.Cancel();

        private async Task LoopAsync(int intervalMs, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try { ScanOnce(); }
                catch (Exception ex) { _log.LogWarning(ex, "Device scan failed."); }
                try { await Task.Delay(intervalMs, ct); }
                catch (OperationCanceledException) { break; }
            }
        }

        /// <summary>Enumerate once, diff against the last snapshot, and raise events.</summary>
        public void ScanOnce()
        {
            var found = new Dictionary<string, DiscoveredDevice>();
            foreach (DiscoveredDevice d in EnumerateSerial().Concat(EnumerateLibUsb()))
                found[d.Key] = d;

            List<DiscoveredDevice> arrived, removed;
            lock (_gate)
            {
                arrived = found.Values.Where(d => !_known.ContainsKey(d.Key)).ToList();
                removed = _known.Values.Where(d => !found.ContainsKey(d.Key)).ToList();
                _known = found;
            }

            foreach (DiscoveredDevice d in removed) { _log.LogInformation("Removed {Dev}", d.Key); Removed?.Invoke(d); }
            foreach (DiscoveredDevice d in arrived) { _log.LogInformation("Arrived {Dev}", d.Key); Arrived?.Invoke(d); }
        }

        private static IEnumerable<DiscoveredDevice> EnumerateSerial()
        {
            foreach (SerialPortInfo p in SerialEnumerator.List())
            {
                if (!AsdDeviceIds.IsCdcLogger(p.Vid, p.Pid))
                    continue; // the filter that ends "probe every COM port"
                yield return new DiscoveredDevice(
                    AsdDeviceKind.CdcLogger, AsdBus.SerialCdc,
                    Key: "serial:" + p.PortName,
                    Vid: p.Vid, Pid: p.Pid, PortName: p.PortName,
                    SerialNumber: p.SerialNumber, Description: p.Description);
            }
        }

        private IEnumerable<DiscoveredDevice> EnumerateLibUsb()
        {
            if (_usb is null) yield break;

            UsbDeviceCollection devices;
            try { devices = _usb.UsbDevices(); }
            catch (Exception ex) { _log.LogWarning(ex, "libusb enumeration failed."); yield break; }

            foreach (UsbDevice d in devices)
            {
                AsdDeviceId? id = AsdDeviceIds.Find(d.VendorId, d.ProductId);
                if (id is null) continue; // not one of ours
                // Key by kind+vid:pid (one dock / one nanotag expected). Serial would need
                // an open, which discovery deliberately avoids.
                yield return new DiscoveredDevice(
                    id.Kind, id.Bus,
                    Key: $"usb:{id.Kind}:{d.VendorId:X4}:{d.ProductId:X4}",
                    Vid: d.VendorId, Pid: d.ProductId, Description: id.DisplayName);
            }
        }

        public void Dispose() => Stop();
    }
}
