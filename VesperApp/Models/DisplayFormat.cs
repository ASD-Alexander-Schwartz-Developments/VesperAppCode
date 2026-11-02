using System;
using System.Globalization;
using VesperApp.Services.Time;

namespace VesperApp.Models
{
    /// <summary>
    /// How dates, clocks and device values are shown on screen, so every view reads the same way:
    /// ISO-like dates ("2026-11-02 13:13:18") as in the Recordings browser and the decoded files,
    /// whatever the Windows regional settings are.
    /// </summary>
    public static class DisplayFormat
    {
        public const string DateTimeSeconds = "yyyy-MM-dd HH:mm:ss";
        public const string DateTimeMinutes = "yyyy-MM-dd HH:mm";

        public static string DateTime(DateTime t) => t.ToString(DateTimeSeconds, CultureInfo.InvariantCulture);

        /// <summary>A feed's ISO-8601 publish stamp in this PC's local time, minutes only; the raw text if it does not parse.</summary>
        public static string Published(string? iso)
        {
            if (string.IsNullOrWhiteSpace(iso)) return string.Empty;
            return DateTimeOffset.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset t)
                ? t.ToLocalTime().ToString(DateTimeMinutes, CultureInfo.InvariantCulture)
                : iso;
        }

        /// <summary>Short clock base for the device list: "UTC", "Local UTC+02:00" or "Unknown".</summary>
        public static string ClockBase(DeviceClockBase b) => b.Kind switch
        {
            ClockBaseKind.Utc => "UTC",
            ClockBaseKind.Local => "Local " + UtcOffset(b.ZoneMinutes),
            _ => "Unknown",
        };

        public static string UtcOffset(int minutes) =>
            $"UTC{(minutes < 0 ? '-' : '+')}{Math.Abs(minutes) / 60:00}:{Math.Abs(minutes) % 60:00}";

        /// <summary>
        /// Storage cell: "512 MB, 45% free", or the download progress while a download runs.
        /// <paramref name="freePercent"/> null = not reported by this device type.
        /// </summary>
        public static string Disk(double sizeGb, double? freePercent, double? downloadPercent)
        {
            if (downloadPercent > 0 && downloadPercent < 100)
                return $"Downloading {downloadPercent.Value.ToString("0", CultureInfo.InvariantCulture)}%";
            string size = sizeGb <= 0 ? string.Empty
                : sizeGb < 1 ? (sizeGb * 1024).ToString("0", CultureInfo.InvariantCulture) + " MB"
                : sizeGb.ToString("0.#", CultureInfo.InvariantCulture) + " GB";
            if (freePercent == null) return size;
            string free = freePercent.Value.ToString("0", CultureInfo.InvariantCulture) + "% free";
            return size.Length == 0 ? free : size + ", " + free;
        }
    }
}
