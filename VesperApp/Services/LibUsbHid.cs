using System;
using System.IO;
using ASDLibUSBWrapper;

namespace VesperApp.Services
{
    /// <summary>
    /// HID report transport over libusb, for Linux/macOS — where (unlike Windows) the kernel
    /// HID driver can be detached and the device's interrupt endpoints driven directly. Reuses
    /// the project's ASDLibUSBWrapper. Reports are 64-byte interrupt transfers on EP1 (no
    /// report-id byte, which is a Windows-HID-API artifact).
    /// </summary>
    internal sealed class LibUsbHidDevice : IHidReportTransport
    {
        private readonly UsbContext _context;
        private readonly UsbDevice _device;
        private readonly UsbEndpointWriter _writer;
        private readonly UsbEndpointReader _reader;
        private readonly int _interfaceId;

        public int ReportSize { get; }

        private LibUsbHidDevice(UsbContext ctx, UsbDevice dev, UsbEndpointWriter w, UsbEndpointReader r, int iface, int size)
        {
            _context = ctx; _device = dev; _writer = w; _reader = r; _interfaceId = iface; ReportSize = size;
        }

        public static LibUsbHidDevice? Open(int vid, int pid)
        {
            var ctx = new UsbContext();
            UsbDevice? dev = null;
            foreach (IUsbDevice d in ctx.FindMultipleDevices(x => x.VendorId == vid && x.ProductId == pid))
            {
                dev = d as UsbDevice;
                break;
            }
            if (dev == null) { ctx.Dispose(); return null; }

            try
            {
                dev.Open();
                try { dev.SetAutoDetachKernelDriver(true); } catch { /* not all platforms */ }

                var cfg = dev.Configs[0];
                dev.SetConfiguration(cfg.ConfigurationValue);

                int iface = cfg.Interfaces.Count > 0 ? cfg.Interfaces[0].Number : 0;
                try { dev.DetachKernelDriver(iface); } catch { /* may already be detached */ }
                dev.ClaimInterface(iface);

                int size = 64;
                var rd = dev.OpenEndpointReader(ReadEndpointID.Ep01, size, EndpointType.Interrupt);
                var wr = dev.OpenEndpointWriter(WriteEndpointID.Ep01, EndpointType.Interrupt);

                return new LibUsbHidDevice(ctx, dev, wr, rd, iface, size);
            }
            catch
            {
                try { dev.Close(); } catch { }
                ctx.Dispose();
                return null;
            }
        }

        public void WriteReport(byte[] payload)
        {
            byte[] buf = payload.Length == ReportSize ? payload : Fit(payload, ReportSize);
            LibUsbError err = _writer.Write(buf, 2000, out _);
            if (err != LibUsbError.Success)
                throw new IOException("HID interrupt write failed: " + err);
        }

        public bool ReadReport(byte[] payload, int timeoutMs)
        {
            byte[] tmp = new byte[ReportSize];
            LibUsbError err = _reader.Read(tmp, timeoutMs, out int n);
            if (err != LibUsbError.Success || n <= 0)
                return false;
            Array.Copy(tmp, payload, Math.Min(payload.Length, ReportSize));
            return true;
        }

        private static byte[] Fit(byte[] src, int size)
        {
            var b = new byte[size];
            Array.Copy(src, b, Math.Min(src.Length, size));
            return b;
        }

        public void Dispose()
        {
            try { _device.ReleaseInterface(_interfaceId); } catch { }
            try { _device.Close(); } catch { }
            try { _context.Dispose(); } catch { }
        }
    }
}
