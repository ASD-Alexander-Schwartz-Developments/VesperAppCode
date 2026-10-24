using System;

namespace VesperApp.Models
{
    /// <summary>
    /// KOL USB operational modes — the wire payload of <c>VESPER_SET_USB_MODE</c> (62)
    /// and the two bytes of the <c>VESPER_GET_USB_MODE</c> (63) reply. Values match the
    /// firmware's <c>usb_mode_t</c> (KOL <c>Core/Inc/usb_mode.h</c>); do not renumber.
    /// Contract: asd-interfaces <c>interfaces/boot-and-modes.md</c>.
    /// </summary>
    public enum KolUsbMode : byte
    {
        /// <summary>CDC console + USB disk (MSC). Boot default when not armed.</summary>
        Idle = 0,

        /// <summary>No USB at all — the device is armed, resets and runs its schedule.
        /// Entered through the firmware's arm+reset path; the device is gone from USB
        /// afterwards by design. On a KOL with operating states this is "Recording".</summary>
        Sampling = 1,

        /// <summary>CDC console + UAC2 microphone (4 ch × 16-bit, 8–96 kHz). No disk.
        /// The OS sees a standard USB microphone; nothing to implement host-side for
        /// playback/recording.</summary>
        LiveAudio = 2,
    }

    /// <summary>
    /// KOL status flags — <c>VESPER_GET_USB_MODE</c> reply byte [2] on firmware with the
    /// operating states (Sleep / Idle / Live Audio / Recording, no magnet). Values match
    /// <c>KOL_ST_*</c> in KOL <c>Core/Inc/kol_state.h</c>.
    /// </summary>
    [Flags]
    public enum KolStatusFlags : byte
    {
        None = 0,
        CardInserted = 0x01,
        CardMounted = 0x02,
        ConfigOk = 0x04,
        RtcSet = 0x08,
        RecordReady = 0x10,
        /// <summary>The board has shown a VBUS edge: it sleeps when unplugged (boards without
        /// the VBUS divider fix never do and idle on battery instead).</summary>
        VbusSense = 0x20,
        VbusPresent = 0x40,
        Host = 0x80,
    }

    /// <summary>
    /// Why a KOL cannot start recording — <c>VESPER_GET_USB_MODE</c> reply byte [3] and the
    /// one-byte NACK payload of a refused record request. Values match <c>kol_rec_reason_t</c>.
    /// </summary>
    public enum KolRecordReason : byte
    {
        Ok = 0,
        NoCard = 1,
        CardError = 2,
        NoConfig = 3,
        NoSensor = 4,
        BadSchedule = 5,
        NoClock = 6,
        DiskFull = 7,
    }

    /// <summary>Reply of <c>VESPER_GET_USB_MODE</c>: the active mode and the one a
    /// pending switch will apply; on KOL firmware with operating states also the status
    /// flags and why recording cannot start.</summary>
    public sealed record KolUsbModeState(KolUsbMode Active, KolUsbMode Pending,
                                         KolStatusFlags? Flags = null, KolRecordReason? Reason = null)
    {
        public bool IsSwitching => Active != Pending;

        /// <summary>The unit runs the KOL operating states (4-byte reply).</summary>
        public bool HasStatus => Flags.HasValue;

        public static KolUsbModeState? Parse(byte[]? payload)
        {
            if (payload is null || payload.Length < 2) return null;
            if (payload.Length < 4)
                return new KolUsbModeState(ToMode(payload[0]), ToMode(payload[1]));
            return new KolUsbModeState(ToMode(payload[0]), ToMode(payload[1]),
                                       (KolStatusFlags)payload[2], KolRecordReasonInfo.From(payload[3]));
        }

        private static KolUsbMode ToMode(byte b) =>
            Enum.IsDefined(typeof(KolUsbMode), b) ? (KolUsbMode)b : KolUsbMode.Idle;
    }

    /// <summary>Outcome of a KOL record request: ACKed (the unit leaves USB and records),
    /// or refused with the reason the firmware gave (null when it gave none - older firmware,
    /// or no reply at all).</summary>
    public sealed record KolRecordResult(bool Started, KolRecordReason? Reason, bool Answered);

    public static class KolUsbModeInfo
    {
        /// <summary>User-facing label for a mode (or "unknown" when not yet read).</summary>
        public static string Label(KolUsbMode? mode) => mode switch
        {
            KolUsbMode.Idle => "Idle (disk + console)",
            KolUsbMode.Sampling => "Sampling (off USB)",
            KolUsbMode.LiveAudio => "Live Audio (USB microphone)",
            _ => "unknown",
        };

        /// <summary>Whether the USB disk (MSC) is exposed in this mode.</summary>
        public static bool HasDisk(KolUsbMode? mode) => mode is null || mode == KolUsbMode.Idle;
    }

    public static class KolRecordReasonInfo
    {
        public static KolRecordReason From(byte b) =>
            Enum.IsDefined(typeof(KolRecordReason), b) ? (KolRecordReason)b : KolRecordReason.CardError;

        /// <summary>Short status, e.g. for the readiness pill.</summary>
        public static string Short(KolRecordReason reason) => reason switch
        {
            KolRecordReason.Ok => "Ready to record",
            KolRecordReason.NoCard => "No SD card",
            KolRecordReason.CardError => "SD card not readable",
            KolRecordReason.NoConfig => "No valid config.json",
            KolRecordReason.NoSensor => "No sensor enabled",
            KolRecordReason.BadSchedule => "Schedule invalid",
            KolRecordReason.NoClock => "Clock not set",
            KolRecordReason.DiskFull => "SD card full",
            _ => "Not ready",
        };

        /// <summary>What to do about it.</summary>
        public static string Advice(KolRecordReason reason) => reason switch
        {
            KolRecordReason.NoCard => "Insert a microSD card. The KOL restarts and mounts it by itself (LED changes from fast blinking to 1 Hz).",
            KolRecordReason.CardError => "The card is inserted but cannot be read. Check it on a PC, or format it (FAT32 or exFAT), copy config.json back and reinsert it.",
            KolRecordReason.NoConfig => "Put a config.json made with the Configuration editor in the root of the KOL's USB disk.",
            KolRecordReason.NoSensor => "config.json has no sensor with a sample rate in configuration 1 or 2. Enable the microphones (or another sensor) in the Configuration editor.",
            KolRecordReason.BadSchedule => "The schedule in config.json has too many entries. Shorten it in the Configuration editor.",
            KolRecordReason.NoClock => "The KOL's clock has not been set since its battery was connected. Press Set Time, then start again.",
            KolRecordReason.DiskFull => "There is less free space than one recording file. Download the recordings and free up space.",
            _ => string.Empty,
        };
    }
}
