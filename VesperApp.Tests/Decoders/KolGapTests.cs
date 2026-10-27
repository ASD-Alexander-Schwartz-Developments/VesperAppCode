using VesperApp.Models;
using VesperApp.Services;
using Xunit;

namespace VesperApp.Tests.Decoders;

/// <summary>KOL 1.8 audio files with data accounting (asd-interfaces interfaces/kol-recording-format.md):
/// 0xFF pad to byte 512, ring nodes ending in SYNC blocks, 512-byte GAP blocks (OVERRUN / INVALID /
/// SUMMARY). The decoder keeps the sample timeline: missing samples become zeros of the exact length,
/// unreliable nodes are zeroed, every event is listed in the sidecar.</summary>
public class KolGapTests
{
    private static readonly DateTime Start = new(2026, 10, 27, 9, 0, 0);
    private const int NodeSamples = 4;            // tiny nodes for the test

    private static byte[] Node(short value) => Enumerable.Range(0, NodeSamples).SelectMany(_ => BitConverter.GetBytes(value)).ToArray();

    private static byte[] Gap(int reason, int nodes, int span)
    {
        var b = Enumerable.Repeat((byte)0xFF, KolGapWriter.BlockLength).ToArray();
        Array.Copy(Raw.Block(KolGapWriter.PreambleGap, Start.AddSeconds(5)), b, 16);
        BitConverter.GetBytes((ushort)reason).CopyTo(b, 16);
        BitConverter.GetBytes((ushort)2).CopyTo(b, 18);
        BitConverter.GetBytes(NodeSamples).CopyTo(b, 20);
        BitConverter.GetBytes(nodes).CopyTo(b, 24);
        BitConverter.GetBytes(span).CopyTo(b, 28);
        return b;
    }

    private static byte[] Sync(int s) => Raw.Block(BinHeader.PreambleSync, Start.AddSeconds(s));

    [Fact]
    public async Task Overrun_InsertsExactSilence_Invalid_ZeroesOldestNodes_SummaryListed()
    {
        using var dir = new TempDir();
        var header = Raw.FileHeader("KOL", 192000, 0, 0, 0, 0, 0);
        var pad = Enumerable.Repeat((byte)0xFF, 512 - header.Length - 16).ToArray();
        byte[] partial = BitConverter.GetBytes((short)9).Concat(BitConverter.GetBytes((short)9)).ToArray();   // 2 samples
        string raw = dir.File("5U2.BIN");
        File.WriteAllBytes(raw, Raw.Concat(header, pad,
            Raw.Block(BinHeader.PreambleHeader, Start),
            Node(1), Sync(1),
            Gap(KolGapWriter.ReasonOverrun, 3, 0),           // 3 nodes = 12 samples missing here
            Node(2), Sync(2),
            Node(3), Sync(3),
            Node(4), Sync(4),
            Gap(KolGapWriter.ReasonInvalid, 2, 3),           // of nodes 2, 3, 4 the oldest two are unreliable
            Node(5), Sync(5),
            partial,
            Gap(KolGapWriter.ReasonSummary, 3, 2),
            Raw.Block(BinHeader.PreambleFooter, Start.AddSeconds(6))));

        await BinaryParser.StripSplitEx(raw, '2', dir.Path, 0);

        string ubn = Assert.Single(Directory.GetFiles(dir.Path, "*.UBN"));
        var pcm = File.ReadAllBytes(ubn);
        short[] s = Enumerable.Range(0, pcm.Length / 2).Select(i => BitConverter.ToInt16(pcm, 2 * i)).ToArray();
        var expect = new List<short>();
        expect.AddRange(Enumerable.Repeat((short)1, NodeSamples));
        expect.AddRange(Enumerable.Repeat((short)0, 3 * NodeSamples));   // overrun
        expect.AddRange(Enumerable.Repeat((short)0, NodeSamples));        // node 2 unreliable
        expect.AddRange(Enumerable.Repeat((short)0, NodeSamples));        // node 3 unreliable
        expect.AddRange(Enumerable.Repeat((short)4, NodeSamples));
        expect.AddRange(Enumerable.Repeat((short)5, NodeSamples));
        expect.AddRange(new short[] { 9, 9 });
        Assert.Equal(expect.ToArray(), s);

        var lines = File.ReadAllLines(ubn + ".txt");
        Assert.Contains(lines, l => l.StartsWith("Gap:") && l.EndsWith(":4:12:lost (3 nodes, mic 2)"));
        Assert.Contains(lines, l => l.StartsWith("Gap:") && l.Contains(":16:8:unreliable, set to zero (2 nodes, mic 2)"));
        Assert.Contains(lines, l => l == lines.First(x => x.StartsWith("Summary:")) && l.EndsWith("lost nodes 3, unreliable nodes 2"));
    }

    [Fact]
    public async Task UnclosedFile_AllDataExtracted_AndMarkedUnclosed()
    {
        using var dir = new TempDir();
        var header = Raw.FileHeader("KOL", 48000, 0, 0, 0, 0, 0);
        var pad = Enumerable.Repeat((byte)0xFF, 512 - header.Length - 16).ToArray();
        string raw = dir.File("7U1.BIN");
        File.WriteAllBytes(raw, Raw.Concat(header, pad, Raw.Block(BinHeader.PreambleHeader, Start),
            Node(3), Sync(1), Gap(KolGapWriter.ReasonOverrun, 1, 0), Node(4), Sync(2)));   // reset: no summary, no FOOTER

        await BinaryParser.StripSplitEx(raw, '1', dir.Path, 0);

        string ubn = Assert.Single(Directory.GetFiles(dir.Path, "*.UBN"));
        Assert.Contains("-0000_00_00_00_00_00.000", ubn);
        var expect = Node(3).Concat(new byte[NodeSamples * 2]).Concat(Node(4)).ToArray();
        Assert.Equal(expect, File.ReadAllBytes(ubn));
        var lines = File.ReadAllLines(ubn + ".txt");
        Assert.Contains(lines, l => l.StartsWith("Unclosed:"));
        Assert.Contains(lines, l => l.StartsWith("Gap:") && l.EndsWith(":lost (1 nodes, mic 2)"));
    }

    [Fact]
    public async Task NoGapBlocks_OutputUnchanged_AndNoGapLines()
    {
        using var dir = new TempDir();
        var header = Raw.FileHeader("KOL", 48000, 0, 0, 0, 0, 0);
        var pad = Enumerable.Repeat((byte)0xFF, 512 - header.Length - 16).ToArray();
        string raw = dir.File("6U0.BIN");
        File.WriteAllBytes(raw, Raw.Concat(header, pad, Raw.Block(BinHeader.PreambleHeader, Start),
            Node(7), Sync(1), Node(8), Sync(2), Gap(KolGapWriter.ReasonSummary, 0, 0), Raw.Block(BinHeader.PreambleFooter, Start.AddSeconds(3))));

        await BinaryParser.StripSplitEx(raw, '0', dir.Path, 0);

        string ubn = Assert.Single(Directory.GetFiles(dir.Path, "*.UBN"));
        Assert.Equal(Node(7).Concat(Node(8)).ToArray(), File.ReadAllBytes(ubn));
        var lines = File.ReadAllLines(ubn + ".txt");
        Assert.DoesNotContain(lines, l => l.StartsWith("Gap:"));
        Assert.Contains(lines, l => l.StartsWith("Summary:") && l.EndsWith("lost nodes 0, unreliable nodes 0"));
    }
}
