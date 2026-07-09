using System;
using System.Linq;
using System.Threading.Tasks;
using ASDLibUSBWrapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ASD.Devices.Dock
{
    /// <summary>
    /// Cross-platform (Linux / macOS / Windows) dock controller that drives the FTDI
    /// CBUS lines via libusb vendor control transfers — the libftdi approach — instead
    /// of the Windows-only D2XX driver. This is what makes the dock work off Windows.
    ///
    /// <para><b>Bench validation:</b> the FTDI vendor request constants and the CBUS
    /// bit-bang nibble layout below follow the documented FTDI/libftdi protocol, but the
    /// exact CBUS pin mapping depends on the dock's FT2232H EEPROM configuration. Verify
    /// against a real dock before trusting in production (analogous to validating D2XX
    /// behaviour). The line→bit assignment matches <see cref="DockLines"/>, shared with
    /// the D2XX controller.</para>
    /// </summary>
    public sealed class FtdiLibUsbDockController : IDockController
    {
        // FTDI vendor requests (libftdi).
        private const byte ReqTypeOut = 0x40; // vendor, host-to-device, device
        private const byte ReqTypeIn = 0xC0;  // vendor, device-to-host, device
        private const byte SioSetBitmode = 0x0B;
        private const byte SioReadPins = 0x0C;
        private const byte BitmodeCbus = 0x20; // CBUS bit-bang mode

        private readonly UsbContext _context;
        private readonly ILogger _log;
        private readonly int _portIndex; // FT2232H port A = 1, B = 2; single-port = 1
        private UsbDevice? _device;
        private bool _ven, _boot0;

        public FtdiLibUsbDockController(UsbContext context, ILogger? log = null, int portIndex = 1)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _log = log ?? NullLogger.Instance;
            _portIndex = portIndex;
        }

        public bool IsOpen => _device?.IsOpen == true;

        public string? LastError { get; private set; }

        public Task<bool> OpenAsync(string? serialNumber = null)
        {
            LastError = null;
            bool sawDock = false;
            foreach (UsbDevice d in _context.UsbDevices())
            {
                if (d.VendorId != AsdDeviceIds.Dock.Vid || d.ProductId != AsdDeviceIds.Dock.Pid)
                    continue;
                sawDock = true;
                if (!d.TryOpen())
                {
                    LastError = "the dock was found but could not be opened — on Linux this "
                        + "usually means the udev rules are not installed (or the dock was not replugged).";
                    continue;
                }
                try
                {
                    if (serialNumber is { Length: > 0 } && d.Info?.SerialNumber != serialNumber)
                    {
                        d.Close();
                        continue;
                    }
                    try { d.SetAutoDetachKernelDriver(true); } catch { }
                    // On Linux, ftdi_sio binds to the dock at plug time. While a kernel
                    // driver holds an interface, SET_CONFIGURATION fails with
                    // LIBUSB_ERROR_BUSY — auto-detach only applies at claim time — so
                    // detach explicitly before touching the configuration.
                    try
                    {
                        if (d.IsKernelDriverActive(0))
                            d.DetachKernelDriver(0);
                    }
                    catch (Exception ex)
                    {
                        _log.LogWarning(ex, "Kernel driver detach failed; continuing.");
                    }
                    d.SetConfiguration(1);
                    d.ClaimInterface(0); // FTDI port A control
                    _device = d;
                    LastError = null;
                    return Task.FromResult(true);
                }
                catch (Exception ex)
                {
                    LastError = ex.Message;
                    _log.LogWarning(ex, "Failed to open dock over libusb.");
                    try { d.Close(); } catch { }
                }
            }
            if (!sawDock)
                LastError = "no dock was found on the USB bus.";
            return Task.FromResult(false);
        }

        public Task<bool> SetLinesAsync(bool ven, bool boot0, bool nrst)
        {
            _ven = ven; _boot0 = boot0;
            if (_device is null || !_device.IsOpen) return Task.FromResult(false);

            byte value = DockLines.Compose(ven, boot0, nrst);
            // CBUS bit-bang mask byte: high nibble = output-enable, low nibble = value.
            // Drive CBUS1..3 as outputs (0xE0) with the composed value.
            byte mask = (byte)(0xE0 | (value & 0x0F));
            ushort wValue = (ushort)((BitmodeCbus << 8) | mask);

            try
            {
                var setup = new UsbSetupPacket(ReqTypeOut, SioSetBitmode, wValue, _portIndex, 0);
                _device.ControlTransfer(setup);
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "FTDI SET_BITMODE control transfer failed.");
                return Task.FromResult(false);
            }
        }

        /// <summary>Read the current CBUS pin states (libftdi READ_PINS); best-effort.</summary>
        public byte? ReadPins()
        {
            if (_device is null || !_device.IsOpen) return null;
            try
            {
                var buf = new byte[1];
                var setup = new UsbSetupPacket(ReqTypeIn, SioReadPins, 0, _portIndex, buf.Length);
                int n = _device.ControlTransfer(setup, buf, 0, buf.Length);
                return n >= 1 ? buf[0] : (byte?)null;
            }
            catch { return null; }
        }

        public async Task<bool> ResetAsync()
        {
            // Bit TRUE asserts NRST, FALSE releases (device runs with all bits low) —
            // same order and 150 ms width as the proven legacy DockAdapter pulse.
            if (!await SetLinesAsync(_ven, _boot0, true)) return false;
            await Task.Delay(150);
            return await SetLinesAsync(_ven, _boot0, false);
        }

        public Task CloseAsync()
        {
            try { _device?.Close(); } catch { }
            _device = null;
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            try { _device?.Close(); } catch { }
            _device = null;
        }
    }
}
