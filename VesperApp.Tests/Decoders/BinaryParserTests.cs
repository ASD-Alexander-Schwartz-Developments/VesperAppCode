using VesperApp.Models;
using VesperApp.Services;
using Xunit;

namespace VesperApp.Tests.Decoders;

/// <summary>Strip/split of raw sensor files: 128-byte file header (0xDEAFDAC0), 16-byte start block
/// (0xA55AA55A), payload, optional sync blocks (0xABCDEFEF) and the footer (0xA55A5AA5).</summary>
public class BinaryParserTests
{
    private static readonly DateTime Start = new(2026, 10, 25, 10, 0, 0);

    // Two IMU10HT records (6-byte barometer field) as payload
    private static byte[] ImuPayload()
    {
        var ms = new MemoryStream();
        var w = new BinaryWriter(ms);
        foreach (short temp in new short[] { 2100, -300 })
        {
            w.Write((byte)0x55); w.Write((byte)0x08); w.Write((byte)0); w.Write((byte)1); w.Write((ushort)1023);
            w.Write(temp); w.Write((ushort)1001); w.Write((ushort)0xAAAA);
        }
        return ms.ToArray();
    }

    private static byte[] Header() => Raw.FileHeader("IMU10HT", rate: 50, bitmask: 0x0F, 0x11, 0x22, 0x33, 0x44, uid: 0xCAFE1234, fwid: 0x0116);

    private static Dictionary<string, string> Sidecar(string intermediate) =>
        File.ReadAllLines(intermediate + ".txt")
            .Select(l => l.Split(':', 2))
            .GroupBy(p => p[0])
            .ToDictionary(g => g.Key, g => g.First()[1]);

    [Fact]
    public async Task StripSplit_CompleteFile_WritesIntermediateAndSidecar()
    {
        using var dir = new TempDir();
        byte[] payload = ImuPayload();
        string raw = dir.File("12M.BIN");
        File.WriteAllBytes(raw, Raw.Concat(Header(),
            Raw.Block(BinHeader.PreambleHeader, Start), payload, Raw.Block(BinHeader.PreambleFooter, Start.AddSeconds(1))));
        string outDir = Directory.CreateDirectory(dir.File("IMU")).FullName;

        Assert.Equal(0, await BinaryParser.StripSplit(raw, outDir, 0));

        string mbn = Path.Combine(outDir, "M2026_10_25_10_00_00_000-M2026_10_25_10_00_01_000.MBN");
        Assert.True(File.Exists(mbn), "intermediate name from start/footer blocks: " + string.Join(", ", Directory.GetFiles(outDir)));
        Assert.Equal(payload, File.ReadAllBytes(mbn));            // blocks stripped

        var meta = Sidecar(mbn);
        Assert.Equal("CAFE1234", meta["DeviceID"]);
        Assert.Equal("116", meta["FWID"]);
        Assert.Equal("IMU10HT", meta["Sensor"]);                  // no NUL padding from the 16-byte name field
        Assert.Equal("50", meta["SampleRate"]);
        Assert.Equal("11", meta["Config0"]);
        Assert.Equal("22", meta["Config1"]);
        Assert.Equal("33", meta["Config2"]);
        Assert.Equal("44", meta["Config3"]);
        Assert.Equal("F", meta["Bitmask"]);
        Assert.False(meta.ContainsKey("Sync"));

        // the sidecar drives the IMU10HT barometer width end to end
        Assert.Equal(6, IMU10Parser.BaroBytesFor(mbn));
    }

    [Fact]
    public async Task StripSplit_SyncBlock_IsRemovedAndListedInSidecar()
    {
        using var dir = new TempDir();
        byte[] p1 = { 1, 2, 3, 4, 5, 6, 7, 8 }, p2 = { 9, 10, 11, 12, 13, 14, 15, 16 };
        string raw = dir.File("7E.BIN");
        File.WriteAllBytes(raw, Raw.Concat(Raw.FileHeader("EXG48", 250, 0, 0, 0, 0, 0),
            Raw.Block(BinHeader.PreambleHeader, Start), p1,
            Raw.Block(BinHeader.PreambleSync, Start, 255, 127), p2,           // sync at offset 24 -> sample 12
            Raw.Block(BinHeader.PreambleFooter, Start.AddSeconds(2))));

        await BinaryParser.StripSplit(raw, dir.Path, 0);

        string ebn = Assert.Single(Directory.GetFiles(dir.Path, "*.EBN"));
        Assert.Equal("E2026_10_25_10_00_00_000-E2026_10_25_10_00_02_000.EBN", Path.GetFileName(ebn));
        Assert.Equal(Raw.Concat(p1, p2), File.ReadAllBytes(ebn));
        string sync = Assert.Single(File.ReadAllLines(ebn + ".txt"), l => l.StartsWith("Sync:"));
        Assert.EndsWith(":12", sync);
        Assert.Contains("10:00:00.5", sync);
    }

    [Fact]
    public async Task StripSplit_SyncLine_Uses24HourClock()
    {
        using var dir = new TempDir();
        DateTime pm = Start.AddHours(5);                                       // 15:00 was written "03:00"
        string raw = dir.File("8E.BIN");
        File.WriteAllBytes(raw, Raw.Concat(Raw.FileHeader("EXG48", 250, 0, 0, 0, 0, 0),
            Raw.Block(BinHeader.PreambleHeader, pm), new byte[8],
            Raw.Block(BinHeader.PreambleSync, pm, 255, 127), new byte[8],
            Raw.Block(BinHeader.PreambleFooter, pm.AddSeconds(2))));

        await BinaryParser.StripSplit(raw, dir.Path, 0);

        string ebn = Assert.Single(Directory.GetFiles(dir.Path, "*.EBN"));
        Assert.Contains("15:00:00.5", Assert.Single(File.ReadAllLines(ebn + ".txt"), l => l.StartsWith("Sync:")));
    }

    [Fact]
    public async Task StripSplit_NoFooter_WritesPartialIntermediateWithOnlyThePayload()
    {
        using var dir = new TempDir();
        byte[] payload = ImuPayload();
        string raw = dir.File("3M.BIN");
        File.WriteAllBytes(raw, Raw.Concat(Header(), Raw.Block(BinHeader.PreambleHeader, Start), payload));

        await BinaryParser.StripSplit(raw, dir.Path, 0);

        string mbn = Assert.Single(Directory.GetFiles(dir.Path, "*.MBN"));
        Assert.Equal("M2026_10_25_10_00_00_000-0000_00_00_00_00_00.000.MBN", Path.GetFileName(mbn));
        Assert.Equal(payload, File.ReadAllBytes(mbn));            // nothing appended after the last record
    }

    [Fact]
    public async Task StripSplit_FileWithoutHeader_FooterAtEndOfFile_GivesEndTimeInName()
    {
        using var dir = new TempDir();
        string raw = dir.File("5L.BIN");
        File.WriteAllBytes(raw, Raw.Concat(Raw.Block(BinHeader.PreambleHeader, Start), new byte[] { 1, 2, 3, 4 },
                                           Raw.Block(BinHeader.PreambleFooter, Start.AddSeconds(3))));

        await BinaryParser.StripSplit(raw, dir.Path, 0);

        string lbn = Assert.Single(Directory.GetFiles(dir.Path, "*.LBN"));
        Assert.Equal("L2026_10_25_10_00_00_000-L2026_10_25_10_00_03_000.LBN", Path.GetFileName(lbn));
        Assert.False(File.Exists(lbn + ".txt"));                  // no file header -> no sidecar
    }

    [Fact]
    public async Task StripSplit_NameWithoutTypeLetter_IsRejected()
    {
        using var dir = new TempDir();
        string raw = dir.File("1234.BIN");
        File.WriteAllBytes(raw, new byte[64]);
        Assert.Equal(-1, await BinaryParser.StripSplit(raw, dir.Path, 0));
    }

    [Theory]
    [InlineData("0H.BIN", true)]
    [InlineData("12U3.bin", true)]
    [InlineData("M2026_10_25.MBN.BIN", false)]
    [InlineData("config.json", false)]
    public void IsRawBin_RecognisesDeviceFiles(string name, bool expected)
        => Assert.Equal(expected, RecordingPipeline.IsRawBin(name));

    [Theory]
    [InlineData("a.HBN", true)]
    [InlineData("a.mbn", true)]
    [InlineData("a.SBN", false)]
    [InlineData("a.csv", false)]
    public void IsDecodableIntermediate_ByExtension(string name, bool expected)
        => Assert.Equal(expected, RecordingPipeline.IsDecodableIntermediate(name));
}

public class BinHeaderTests
{
    [Fact]
    public void StartBlock_ParsesBcdTimestampAndPointsPastItself()
    {
        byte[] b = Raw.Block(BinHeader.PreambleHeader, new DateTime(2026, 10, 25, 9, 30, 0), 255, 0);
        var h = new BinHeader(b, 0, 'H');                         // exactly one 16-byte block
        Assert.True(h.IsHeaderHeader);
        Assert.Equal(BinHeaderType.BIN_TYPE_HEADER, h.HeaderType);
        Assert.Equal(new DateTime(2026, 10, 25, 9, 30, 0, 996), h.HeaderTimestamp);   // 1000 * 255 / 256
        Assert.Equal(16, h.StartPosition);
        Assert.Equal("H2026_10_25_09_30_00_996", h.ToString());
    }

    [Fact]
    public void FooterAndSync_KeepTheirOwnPosition()
    {
        byte[] b = Raw.Concat(new byte[5], Raw.Block(BinHeader.PreambleFooter, new DateTime(2026, 1, 2, 3, 4, 5)));
        var f = new BinHeader(b, 5, 'M');
        Assert.True(f.IsHeaderFooter);
        Assert.Equal(5, f.StartPosition);
        Assert.Equal(new DateTime(2026, 1, 2, 3, 4, 5), f.HeaderTimestamp);

        var s = new BinHeader(Raw.Block(BinHeader.PreambleSync, new DateTime(2026, 1, 2, 3, 4, 5)), 0, 'M');
        Assert.Equal(BinHeaderType.BIN_TYPE_SYNC, s.HeaderType);
    }

    [Fact]
    public void UnknownPreamble_IsInvalid()
    {
        var h = new BinHeader(new byte[32], 0, 'U');
        Assert.False(h.IsHeaderValid);
        Assert.Equal(BinHeaderType.BIN_TYPE_INVALID, h.HeaderType);
        Assert.Equal(DateTime.MinValue, h.HeaderTimestamp);
    }

    [Fact]
    public void BinHeader_ToBytes_IsTheBlockAtItsOffset()
    {
        // ToBytes copied 16 bytes from offset 0 of the buffer instead of the block's own position.
        byte[] buf = new byte[40];
        for (int k = 0; k < buf.Length; k++) buf[k] = (byte)(0xC0 + k);
        byte[] block = { 0x5A, 0xA5, 0x5A, 0xA5, 0x09, 0x30, 0x15, 0x00, 0x07, 0x10, 0x25, 0x26, 0xFF, 0x00, 0x00, 0x00 };
        Array.Copy(block, 0, buf, 20, block.Length);
        var h = new BinHeader(buf, 20, 'H');
        Assert.Equal(block, h.ToBytes());
        Assert.Empty(new BinHeader(buf, 30, 'H').ToBytes());          // not enough bytes left: no header
    }

    [Fact]
    public void BinaryTypeHeader_ParsesAllFields()
    {
        var h = new BinaryTypeHeader(Raw.FileHeader("LOGGER", 60000, 0x2, 0x2F, 0x301, 7, 8, uid: 0x47625051, fwid: 0x0108, winLen: 5, winRate: 6));
        Assert.Equal("LOGGER", h.DeviceDriverName);
        Assert.Equal(0x47625051u, h.UId);
        Assert.Equal((ushort)0x0108, h.FwId);
        Assert.Equal(60000u, h.SamplingRate);
        Assert.Equal(5u, h.WindowLength);
        Assert.Equal(6u, h.WindowRate);
        Assert.Equal(0x2u, h.Bitmask);
        Assert.Equal(0x2Fu, h.Configuration0);
        Assert.Equal(0x301u, h.Configuration1);
        Assert.Equal(7u, h.Configuration2);
        Assert.Equal(8u, h.Configuration3);
        Assert.Equal(128, h.ToBytes().Length);
    }
}
