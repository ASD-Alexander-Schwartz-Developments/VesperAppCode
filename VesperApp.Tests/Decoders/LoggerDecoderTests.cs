using System.Text.Json;
using System.Text.RegularExpressions;
using VesperApp.Services;
using Xunit;

namespace VesperApp.Tests.Decoders;

/// <summary>LOGGER pseudo-sensor (/log/&lt;n&gt;H.BIN) against the golden fixture logger-kol.H.BIN
/// (asd-interfaces interfaces/logger-file.md, KOL device type 3).</summary>
public class LoggerDecoderTests
{
    private const string Fixture = "logger-kol.H.BIN";
    private static readonly DateTime Epoch = new(2000, 1, 1);

    private static string Time(long t) => Epoch.AddSeconds(t).ToString("yyyy-MM-dd HH:mm:ss");

    private static string[] ExpectedSampleLines(JsonElement exp)
    {
        // KOL sensor names by driver type bit (logger.c SENSOR_Typedef on KOL)
        string[] kol = { "DMIKE", "KOL", "EADC", "IMU", "EEG", "THCAMERA", "ALS", "AUX4", "TPRH31", "LED", "LOGGER" };
        return exp.GetProperty("samples").EnumerateArray().Select(s =>
        {
            int temp = s.GetProperty("temp_centi_c").GetInt32();
            uint bits = s.GetProperty("sensors").GetUInt32();
            var on = Enumerable.Range(0, 32).Where(b => (bits & (1u << b)) != 0).Select(b => kol[b]).ToList();
            return string.Join(",",
                Time(s.GetProperty("t").GetInt64()),
                s.GetProperty("battery_mv").GetInt32().ToString(),
                temp == int.MinValue ? "" : (temp / 100.0).ToString("F2"),
                s.GetProperty("rtc_ppb").GetInt32().ToString(),
                on.Count == 0 ? "none" : string.Join("+", on));
        }).ToArray();
    }

    [Fact]
    public void BatteryColumn_AboveRange_NoReading_AndValue()
    {
        using var dir = new TempDir();
        static byte[] Sample(uint secs, int battery)
        {
            var r = new byte[12];
            r[0] = 0x01; r[1] = 12; BitConverter.GetBytes((ushort)0x1).CopyTo(r, 2);   // battery field only
            BitConverter.GetBytes(secs).CopyTo(r, 4);
            BitConverter.GetBytes(battery).CopyTo(r, 8);
            return r;
        }
        byte[] data = Sample(60, 1862).Concat(Sample(120, LoggerParser.BatteryAboveRange)).Concat(Sample(180, -1)).ToArray();
        string name = Path.Combine(dir.Path, "H.HBN");

        using (var p = new LoggerParser(name, data, 0x301)) { p.WriteFile(); }

        string[] lines = File.ReadAllLines(name + ".csv");
        Assert.Equal("2000-01-01 00:01:00,1862,,,", lines[1]);
        Assert.Equal("2000-01-01 00:02:00,above range,,,", lines[2]);   // KOL: node above ~3.9 V, a reading
        Assert.Equal("2000-01-01 00:03:00,,,,", lines[3]);              // -1: no reading
    }

    [Fact]
    public async Task AutoDecode_RawLoggerFile_ProducesHealthIntermediateSamplesAndEvents()
    {
        using var dir = new TempDir();
        string raw = Fixtures.CopyAs(Fixture, dir);
        JsonElement exp = Fixtures.Json(Fixture);

        DecodeSummary sum = await RecordingPipeline.AutoDecodeAsync(new[] { raw });

        Assert.Equal(1, sum.RawParsed);
        Assert.Equal(0, sum.Failed);
        Assert.Equal(1, sum.Csv);

        string health = Path.Combine(dir.Path, "HEALTH");
        string hbn = Assert.Single(Directory.GetFiles(health, "*.HBN"));
        // start block 2026-10-25 09:30:00, footer 09:33:01 (subsecond fraction 255, value 0 -> .996)
        Assert.Matches(new Regex(@"^H2026_10_25_09_30_00_\d{3}-H2026_10_25_09_33_01_\d{3}\.HBN$"), Path.GetFileName(hbn));

        // The intermediate is the record area: the bytes between start block and footer.
        Assert.Equal(exp.GetProperty("record_bytes").GetInt32(), new FileInfo(hbn).Length);

        // Sidecar: header fields the decoder needs (Config1 = format | device type << 8).
        string[] meta = File.ReadAllLines(hbn + ".txt");
        Assert.Contains("Sensor:LOGGER", meta);                 // NUL padding of the header name trimmed
        Assert.Contains("SampleRate:" + exp.GetProperty("period_ms").GetInt32(), meta);
        Assert.Contains("Config0:" + exp.GetProperty("logmask").GetInt32().ToString("X"), meta);
        Assert.Contains("Config1:301", meta);

        string[] samples = File.ReadAllLines(hbn + ".csv");
        Assert.Equal("Time,Battery [mV],MCU temperature [C],RTC correction [ppb],Sampling sensors", samples[0]);
        Assert.Equal(ExpectedSampleLines(exp), samples.Skip(1).ToArray());
        // spot checks of the contract details
        Assert.Equal("2026-10-25 09:31:00,2950,24.31,-205000,KOL", samples[1]);
        Assert.Equal("2026-10-25 09:32:00,2948,,-205000,none", samples[2]);          // 0x80000000 = no reading
        Assert.Equal("2026-10-25 09:33:00,2947,-5.12,-204000,KOL+TPRH31", samples[3]); // negative temperature

        string[] events = File.ReadAllLines(hbn + ".log.csv");
        Assert.Equal("Time,Level,Code,Event", events[0]);
        var expEvents = exp.GetProperty("events").EnumerateArray().ToList();
        Assert.Equal(expEvents.Count, events.Length - 1);
        for (int i = 0; i < expEvents.Count; i++)
        {
            string[] col = events[i + 1].Split(',');
            ushort code = (ushort)expEvents[i].GetProperty("code").GetInt32();
            Assert.Equal(Time(expEvents[i].GetProperty("t").GetInt64()), col[0]);
            Assert.Equal(LoggerParser.LevelName(expEvents[i].GetProperty("level").GetInt32()), col[1]);
            Assert.Equal($"0x{code:X4}", col[2]);
            Assert.Equal(LoggerParser.CodeName(code, 3), col[3]);
        }
        Assert.Equal("2026-10-25 09:30:00,debug,0x0101,Firmware started", events[1]);
        Assert.Equal("2026-10-25 09:30:00,error,0x0107,Error handler ran since the previous boot", events[3]);
        Assert.Equal("2026-10-25 09:31:15,warning,0x0603,Audio samples missed (overrun)", events[5]);
        Assert.Equal("2026-10-25 09:32:01,error,0x0808,Configured sensor not found: TPRH31", events[6]);
    }

    [Fact]
    public void Parser_RecordArea_WithAndWithoutStartBlock_DecodesIdentically()
    {
        using var dir = new TempDir();
        byte[] file = File.ReadAllBytes(Fixtures.PathOf(Fixture));
        byte[] withBlock = file.Skip(128).Take(file.Length - 128 - 16).ToArray();   // start block + records
        byte[] recordsOnly = withBlock.Skip(16).ToArray();

        string a = dir.File("a.HBN"), b = dir.File("b.HBN");
        using (var p = new LoggerParser(a, withBlock, 0x301)) { Assert.Equal(0, p.BadRecords); p.WriteFile(); }
        using (var p = new LoggerParser(b, recordsOnly, 0x301)) { Assert.Equal(0, p.BadRecords); p.WriteFile(); }

        Assert.Equal(File.ReadAllLines(a + ".csv"), File.ReadAllLines(b + ".csv"));
        Assert.Equal(File.ReadAllLines(a + ".log.csv"), File.ReadAllLines(b + ".log.csv"));
        Assert.Equal(4, File.ReadAllLines(a + ".csv").Length);
        Assert.Equal(7, File.ReadAllLines(a + ".log.csv").Length);
    }

    [Fact]
    public void Parser_CorruptWord_IsSkippedAndCountedThenResynchronises()
    {
        using var dir = new TempDir();
        byte[] file = File.ReadAllBytes(Fixtures.PathOf(Fixture));
        byte[] records = file.Skip(128 + 16).Take(file.Length - 128 - 32).ToArray();
        byte[] corrupt = Raw.Concat(new byte[] { 0x77, 0x03, 0x00, 0x00 }, records);   // bad type, bad size

        string f = dir.File("c.HBN");
        using (var p = new LoggerParser(f, corrupt, 0x301))
        {
            Assert.Equal(1, p.BadRecords);
            p.WriteFile();
        }
        Assert.Equal(4, File.ReadAllLines(f + ".csv").Length);
        Assert.Equal(7, File.ReadAllLines(f + ".log.csv").Length);
    }

    [Fact]
    public void Parser_PartialSample_OnlyPresentFieldsFilled()
    {
        using var dir = new TempDir();
        // sample with info = battery | sensors (bits 0 and 3), 16 bytes
        var ms = new MemoryStream();
        var w = new BinaryWriter(ms);
        w.Write((byte)0x01); w.Write((byte)16); w.Write((ushort)0x09); w.Write(846235860u);
        w.Write(3001); w.Write(1u << 13);      // Vesper bit 13 = LOGGER
        string f = dir.File("p.HBN");
        using (var p = new LoggerParser(f, ms.ToArray(), 0x101)) p.WriteFile();   // device type 1 (Vesper)
        string[] lines = File.ReadAllLines(f + ".csv");
        Assert.Equal("2026-10-25 09:31:00,3001,,,LOGGER", lines[1]);
        Assert.False(File.Exists(f + ".log.csv"));        // no events -> no event file
    }

    [Theory]
    [InlineData((ushort)0x0808, (byte)3, "Configured sensor not found: TPRH31")]
    [InlineData((ushort)0x0809, (byte)1, "Configured sensor not found: TPRH31")]   // Vesper numbering
    [InlineData((ushort)0x0803, (byte)3, "Configured sensor not found: IMU")]
    [InlineData((ushort)0x0804, (byte)1, "Configured sensor not found: IMU")]
    [InlineData((ushort)0x0840, (byte)3, "Configured sensor not found: type 64")]
    [InlineData((ushort)0x0104, (byte)3, "Reset: software")]
    [InlineData((ushort)0x0405, (byte)1, "RTC synchronised by the host")]
    [InlineData((ushort)0x0A99, (byte)3, "unknown")]
    // KOL watchdog (kol_wdg.h): option-byte reset, IWDG off, supervisor resets per client, HardFault
    [InlineData((ushort)0x0109, (byte)3, "Reset: option bytes loaded (one-time setup)")]
    [InlineData((ushort)0x010A, (byte)3, "Watchdog not running (option bytes not set)")]
    [InlineData((ushort)0x0900, (byte)3, "Reset by the watchdog supervisor: main loop stalled")]
    [InlineData((ushort)0x0901, (byte)3, "Reset by the watchdog supervisor: audio recorder stalled")]
    [InlineData((ushort)0x0904, (byte)3, "Reset by the watchdog supervisor: TPRH stalled")]
    [InlineData((ushort)0x0907, (byte)3, "Reset by the watchdog supervisor: client 7 stalled")]
    [InlineData((ushort)0x090F, (byte)3, "Reset after a CPU fault (HardFault)")]
    // codes added after the first catalogue (Nanotag NAND, VesperU5 1.22)
    [InlineData((ushort)0x0205, (byte)0, "NAND page read uncorrectable (ECC)")]
    [InlineData((ushort)0x0206, (byte)0, "NAND page read corrected at/above the ECC threshold")]
    [InlineData((ushort)0x0207, (byte)0, "NAND block retired")]
    [InlineData((ushort)0x0208, (byte)0, "NAND bad block table repaired at boot")]
    [InlineData((ushort)0x0304, (byte)1, "Battery low: recording stopped")]
    [InlineData((ushort)0x0505, (byte)1, "config.json refused, nothing applied")]
    // Nanotag (device type 0): its own SENSOR_Typedef numbering
    [InlineData((ushort)0x0401, (byte)0, "MCU temperature reading not possible")]
    [InlineData((ushort)0x0504, (byte)0, "Schedule finished")]
    [InlineData((ushort)0x0704, (byte)0, "Sensor reading failed")]
    [InlineData((ushort)0x0800, (byte)0, "Configured sensor not found: ACLYS")]
    [InlineData((ushort)0x0801, (byte)0, "Configured sensor not found: NANOACC")]
    [InlineData((ushort)0x0802, (byte)0, "Configured sensor not found: PROXTIT")]
    [InlineData((ushort)0x0807, (byte)0, "Configured sensor not found: ALS3001D")]
    [InlineData((ushort)0x0809, (byte)0, "Configured sensor not found: TPRH31")]
    public void CodeName_MapsCatalogueAndMissingSensorPerFamily(ushort code, byte deviceType, string expected)
        => Assert.Equal(expected, LoggerParser.CodeName(code, deviceType));

    [Theory]
    [InlineData((byte)3, 0u, "none")]
    [InlineData((byte)3, 0x102u, "KOL+TPRH31")]
    [InlineData((byte)1, 0x3u, "GPS+DMIKE")]
    [InlineData((byte)3, 0x80000000u, "bit31")]
    [InlineData((byte)0, 0x283u, "ACLYS+NANOACC+ALS3001D+TPRH31")]   // Nanotag
    [InlineData((byte)0, 0x4u, "PROXTIT")]
    public void SensorNames_UsesFamilySpecificBitNames(byte deviceType, uint bits, string expected)
        => Assert.Equal(expected, LoggerParser.SensorNames(deviceType, bits));

    [Theory]
    [InlineData(1, "error")]
    [InlineData(2, "warning")]
    [InlineData(3, "debug")]
    [InlineData(7, "7")]
    public void LevelName_Maps(int level, string expected) => Assert.Equal(expected, LoggerParser.LevelName(level));
}
