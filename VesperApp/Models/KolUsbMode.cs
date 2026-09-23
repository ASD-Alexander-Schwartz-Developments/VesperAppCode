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
        /// afterwards by design.</summary>
        Sampling = 1,

        /// <summary>CDC console + UAC2 microphone (4 ch × 16-bit, 8–96 kHz). No disk.
        /// The OS sees a standard USB microphone; nothing to implement host-side for
        /// playback/recording.</summary>
        LiveAudio = 2,
    }

    /// <summary>Reply of <c>VESPER_GET_USB_MODE</c>: the active mode and the one a
    /// pending switch will apply.</summary>
    public sealed record KolUsbModeState(KolUsbMode Active, KolUsbMode Pending)
    {
        public bool IsSwitching => Active != Pending;

        public static KolUsbModeState? Parse(byte[]? payload)
        {
            if (payload is null || payload.Length < 2) return null;
            return new KolUsbModeState(ToMode(payload[0]), ToMode(payload[1]));
        }

        private static KolUsbMode ToMode(byte b) =>
            Enum.IsDefined(typeof(KolUsbMode), b) ? (KolUsbMode)b : KolUsbMode.Idle;
    }

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
}
