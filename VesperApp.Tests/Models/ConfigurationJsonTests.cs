using System.Text.Json;
using VesperApp.Models;
using Xunit;

namespace VesperApp.Tests.Models;

/// <summary>config.json model: TryParse, the polymorphic driver converter and round trips.</summary>
public class ConfigurationJsonTests
{
    /// <summary>The converter set ConfigurationJSON.TryParse uses.</summary>
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

    /// <summary>Every concrete driver class (Models/Config*Driver.cs, ConfigNanotagAcc).</summary>
    public static IEnumerable<object[]> DriverTypes() =>
        typeof(ConfigurationDeviceDriver).Assembly.GetTypes()
            .Where(t => typeof(ConfigurationDeviceDriver).IsAssignableFrom(t) && !t.IsAbstract
                        && t != typeof(ConfigurationDeviceDriver) && t.GetConstructor(Type.EmptyTypes) != null)
            .OrderBy(t => t.Name)
            .Select(t => new object[] { t.Name });

    private static Type TypeOf(string name) => typeof(ConfigurationDeviceDriver).Assembly.GetTypes().Single(t => t.Name == name);

    private static string[] Keys(JsonElement o) => o.EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToArray();

    [Fact]
    public void DriverTypes_CoverEveryDriverInTheConverter()
    {
        var names = DriverTypes().Select(o => (string)o[0]).ToList();
        Assert.True(names.Count >= 17, string.Join(", ", names));
        Assert.Contains("ConfigLoggerDriver", names);
        Assert.Contains("ConfigATLASDriver", names);
    }

    [Theory]
    [MemberData(nameof(DriverTypes))]
    public void Driver_DeserialisesToItsOwnTypeByName_AndKeysSurviveRoundTrip(string typeName)
    {
        var original = (ConfigurationDeviceDriver)Activator.CreateInstance(TypeOf(typeName))!;
        var config = new ConfigurationJSON { Name = "TEST" };
        config.DeviceDrivers.Add(original);

        string json = JsonSerializer.Serialize(config, Options());
        ConfigurationJSON? back = ConfigurationJSON.TryParse(json);

        Assert.True(back != null, "TryParse failed on " + json);
        ConfigurationDeviceDriver d = Assert.Single(back!.DeviceDrivers);
        Assert.IsType(original.GetType(), d);
        Assert.Equal(original.Name, d.Name);

        string json2 = JsonSerializer.Serialize(back, Options());
        using var a = JsonDocument.Parse(json);
        using var b = JsonDocument.Parse(json2);
        JsonElement da = a.RootElement.GetProperty("devices")[0], db = b.RootElement.GetProperty("devices")[0];
        Assert.Equal(original.Name, da.GetProperty("name").GetString());
        Assert.Equal("name", da.EnumerateObject().First().Name);   // the converter dispatches on a leading "name"
        Assert.Equal(Keys(da), Keys(db));
        // every key once, firmware spelling (an override without its JsonPropertyName adds e.g. "FileSize" next to "fileSize")
        Assert.Equal(Keys(da).Length, Keys(da).Select(k => k.ToLowerInvariant()).Distinct().Count());
        Assert.Equal(da.GetProperty("bitmask").GetUInt32(), db.GetProperty("bitmask").GetUInt32());
        Assert.Equal(da.GetProperty("fileSize").GetUInt32(), db.GetProperty("fileSize").GetUInt32());
    }

    [Fact]
    public void KolConfig_RoundTrip_KeepsDriverSpecificKeys()
    {
        var kol = new ConfigKOLDriver { MikesOn = 2 };
        kol.SampleRate = new uint[] { 0, 192000, 48000 };
        var imu = new ConfigIMU10Driver { ImuRange = 3 };
        imu.SampleRate = new uint[] { 0, 100, 0 };
        var logger = new ConfigLoggerDriver { LogLevel = LoggerLogLevel.Debug, LogBattery = false };
        logger.SampleRate1 = 30000;

        var config = new ConfigurationJSON { Name = "KOL", CDrift = 32999, TempComp = true, ScheduleType = ScheduleTypes.Daily };
        config.Schedule.Add(new ConfigScheduleJSONItem { Alarm = new DateTime(2000, 1, 1, 6, 30, 0), Configuration = WorkingConfiguration.Config1 });
        config.DeviceDrivers.AddRange(new ConfigurationDeviceDriver[] { kol, new ConfigTPRH31Driver(), imu, new ConfigIMU10HTDriver(), logger });

        string json = JsonSerializer.Serialize(config, Options());
        using (var doc = JsonDocument.Parse(json))
        {
            JsonElement root = doc.RootElement;
            Assert.Equal("KOL", root.GetProperty("name").GetString());
            Assert.Equal(1, root.GetProperty("scheduleType").GetInt32());
            Assert.Equal(32999u, root.GetProperty("cdrift").GetUInt32());
            Assert.True(root.GetProperty("tcomp").GetBoolean());
            Assert.False(root.TryGetProperty("tck", out _));     // null -> omitted
            Assert.Equal("2000-01-01 06:30:00", root.GetProperty("schedule")[0].GetProperty("time").GetString());
            JsonElement devs = root.GetProperty("devices");
            Assert.Equal(new[] { "KOL", "TPRH31", "IMU10", "IMU10HT", "LOGGER" },
                         devs.EnumerateArray().Select(d => d.GetProperty("name").GetString()).ToArray());
            Assert.Equal(2u, devs[0].GetProperty("mikeon").GetUInt32());
            Assert.Equal(0x8000, devs[0].GetProperty("filter").GetInt32() & 0x8000);   // automatic chain bit
            Assert.Equal(3u, devs[2].GetProperty("imuRange").GetUInt32());
            Assert.Equal(0x3Eu, devs[4].GetProperty("logmask").GetUInt32());           // no battery, debug level
            Assert.Equal(30000u, devs[4].GetProperty("sampleRate")[1].GetUInt32());
        }

        ConfigurationJSON back = ConfigurationJSON.TryParse(json)!;
        Assert.NotNull(back);
        Assert.Equal(ScheduleTypes.Daily, back.ScheduleType);
        Assert.Equal(32999u, back.CDrift);
        Assert.True(back.TempComp);
        Assert.Equal(new DateTime(2000, 1, 1, 6, 30, 0), Assert.Single(back.Schedule).Alarm);
        Assert.Equal(WorkingConfiguration.Config1, back.Schedule[0].Configuration);

        var k = Assert.IsType<ConfigKOLDriver>(back.DeviceDrivers[0]);
        Assert.Equal(2u, k.MikesOn);
        Assert.Equal(new uint[] { 0, 192000, 48000 }, k.SampleRate);
        Assert.IsType<ConfigTPRH31Driver>(back.DeviceDrivers[1]);
        Assert.Equal(3u, Assert.IsType<ConfigIMU10Driver>(back.DeviceDrivers[2]).ImuRange);
        Assert.IsType<ConfigIMU10HTDriver>(back.DeviceDrivers[3]);
        var l = Assert.IsType<ConfigLoggerDriver>(back.DeviceDrivers[4]);
        Assert.Equal(0x3Eu, l.LogMask);
        Assert.False(l.LogBattery);
        Assert.Equal(LoggerLogLevel.Debug, l.LogLevel);
        Assert.Equal(30000u, l.SampleRate1);
    }

    [Fact]
    public void TryParse_FirmwareStyleJson()
    {
        const string json = """
        {
          "name": "KOL", "minhw": "4.0", "scheduleType": 0, "magnetOff": false, "battery": 19,
          "poweron": "2000-00-01 02:03:04", "schedule": [],
          "devices": [
            { "name": "LOGGER", "sampleRate": [0, 60000, 0], "wLen": [0,0,0], "wRate": [0,0,0], "bitmask": 0, "logmask": 19, "fileSize": 1048576 },
            { "name": "imu10", "sampleRate": [0, 50, 50], "wLen": [0,0,0], "wRate": [0,0,0], "bitmask": 15, "imuRange": 2, "fileSize": 524288 }
          ]
        }
        """;
        ConfigurationJSON c = ConfigurationJSON.TryParse(json)!;
        Assert.NotNull(c);
        Assert.False(c.IsMagnetOffEnabled);
        Assert.Equal(19u, c.BatteryCapacity);
        Assert.True(c.PowerOn.IsRelative);
        Assert.Equal("2000-00-01 02:03:04", c.PowerOn.ToString());

        var l = Assert.IsType<ConfigLoggerDriver>(c.DeviceDrivers[0]);
        Assert.True(l.LogBattery);
        Assert.True(l.LogTemperature);
        Assert.False(l.LogRtcCorrection);
        Assert.False(l.LogSensors);
        Assert.Equal(LoggerLogLevel.Errors, l.LogLevel);
        Assert.Equal(60000u, l.SampleRate1);

        var i = Assert.IsType<ConfigIMU10Driver>(c.DeviceDrivers[1]);     // name matched case-insensitively
        Assert.Equal(2u, i.ImuRange);
        Assert.Equal(15u, i.Bitmask);
    }

    [Theory]
    [InlineData("{}")]                                                // no device name
    [InlineData("""{ "name": "  " }""")]
    [InlineData("not json")]
    [InlineData("""{ "name": "KOL", "scheduleType": 9 }""")]       // unknown schedule type
    [InlineData("""{ "name": "KOL", "devices": [ { "sampleRate": [0,1,1], "name": "KOL" } ] }""")]   // "name" must come first
    public void TryParse_RejectsNonConfigurations(string json) => Assert.Null(ConfigurationJSON.TryParse(json));

    [Fact]
    public void TryParse_UnknownDriver_IsKeptVerbatim()
    {
        // A driver from a newer firmware / app: the config still opens (it was rejected as a whole),
        // the entry is a ConfigUnknownDriver and is written back exactly as read.
        var c = ConfigurationJSON.TryParse("""{ "name": "KOL", "devices": [ { "name": "KOL", "sampleRate": [0, 48000, 0] }, { "name": "FUTURE", "bitmask": 1, "newKey": [1, 2] } ] }""");
        Assert.NotNull(c);
        Assert.Equal(2, c!.DeviceDrivers.Count);
        Assert.IsType<ConfigKOLDriver>(c.DeviceDrivers[0]);
        var u = Assert.IsType<ConfigUnknownDriver>(c.DeviceDrivers[1]);
        Assert.Equal("FUTURE", u.Name);

        var opt = new System.Text.Json.JsonSerializerOptions();
        opt.Converters.Add(new ConfigurationDeviceDriver.ConfigurationDeviceDriverConverter());
        string back = System.Text.Json.JsonSerializer.Serialize(u as ConfigurationDeviceDriver, opt);
        Assert.Equal("""{"name":"FUTURE","bitmask":1,"newKey":[1,2]}""", back);
    }

    [Fact]
    public void PowerOn_AbsoluteTime_RoundTrips()
    {
        var p = new ConfigurationJSON.PowerOnTime("2026-10-25 09:30:00");
        Assert.False(p.IsRelative);
        Assert.Equal("2026-10-25 09:30:00", p.ToString());
        Assert.Equal(string.Empty, new ConfigurationJSON.PowerOnTime().ToString());
    }

    [Fact]
    public void LoggerFacade_BitsAndLevelMapToLogmask()
    {
        var l = new ConfigLoggerDriver();
        Assert.Equal(0x2Fu, l.LogMask);                           // default: all fields, warnings (= fixture logmask 47)
        Assert.Equal(60000u, l.SampleRate[1]);
        Assert.Equal(1024u * 1024u, l.FileSize);

        l.LogTemperature = false;
        Assert.Equal(0x2Du, l.LogMask);
        l.LogLevel = LoggerLogLevel.Off;
        Assert.Equal(0x0Du, l.LogMask);
        l.LogLevel = LoggerLogLevel.Debug;
        l.LogSensors = false;
        Assert.Equal(0x35u, l.LogMask);
        Assert.True(l.LogBattery && l.LogRtcCorrection);

        l.SampleRate1 = 10;                                       // below the 1 s minimum
        Assert.Equal(1000u, l.SampleRate1);
        l.SampleRate2 = 0;                                        // 0 = no samples
        Assert.Equal(0u, l.SampleRate2);
    }

    [Fact]
    public void KolDriver_MikesOnIsCappedAtFour() => Assert.Equal(4u, new ConfigKOLDriver { MikesOn = 9 }.MikesOn);

    [Fact]
    public void SupportedDrivers_PerProduct()
    {
        Assert.Equal(new[] { "KOL", "TPRH31", "IMU10", "IMU10HT", "LOGGER" }, Kol.SupportedDeviceDrivers.Select(d => d.Name).ToArray());
        Assert.Contains(Vesper.SupportedDeviceDrivers, d => d is ConfigLoggerDriver);
        Assert.Contains(Pipistrelle.SupportedDeviceDrivers, d => d is ConfigLoggerDriver);
        Assert.Equal(Vesper.SupportedDeviceDrivers.Count, Vesper.SupportedDeviceDrivers.Select(d => d.Name).Distinct().Count());
    }

    [Theory]
    [InlineData(0u, 0u)]
    [InlineData(1u, 65536u)]
    [InlineData(98303u, 65536u)]
    [InlineData(98304u, 131072u)]
    [InlineData(8u * 1024 * 1024, 8u * 1024 * 1024)]
    public void FileSize_RoundsToWholeBuffers(uint value, uint expected)
        => Assert.Equal(expected, ConfigurationDeviceDriver.RoundFileSize(value));
}
