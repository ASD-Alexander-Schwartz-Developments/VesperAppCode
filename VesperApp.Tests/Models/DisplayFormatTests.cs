using VesperApp.Models;
using VesperApp.Services;
using VesperApp.Services.Time;
using Xunit;

namespace VesperApp.Tests.Models;

/// <summary>One date style on screen ("2026-11-02 13:13:18") and readable device-list cells.</summary>
public class DisplayFormatTests
{
    [Fact]
    public void Dates_are_iso_like_whatever_the_culture()
    {
        var saved = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("en-GB");
            Assert.Equal("2026-11-02 07:05:09", DisplayFormat.DateTime(new DateTime(2026, 11, 2, 7, 5, 9)));
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = saved; }
    }

    [Fact]
    public void Feed_publish_stamp_shows_local_minutes()
    {
        var utc = new DateTime(2026, 11, 2, 10, 12, 33, DateTimeKind.Utc);
        string want = utc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        Assert.Equal(want, DisplayFormat.Published("2026-11-02T10:12:33Z"));
        Assert.Equal(want, new ReleaseEntry { Published = "2026-11-02T10:12:33Z" }.PublishedText);
        Assert.Equal("", DisplayFormat.Published(null));
        Assert.Equal("soon", DisplayFormat.Published("soon"));
    }

    [Fact]
    public void Clock_base_is_short()
    {
        Assert.Equal("UTC", DisplayFormat.ClockBase(DeviceClockBase.Utc));
        Assert.Equal("Local UTC+02:00", DisplayFormat.ClockBase(new DeviceClockBase(ClockBaseKind.Local, 120)));
        Assert.Equal("Local UTC-03:30", DisplayFormat.ClockBase(new DeviceClockBase(ClockBaseKind.Local, -210)));
        Assert.Equal("Unknown", DisplayFormat.ClockBase(DeviceClockBase.Unknown));
    }

    [Theory]
    [InlineData(0.5, 45.0, 100.0, "512 MB, 45% free")]
    [InlineData(0.5, 45.0, 0.0, "512 MB, 45% free")]
    [InlineData(0.5, 45.0, 37.4, "Downloading 37%")]
    [InlineData(29.7, null, null, "29.7 GB")]
    [InlineData(0.0, 12.0, null, "12% free")]
    [InlineData(0.0, null, null, "")]
    public void Storage_cell(double sizeGb, double? free, double? download, string want) =>
        Assert.Equal(want, DisplayFormat.Disk(sizeGb, free, download));
}
