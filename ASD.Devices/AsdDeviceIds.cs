using System.Collections.Generic;
using System.Linq;

namespace ASD.Devices
{
    /// <summary>The kind of ASD device.</summary>
    public enum AsdDeviceKind { Unknown, CdcLogger, Kol, Vesper, Pipistrelle, Nanotag, Dock, StDfu, NanotagBootloader }

    /// <summary>The bus / transport a device presents on.</summary>
    public enum AsdBus { SerialCdc, LibUsbBulk, Ftdi, Hid }

    /// <summary>A known USB identity for an ASD device family.</summary>
    public sealed record AsdDeviceId(AsdDeviceKind Kind, int Vid, int Pid, AsdBus Bus, string DisplayName);

    /// <summary>
    /// Central registry of ASD USB VID/PIDs — the single place discovery filters on, so
    /// we open only our devices instead of probing every port. CDC loggers (KOL/Vesper)
    /// share a PID and are disambiguated later by the firmware GET_VER device-type byte;
    /// at the bus level they're all <see cref="AsdDeviceKind.CdcLogger"/>. Also lists the
    /// bootloaders the firmware updater talks to (ST DFU, Nanotag HID).
    /// </summary>
    public static class AsdDeviceIds
    {
        // STMicroelectronics VID.
        public const int StVid = 0x0483;        // 1155

        /// <summary>The product id licensed from ST explicitly for the ASD loggers —
        /// shared by KOL, Vesper and Pipistrelle (composite device, CDC data on MI_01,
        /// e.g. <c>USB\VID_0483&amp;PID_A4F4&amp;MI_01</c>). Devices are disambiguated by
        /// the firmware GET_VER device-type byte.</summary>
        public const int CdcLoggerPid = 0xA4F4;

        // Deprecated temporary ids used by older firmware versions — still accepted by
        // the discovery filter for backwards compatibility, never used on new firmware.
        public const int LegacyKolVesperPid = 0x5710;   // 22288
        public const int LegacyPipistrellePid = 0x570F; // 22287

        /// <summary>ST ROM DFU bootloader (KOL / Vesper / Pipistrelle): entered through the
        /// dock BOOT0 line, or dockless via VESPER_ENTER_BOOTLOADER (64) on KOL.</summary>
        public const int StDfuPid = 0xDF11;

        // Microchip VID (Nanotag) and the Nanotag HID bootloader (AN1388).
        public const int MicrochipVid = 0x04D8;
        public const int NanotagBootloaderPid = 0x003C;

        /// <summary>Bus-level identity of any CDC logger on the shared PID. The kind is
        /// unknown until the firmware GET_VER device-type byte is read.</summary>
        public static readonly AsdDeviceId CdcLogger = new(AsdDeviceKind.CdcLogger, StVid, CdcLoggerPid, AsdBus.SerialCdc, "ASD logger");

        public static readonly AsdDeviceId Kol = new(AsdDeviceKind.Kol, StVid, CdcLoggerPid, AsdBus.SerialCdc, "KOL");
        public static readonly AsdDeviceId Vesper = new(AsdDeviceKind.Vesper, StVid, CdcLoggerPid, AsdBus.SerialCdc, "VT04-VESPER");
        public static readonly AsdDeviceId Pipistrelle = new(AsdDeviceKind.Pipistrelle, StVid, CdcLoggerPid, AsdBus.SerialCdc, "VT04-PP");
        public static readonly AsdDeviceId Nanotag = new(AsdDeviceKind.Nanotag, MicrochipVid, 0xFE57, AsdBus.LibUsbBulk, "Nanotag");
        public static readonly AsdDeviceId Dock = new(AsdDeviceKind.Dock, 0x0403, 0x6001, AsdBus.Ftdi, "Dock");
        public static readonly AsdDeviceId StDfu = new(AsdDeviceKind.StDfu, StVid, StDfuPid, AsdBus.LibUsbBulk, "STM32 DFU bootloader");
        public static readonly AsdDeviceId NanotagBootloader = new(AsdDeviceKind.NanotagBootloader, MicrochipVid, NanotagBootloaderPid, AsdBus.Hid, "Nanotag bootloader");

        /// <summary>Every known identity. KOL, Vesper and Pipistrelle share one VID:PID;
        /// <see cref="Find"/> maps that pair to <see cref="CdcLogger"/>, not to a family.</summary>
        public static readonly IReadOnlyList<AsdDeviceId> All =
            new[] { Kol, Vesper, Pipistrelle, Nanotag, Dock, StDfu, NanotagBootloader };

        /// <summary>True when (vid,pid) is one of OUR CDC loggers — the filter that
        /// replaces "open and probe every COM port".</summary>
        public static bool IsCdcLogger(int? vid, int? pid) =>
            vid == StVid && (pid == CdcLoggerPid || pid == LegacyKolVesperPid || pid == LegacyPipistrellePid);

        /// <summary>The libusb (non-serial) device ids we look for.</summary>
        public static IEnumerable<AsdDeviceId> LibUsbIds =>
            All.Where(d => d.Bus is AsdBus.LibUsbBulk or AsdBus.Ftdi);

        /// <summary>The identity for (vid,pid). The shared CDC logger pair returns
        /// <see cref="CdcLogger"/> (KOL vs Vesper vs Pipistrelle needs GET_VER).</summary>
        public static AsdDeviceId? Find(int vid, int pid) =>
            vid == StVid && pid == CdcLoggerPid
                ? CdcLogger
                : All.FirstOrDefault(d => d.Vid == vid && d.Pid == pid);
    }
}
