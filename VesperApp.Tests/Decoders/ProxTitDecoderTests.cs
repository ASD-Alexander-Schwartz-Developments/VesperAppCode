using VesperApp.Models;
using VesperApp.Services;
using Xunit;

namespace VesperApp.Tests.Decoders;

/// <summary>
/// ProxTit I2C contract v2 (ProxTit Proxtit/docs/proxtit_i2c_contract.md § 9, asd-interfaces
/// nanotag-addon-pages.md): the source record "PXT1" naming the recording tag, at offset 64 of a VesperU5
/// /prox file header and of a Nanotag PRX page's spare; marker records (RSSI 127, address {type, FF x5}).
/// </summary>
public class ProxTitDecoderTests
{
    private static readonly DateTime Start = new(2026, 10, 25, 10, 0, 0);
    private static readonly byte[] TagLsb = { 0x07, 0xD2, 0xAE, 0xEA, 0x35, 0x90 };
    private const string TagText = "90:35:EA:AE:D2:07";

    private static byte[] Source(byte variant = 0, uint boot = 5)
    {
        var ms = new MemoryStream(); var w = new BinaryWriter(ms);
        w.Write(ProxSource.Magic); w.Write(TagLsb); w.Write(new byte[] { 2, 2, 0 }); w.Write(variant);
        w.Write((ushort)2); w.Write(boot);
        return ms.ToArray();
    }

    private static byte[] Encounter(uint unixStart, uint duration, byte[] addrLsb, sbyte rssi, sbyte tx)
    {
        var ms = new MemoryStream(); var w = new BinaryWriter(ms);
        w.Write(unixStart); w.Write(duration); w.Write(addrLsb); w.Write(rssi); w.Write(tx);
        return ms.ToArray();
    }

    private static byte[] Marker(byte type, uint time, uint value, sbyte extra) =>
        Encounter(time, value, new byte[] { type, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF }, 127, extra);

    private static Dictionary<string, string> Sidecar(string intermediate) =>
        File.ReadAllLines(intermediate + ".txt").Select(l => l.Split(':', 2))
            .GroupBy(p => p[0]).ToDictionary(g => g.Key, g => g.First()[1]);

    [Fact]
    public void Source_ParsesAndRejects()
    {
        byte[] buf = Enumerable.Repeat((byte)0xFF, 128).ToArray();
        Assert.Null(ProxSource.Parse(buf, 64));                  // 0xFF fill: older loggers
        Source(variant: 1, boot: 0x01020304).CopyTo(buf, 64);
        ProxSource? s = ProxSource.Parse(buf, 64);
        Assert.NotNull(s);
        Assert.Equal(TagText, s!.Address);
        Assert.Equal("02.02.00", s.Firmware);
        Assert.Equal("ProxLink", s.VariantName);
        Assert.Equal(2, s.Contract);
        Assert.Equal(0x01020304u, s.BootCount);
        Assert.Null(ProxSource.Parse(buf, 120));                 // would run past the buffer
        Assert.Equal("PXT1", System.Text.Encoding.ASCII.GetString(buf, 64, 4));
    }

    [Fact]
    public void Parser_TagColumnAndMarkers()
    {
        byte[] data = Raw.Concat(
            Marker(0x01, 1793005000, 0x3, 5),                                   // BOOT
            Marker(0x02, 1793005100, 1793005000 - 20, 0),                       // TIMESET +120 s
            Encounter(1793005200, 60, new byte[] { 6, 5, 4, 3, 2, 1 }, -60, -1),
            Marker(0x03, 1793005900, 3, 0),                                     // POWEROFF host sleep
            Marker(0xFE, 0, 0, 0));                                             // UNREADABLE
        using var dir = new TempDir();
        string path = dir.File("x.XBN");
        using (var p = new ProxEncounterParser(path, data, TagText))
        {
            Assert.Equal(1, p.Records);
            Assert.Equal(4, p.Markers);
            p.WriteFile();
        }
        string[] csv = File.ReadAllLines(path + ".csv");
        Assert.Equal("Start,End,Duration [s],Peer address,RSSI [dBm],TX power [dBm],Tag address,Record", csv[0]);
        Assert.Equal("2026-10-26 08:56:40,,,,,," + TagText + ",BOOT reset cause 0x3 boot 5", csv[1]);
        Assert.Equal("2026-10-26 08:58:20,,,,,," + TagText + ",TIMESET clock was 2026-10-26 08:56:20 (step +120 s)", csv[2]);
        Assert.Equal("2026-10-26 09:00:00,2026-10-26 09:01:00,60,01:02:03:04:05:06,-60,-1," + TagText + ",encounter", csv[3]);
        Assert.Equal("2026-10-26 09:11:40,,,,,," + TagText + ",POWEROFF host sleep", csv[4]);
        Assert.Equal(",,,,,," + TagText + ",UNREADABLE record (tag flash)", csv[5]);
    }

    [Fact]
    public void Parser_NoSource_LeavesTagEmpty()
    {
        using var p = new ProxEncounterParser("unused", Encounter(1793005200, 1, new byte[] { 1, 2, 3, 4, 5, 6 }, -50, 0));
        Assert.Equal(1, p.Records);
        Assert.Equal(0, p.Markers);
    }

    [Fact]
    public async Task VesperXBin_SourceRecordInHeader_ReachesSidecarAndCsv()
    {
        using var dir = new TempDir();
        byte[] header = Raw.FileHeader("PROXTIT", rate: 60000, bitmask: 0, 1, 0, 0, 0, uid: 0x47625051, fwid: 0x0116);
        Source().CopyTo(header, ProxSource.Offset);                         // into the 0xFF fill, as VesperU5 does
        byte[] recs = Raw.Concat(Encounter(1793005200, 60, new byte[] { 6, 5, 4, 3, 2, 1 }, -60, 0),
                                 Marker(0x04, 1793005300, 1, 1));
        string raw = dir.File("7X.BIN");
        File.WriteAllBytes(raw, Raw.Concat(header,
            Raw.Block(BinHeader.PreambleHeader, Start), recs, Raw.Block(BinHeader.PreambleFooter, Start.AddMinutes(5))));
        string outDir = Directory.CreateDirectory(dir.File("PRX")).FullName;

        Assert.Equal(0, await BinaryParser.StripSplit(raw, outDir, 0));
        string xbn = Assert.Single(Directory.GetFiles(outDir, "*.XBN"));
        Assert.Equal(recs, File.ReadAllBytes(xbn));
        var meta = Sidecar(xbn);
        Assert.Equal(TagText, meta["SourceTag"]);
        Assert.Equal("02.02.00", meta["SourceTagFW"]);
        Assert.Equal("BLE", meta["SourceTagVariant"]);
        Assert.Equal("5", meta["SourceTagBoot"]);

        DecodeSummary sum = await RecordingPipeline.DecodeFilesAsync(new[] { xbn });
        Assert.Equal(1, sum.Csv);
        string[] csv = File.ReadAllLines(xbn + ".csv");
        Assert.Equal("2026-10-26 09:00:00,2026-10-26 09:01:00,60,01:02:03:04:05:06,-60,0," + TagText + ",encounter", csv[1]);
        Assert.Equal("2026-10-26 09:01:40,,,,,," + TagText + ",START previous mode 1 clock set", csv[2]);
    }

    [Fact]
    public async Task VesperXBin_WithoutSourceRecord_DecodesAsBefore()
    {
        using var dir = new TempDir();
        string raw = dir.File("8X.BIN");
        File.WriteAllBytes(raw, Raw.Concat(Raw.FileHeader("PROXTIT", 60000, 0, 1, 0, 0, 0),
            Raw.Block(BinHeader.PreambleHeader, Start), Encounter(1793005200, 60, new byte[] { 6, 5, 4, 3, 2, 1 }, -60, 0),
            Raw.Block(BinHeader.PreambleFooter, Start.AddMinutes(5))));
        string outDir = Directory.CreateDirectory(dir.File("PRX")).FullName;
        await BinaryParser.StripSplit(raw, outDir, 0);
        string xbn = Assert.Single(Directory.GetFiles(outDir, "*.XBN"));
        Assert.False(Sidecar(xbn).ContainsKey("SourceTag"));
        await RecordingPipeline.DecodeFilesAsync(new[] { xbn });
        Assert.EndsWith(",-60,0,,encounter", File.ReadAllLines(xbn + ".csv")[1]);
    }

    [Fact]
    public void NanotagPrxPage_SourceInSpare_ReachesSidecarAndCsv()
    {
        byte[] page = Enumerable.Repeat((byte)0xFF, NanotagPage.FullSize).ToArray();
        byte[] recs = Encounter(1793005200, 125, new byte[] { 0x66, 0x55, 0x44, 0x33, 0x22, 0x11 }, -71, -1);
        recs.CopyTo(page, 0);
        using (var ms = new MemoryStream())
        using (var bw = new BinaryWriter(ms))
        {
            bw.Write(NanotagPage.Preamble); bw.Write((ushort)62); bw.Write(NanotagPage.TypePrx); bw.Write(NanotagPage.SubTypeProxtit);
            bw.Write((ushort)0); bw.Write((ushort)0x9000); bw.Write((uint)recs.Length); bw.Write(0u); bw.Write(0u);
            bw.Write(0x00000129u);                                              // FW_VER 1.41
            bw.Write(Nanotag.ToTimestamp(Start)); bw.Write((ushort)0); bw.Write((ushort)60000); bw.Write(0u); bw.Write(60000u); bw.Write(0u);
            ms.ToArray().CopyTo(page, NanotagPage.DataSize);
        }
        Source().CopyTo(page, NanotagPage.DataSize + ProxSource.Offset);

        NanotagPage p = NanotagPage.TryParse(page)!;
        Assert.Equal(TagText, p.Source!.Address);

        var c = new NanotagAddonCollector();
        Assert.True(c.Add(p));
        using var dir = new TempDir();
        string x = Assert.Single(c.WriteAll(dir.Path, "ABC123"));
        Assert.Equal(TagText, Sidecar(x)["SourceTag"]);
        Assert.Equal("2026-10-26 09:00:00,2026-10-26 09:02:05,125,11:22:33:44:55:66,-71,-1," + TagText + ",encounter",
            File.ReadAllLines(x + ".csv")[1]);
    }
}
