using System.Text;
using System.Text.Json.Nodes;
using VesperApp.Models;
using VesperApp.Services;
using Xunit;

namespace VesperApp.Tests.Services;

/// <summary>
/// Nanotag page helpers: GET_DISKHEALTH parsing (firmware 1.39, asd-interfaces nanotag-usb-vendor.md), the config
/// read-back report from the three EEPROM rows, packed RTC times, the console command history.
/// </summary>
public class NanotagServiceTests
{
    private static byte[] Health(params uint[] v)
    {
        var b = new byte[NanotagDiskHealth.Length];
        for (int i = 0; i < 9; i++) BitConverter.GetBytes(v[i]).CopyTo(b, i * 4);
        return b;
    }

    [Fact]
    public void DiskHealth_ParsesTheFirmwareLayout()
    {
        byte[] p = Health(7, 2041, 1, 12, 2, 3, 256, 256 + 6400, 126848);
        p[36] = 95; p[37] = 0x01; p[38] = 42;
        NanotagDiskHealth h = NanotagDiskHealth.Parse(p)!;
        Assert.Equal(7u, h.BadBlocks);
        Assert.Equal(2041u, h.GoodBlocks);
        Assert.Equal(1u, h.BlocksToRetire);
        Assert.Equal(12u, h.EccCorrected);
        Assert.Equal(2u, h.EccUncorrectable);
        Assert.Equal(3u, h.ReadErrors);
        Assert.Equal(6400u, h.PagesWritten);
        Assert.Equal(126848u, h.DataEndPage);
        Assert.Equal(95, h.FreePercent);
        Assert.True(h.Formatting);
        Assert.False(h.DataAreaFull);
        Assert.Equal(42, h.FormatPercent);

        string text = NanotagService.Describe(h);
        Assert.Contains("2041 good, 7 bad, 1 to retire", text);
        Assert.Contains("6400 of 126592 pages written, 95 % free", text);
        Assert.Contains("Format in progress: 42 %", text);
        Assert.Contains("format before the next deployment", text);          // uncorrectable pages seen
    }

    [Fact]
    public void DiskHealth_ShortReplyIsNull()
    {
        Assert.Null(NanotagDiskHealth.Parse(null));
        Assert.Null(NanotagDiskHealth.Parse(new byte[39]));
    }

    [Theory]
    [InlineData(0, "OK")]
    [InlineData(1, "ECC corrected")]
    [InlineData(3, "uncorrectable ECC error")]
    [InlineData(0xEE, "read error")]
    [InlineData(0x55, "status 0x55")]
    public void PageStatusNames(byte s, string name) => Assert.Equal(name, NanotagService.PageStatusName(s));

    [Fact]
    public void ConfigReport_FromTheConverterRows()
    {
        const string json = """
            { "name": "NANOTAG", "minhw": "1", "scheduleType": 2, "magnetOff": true, "battery": 60,
              "poweron": "2026-10-25 12:34:56", "tcomp": true, "tck": 34, "tct0": 255, "tcper": 600,
              "schedule": [ { "time": "2026-11-01 06:00:00", "config": 1 }, { "time": "2026-11-01 18:30:00", "config": 2 } ],
              "devices": [] }
            """;
        byte[] cfg = Nanotag.ConfigBinaryConverter(json);
        Assert.Equal(768, cfg.Length);
        byte[] gen = cfg[..256], dev = cfg[256..512], sch = cfg[512..768];

        // one driver slot, written as the firmware's device_config_params_t
        Encoding.ASCII.GetBytes("TPRH31").CopyTo(dev, 64);
        BitConverter.GetBytes(10000u).CopyTo(dev, 64 + 16);
        BitConverter.GetBytes(0x02u).CopyTo(dev, 64 + 44);

        JsonObject r = NanotagService.DescribeConfig(gen, dev, sch);
        JsonObject g = r["general"]!.AsObject();
        Assert.Equal("dated", (string?)g["schedule_type"]);
        Assert.Equal(60, (int)g["battery_capacity_mAh"]!);
        Assert.True((bool)g["magnet_off"]!);
        Assert.True((bool)g["tcomp"]!);
        Assert.Equal(255, (int)g["tct0_0p1C"]!);
        Assert.Equal("2026-10-25 12:34:56", (string?)g["delayed_start"]);

        JsonArray drivers = r["drivers"]!.AsArray();
        Assert.Single(drivers);
        Assert.Equal("TPRH31", (string?)drivers[0]!["driver"]);
        Assert.Equal(2, (int)drivers[0]!["slot"]!);
        Assert.Equal(10000L, (long)drivers[0]!["sample_rate"]![0]!);
        Assert.Equal("0x00000002", (string?)drivers[0]!["control_bitmask"]);

        JsonArray s = r["schedule"]!.AsArray();
        Assert.Equal(2, s.Count);
        Assert.Equal("2026-11-01 06:00:00", (string?)s[0]!["time"]);
        Assert.Equal(2, (int)s[1]!["config"]!);
    }

    [Fact]
    public void Crc32_MatchesTheFirmware()
    {
        Assert.Equal(0xCBF43926u, NanotagService.Crc32(Encoding.ASCII.GetBytes("123456789")));   // IEEE check value
        Assert.Equal(0u, NanotagService.Crc32(ReadOnlySpan<byte>.Empty));
    }

    [Fact]
    public void PageCrc_CheckedWhenPresent()
    {
        var page = new byte[4096 + 128];
        for (int i = 0; i < 4096; i++) page[i] = (byte)(i * 7);
        Assert.True(NanotagService.PageCrcOk(page));                          // 0: no CRC (older firmware)
        BitConverter.GetBytes(NanotagService.Crc32(page.AsSpan(0, 4096))).CopyTo(page, NanotagService.PageCrcOffset);
        Assert.True(NanotagService.PageCrcOk(page));
        page[100] ^= 0x01;
        Assert.False(NanotagService.PageCrcOk(page));
    }

    [Fact]
    public void PackedTimes()
    {
        uint t = (6u << 26) | (11u << 22) | (1u << 17) | (18u << 12) | (30u << 6) | 5u;
        Assert.Equal("2026-11-01 18:30:05", NanotagService.FormatPacked(t, NanotagService.PackedKind.Absolute));
        Assert.Equal("18:30:05", NanotagService.FormatPacked(t, NanotagService.PackedKind.Daily));
        uint rel = (3u << 17) | (2u << 12);
        Assert.Equal("+3 d 02:00:00", NanotagService.FormatPacked(rel, NanotagService.PackedKind.Relative));
    }

    [Fact]
    public void ConsoleHistory_UpAndDown()
    {
        var h = new ConsoleHistory(3);
        Assert.Null(h.Previous());
        h.Add("ver"); h.Add("disk"); h.Add("disk"); h.Add("  "); h.Add("config");
        Assert.Equal(new[] { "ver", "disk", "config" }, h.Items);
        Assert.Equal("config", h.Previous());
        Assert.Equal("disk", h.Previous());
        Assert.Equal("ver", h.Previous());
        Assert.Equal("ver", h.Previous());                                   // stays on the oldest
        Assert.Equal("disk", h.Next());
        Assert.Equal("config", h.Next());
        Assert.Equal("", h.Next());                                          // past the newest: empty line
        h.Add("help");
        Assert.Equal(new[] { "disk", "config", "help" }, h.Items);           // capped at 3
    }
}
