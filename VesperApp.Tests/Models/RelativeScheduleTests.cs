using System.Text.Json;
using VesperApp.Models;
using VesperApp.ViewModels;
using Xunit;

namespace VesperApp.Tests.Models;

/// <summary>Relative schedule entries: day offset 0..31 + time, written "2000-00-DD hh:mm:ss" in config.json
/// (VesperU5 / KOL zero year and month, add the rest to the arming time) and as year 0 / day = offset in the
/// Nanotag binary config (asd-interfaces interfaces/vesper-schedule.md).</summary>
public class RelativeScheduleTests
{
    private static JsonSerializerOptions Options()
    {
        var o = new JsonSerializerOptions();
        o.Converters.Add(new ConfigurationJSON.ScheduleTypesEnumConverter());
        o.Converters.Add(new ConfigurationDeviceDriver.ConfigurationDeviceDriverConverter());
        o.Converters.Add(new ConfigurationJSON.VesperDateTimeConverter());
        o.Converters.Add(new ConfigurationJSON.VesperPowerOnConverter());
        o.Converters.Add(new VesperDateTimeAlarmConverter());
        return o;
    }

    [Theory]
    [InlineData(0, 6, 30, "2000-00-00 06:30:00")]      // same day as the start
    [InlineData(1, 0, 0, "2000-00-01 00:00:00")]
    [InlineData(31, 23, 59, "2000-00-31 23:59:00")]
    public void Relative_EncodesDayOffsetAndTime(int days, int h, int m, string expected)
    {
        var e = ConfigScheduleJSONItem.Relative(days, new TimeSpan(h, m, 0), WorkingConfiguration.Config1);
        Assert.True(e.IsRelative);
        Assert.Equal(expected, e.Time);
        Assert.Equal(new TimeSpan(days, h, m, 0), e.RelativeOffset);
    }

    [Fact]
    public void Relative_OutOfRange_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ConfigScheduleJSONItem.Relative(32, TimeSpan.Zero, WorkingConfiguration.Off));
        Assert.Throws<ArgumentOutOfRangeException>(() => ConfigScheduleJSONItem.Relative(0, TimeSpan.FromHours(-1), WorkingConfiguration.Off));
    }

    [Fact]
    public void ConfigJson_RelativeSchedule_RoundTripsDayZero()
    {
        var config = new ConfigurationJSON { Name = "vesper", ScheduleType = ScheduleTypes.Relative };
        config.Schedule.Add(ConfigScheduleJSONItem.Relative(0, new TimeSpan(18, 0, 0), WorkingConfiguration.Config1));
        config.Schedule.Add(ConfigScheduleJSONItem.Relative(0, new TimeSpan(20, 0, 0), WorkingConfiguration.Off));
        config.Schedule.Add(ConfigScheduleJSONItem.Relative(2, new TimeSpan(6, 15, 0), WorkingConfiguration.Config2));

        string json = JsonSerializer.Serialize(config, Options());
        using (var doc = JsonDocument.Parse(json))
        {
            JsonElement s = doc.RootElement.GetProperty("schedule");
            Assert.Equal("2000-00-00 18:00:00", s[0].GetProperty("time").GetString());
            Assert.Equal(1, s[0].GetProperty("config").GetInt32());
            Assert.Equal("2000-00-00 20:00:00", s[1].GetProperty("time").GetString());
            Assert.Equal("2000-00-02 06:15:00", s[2].GetProperty("time").GetString());
            Assert.Equal(3, doc.RootElement.GetProperty("scheduleType").GetInt32());
        }

        ConfigurationJSON back = ConfigurationJSON.TryParse(json)!;
        Assert.NotNull(back);
        Assert.Equal(new[] { new TimeSpan(0, 18, 0, 0), new TimeSpan(0, 20, 0, 0), new TimeSpan(2, 6, 15, 0) },
                     back.Schedule.Select(e => e.RelativeOffset).ToArray());
        Assert.All(back.Schedule, e => Assert.True(e.IsRelative));
        Assert.Equal(json, JsonSerializer.Serialize(back, Options()));
    }

    [Fact]
    public void ConfigJson_OlderDatePickerEntry_KeepsFirmwareMeaning()
    {
        // older VesperApp builds wrote relative entries from a date picker: the firmware reads the day of month
        // as the day offset and drops year and month
        var e = new ConfigScheduleJSONItem { Time = "2026-03-05 10:00:00" };
        Assert.False(e.IsRelative);
        Assert.Equal(new TimeSpan(5, 10, 0, 0), e.RelativeOffset);
        e.IsRelative = true;                                   // saved again as a relative schedule
        Assert.Equal("2000-00-05 10:00:00", e.Time);
    }

    [Theory]
    [InlineData("2000-00-32 00:00:00")]
    [InlineData("2000-00-01 24:00:00")]
    [InlineData("tomorrow")]
    public void ConfigJson_BadTime_Throws(string text)
        => Assert.Throws<FormatException>(() => new ConfigScheduleJSONItem { Time = text });

    [Fact]
    public void ConfigJson_DatedEntries_Unchanged()
    {
        var e = new ConfigScheduleJSONItem { Alarm = new DateTime(2026, 11, 2, 6, 0, 0) };
        Assert.Equal("2026-11-02 06:00:00", e.Time);
        Assert.Equal("02/11/2026 06:00:00", e.DisplayTime);
        Assert.Equal("Day 0  06:00:00", ConfigScheduleJSONItem.Relative(0, TimeSpan.FromHours(6), WorkingConfiguration.Off).DisplayTime);
    }

    private static uint Packed(int y, int mo, int d, int h, int mi, int s)
        => (uint)(y << 26 | mo << 22 | d << 17 | h << 12 | mi << 6 | s);

    [Fact]
    public void Nanotag_RelativeEntries_YearZeroDayOffset()
    {
        string json = """
            { "name": "NANOTAG", "minhw": "1", "scheduleType": 3, "magnetOff": true, "battery": 60,
              "schedule": [ { "time": "2000-00-00 06:30:00", "config": 1 }, { "time": "2000-00-03 18:00:00", "config": 0 } ],
              "devices": [] }
            """;
        byte[] b = Nanotag.ConfigBinaryConverter(json);
        Assert.Equal(768, b.Length);
        Assert.Equal(5, BitConverter.ToUInt16(b, 8));                                  // Nanotag relative
        Assert.Equal(Packed(0, 0, 0, 6, 30, 0), BitConverter.ToUInt32(b, 512));       // entry 0: day 0
        Assert.Equal(1u, BitConverter.ToUInt32(b, 516));
        Assert.Equal(Packed(0, 0, 3, 18, 0, 0), BitConverter.ToUInt32(b, 520));
        Assert.Equal(0u, BitConverter.ToUInt32(b, 524));
    }

    [Fact]
    public void Nanotag_OlderDatePickerRelativeEntry_IsDayOfMonthOffset()
    {
        var e = new ConfigScheduleJSONItem { Time = "2026-03-05 10:00:00" };
        Assert.Equal(Packed(0, 0, 5, 10, 0, 0), Nanotag.ScheduleEntryTimestamp(e, ScheduleTypes.Relative));
        Assert.Equal(Nanotag.ToTimestamp(e.Alarm), Nanotag.ScheduleEntryTimestamp(e, ScheduleTypes.Dated));
    }

    [Theory]
    [InlineData(0, 6, 30, 45, "2000-00-00 06:30:00")]
    [InlineData(40, 1, 0, 0, "2000-00-31 01:00:00")]    // clamped to the day field
    public void Editor_BuildRelativeEntry_DayOffsetAndMinutes(int days, int h, int m, int s, string expected)
        => Assert.Equal(expected, ScheduleControlViewModel.BuildRelativeEntry(days, new TimeSpan(h, m, s), WorkingConfiguration.Config1).Time);
}
