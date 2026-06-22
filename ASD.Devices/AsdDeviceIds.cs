using System.Collections.Generic;
using System.Linq;

namespace ASD.Devices
{
    /// <summary>The kind of ASD device.</summary>
    public enum AsdDeviceKind { Unknown, CdcLogger, Kol, Vesper, Pipistrelle, Nanotag, Dock }

    /// <summary>The bus / transport a device presents on.</summary>
    public enum AsdBus { SerialCdc, LibUsbBulk, Ftdi }

    /// <summary>A known USB identity for an ASD device family.</summary>
    public sealed record AsdDeviceId(AsdDeviceKind Kind, int Vid, int Pid, AsdBus Bus, string DisplayName);

    /// <summary>
    /// Central registry of ASD USB VID/PIDs — the single place discovery filters on, so
    /// we open only our devices instead of probing every port. CDC loggers (KOL/Vesper)
    /// share a PID and are disambiguated later by the firmware GET_VER device-type byte;
    /// at the bus level they're all <see cref="AsdDeviceKind.CdcLogger"/>.
    /// </summary>
    public static class AsdDeviceIds
    {
        // STMicroelectronics VID; STM32U5 USB-CDC product ids.
        public const int StVid = 0x0483;        // 1155
        public const int KolVesperPid = 0x5710; // 22288 (KOL and Vesper share this)
        public const int PipistrellePid = 0x570F; // 22287

        public static readonly AsdDeviceId Kol = new(AsdDeviceKind.Kol, StVid, KolVesperPid, AsdBus.SerialCdc, "KOL");
        public static readonly AsdDeviceId Vesper = new(AsdDeviceKind.Vesper, StVid, KolVesperPid, AsdBus.SerialCdc, "VT04-VESPER");
        public static readonly AsdDeviceId Pipistrelle = new(AsdDeviceKind.Pipistrelle, StVid, PipistrellePid, AsdBus.SerialCdc, "VT04-PP");
        public static readonly AsdDeviceId Nanotag = new(AsdDeviceKind.Nanotag, 0x04D8, 0xFE57, AsdBus.LibUsbBulk, "Nanotag");
        public static readonly AsdDeviceId Dock = new(AsdDeviceKind.Dock, 0x0403, 0x6001, AsdBus.Ftdi, "Dock");

        public static readonly IReadOnlyList<AsdDeviceId> All =
            new[] { Kol, Pipistrelle, Nanotag, Dock };

        /// <summary>True when (vid,pid) is one of OUR CDC loggers — the filter that
        /// replaces "open and probe every COM port".</summary>
        public static bool IsCdcLogger(int? vid, int? pid) =>
            vid == StVid && (pid == KolVesperPid || pid == PipistrellePid);

        /// <summary>The libusb (non-serial) device ids we look for.</summary>
        public static IEnumerable<AsdDeviceId> LibUsbIds =>
            All.Where(d => d.Bus is AsdBus.LibUsbBulk or AsdBus.Ftdi);

        public static AsdDeviceId? Find(int vid, int pid) =>
            All.FirstOrDefault(d => d.Vid == vid && d.Pid == pid);
    }
}
