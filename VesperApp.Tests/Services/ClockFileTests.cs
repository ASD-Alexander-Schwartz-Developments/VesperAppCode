using System.IO;
using VesperApp.Services;
using VesperApp.Services.Time;
using Xunit;

namespace VesperApp.Tests.Services;

/// <summary>
/// Vesper / Pipistrelle / KOL clock base (asd-interfaces rtc-time-sync.md § Clock base): CLOCK.TXT written by the unit
/// at every armed start, and the 12-byte VESPER_SET_RTC that carries the base.
/// </summary>
public class ClockFileTests
{
    private static readonly string[] Sample =
    {
        "# recording start (unit time), clock: UTC / local UTC+hh:mm (UTC = time - offset) / unknown",
        "2026-11-01 09:14:48 UTC",
        "2026-11-02 11:00:05 local UTC+02:00",
        "2026-11-03 08:00:00 unknown",
        "2026-11-04 06:10:00 local UTC-03:30",
        "garbage line",
    };

    [Fact]
    public void Parse_ReadsEveryValidLine()
    {
        var f = ClockFile.Parse(Sample);
        Assert.Equal(4, f.Entries.Count);
        Assert.Equal(DeviceClockBase.Utc, f.Entries[0].Base);
        Assert.Equal(new DeviceClockBase(ClockBaseKind.Local, 120), f.Entries[1].Base);
        Assert.Equal(DeviceClockBase.Unknown, f.Entries[2].Base);
        Assert.Equal(new DeviceClockBase(ClockBaseKind.Local, -210), f.Entries[3].Base);
    }

    [Fact]
    public void BaseAt_TakesTheLineInForce()
    {
        var f = ClockFile.Parse(Sample);
        Assert.Equal(DeviceClockBase.Unknown, f.BaseAt(new DateTime(2026, 10, 31, 12, 0, 0)));   // before any start
        Assert.Equal(DeviceClockBase.Utc, f.BaseAt(new DateTime(2026, 11, 1, 9, 14, 48)));       // at the start
        Assert.Equal(DeviceClockBase.Utc, f.BaseAt(new DateTime(2026, 11, 2, 11, 0, 4)));
        Assert.Equal(120, f.BaseAt(new DateTime(2026, 11, 2, 23, 0, 0)).ZoneMinutes);
        Assert.False(f.BaseAt(new DateTime(2026, 11, 3, 9, 0, 0)).IsKnown);
        // a local recording converts with its own offset: 13:40 at UTC+02:00 is 11:40 UTC
        var t = new DateTime(2026, 11, 2, 13, 40, 0);
        Assert.Equal(new DateTime(2026, 11, 2, 11, 40, 0), f.BaseAt(t).ToUtc(t));
    }

    [Fact]
    public void FindFor_LooksInTheFolderAndAbove()
    {
        string root = Path.Combine(Path.GetTempPath(), "clk-" + Guid.NewGuid().ToString("N"));
        string deep = Path.Combine(root, "REC", "2026_11_01");
        Directory.CreateDirectory(deep);
        try
        {
            string bin = Path.Combine(deep, "00001G.BIN");
            File.WriteAllText(bin, "");
            Assert.Null(ClockFile.FindFor(bin));
            File.WriteAllLines(Path.Combine(root, "clock.txt"), Sample);           // FAT names: any case
            var f = ClockFile.FindFor(bin);
            Assert.NotNull(f);
            Assert.Equal(4, f!.Entries.Count);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void SetRtcPayload_CarriesBaseWeekdayAndZone()
    {
        var t = new DateTime(2026, 11, 1, 13, 40, 5);                              // a Sunday
        byte[] b = SerialMessage.DateTimeToBytes(t, new DeviceClockBase(ClockBaseKind.Local, -210));
        Assert.Equal(12, b.Length);
        Assert.Equal(new byte[] { 5, 40, 13, 1, 11, 0xEA, 0x07 }, b[..7]);
        Assert.Equal(2, b[7]);                                                      // local
        Assert.Equal(0, b[8]);
        Assert.Equal(7, b[9]);                                                      // STM32 Sunday
        Assert.Equal(-210, (short)(b[10] | (b[11] << 8)));
        Assert.Equal(1, SerialMessage.DateTimeToBytes(new DateTime(2026, 11, 2), DeviceClockBase.Utc)[9]);   // Monday
        Assert.Equal(0, SerialMessage.DateTimeToBytes(t, DeviceClockBase.Unknown)[7]);
    }
}
