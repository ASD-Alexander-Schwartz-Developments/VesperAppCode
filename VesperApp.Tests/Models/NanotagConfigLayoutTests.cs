using System.Runtime.InteropServices;
using VesperApp.Models;
using Xunit;

namespace VesperApp.Tests.Models;

/// <summary>
/// The host's NanotagGeneralConfiguration must match the firmware's device_general_params_t
/// (NANOTAG firmware src/params.h, 1.39) byte for byte — the 256-byte general row is copied raw
/// into the device's EEPROM. asd-interfaces interfaces/rtc-time-sync.md (Nanotag compensation table).
/// </summary>
public class NanotagConfigLayoutTests
{
    [Theory]
    [InlineData("minimum_hw_ver", 0)]
    [InlineData("isMagnetOffEnabled", 2)]
    [InlineData("config_rev", 3)]
    [InlineData("battery_capacity", 4)]
    [InlineData("reference_year", 6)]
    [InlineData("Schedule_type", 8)]
    [InlineData("powerOnUnused", 10)]
    [InlineData("rtc_tcomp", 11)]
    [InlineData("rtc_tck", 12)]
    [InlineData("rtc_tct0", 14)]
    [InlineData("rtc_tcper", 16)]
    [InlineData("poweron", 18)]
    [InlineData("res", 22)]
    [InlineData("reserved2", 64)]
    [InlineData("reserved3", 128)]
    [InlineData("reserved4", 192)]
    public void GeneralRow_FieldOffsetsMatchFirmware(string field, int offset)
        => Assert.Equal(offset, (int)Marshal.OffsetOf<NanotagGeneralConfiguration>(field));

    [Fact]
    public void GeneralRow_Is256Bytes() => Assert.Equal(256, Marshal.SizeOf<NanotagGeneralConfiguration>());

    [Fact]
    public void Config_Is768Bytes() => Assert.Equal(3 * Nanotag.CONFIG_CHUNK_SIZE, Marshal.SizeOf<NanotagConfiguration>());

    private const string BaseJson = """
        "name": "NANOTAG", "minhw": "1", "scheduleType": 0, "magnetOff": true, "battery": 60,
        "poweron": "2026-10-25 12:34:56", "schedule": [], "devices": []
        """;

    [Fact]
    public void Converter_WritesRtcCompensationAtFirmwareOffsets()
    {
        string json = "{" + BaseJson + """, "tcomp": true, "tck": 34, "tct0": 255, "tcper": 600 }""";
        byte[] b = Nanotag.ConfigBinaryConverter(json);

        Assert.Equal(768, b.Length);
        Assert.Equal(1, b[2]);                                    // magnet off
        Assert.Equal(60, BitConverter.ToUInt16(b, 4));            // battery
        Assert.Equal(0, b[10]);                                   // powerOn byte (unused by firmware)
        Assert.Equal(1, b[11]);                                   // rtc_tcomp
        Assert.Equal(34, BitConverter.ToUInt16(b, 12));           // rtc_tck
        Assert.Equal(255, BitConverter.ToInt16(b, 14));           // rtc_tct0
        Assert.Equal(600, BitConverter.ToUInt16(b, 16));          // rtc_tcper
        Assert.All(b[22..64], x => Assert.Equal(0, x));
    }

    [Fact]
    public void Converter_PowerOnTimeNoLongerSpillsIntoRtcFields()
    {
        // Before the fix the packed u32 power-on time occupied bytes 10..13, so a dated power-on
        // could switch rtc_tcomp on and set a garbage curvature.
        byte[] b = Nanotag.ConfigBinaryConverter("{" + BaseJson + "}");
        Assert.Equal(768, b.Length);
        Assert.All(b[10..18], x => Assert.Equal(0, x));
    }

    // ---- delayed start ("poweron", firmware 1.39): u32 at offset 18, the packed RTC date/time ----

    private static uint PowerOnWord(string poweron)
    {
        string json = "{" + BaseJson.Replace("\"2026-10-25 12:34:56\"", "\"" + poweron + "\"") + "}";
        byte[] b = Nanotag.ConfigBinaryConverter(json);
        Assert.Equal(768, b.Length);
        Assert.All(b[10..18].Where((_, i) => i != 1), x => Assert.Equal(0, x));   // 10, 12..17 untouched (11 = tcomp off)
        Assert.All(b[22..64], x => Assert.Equal(0, x));
        return BitConverter.ToUInt32(b, 18);
    }

    private static uint Packed(int yearsSince2020, int month, int day, int hour, int minute, int second)
        => (uint)(yearsSince2020 << 26 | month << 22 | day << 17 | hour << 12 | minute << 6 | second);

    [Fact]
    public void DelayedStart_None_IsZero()
        => Assert.Equal(0u, PowerOnWord(""));                      // = the bytes older configs have there

    [Fact]
    public void DelayedStart_Absolute_IsPackedDateSince2020()
        => Assert.Equal(Packed(6, 10, 25, 12, 34, 56), PowerOnWord("2026-10-25 12:34:56"));

    [Fact]
    public void DelayedStart_Relative_HasYearAndMonthZero()
        // config.json "2000-00-DD hh:mm:ss" (as for VesperU5 / KOL): days + time after arming
        => Assert.Equal(Packed(0, 0, 3, 4, 5, 6), PowerOnWord("2000-00-03 04:05:06"));

    [Fact]
    public void DelayedStart_RelativeOver31Days_IsRefused()
    {
        string json = "{" + BaseJson.Replace("\"2026-10-25 12:34:56\"", "\"2000-00-45 00:00:00\"") + "}";
        Assert.Empty(Nanotag.ConfigBinaryConverter(json));        // no config rather than a wrong delay
    }

    private static string ScheduleJson(int entries)
    {
        var items = string.Join(", ", Enumerable.Range(0, entries)
            .Select(i => $"{{ \"time\": \"2026-11-{1 + i / 24:00} {i % 24:00}:00:00\", \"config\": 1 }}"));
        return "{" + BaseJson.Replace("\"schedule\": []", "\"schedule\": [" + items + "]") + "}";
    }

    [Fact]
    public void Schedule_31Entries_IsWritten()
        => Assert.Equal(768, Nanotag.ConfigBinaryConverter(ScheduleJson(Nanotag.MAX_SCHEDULE_ENTRIES)).Length);

    [Fact]
    public void Schedule_32Entries_IsRefused()
        // the firmware refuses to arm with all 32 slots used (app.c: count >= MAX_SCHEDULE_SIZE)
        => Assert.Empty(Nanotag.ConfigBinaryConverter(ScheduleJson(Nanotag.MAX_SCHEDULE_SIZE)));

    [Fact]
    public void DelayedStart_AbsoluteBefore2021_IsRefused()
    {
        // year 0 would read as a relative delay on the tag
        string json = "{" + BaseJson.Replace("\"2026-10-25 12:34:56\"", "\"2020-05-01 00:00:00\"") + "}";
        Assert.Empty(Nanotag.ConfigBinaryConverter(json));
    }

    [Theory]
    [InlineData(0, 0)]      // continuous
    [InlineData(1, 3)]      // daily
    [InlineData(2, 2)]      // dated
    [InlineData(3, 5)]      // relative
    public void ScheduleType_MapsToNanotagNumbers(int jsonType, int nanotagType)
    {
        string json = "{" + BaseJson.Replace("\"scheduleType\": 0", "\"scheduleType\": " + jsonType) + "}";
        byte[] b = Nanotag.ConfigBinaryConverter(json);
        Assert.Equal(nanotagType, BitConverter.ToUInt16(b, 8));
    }

    [Fact]
    public void ScheduleEntries_DailyYear2000_IsYearZero()
    {
        // daily entries are written with year 2000; the year field used to get 2020 (wrapping to 36)
        Assert.Equal(Packed(0, 10, 25, 6, 0, 0), Nanotag.ToTimestamp(new DateTime(2000, 10, 25, 6, 0, 0)));
        Assert.Equal(Packed(6, 1, 2, 6, 0, 0), Nanotag.ToTimestamp(new DateTime(2026, 1, 2, 6, 0, 0)));
    }
}
