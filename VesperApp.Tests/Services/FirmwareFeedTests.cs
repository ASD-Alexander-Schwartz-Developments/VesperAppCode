using System.Net;
using System.Security.Cryptography;
using System.Text;
using FirmwareUpdater.FileFormat;
using FirmwareUpdater.Memory;
using VesperApp.Models;
using VesperApp.Services;
using VesperApp.ViewModels;
using Xunit;

namespace VesperApp.Tests.Services;

/// <summary>
/// Firmware updates without the CDN or a device: the release feed (index.json) and asset download over a fake
/// HTTP handler, the per-device release list of the Firmware page, version comparison, and the image preparation
/// in front of the STM32 DFU flasher (Intel HEX parsing, 16-byte flash alignment).
/// </summary>
public class FirmwareFeedTests
{
    private sealed class FakeHttp : HttpMessageHandler
    {
        public readonly Dictionary<string, (HttpStatusCode Code, byte[] Body)> Routes = new();
        public readonly List<Uri> Requested = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requested.Add(request.RequestUri!);
            var (code, body) = Routes.TryGetValue(request.RequestUri!.AbsoluteUri, out var r) ? r : (HttpStatusCode.NotFound, Array.Empty<byte>());
            return Task.FromResult(new HttpResponseMessage(code) { Content = new ByteArrayContent(body) });
        }
    }

    private const string Index = "https://cdn.test/firmware/index.json";

    private static (ReleaseFeedService Svc, FakeHttp Http) Feed(string? json = null, Func<Uri, Uri>? signer = null)
    {
        var http = new FakeHttp();
        if (json != null) http.Routes[Index] = (HttpStatusCode.OK, Encoding.UTF8.GetBytes(json));
        return (new ReleaseFeedService(new Uri(Index), new HttpClient(http), signer), http);
    }

    // ---- feed ----

    [Fact]
    public async Task GetReleases_ParsesTheCiFeed_CaseInsensitive_CommentsAndTrailingCommas()
    {
        var (svc, _) = Feed("""
            {
              "schema": 1, "updated": "2026-09-25T10:00:00Z",
              // written by publish-feed
              "releases": [
                { "version": "1.33", "name": "Nanotag 1.33", "target": "nanotag", "asset": "nanotag/NANOTAG_1.33.hex",
                  "size": 123456, "sha256": "AB12", },
                { "Version": "1.7", "Target": "kol", "Asset": "kol/KOL_1.7.bin" },
              ],
            }
            """);

        IReadOnlyList<ReleaseEntry> r = await svc.GetReleasesAsync();

        Assert.Equal(2, r.Count);
        Assert.Equal("1.33", r[0].Version);
        Assert.Equal("nanotag", r[0].Target);
        Assert.Equal(123456, r[0].Size);
        Assert.Equal("AB12", r[0].Sha256);
        Assert.Equal("kol/KOL_1.7.bin", r[1].Asset);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("""{"schema":1}""")]
    public async Task GetReleases_EmptyFeed_IsAnEmptyList(string json)
        => Assert.Empty(await Feed(json).Svc.GetReleasesAsync());

    [Fact]
    public async Task GetReleases_FeedMissing_Throws()
        => await Assert.ThrowsAsync<HttpRequestException>(() => Feed().Svc.GetReleasesAsync());

    [Theory]
    [InlineData("nanotag/NANOTAG_1.33.hex", "https://cdn.test/firmware/nanotag/NANOTAG_1.33.hex")]
    [InlineData("../plugins/x.zip", "https://cdn.test/plugins/x.zip")]
    [InlineData("/abs/KOL.bin", "https://cdn.test/abs/KOL.bin")]
    [InlineData("https://other.test/KOL.bin", "https://other.test/KOL.bin")]
    public void ResolveAsset_RelativeToTheFeed_AbsoluteAsIs(string asset, string expected)
        => Assert.Equal(expected, Feed().Svc.ResolveAssetUri(new ReleaseEntry { Asset = asset }).AbsoluteUri);

    [Fact]
    public void ResolveAsset_GoesThroughTheSigner_AndNeedsAnAsset()
    {
        var (svc, _) = Feed(signer: u => new Uri(u.AbsoluteUri + "?sig=1"));
        Assert.Equal("https://cdn.test/firmware/k.bin?sig=1", svc.ResolveAssetUri(new ReleaseEntry { Asset = "k.bin" }).AbsoluteUri);
        Assert.Throws<InvalidOperationException>(() => svc.ResolveAssetUri(new ReleaseEntry { Asset = " " }));
    }

    // ---- download ----

    private static readonly byte[] Image = Enumerable.Range(0, 200_000).Select(i => (byte)(i * 7)).ToArray();
    private static string Sha(byte[] b) => Convert.ToHexString(SHA256.HashData(b));

    private static (ReleaseFeedService Svc, ReleaseEntry Entry) Asset(string? sha256)
    {
        var (svc, http) = Feed();
        http.Routes["https://cdn.test/firmware/kol/KOL_1.7.bin"] = (HttpStatusCode.OK, Image);
        return (svc, new ReleaseEntry { Version = "1.7", Asset = "kol/KOL_1.7.bin", Sha256 = sha256 });
    }

    private sealed class Collect : IProgress<double>
    {
        public readonly List<double> Values = new();
        public void Report(double value) { lock (Values) Values.Add(value); }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]                    // lower-case hex
    [InlineData(true, true)]                     // "0x" prefix
    public async Task Download_WritesTheFile_VerifiesSha_ReportsProgress(bool lower, bool prefix)
    {
        using var dir = new TempDir();
        string sha = Sha(Image);
        if (lower) sha = sha.ToLowerInvariant();
        if (prefix) sha = "0x" + sha;
        var (svc, entry) = Asset(sha);
        var progress = new Collect();

        await svc.DownloadAssetAsync(entry, dir.File("sub/KOL_1.7.bin"), progress);

        Assert.Equal(Image, File.ReadAllBytes(dir.File("sub/KOL_1.7.bin")));
        Assert.NotEmpty(progress.Values);
        Assert.Equal(1.0, progress.Values.Max(), 9);
    }

    [Fact]
    public async Task Download_ShaMismatch_DeletesTheFile()
    {
        using var dir = new TempDir();
        var (svc, entry) = Asset(new string('0', 64));
        var ex = await Assert.ThrowsAsync<InvalidDataException>(() => svc.DownloadAssetAsync(entry, dir.File("KOL.bin")));
        Assert.Contains("SHA-256 mismatch", ex.Message);
        Assert.False(File.Exists(dir.File("KOL.bin")));
    }

    [Fact]
    public async Task Download_NoSha_IsWrittenUnverified_MissingAssetThrows()
    {
        using var dir = new TempDir();
        var (svc, entry) = Asset(null);
        await svc.DownloadAssetAsync(entry, dir.File("KOL.bin"));
        Assert.Equal(Image.Length, new FileInfo(dir.File("KOL.bin")).Length);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            svc.DownloadAssetAsync(new ReleaseEntry { Asset = "kol/KOL_9.9.bin" }, dir.File("x.bin")));
    }

    // ---- Firmware page list ----

    [Fact]
    public void ReleasesFor_MatchesTheDeviceTypeName_PlusUntargeted_InFeedOrder()
    {
        ReleaseEntry E(string v, string? t) => new() { Version = v, Target = t };
        var feed = new[] { E("1.8", "kol"), E("1.33", "nanotag"), E("1.7", "KOL"), E("any", null), E("blank", " "), E("1.22", "vesper"), E("2.3", "win-x64") };

        Assert.Equal(new[] { "1.8", "1.7", "any", "blank" }, FirmwareUpgradesViewModel.ReleasesFor(feed, DeviceTypes.Kol).Select(r => r.Version));
        Assert.Equal(new[] { "1.33", "any", "blank" }, FirmwareUpgradesViewModel.ReleasesFor(feed, DeviceTypes.Nanotag).Select(r => r.Version));
        Assert.Equal(new[] { "any", "blank" }, FirmwareUpgradesViewModel.ReleasesFor(feed, DeviceTypes.Pipistrelle).Select(r => r.Version));
    }

    // ---- version comparison ----

    [Theory]
    [InlineData("1.33", "1.31", true)]
    [InlineData("1.31", "1.33", false)]
    [InlineData("1.10", "1.9", true)]            // numeric, not string order
    [InlineData("v2.3.2", "2.3.1", true)]
    [InlineData("2.3.1", "2.3.1", false)]
    [InlineData("1.0", null, true)]              // nothing installed
    [InlineData(null, "1.0", false)]
    [InlineData("nightly-b", "nightly-a", true)] // not a version: any difference counts
    [InlineData("Nightly", "nightly", false)]
    public void IsNewer(string? feed, string? installed, bool expected)
        => Assert.Equal(expected, PluginUpdateService.IsNewer(feed, installed));

    // ---- DFU image preparation ----

    private static string Rec(int type, int address, params byte[] data)
    {
        var bytes = new List<byte> { (byte)data.Length, (byte)(address >> 8), (byte)address, (byte)type };
        bytes.AddRange(data);
        byte sum = (byte)(-bytes.Sum(b => b) & 0xFF);
        return ":" + Convert.ToHexString(bytes.ToArray()) + sum.ToString("X2");
    }

    private static RawMemory ParseHex(TempDir dir, params string[] lines)
    {
        string path = dir.File("fw.hex");
        File.WriteAllLines(path, lines);
        return IntelHex.ParseFile(path);
    }

    [Fact]
    public void IntelHex_ExtendedLinearAddress_AndContiguousRecordsMerge()
    {
        using var dir = new TempDir();
        RawMemory m = ParseHex(dir,
            Rec(4, 0, 0x08, 0x00),
            Rec(0, 0x0238, 1, 2, 3, 4),
            Rec(0, 0x023C, 5, 6),
            Rec(0, 0x1000, 9),
            Rec(1, 0));

        Assert.Equal(2, m.Segments.Count);
        Assert.Equal(0x08000238ul, m.Segments[0].StartAddress);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6 }, m.Segments[0].Data);
        Assert.Equal(0x08001000ul, m.Segments[1].StartAddress);
    }

    [Fact]
    public void IntelHex_BadChecksum_MissingEnd_Empty_AreRefused()
    {
        using var dir = new TempDir();
        string bad = Rec(0, 0, 1, 2);
        bad = bad[..^2] + (bad[^2..] == "00" ? "01" : "00");
        Assert.Contains("incorrect checksum (line 1)", Assert.Throws<ArgumentException>(() => ParseHex(dir, bad, Rec(1, 0))).Message);
        Assert.Contains("End Of File", Assert.Throws<ArgumentException>(() => ParseHex(dir, Rec(0, 0, 1))).Message);
        Assert.Contains("empty", Assert.Throws<ArgumentException>(() => ParseHex(dir)).Message);
        Assert.Contains("invalid format (line 1)", Assert.Throws<ArgumentException>(() => ParseHex(dir, "garbage", Rec(1, 0))).Message);
    }

    private static RawMemory Mem(params (ulong At, byte[] Data)[] segs)
    {
        var m = new RawMemory();
        foreach (var s in segs) Assert.True(m.TryAddSegment(new Segment(s.At, s.Data)));
        return m;
    }

    [Fact]
    public void FlashAlignment_PadsToQuadWords_WithErasedFlash()
    {
        /* the STM32U585 ROM bootloader ignored a segment at 0x08000238 */
        RawMemory n = FlashAlignment.Normalize(Mem((0x08000238, new byte[] { 1, 2, 3, 4, 5 })));

        Segment s = Assert.Single(n.Segments);
        Assert.Equal(0x08000230ul, s.StartAddress);
        Assert.Equal(16, s.Length);
        Assert.Equal(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 1, 2, 3, 4, 5, 0xFF, 0xFF, 0xFF }, s.Data);
    }

    [Fact]
    public void FlashAlignment_MergesSmallGaps_KeepsLargeOnesApart()
    {
        RawMemory n = FlashAlignment.Normalize(Mem(
            (0x08000000, new byte[16]),
            (0x08000100, new byte[] { 7 }),                // 240-byte gap: merged
            (0x08010000, new byte[] { 8 })));              // far away: own segment

        Assert.Equal(2, n.Segments.Count);
        Assert.Equal(0x08000000ul, n.Segments[0].StartAddress);
        Assert.Equal(0x110, n.Segments[0].Length);
        Assert.Equal(7, n.Segments[0].Data[0x100]);
        Assert.Equal(0xFF, n.Segments[0].Data[0x50]);
        Assert.Equal(0x08010000ul, n.Segments[1].StartAddress);
        Assert.Equal(16, n.Segments[1].Length);
        Assert.All(n.Segments, s => Assert.Equal(0ul, s.StartAddress % 16));
    }

    [Fact]
    public void FlashAlignment_EmptyImage_IsUnchanged()
    {
        var empty = new RawMemory();
        Assert.Same(empty, FlashAlignment.Normalize(empty));
    }
}
