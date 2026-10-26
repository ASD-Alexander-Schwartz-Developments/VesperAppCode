using System.Text;
using System.Text.Json;
using VesperApp.Models;
using VesperApp.Services;
using Xunit;

namespace VesperApp.Tests.Decoders;

/// <summary>
/// Nanotag NAND pages from synthetic vectors built from the firmware structs (CgFSInterface.h FSGenericMetaData_t,
/// logger_record.c, addon.c, prox_log_entry_t): the LOGGER log area (asd-interfaces interfaces/logger-file.md,
/// Nanotag) and the add-on pages 0x23 / 0x24 (nanotag-addon-pages.md).
/// </summary>
public class NanotagPagesTests
{
    private const uint FwVer139 = 0x00000127;

    /// <summary>A 4096 + 128 byte page: records from offset 0, 0xFF tail, FSGenericMetaData_t in the spare.</summary>
    private static byte[] Page(byte type, byte subType, byte[] records, Action<BinaryWriter> metadata, uint fwVer = FwVer139)
    {
        var p = Enumerable.Repeat((byte)0xFF, NanotagPage.FullSize).ToArray();
        records.CopyTo(p, 0);
        var spare = new MemoryStream();
        var w = new BinaryWriter(spare);
        w.Write(0x54445341u);               // preamble
        w.Write((ushort)60);                // metaDataLength
        w.Write(type);
        w.Write(subType);
        w.Write((ushort)0);                 // pageRevision
        w.Write((ushort)0x9000);            // battLevel
        w.Write((uint)records.Length);      // dataLength
        w.Write(0u); w.Write(0u);           // checksums
        w.Write(fwVer);                     // offset 24
        metadata(w);                        // offset 28: union
        spare.ToArray().CopyTo(p, NanotagPage.DataSize);
        return p;
    }

    private static byte[] LogPage(uint seq, uint first, uint last, byte[] records) =>
        Page(NanotagPage.TypeLog, 0xFF, records, w =>
        {
            w.Write(seq); w.Write(first); w.Write(last);
            w.Write((ushort)0x2F); w.Write((byte)1); w.Write((byte)0);
        });

    private static byte[] AddonPage(byte type, byte sub, uint seqId, uint stamp, uint periodMs, byte[] records) =>
        Page(type, sub, records, w =>
        {
            w.Write(stamp); w.Write((ushort)0); w.Write((ushort)periodMs); w.Write(seqId); w.Write(periodMs); w.Write(0u);
        });

    // LOGGER records (logger_record.c): u8 type, u8 size, u16 info, u32 seconds since 2000
    private static byte[] Sample(uint t, int mv, int centiC, int ppb, uint sensors)
    {
        var ms = new MemoryStream(); var w = new BinaryWriter(ms);
        w.Write((byte)0x01); w.Write((byte)24); w.Write((ushort)0x0F); w.Write(t);
        w.Write(mv); w.Write(centiC); w.Write(ppb); w.Write(sensors);
        return ms.ToArray();
    }

    private static byte[] Event(uint t, int level, ushort code)
    {
        var ms = new MemoryStream(); var w = new BinaryWriter(ms);
        w.Write((byte)(0x10 + level)); w.Write((byte)8); w.Write(code); w.Write(t);
        return ms.ToArray();
    }

    private static readonly DateTime Epoch = new(2000, 1, 1);
    private static uint Secs(DateTime t) => (uint)(t - Epoch).TotalSeconds;

    private static byte Bcd(int v) => (byte)((v / 10) << 4 | (v % 10));

    /// <summary>12-byte slow-sensor header: header, flags, weekday/month/date/year BCD, h/m/s BCD + hundredths, u16 subsecond.</summary>
    private static void SlowHeader(BinaryWriter w, byte header, DateTime t)
    {
        w.Write(header); w.Write((byte)0);
        w.Write((byte)1); w.Write(Bcd(t.Month)); w.Write(Bcd(t.Day)); w.Write(Bcd(t.Year - 2000));
        w.Write(Bcd(t.Hour)); w.Write(Bcd(t.Minute)); w.Write(Bcd(t.Second)); w.Write((byte)0);
        w.Write((ushort)1023);                                   // subsecond: 0 ms
    }

    private static byte[] Tprh(DateTime t, ushort rawT, ushort rawRh)
    {
        var ms = new MemoryStream(); var w = new BinaryWriter(ms);
        SlowHeader(w, 0x59, t);
        w.Write((byte)(rawT >> 8)); w.Write((byte)rawT); w.Write((byte)0x00);     // T MSB, LSB, CRC
        w.Write((byte)(rawRh >> 8)); w.Write((byte)rawRh); w.Write((byte)0x00);
        return ms.ToArray();
    }

    private static byte[] Als(DateTime t, ushort result)
    {
        var ms = new MemoryStream(); var w = new BinaryWriter(ms);
        SlowHeader(w, 0x5A, t);
        w.Write(result);
        return ms.ToArray();
    }

    private static byte[] Encounter(uint unixStart, uint duration, byte[] addrLsbFirst, sbyte rssi, sbyte tx)
    {
        var ms = new MemoryStream(); var w = new BinaryWriter(ms);
        w.Write(unixStart); w.Write(duration); w.Write(addrLsbFirst); w.Write(rssi); w.Write(tx);
        return ms.ToArray();
    }

    /// <summary>Nanotag RTC MODE2 word: second:6 minute:6 hour:5 day:5 month:4 year:6 (since 2020).</summary>
    private static uint Rtc(DateTime t) => Nanotag.ToTimestamp(t);

    // ───────────── log area ─────────────

    [Fact]
    public void LogRange_ParsesFourWords()
    {
        byte[] r = new uint[] { 1000, 2000, 1500, 1600 }.SelectMany(BitConverter.GetBytes).ToArray();
        Assert.Equal((1000u, 2000u, 1500u, 1600u), NanotagLog.ParseRange(r)!.Value);
        Assert.Null(NanotagLog.ParseRange(new byte[15]));
    }

    [Fact]
    public void LogReadOrder_NoWrap_WrapAndEmpty()
    {
        Assert.Equal(new uint[] { 10, 11, 12 }, NanotagLog.ReadOrder(10, 20, 10, 12));
        Assert.Equal(new uint[] { 18, 19, 10, 11 }, NanotagLog.ReadOrder(10, 20, 18, 11));    // ring wrapped
        Assert.Equal(new uint[] { 15 }, NanotagLog.ReadOrder(10, 20, 15, 15));
        Assert.Empty(NanotagLog.ReadOrder(10, 20, NanotagLog.Empty, NanotagLog.Empty));       // empty log
        Assert.Empty(NanotagLog.ReadOrder(10, 20, 25, 12));                                   // outside the area
        Assert.Equal(10, NanotagLog.ReadOrder(10, 20, 11, 10).Count);                         // full ring
    }

    [Theory]
    [InlineData(0u, 64u)]
    [InlineData(63u, 64u)]
    [InlineData(64u, 128u)]
    public void LogNextBlock(uint page, uint next) => Assert.Equal(next, NanotagLog.NextBlock(page));

    [Fact]
    public void LogPages_AssembledInSeqOrder_DecodeWithNanotagNames()
    {
        var t0 = new DateTime(2026, 10, 26, 8, 0, 0);
        byte[] r1 = Event(Secs(t0), 3, 0x0101).Concat(Event(Secs(t0), 3, 0x0104)).Concat(Event(Secs(t0), 3, 0x0501)).ToArray();
        byte[] r2 = Sample(Secs(t0.AddSeconds(60)), 3950, 2312, -1500, 0x283)
            .Concat(Event(Secs(t0.AddSeconds(61)), 1, 0x0809))
            .Concat(Event(Secs(t0.AddSeconds(62)), 2, 0x0206)).ToArray();

        // read in ring order: the newer page first (wrapped), plus an unrelated data page and an erased page
        var pages = new[]
        {
            NanotagPage.TryParse(LogPage(8, Secs(t0.AddSeconds(60)), Secs(t0.AddSeconds(62)), r2)),
            NanotagPage.TryParse(LogPage(7, Secs(t0), Secs(t0), r1)),
            NanotagPage.TryParse(AddonPage(NanotagPage.TypeSns, 9, 0, 0, 1000, new byte[18])),
            NanotagPage.TryParse(Enumerable.Repeat((byte)0xFF, NanotagPage.FullSize).ToArray()),
        }.Where(p => p != null).Select(p => p!).ToList();
        Assert.Equal(3, pages.Count);

        byte[] records = NanotagLog.Assemble(pages, out ushort logmask, out uint first, out uint last);
        Assert.Equal(r1.Concat(r2).ToArray(), records);
        Assert.Equal(0x2F, logmask);
        Assert.Equal(Secs(t0), first);
        Assert.Equal(Secs(t0.AddSeconds(62)), last);

        using var dir = new TempDir();
        string hbn = NanotagLog.Write(dir.Path, records, logmask, first, last, "ABC123")!;
        Assert.Equal("H2026_10_26_08_00_00_000-H2026_10_26_08_01_02_000.HBN", Path.GetFileName(hbn));
        Assert.Contains("Config1:1", File.ReadAllLines(hbn + ".txt"));

        string[] samples = File.ReadAllLines(hbn + ".csv");
        Assert.Equal("2026-10-26 08:01:00,3950,23.12,-1500,ACLYS+NANOACC+ALS3001D+TPRH31", samples[1]);
        string[] events = File.ReadAllLines(hbn + ".log.csv");
        Assert.Equal(6, events.Length);
        Assert.Equal("2026-10-26 08:00:00,debug,0x0104,Reset: software", events[2]);
        Assert.Equal("2026-10-26 08:01:01,error,0x0809,Configured sensor not found: TPRH31", events[4]);
        Assert.Equal("2026-10-26 08:01:02,warning,0x0206,NAND page read corrected at/above the ECC threshold", events[5]);
    }

    [Fact]
    public void LogWrite_NoRecords_WritesNothing()
    {
        using var dir = new TempDir();
        Assert.Null(NanotagLog.Write(dir.Path, Array.Empty<byte>(), 0, 0, 0));
        Assert.False(Directory.Exists(Path.Combine(dir.Path, "HEALTH")));
    }

    [Fact]
    public void Page_TryParse_ReadsSpareFields()
    {
        var p = NanotagPage.TryParse(AddonPage(NanotagPage.TypePrx, 2, 5, 0x1234, 30000, new byte[32]))!;
        Assert.Equal(NanotagPage.TypePrx, p.PageType);
        Assert.Equal(2, p.PageSubType);
        Assert.Equal(32u, p.DataLength);
        Assert.Equal(5u, p.SnsSeqId);
        Assert.Equal(0x1234u, p.SnsTimestamp);
        Assert.Equal(30000u, p.SnsPeriodMs);
        Assert.Equal(new Version(1, 39), NanotagPage.ReleaseOf(p.FirmwareVersion));
        Assert.Equal(new Version(1, 37), NanotagPage.ReleaseOf(0x00010125));     // <= 1.37: MINOR twice
        Assert.Null(NanotagPage.TryParse(new byte[100]));
    }

    // ───────────── add-on pages ─────────────

    [Fact]
    public void AddonPages_TprhAlsProx_WrittenAsVesperOutputsAndDecoded()
    {
        var t0 = new DateTime(2026, 10, 26, 9, 0, 0);
        var c = new NanotagAddonCollector();

        // TPRH31 session 1: two pages (seq 0, 1); session 2 starts again at seq 0
        byte[] tp1 = Tprh(t0, 0x6666, 0x8000).Concat(Tprh(t0.AddSeconds(10), 0x6666, 0x8000)).ToArray();
        byte[] tp2 = Tprh(t0.AddSeconds(20), 0x6666, 0x4000).ToArray();
        Assert.True(c.Add(NanotagPage.TryParse(AddonPage(NanotagPage.TypeSns, 9, 0, Rtc(t0), 10000, tp1))!));
        Assert.True(c.Add(NanotagPage.TryParse(AddonPage(NanotagPage.TypeSns, 9, 1, Rtc(t0.AddSeconds(20)), 10000, tp2))!));
        Assert.True(c.Add(NanotagPage.TryParse(AddonPage(NanotagPage.TypeSns, 9, 0, Rtc(t0.AddHours(2)), 10000, Tprh(t0.AddHours(2), 0x6666, 0x8000)))!));

        // ALS3001D: E = 2, M = 100 -> 4.00 lux
        Assert.True(c.Add(NanotagPage.TryParse(AddonPage(NanotagPage.TypeSns, 7, 0, Rtc(t0), 5000, Als(t0, 0x2064)))!));

        // ProxTit: two encounters
        byte[] prx = Encounter(1793005200, 125, new byte[] { 0x66, 0x55, 0x44, 0x33, 0x22, 0x11 }, -71, -1)
            .Concat(Encounter(1793005300, 5, new byte[] { 1, 2, 3, 4, 5, 6 }, -90, 0)).ToArray();
        Assert.True(c.Add(NanotagPage.TryParse(AddonPage(NanotagPage.TypePrx, 2, 0, Rtc(t0), 1000, prx))!));

        // a GPS snapshot page is not an add-on page
        Assert.False(c.Add(NanotagPage.TryParse(Page(NanotagPage.TypeSnap, 0, new byte[64], w => w.Write(0u)))!));
        Assert.Equal(5, c.Pages);

        using var dir = new TempDir();
        List<string> files = c.WriteAll(dir.Path, "ABC123");
        Assert.Equal(4, files.Count);

        string[] trh = Directory.GetFiles(Path.Combine(dir.Path, "TPRH"), "*.RBN").OrderBy(f => f).ToArray();
        Assert.Equal(2, trh.Length);
        Assert.Equal("R2026_10_26_09_00_00_000-R2026_10_26_09_00_20_000.RBN", Path.GetFileName(trh[0]));
        Assert.Equal(tp1.Concat(tp2).ToArray(), File.ReadAllBytes(trh[0]));
        string[] meta = File.ReadAllLines(trh[0] + ".txt");
        Assert.Contains("Sensor:TPRH31", meta);
        Assert.Contains("SampleRate:10000", meta);
        Assert.Contains("FWID:1.39", meta);
        string[] csv = File.ReadAllLines(trh[0] + ".csv");
        Assert.Equal(4, csv.Length);                                       // header + 3 samples
        Assert.EndsWith(",25.00,50.00", csv[1]);
        Assert.EndsWith(",25.00,25.00", csv[3]);

        string als = Assert.Single(Directory.GetFiles(Path.Combine(dir.Path, "ALS"), "*.LBN"));
        Assert.StartsWith("L2026_10_26_09_00_00_000", Path.GetFileName(als));
        Assert.EndsWith(",4.00", File.ReadAllLines(als + ".csv")[1]);

        string x = Assert.Single(Directory.GetFiles(Path.Combine(dir.Path, "PRX"), "*.XBN"));
        Assert.StartsWith("X2026_10_26_09_00_00_000", Path.GetFileName(x));
        string[] enc = File.ReadAllLines(x + ".csv");
        Assert.Equal("Start,End,Duration [s],Peer address,RSSI [dBm],TX power [dBm]", enc[0]);
        Assert.Equal("2026-10-26 09:00:00,2026-10-26 09:02:05,125,11:22:33:44:55:66,-71,-1", enc[1]);
        Assert.Equal("2026-10-26 09:01:40,2026-10-26 09:01:45,5,06:05:04:03:02:01,-90,0", enc[2]);
    }

    [Fact]
    public void ProxEncounterParser_SkipsBlankRecordsAndPartialTail()
    {
        byte[] data = Encounter(1793005200, 1, new byte[6] { 1, 2, 3, 4, 5, 6 }, -50, 2)
            .Concat(Enumerable.Repeat((byte)0xFF, 16)).Concat(new byte[16]).Concat(new byte[5]).ToArray();
        using var p = new ProxEncounterParser("unused", data);
        Assert.Equal(1, p.Records);
        Assert.Equal(2, p.Skipped);
    }

    [Fact]
    public async Task DecodeFiles_VesperXbn_DecodesEncounters()
    {
        // VesperU5 X.BIN after the splitter: PRX/*.XBN holding the 16-byte records
        using var dir = new TempDir();
        string xbn = dir.File("X2026_10_26_09_00_00_000-X2026_10_26_10_00_00_000.XBN");
        File.WriteAllBytes(xbn, Encounter(1793005200, 60, new byte[] { 6, 5, 4, 3, 2, 1 }, -60, 0));
        Assert.True(RecordingPipeline.IsDecodableIntermediate(xbn));
        DecodeSummary sum = await RecordingPipeline.DecodeFilesAsync(new[] { xbn });
        Assert.Equal(1, sum.Csv);
        Assert.Equal("2026-10-26 09:00:00,2026-10-26 09:01:00,60,01:02:03:04:05:06,-60,0", File.ReadAllLines(xbn + ".csv")[1]);
    }

    // ───────────── config slots ─────────────

    [Fact]
    public void Config_LoggerAndProxtitSlots_CarryLogmaskAndProxtitParams()
    {
        var logger = new ConfigLoggerDriver { LogLevel = LoggerLogLevel.Debug };
        logger.SampleRate1 = 10000;
        var prx = new ConfigProxtitDriver { ProxMode = 1, BeaconInterval = 1000, ScanInterval = 200, ScanWindow = 100, DeltaRssi = 0, AvgSeconds = 30, RssiGate = -85 };
        var config = new ConfigurationJSON { Name = "NANOTAG", ScheduleType = ScheduleTypes.Continues };
        config.DeviceDrivers.AddRange(new ConfigurationDeviceDriver[] { new ConfigACLYSDriver(), logger, prx });

        var o = new JsonSerializerOptions();
        o.Converters.Add(new ConfigurationJSON.ScheduleTypesEnumConverter());
        o.Converters.Add(new ConfigurationDeviceDriver.ConfigurationDeviceDriverConverter());
        o.Converters.Add(new ConfigurationJSON.VesperDateTimeConverter());
        o.Converters.Add(new ConfigurationJSON.VesperPowerOnConverter());
        o.Converters.Add(new VesperDateTimeAlarmConverter());
        byte[] b = Nanotag.ConfigBinaryConverter(JsonSerializer.Serialize(config, o));
        Assert.Equal(768, b.Length);

        int slot1 = 256 + 64, slot2 = 256 + 128;
        Assert.Equal("LOGGER", Encoding.ASCII.GetString(b, slot1, 6));
        Assert.Equal(10000u, BitConverter.ToUInt32(b, slot1 + 16));               // sampleRate config 1
        Assert.Equal(0x3Fu, BitConverter.ToUInt32(b, slot1 + 48));                // raw_data[0] = logmask
        Assert.Equal("PROXTIT", Encoding.ASCII.GetString(b, slot2, 7));
        ushort[] p = Enumerable.Range(0, 8).Select(k => BitConverter.ToUInt16(b, slot2 + 48 + 2 * k)).ToArray();
        Assert.Equal(new ushort[] { 1, 1000, 0xFFFF, 200, 100, 0, 30, unchecked((ushort)(short)-85) }, p);
    }

    [Fact]
    public void Nanotag_DriverList_OffersLogger()
        => Assert.Contains(Nanotag.SupportedDeviceDrivers, d => d is ConfigLoggerDriver);
}
