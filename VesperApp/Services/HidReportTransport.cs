using System;

namespace VesperApp.Services
{
    /// <summary>
    /// Platform-neutral HID report channel for the Nanotag bootloader. Reports here are the
    /// raw report payload (no OS report-id byte); each implementation handles the report-id
    /// framing (Windows HID API) or raw interrupt transfers (libusb) underneath.
    /// </summary>
    internal interface IHidReportTransport : IDisposable
    {
        /// <summary>Payload bytes per report (64 for the Nanotag bootloader).</summary>
        int ReportSize { get; }

        /// <summary>Send one report payload (padded/truncated to <see cref="ReportSize"/>).</summary>
        void WriteReport(byte[] payload);

        /// <summary>Receive one report payload into <paramref name="payload"/> (length == <see cref="ReportSize"/>).</summary>
        bool ReadReport(byte[] payload, int timeoutMs);
    }

    internal static class HidReportTransport
    {
        /// <summary>
        /// Opens the bootloader's HID interface. On Windows the device is owned by hidusb.sys,
        /// so the Win32 HID API is used; on Linux/macOS libusb can detach the kernel driver and
        /// drive the interrupt endpoints directly, so the project's libusb layer is reused.
        /// </summary>
        public static IHidReportTransport? Open(int vid, int pid)
            => OperatingSystem.IsWindows()
                ? Win32HidDevice.Open(vid, pid)
                : LibUsbHidDevice.Open(vid, pid);
    }
}
