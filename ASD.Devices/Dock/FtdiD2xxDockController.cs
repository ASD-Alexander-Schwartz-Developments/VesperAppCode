using System;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using FTD2XX_NET;

namespace ASD.Devices.Dock
{
    /// <summary>
    /// Windows dock controller using the FTDI D2XX driver (FTD2XX.Net). Faithful port of
    /// the legacy DockAdapter bit-bang: read CBUS pin states, set/clear BOOT0/VEN/NRST,
    /// write back in CBUS bit-bang mode. Only used on Windows; other platforms use
    /// <see cref="FtdiLibUsbDockController"/>.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public sealed class FtdiD2xxDockController : IDockController
    {
        private readonly FTDI _ftdi = new();
        private bool _ven, _boot0;

        public bool IsOpen => _ftdi.IsOpen;

        public string? LastError { get; private set; }

        public Task<bool> OpenAsync(string? serialNumber = null)
        {
            FTDI.FT_STATUS st = serialNumber is { Length: > 0 }
                ? _ftdi.OpenBySerialNumber(serialNumber)
                : _ftdi.OpenByIndex(0);
            LastError = st == FTDI.FT_STATUS.FT_OK ? null : $"FTDI D2XX open failed ({st}).";
            return Task.FromResult(st == FTDI.FT_STATUS.FT_OK);
        }

        public Task<bool> SetLinesAsync(bool ven, bool boot0, bool nrst)
        {
            _ven = ven; _boot0 = boot0;
            if (!_ftdi.IsOpen) return Task.FromResult(false);

            byte cbus = 0;
            if (_ftdi.GetPinStates(ref cbus) != FTDI.FT_STATUS.FT_OK)
                return Task.FromResult(false);

            cbus = boot0 ? (byte)(cbus | DockLines.Boot0) : (byte)(cbus & ~DockLines.Boot0);
            cbus = ven ? (byte)(cbus | DockLines.Ven) : (byte)(cbus & ~DockLines.Ven);
            cbus = nrst ? (byte)(cbus | DockLines.Nrst) : (byte)(cbus & ~DockLines.Nrst);

            bool ok = _ftdi.SetBitMode(cbus, FTDI.FT_BIT_MODES.FT_BIT_MODE_CBUS_BITBANG) == FTDI.FT_STATUS.FT_OK;
            return Task.FromResult(ok);
        }

        public async Task<bool> ResetAsync()
        {
            // Pulse reset, preserving VEN/BOOT0. Bit TRUE asserts NRST, FALSE releases
            // it (the device runs with all bits low) — same order and 150 ms width as
            // the proven legacy DockAdapter. Ending on TRUE would hold the device in
            // reset and take it off the USB bus entirely.
            if (!await SetLinesAsync(_ven, _boot0, true)) return false;
            await Task.Delay(150);
            return await SetLinesAsync(_ven, _boot0, false);
        }

        public Task CloseAsync()
        {
            if (_ftdi.IsOpen) _ftdi.Close();
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            if (_ftdi.IsOpen) _ftdi.Close();
        }
    }
}
