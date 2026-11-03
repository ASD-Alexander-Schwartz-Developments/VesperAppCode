using VesperApp.Models;
using VesperApp.Services;
using Xunit;

namespace VesperApp.Tests.Decoders;

/// <summary>VesperU5 U.BIN (asd-interfaces interfaces/kol-recording-format.md, "Vesper / Pipistrelle U.BIN"): a SYNC
/// ends every second half, so GAP blocks also sit between two halves, inside a segment; INVALID with span 0 covers
/// the last nodes × node_samples samples. And a block magic that occurs in the PCM (bench 2026-11-03, 13U.BIN) is
/// data: before, it threw on its garbage date and the whole file was dropped.</summary>
public class VesperGapTests
{
    private static readonly DateTime Start = new(2026, 11, 3, 13, 9, 0);
    private const int Half0 = 4, Half1 = 3;          // samples of the two halves (16384 / 16376 on the unit)

    private static byte[] Pcm(short value, int samples) => Enumerable.Range(0, samples).SelectMany(_ => BitConverter.GetBytes(value)).ToArray();

    private static byte[] Gap(int reason, int nodeSamples, int nodes, int span)
    {
        var b = Enumerable.Repeat((byte)0xFF, KolGapWriter.BlockLength).ToArray();
        Array.Copy(Raw.Block(KolGapWriter.PreambleGap, Start.AddSeconds(1)), b, 16);
        BitConverter.GetBytes((ushort)reason).CopyTo(b, 16);
        BitConverter.GetBytes((ushort)0).CopyTo(b, 18);
        BitConverter.GetBytes(nodeSamples).CopyTo(b, 20);
        BitConverter.GetBytes(nodes).CopyTo(b, 24);
        BitConverter.GetBytes(span).CopyTo(b, 28);
        return b;
    }

    private static byte[] Sync(int s) => Raw.Block(BinHeader.PreambleSync, Start.AddSeconds(s));

    private static short[] Decode(string ubn)
    {
        var pcm = File.ReadAllBytes(ubn);
        return Enumerable.Range(0, pcm.Length / 2).Select(i => BitConverter.ToInt16(pcm, 2 * i)).ToArray();
    }

    [Fact]
    public async Task OverrunBetweenHalves_InvalidSpan0_ExactSamples()
    {
        using var dir = new TempDir();
        string raw = dir.File("3U.BIN");
        File.WriteAllBytes(raw, Raw.Concat(Raw.FileHeader("SPH0641", 192000, 0, 0, 0, 0, 0),
            Raw.Block(BinHeader.PreambleHeader, Start),
            Pcm(1, Half0), Pcm(2, Half1), Sync(1),
            Pcm(3, Half0),
            Gap(KolGapWriter.ReasonOverrun, Half1, 1, 0),     // half 1 (with its SYNC) lost
            Gap(KolGapWriter.ReasonOverrun, Half0, 1, 0),     // and the next half 0
            Pcm(6, Half1), Sync(3),
            Gap(KolGapWriter.ReasonInvalid, Half1, 1, 0),     // the half just written (6) is unreliable
            Pcm(7, Half0), Pcm(8, Half1), Sync(4),
            Gap(KolGapWriter.ReasonSummary, 0, 2, 1),
            Raw.Block(BinHeader.PreambleFooter, Start.AddSeconds(5))));

        await BinaryParser.StripSplitEx(raw, null, dir.Path, 0);

        string ubn = Assert.Single(Directory.GetFiles(dir.Path, "*.UBN"));
        var expect = new List<short>();
        expect.AddRange(Pcm16(1, Half0)); expect.AddRange(Pcm16(2, Half1));
        expect.AddRange(Pcm16(3, Half0));
        expect.AddRange(new short[Half1 + Half0]);                        // lost
        expect.AddRange(new short[Half1]);                                // unreliable, zeroed - not the half before
        expect.AddRange(Pcm16(7, Half0)); expect.AddRange(Pcm16(8, Half1));
        Assert.Equal(expect.ToArray(), Decode(ubn));

        var lines = File.ReadAllLines(ubn + ".txt");
        Assert.Equal(2, lines.Count(l => l.StartsWith("Gap:") && l.Contains(":lost (1 nodes")));
        Assert.Contains(lines, l => l.StartsWith("Gap:") && l.EndsWith($":{Half0 + Half1 + Half0 + Half1 + Half0}:{Half1}:unreliable, set to zero (1 nodes, mic 0)"));
        Assert.Contains(lines, l => l.StartsWith("Summary:") && l.EndsWith("lost nodes 2, unreliable nodes 1"));
    }

    private static short[] Pcm16(short v, int n) => Enumerable.Repeat(v, n).ToArray();

    [Theory]
    [InlineData(0xABCDEFEFu)]
    [InlineData(0xA55A5AA5u)]
    [InlineData(0xA55AA55Au)]
    [InlineData(0xA55A3CC3u)]
    public async Task MagicInPcm_IsData_FileKept(uint magic)
    {
        using var dir = new TempDir();
        // PCM holding the magic at an odd byte offset, followed by sample bytes that are no RTC stamp
        byte[] pcm = Raw.Concat(Pcm(5, 3), new byte[] { 0x11 }, BitConverter.GetBytes(magic),
            new byte[] { 0xCE, 0xC4, 0xC5, 0x1C, 0xE7, 0x97, 0x06, 0xDF, 0xFD, 0xDB, 0xF1, 0x1D }, new byte[] { 0x22 }, Pcm(9, 4));
        string raw = dir.File("13U.BIN");
        File.WriteAllBytes(raw, Raw.Concat(Raw.FileHeader("SPH0641", 192000, 0, 0, 0, 0, 0),
            Raw.Block(BinHeader.PreambleHeader, Start), pcm, Raw.Block(BinHeader.PreambleFooter, Start.AddSeconds(1))));

        await BinaryParser.StripSplitEx(raw, null, dir.Path, 0);

        string ubn = Assert.Single(Directory.GetFiles(dir.Path, "*.UBN"));
        Assert.Equal(pcm, File.ReadAllBytes(ubn));
    }

    [Theory]
    [InlineData(0x00, 0x30, 0x12, true)]
    [InlineData(0x24, 0x30, 0x12, false)]         // hour 24
    [InlineData(0x13, 0x6A, 0x12, false)]         // not BCD
    [InlineData(0x13, 0x09, 0x00, false)]         // month 0
    public void IsValidStamp(byte hour, byte minute, byte month, bool valid)
    {
        var b = new byte[] { hour, minute, 0x59, 0, 2, month, 0x31, 0x26, 0xFF, 0, 0, 0 };
        Assert.Equal(valid, BinHeader.IsValidStamp(b, 0));
    }
}
