using VesperApp.Services.Time;
using Xunit;

namespace VesperApp.Tests.Services;

/// <summary>
/// Logger clock base (asd-interfaces nanotag-usb-vendor.md § Clock base, decision 2026-11-01): UTC or local with a
/// fixed offset, stored on the logger and in its data; the conversions and the user-facing wording.
/// </summary>
public class DeviceClockBaseTests
{
    [Fact]
    public void Utc_IsZoneZero_AndConvertsUnchanged()
    {
        var b = DeviceClockBase.For(deviceUtc: true, new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc));
        Assert.Equal(ClockBaseKind.Utc, b.Kind);
        Assert.Equal(0, b.ZoneMinutes);
        Assert.Equal(new DateTime(2026, 11, 1, 9, 14, 48), b.ToUtc(new DateTime(2026, 11, 1, 9, 14, 48)));
        Assert.Equal(DateTimeKind.Utc, b.ToUtc(new DateTime(2026, 11, 1, 9, 14, 48))!.Value.Kind);
        Assert.Equal("UTC", b.Describe());
    }

    [Fact]
    public void Local_TakesThePcOffsetAtTheSetTime()
    {
        var when = new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc);
        var b = DeviceClockBase.For(deviceUtc: false, when);
        Assert.Equal(ClockBaseKind.Local, b.Kind);
        Assert.Equal((short)TimeZoneInfo.Local.GetUtcOffset(when).TotalMinutes, b.ZoneMinutes);
    }

    [Fact]
    public void Local_ConvertsWithTheFixedOffset_AcrossDst()
    {
        var b = new DeviceClockBase(ClockBaseKind.Local, 120);          // set in winter at UTC+2: stays +2 all deployment
        Assert.Equal(new DateTime(2026, 11, 1, 9, 14, 48), b.ToUtc(new DateTime(2026, 11, 1, 11, 14, 48)));
        Assert.Equal(new DateTime(2027, 7, 1, 10, 0, 0), b.ToUtc(new DateTime(2027, 7, 1, 12, 0, 0)));
        Assert.Equal("local time (UTC+02:00)", b.Describe());
        Assert.Equal("local time (UTC-03:30)", new DeviceClockBase(ClockBaseKind.Local, -210).Describe());
    }

    [Fact]
    public void Unknown_HasNoUtc()
    {
        Assert.Null(DeviceClockBase.Unknown.ToUtc(new DateTime(2026, 11, 1, 9, 0, 0)));
        Assert.False(DeviceClockBase.Unknown.IsKnown);
        Assert.Equal("UTC or local not known", DeviceClockBase.Unknown.Describe());
        Assert.Equal(DeviceClockBase.Unknown, default(DeviceClockBase));   // a set without a base sends "unknown"
    }

    [Theory]
    [InlineData(1, 0, ClockBaseKind.Utc, 0)]
    [InlineData(1, 120, ClockBaseKind.Utc, 0)]                       // UTC carries no zone
    [InlineData(2, 120, ClockBaseKind.Local, 120)]
    [InlineData(2, -840, ClockBaseKind.Local, -840)]
    [InlineData(2, 841, ClockBaseKind.Unknown, 0)]                   // out of range
    [InlineData(0, 120, ClockBaseKind.Unknown, 0)]
    [InlineData(3, 0, ClockBaseKind.Unknown, 0)]
    [InlineData(0xFF, -1, ClockBaseKind.Unknown, 0)]                 // erased spare of an old page
    public void FromWire(byte kind, short zone, ClockBaseKind expectKind, short expectZone)
    {
        var b = DeviceClockBase.FromWire(kind, zone);
        Assert.Equal(expectKind, b.Kind);
        Assert.Equal(expectZone, b.ZoneMinutes);
    }
}
