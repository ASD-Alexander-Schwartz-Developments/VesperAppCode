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
    [InlineData((ushort)0x0999, (byte)3, "unknown")]
    public void CodeName_MapsCatalogueAndMissingSensorPerFamily(ushort code, byte deviceType, string expected)
        => Assert.Equal(expected, LoggerParser.CodeName(code, deviceType));

    [Theory]
    [InlineData((byte)3, 0u, "none")]
    [InlineData((byte)3, 0x102u, "KOL+TPRH31")]
    [InlineData((byte)1, 0x3u, "GPS+DMIKE")]
    [InlineData((byte)3, 0x80000000u, "bit31")]
    public void SensorNames_UsesFamilySpecificBitNames(byte deviceType, uint bits, string expected)
        => Assert.Equal(expected, LoggerParser.SensorNames(deviceType, bits));

    [Theory]
    [InlineData(1, "error")]
    [InlineData(2, "warning")]
    [InlineData(3, "debug")]
    [InlineData(7, "7")]
    public void LevelName_Maps(int level, string expected) => Assert.Equal(expected, LoggerParser.LevelName(level));
}
