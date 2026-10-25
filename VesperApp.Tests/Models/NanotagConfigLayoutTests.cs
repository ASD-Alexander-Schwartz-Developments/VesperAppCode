using System.Runtime.InteropServices;
using VesperApp.Models;
using Xunit;

namespace VesperApp.Tests.Models;

/// <summary>
/// The host's NanotagGeneralConfiguration must match the firmware's device_general_params_t
/// (NANOTAG firmware src/params.h, 1.37) byte for byte — the 256-byte general row is copied raw
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
    [InlineData("poweron", 10)]
    [InlineData("rtc_tcomp", 11)]
    [InlineData("rtc_tck", 12)]
    [InlineData("rtc_tct0", 14)]
    [InlineData("rtc_tcper", 16)]
    [InlineData("res", 18)]
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
        Assert.All(b[18..64], x => Assert.Equal(0, x));
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
}
