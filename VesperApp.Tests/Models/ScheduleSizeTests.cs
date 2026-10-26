using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using VesperApp.Models;
using VesperApp.ViewModels;
using Xunit;
using Xunit.Abstractions;

namespace VesperApp.Tests.Models;

/// <summary>
/// Schedule size: VesperApp writes at most 150 entries for Vesper / Pipistrelle. VesperU5 arms with at most 159
/// (params.h MAX_SCHEDULE_SIZE 160, app_threadx.c: count >= MAX_SCHEDULE_SIZE stops it) and reads config.json only
/// when the file is smaller than 16384 bytes (params.c CACHE_SIZE, fx_file_current_file_size &lt; CACHE_SIZE).
/// </summary>
public class ScheduleSizeTests
{
    private const int FirmwareMaxScheduleSize = 160;   // VesperU5 Core/Inc/params.h
    private const int FirmwareCacheSize = 16384;       // VesperU5 Core/Src/params.c
    private const int FirmwareMaxDevices = 4;          // VesperU5 MAX_LOADED_DEVICES

    private readonly ITestOutputHelper output;

    public ScheduleSizeTests(ITestOutputHelper output) => this.output = output;

    /// <summary>The converter set the editor saves with (ScheduleEditorViewModel.SaveConfiguration).</summary>
    private static JsonSerializerOptions Options()
    {
        var o = new JsonSerializerOptions { WriteIndented = false };
        o.Converters.Add(new ConfigurationJSON.ScheduleTypesEnumConverter());
        o.Converters.Add(new ConfigurationDeviceDriver.ConfigurationDeviceDriverConverter());
        o.Converters.Add(new ConfigurationJSON.VesperDateTimeConverter());
        o.Converters.Add(new ConfigurationJSON.VesperPowerOnConverter());
        o.Converters.Add(new VesperDateTimeAlarmConverter());
        return o;
    }

    [Fact]
    public void VesperAndPipistrelle_Accept150Entries_BelowTheFirmwareArmLimit()
    {
        Assert.Equal(150, Vesper.MAX_SCHEDULE_SIZE);
        Assert.Equal(150, Pipistrelle.MAX_SCHEDULE_SIZE);
        Assert.True(Vesper.MAX_SCHEDULE_SIZE < FirmwareMaxScheduleSize);   // count >= 160 refuses to arm
        Assert.Equal(150, DeviceCapabilities.MaxScheduleEntries(DeviceTypes.Vesper));
        Assert.Equal(150, DeviceCapabilities.MaxScheduleEntries(DeviceTypes.Pipistrelle));
        Assert.Equal(150, DeviceCapabilities.MaxScheduleEntries(null));
        Assert.Equal(Kol.MAX_SCHEDULE_SIZE, DeviceCapabilities.MaxScheduleEntries(DeviceTypes.Kol));
        Assert.Equal(Nanotag.MAX_SCHEDULE_ENTRIES, DeviceCapabilities.MaxScheduleEntries(DeviceTypes.Nanotag));
    }

    /// <summary>Every JSON number at the widest a uint32 prints (4294967295), every non-name string padded to 16.</summary>
    private static void Widen(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject o:
                foreach (var key in o.Select(p => p.Key).ToList())
                {
                    JsonNode? v = o[key];
                    if (v is JsonValue jv && jv.TryGetValue(out JsonElement e))
                    {
                        if (e.ValueKind == JsonValueKind.Number) o[key] = uint.MaxValue;
                        else if (e.ValueKind == JsonValueKind.String && key != "name" && key != "time" && e.GetString()!.Length < 16)
                            o[key] = new string('X', 16);
                    }
                    else Widen(v);
                }
                break;
            case JsonArray a:
                for (int i = 0; i < a.Count; i++)
                {
                    if (a[i] is JsonValue jv && jv.TryGetValue(out JsonElement e) && e.ValueKind == JsonValueKind.Number)
                        a[i] = uint.MaxValue;
                    else Widen(a[i]);
                }
                break;
        }
    }

    [Fact]
    public void WorstCaseConfigJson_With150Entries_FitsTheFirmwareConfigBuffer()
    {
        // the four widest Vesper / Pipistrelle drivers, every number at its widest
        var drivers = Vesper.SupportedDeviceDrivers.Concat(Pipistrelle.SupportedDeviceDrivers)
            .Select(d => d.GetType()).Distinct()
            .Select(t =>
            {
                var c = new ConfigurationJSON { Name = "vesper" };
                c.DeviceDrivers.Add((ConfigurationDeviceDriver)Activator.CreateInstance(t)!);
                JsonNode dev = JsonNode.Parse(JsonSerializer.Serialize(c, Options()))!["devices"]![0]!;
                Widen(dev);
                return dev.ToJsonString();
            })
            .OrderByDescending(s => s.Length)
            .Take(FirmwareMaxDevices)
            .ToList();

        var config = new ConfigurationJSON
        {
            Name = "vesper",
            CDrift = uint.MaxValue,
            TempComp = true,
            TempCompCurvature = uint.MaxValue,
            TempCompTurnover = uint.MaxValue,
            TempCompPeriod = uint.MaxValue,
            BatteryCapacity = uint.MaxValue,
            ScheduleType = ScheduleTypes.Dated,
        };
        config.PowerOn.PowerOn = "2099-12-31 23:59:58";
        for (int i = 0; i < Vesper.MAX_SCHEDULE_SIZE; i++)
            config.Schedule.Add(new ConfigScheduleJSONItem { Alarm = new DateTime(2099, 12, 31, 23, 59, 58), Configuration = WorkingConfiguration.Config2 });

        JsonNode root = JsonNode.Parse(JsonSerializer.Serialize(config, Options()))!;
        foreach (string key in new[] { "minhw", "prxkey" })
            root[key] = new string('X', 16);       // firmware fields of 16 characters, at their longest
        var devices = new JsonArray(drivers.Select(d => JsonNode.Parse(d)).ToArray());
        root["devices"] = devices;
        foreach (JsonNode? e in root["schedule"]!.AsArray())
            e!["config"] = uint.MaxValue;

        // the editor writes the JSON with StreamWriter.WriteLineAsync: + a line break
        int size = Encoding.UTF8.GetByteCount(root.ToJsonString() + "\r\n");
        output.WriteLine($"worst-case config.json, {Vesper.MAX_SCHEDULE_SIZE} entries + {drivers.Count} devices: {size} bytes (limit < {FirmwareCacheSize})");

        Assert.Equal(Vesper.MAX_SCHEDULE_SIZE, root["schedule"]!.AsArray().Count);
        Assert.True(size < FirmwareCacheSize, $"{size} bytes");
    }

    [Fact]
    public void ScheduleControl_RefusesEntriesBeyondTheDeviceLimit()
    {
        var vm = new ScheduleControlViewModel(Array.Empty<ConfigScheduleJSONItem>());
        Assert.Equal(150, vm.MaxEntries);

        for (int i = 0; i < 150; i++)
            Assert.True(vm.TryAddEntry(new ConfigScheduleJSONItem { Alarm = new DateTime(2000, 1, 1, 0, 0, 0).AddMinutes(i), Configuration = WorkingConfiguration.Config1 }));

        Assert.False(vm.CanAddEntry);
        Assert.False(vm.TryAddEntry(new ConfigScheduleJSONItem { Alarm = new DateTime(2000, 1, 2), Configuration = WorkingConfiguration.Off }));
        Assert.Equal(150, vm.ScheduleEventsList.Count);
        Assert.Equal("150 / 150", vm.EntryCountText);
        Assert.False(vm.IsOverLimit);

        vm.MaxEntries = DeviceCapabilities.MaxScheduleEntries(DeviceTypes.Kol);   // a 150-entry file, then KOL chosen
        Assert.True(vm.IsOverLimit);
    }
}
