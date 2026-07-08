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
    }
}
