using System;
using ASDLibUSBWrapper;
using Microsoft.Extensions.Logging;

namespace ASD.Devices.Dock
{
    /// <summary>
    /// Picks the right <see cref="IDockController"/> for the platform: the FTDI D2XX
    /// driver on Windows (the proven path), or the cross-platform libusb FTDI controller
    /// elsewhere. Callers depend only on <see cref="IDockController"/>.
    /// </summary>
    public static class DockControllerFactory
    {
        /// <param name="usbContext">libusb context, required on non-Windows; ignored on Windows.</param>
        public static IDockController Create(UsbContext? usbContext = null, ILogger? log = null)
        {
            if (OperatingSystem.IsWindows())
                return new FtdiD2xxDockController();

            if (usbContext is null)
                throw new InvalidOperationException(
                    "A libusb UsbContext is required to control the dock on non-Windows platforms.");
            return new FtdiLibUsbDockController(usbContext, log);
        }
    }
}
