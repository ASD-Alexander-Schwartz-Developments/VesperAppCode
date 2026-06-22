using System;
using System.Threading.Tasks;

namespace ASD.Devices.Dock
{
    /// <summary>
    /// Controls the ASD dock's STM32 boot/power lines (VEN, BOOT0, NRST) regardless of
    /// the underlying FTDI access method. Two implementations:
    /// <see cref="FtdiD2xxDockController"/> (Windows, FTD2XX.Net) and
    /// <see cref="FtdiLibUsbDockController"/> (cross-platform, FTDI vendor control
    /// transfers over libusb). The shell talks to this interface only, so the dock is
    /// no longer Windows-only.
    /// </summary>
    public interface IDockController : IDisposable
    {
        bool IsOpen { get; }

        /// <summary>Open the dock by its serial number (or the first dock if null).</summary>
        Task<bool> OpenAsync(string? serialNumber = null);

        /// <summary>Drive the three control lines via CBUS bit-bang.</summary>
        Task<bool> SetLinesAsync(bool ven, bool boot0, bool nrst);

        /// <summary>Pulse NRST low→high to reset the target.</summary>
        Task<bool> ResetAsync();

        Task CloseAsync();
    }

    /// <summary>
    /// CBUS bit assignment for the dock's control lines, shared by both controllers.
    /// Matches the legacy FTD2XX bit-bang in DockAdapter (BOOT0=bit1, VEN=bit2, NRST=bit3).
    /// </summary>
    public static class DockLines
    {
        public const byte Boot0 = 0x02; // CBUS1
        public const byte Ven = 0x04;   // CBUS2
        public const byte Nrst = 0x08;  // CBUS3
        public const byte Mask = Boot0 | Ven | Nrst;

        /// <summary>Compose the CBUS value nibble from the three logical lines.</summary>
        public static byte Compose(bool ven, bool boot0, bool nrst)
        {
            byte v = 0;
            if (boot0) v |= Boot0;
            if (ven) v |= Ven;
            if (nrst) v |= Nrst;
            return v;
        }
    }
}
