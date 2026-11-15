using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using VesperApp.Services;
using Xunit;

namespace VesperApp.Tests.Services;

/// <summary>
/// Help image cache: an image replaced on the wiki under the same file name must replace the cached one.
/// Before, a cached image was never requested again, so the Help tab kept old screenshots for good.
/// </summary>
public class WikiHelpImageCacheTests
{
    private const string Page = "text ![shot](images/tab-clock.png) more";

    /// <summary>A wiki that serves one image with an ETag and answers If-None-Match.</summary>
    private sealed class FakeWiki : HttpMessageHandler
    {
        public byte[] Content = { 1, 2, 3 };
        public string? ETag = "\"v1\"";
        public int Requests, NotModified;
        public bool Offline;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests++;
            if (Offline) throw new HttpRequestException("offline");
            if (ETag != null && request.Headers.TryGetValues("If-None-Match", out var sent) && string.Join(",", sent) == ETag)
            {
                NotModified++;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotModified));
            }
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Content) };
            if (ETag != null) response.Headers.ETag = EntityTagHeaderValue.Parse(ETag);
            return Task.FromResult(response);
        }
    }

    private static WikiHelpService Service(FakeWiki wiki, TempDir dir) => new(new HttpClient(wiki), dir.Path);
    private static string Cached(TempDir dir) => Path.Combine(dir.Path, "tab-clock.png");

    [Fact]
    public async Task MissingImage_IsDownloaded_WithItsETag()
    {
        using var dir = new TempDir();
        var wiki = new FakeWiki();

        Assert.True(await Service(wiki, dir).PrefetchImagesAsync(Page));

        Assert.Equal(wiki.Content, File.ReadAllBytes(Cached(dir)));
        Assert.Equal("\"v1\"", File.ReadAllText(WikiHelpService.ETagFileFor(Cached(dir))));
    }

    [Fact]
    public async Task UnchangedImage_IsNotDownloadedAgain()
    {
        using var dir = new TempDir();
        var wiki = new FakeWiki();
        await Service(wiki, dir).PrefetchImagesAsync(Page);

        // the next app run: a new service on the same cache
        Assert.False(await Service(wiki, dir).PrefetchImagesAsync(Page));

        Assert.Equal(2, wiki.Requests);
        Assert.Equal(1, wiki.NotModified);
    }

    [Fact]
    public async Task ImageReplacedOnTheWiki_ReplacesTheCachedOne()
    {
        using var dir = new TempDir();
        var wiki = new FakeWiki();
        await Service(wiki, dir).PrefetchImagesAsync(Page);

        wiki.Content = new byte[] { 9, 9, 9, 9 };
        wiki.ETag = "\"v2\"";
        Assert.True(await Service(wiki, dir).PrefetchImagesAsync(Page));

        Assert.Equal(wiki.Content, File.ReadAllBytes(Cached(dir)));
        Assert.Equal("\"v2\"", File.ReadAllText(WikiHelpService.ETagFileFor(Cached(dir))));
    }

    [Fact]
    public async Task CacheFromAnOlderVersion_WithoutETag_IsComparedByContent()
    {
        using var dir = new TempDir();
        var wiki = new FakeWiki();

        File.WriteAllBytes(Cached(dir), wiki.Content);                       // same image, no .etag beside it
        Assert.False(await Service(wiki, dir).PrefetchImagesAsync(Page));
        Assert.True(File.Exists(WikiHelpService.ETagFileFor(Cached(dir))));  // kept for the next check

        File.Delete(WikiHelpService.ETagFileFor(Cached(dir)));
        File.WriteAllBytes(Cached(dir), new byte[] { 7 });                   // an old screenshot
        Assert.True(await Service(wiki, dir).PrefetchImagesAsync(Page));
        Assert.Equal(wiki.Content, File.ReadAllBytes(Cached(dir)));
    }

    [Fact]
    public async Task CachedImage_IsCheckedOncePerRun()
    {
        using var dir = new TempDir();
        var wiki = new FakeWiki();
        WikiHelpService service = Service(wiki, dir);

        await service.PrefetchImagesAsync(Page);
        await service.PrefetchImagesAsync(Page);
        await service.PrefetchImagesAsync(Page);

        Assert.Equal(1, wiki.Requests);
    }

    [Fact]
    public async Task OfflinePage_DoesNotAskForCachedImages()
    {
        using var dir = new TempDir();
        var wiki = new FakeWiki();
        await Service(wiki, dir).PrefetchImagesAsync(Page);

        Assert.False(await Service(wiki, dir).PrefetchImagesAsync(Page, revalidate: false));

        Assert.Equal(1, wiki.Requests);
    }

    [Fact]
    public async Task WikiNotReachable_KeepsTheCachedImage()
    {
        using var dir = new TempDir();
        var wiki = new FakeWiki();
        await Service(wiki, dir).PrefetchImagesAsync(Page);

        wiki.Offline = true;
        Assert.False(await Service(wiki, dir).PrefetchImagesAsync(Page));

        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(Cached(dir)));
    }

    [Fact]
    public async Task ServerWithoutETag_StillReplacesAChangedImage()
    {
        using var dir = new TempDir();
        var wiki = new FakeWiki { ETag = null };
        await Service(wiki, dir).PrefetchImagesAsync(Page);
        Assert.False(File.Exists(WikiHelpService.ETagFileFor(Cached(dir))));

        Assert.False(await Service(wiki, dir).PrefetchImagesAsync(Page));     // same content
        wiki.Content = new byte[] { 5, 5 };
        Assert.True(await Service(wiki, dir).PrefetchImagesAsync(Page));
        Assert.Equal(wiki.Content, File.ReadAllBytes(Cached(dir)));
    }
}
