using System;

namespace VesperApp.Services.Time
{
    /// <summary>Which time a logger's clock keeps (asd-interfaces interfaces/nanotag-usb-vendor.md § Clock base).</summary>
    public enum ClockBaseKind : byte { Unknown = 0, Utc = 1, Local = 2 }

    /// <summary>
    /// The base of a logger clock: UTC, or a local time with its offset fixed when the clock was set (the logger does
    /// not follow daylight-saving changes, so UTC = device time - zone for the whole deployment). The logger stores
    /// it with its clock and stamps it into its data, so whoever decodes gets UTC on any PC.
    /// </summary>
    public readonly record struct DeviceClockBase(ClockBaseKind Kind, short ZoneMinutes)
    {
        public static DeviceClockBase Unknown => new(ClockBaseKind.Unknown, 0);
        public static DeviceClockBase Utc => new(ClockBaseKind.Utc, 0);

        /// <summary>The base for a clock set at <paramref name="trueUtc"/> with the "Logger clock" setting.</summary>
        public static DeviceClockBase For(bool deviceUtc, DateTime trueUtc) =>
            deviceUtc ? Utc : new(ClockBaseKind.Local, (short)Math.Round(TimeZoneInfo.Local.GetUtcOffset(trueUtc).TotalMinutes));

        /// <summary>From the wire: an unknown kind or an out-of-range zone reads as unknown.</summary>
        public static DeviceClockBase FromWire(byte kind, short zoneMinutes) => kind switch
        {
            (byte)ClockBaseKind.Utc => Utc,
            (byte)ClockBaseKind.Local when Math.Abs((int)zoneMinutes) <= 14 * 60 => new(ClockBaseKind.Local, zoneMinutes),
            _ => Unknown,
        };

        public bool IsKnown => Kind != ClockBaseKind.Unknown;

        /// <summary>UTC of a device time, or null when the base is unknown.</summary>
        public DateTime? ToUtc(DateTime deviceTime) =>
            IsKnown ? DateTime.SpecifyKind(deviceTime.AddMinutes(-ZoneMinutes), DateTimeKind.Utc) : null;

        /// <summary>The base for the user: "UTC", "local time (UTC+02:00)" or "UTC or local not known".</summary>
        public string Describe() => Kind switch
        {
            ClockBaseKind.Utc => "UTC",
            ClockBaseKind.Local => $"local time (UTC{(ZoneMinutes < 0 ? '-' : '+')}{Math.Abs((int)ZoneMinutes) / 60:00}:{Math.Abs((int)ZoneMinutes) % 60:00})",
            _ => "UTC or local not known",
        };
    }
}
