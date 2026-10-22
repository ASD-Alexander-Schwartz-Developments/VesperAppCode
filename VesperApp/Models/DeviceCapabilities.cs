using System;

namespace VesperApp.Models
{
    /// <summary>
    /// Per-product hardware capabilities used to drive the device tests. Single source
    /// of truth so test panels don't bake in product assumptions.
    ///
    /// Microphones: KOL is the 4-mic array; Vesper and Pipistrelle each carry a single
    /// MEMS mic; Nanotag has none. Update the maps here if a product's hardware changes.
    /// </summary>
    public static class DeviceCapabilities
    {
        /// <summary>Number of microphones the product physically has (0 = none / unknown).</summary>
        public static int MicCount(DeviceTypes? product) => product switch
        {
            DeviceTypes.Kol => 4,
            DeviceTypes.Vesper => 1,
            DeviceTypes.Pipistrelle => 1,
            _ => 0, // Nanotag, null and anything unknown: no microphone
        };

        /// <summary>Whether the product has at least one microphone to test.</summary>
        public static bool HasMicrophones(DeviceTypes? product) => MicCount(product) >= 1;

        /// <summary>
        /// Mic-count choices to offer in the UI for a product. KOL ships in 1/2/4-mic
        /// variants so all three are valid; single-mic products offer only 1; products
        /// with no mic offer nothing.
        /// </summary>
        public static int[] MicCountOptions(DeviceTypes? product) => product switch
        {
            DeviceTypes.Kol => new[] { 1, 2, 4 },
            _ when MicCount(product) >= 1 => new[] { 1 },
            _ => Array.Empty<int>(),
        };

        /// <summary>
        /// Whether the product has a GNSS receiver the RF/positioning device test can
        /// exercise (the in-FW VESPER_GPS_SELFTEST). Confirmed for Vesper (VT04-VESPER);
        /// add other products here if they gain GNSS.
        /// </summary>
        public static bool HasGnss(DeviceTypes? product) => product switch
        {
            DeviceTypes.Vesper => true,
            _ => false,
        };

        /// <summary>
        /// First Vesper / Pipistrelle firmware whose console ids 62-66 are the USB mode / DFU entry / mic
        /// parameter commands (as on KOL). Older VesperU5 firmware uses 62-67 for dock bench tests, so these
        /// commands must never reach it. This MUST follow the Vesper version decision: if the v1.20
        /// disk-corruption hotfix ships as 1.21, the audio-platform firmware becomes 1.22 and so must this.
        /// </summary>
        public static readonly Version VesperUsbModesFirmware = new(1, 21);

        /// <summary>
        /// Whether the product family can expose host-commanded USB operational modes
        /// (<see cref="KolUsbMode"/>: Idle / Sampling / Live Audio): KOL, and Vesper / Pipistrelle
        /// from <see cref="VesperUsbModesFirmware"/>. Use <see cref="SupportsUsbModes"/> for a unit.
        /// </summary>
        public static bool HasUsbModes(DeviceTypes? product) =>
            product is DeviceTypes.Kol or DeviceTypes.Vesper or DeviceTypes.Pipistrelle;

        /// <summary>
        /// Whether this unit answers the USB mode commands (62-66: SET/GET_USB_MODE, ENTER_BOOTLOADER,
        /// SET/GET_MIC_PARAMS): any KOL (unchanged), a Vesper / Pipistrelle only with firmware
        /// <see cref="VesperUsbModesFirmware"/> or later (unknown firmware = no).
        /// </summary>
        public static bool SupportsUsbModes(DeviceTypes? product, Version? firmware) => product switch
        {
            DeviceTypes.Kol => true,
            DeviceTypes.Vesper or DeviceTypes.Pipistrelle => firmware != null && firmware >= VesperUsbModesFirmware,
            _ => false,
        };

        /// <summary>Live Audio channels the unit streams as a USB microphone (one per microphone).</summary>
        public static int LiveAudioChannels(DeviceTypes? product) => MicCount(product);

        /// <summary>Name the unit's Live Audio microphone has on the host (USB product string).</summary>
        public static string LiveAudioDeviceName(DeviceTypes? product) => product switch
        {
            DeviceTypes.Kol => "KOL",
            DeviceTypes.Vesper or DeviceTypes.Pipistrelle => "Vesper Wildlife Logger",
            _ => string.Empty,
        };

        /// <summary>Product name for user-facing texts.</summary>
        public static string DisplayName(DeviceTypes? product) => product switch
        {
            DeviceTypes.Kol => "KOL",
            DeviceTypes.Vesper => "Vesper",
            DeviceTypes.Pipistrelle => "Pipistrelle",
            DeviceTypes.Nanotag => "Nanotag",
            _ => "device",
        };

        /// <summary>
        /// Whether firmware is flashed straight over the device's own USB connection
        /// (no docking station): Nanotag (HID bootloader) and KOL (commanded ST DFU
        /// entry). VT04-VESPER / VT04-PP need the dock's BOOT0/reset lines unless the
        /// unit already runs firmware with commanded DFU entry (<see cref="SupportsUsbModes"/>).
        /// </summary>
        public static bool FlashesWithoutDock(DeviceTypes? product, Version? firmware = null) =>
            product == DeviceTypes.Nanotag || SupportsUsbModes(product, firmware);
    }
}
