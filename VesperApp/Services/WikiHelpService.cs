using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Platform;

namespace VesperApp.Services
{
    /// <summary>One entry of the Help navigation: either a section header (no page) or a wiki page link.</summary>
    public sealed record HelpNavItem(string Title, string? PageName)
    {
        public bool IsSection => PageName is null;
        public bool IsPage => PageName is not null;
    }

    /// <summary>A fetched help page plus where it came from (live wiki vs. offline copy).</summary>
    public sealed record HelpPage(string Markdown, bool FromCache);

    /// <summary>
    /// Serves the Help tab from the project's public GitHub wiki, read over plain HTTPS from
    /// <c>raw.githubusercontent.com/wiki/…</c> — like <see cref="ReleaseFeedService"/>, this uses
    /// <b>no credentials or GitHub API</b>. Every successful fetch is cached under
    /// <c>%LOCALAPPDATA%/VesperApp/helpcache</c> so Help keeps working offline; a copy of the wiki
    /// authored with the app ships as embedded resources (<c>docs/wiki/*.md</c>) as the
    /// first-run fallback before any fetch has succeeded.
    /// </summary>
    public sealed class WikiHelpService
    {
        public const string Owner = "ASD-Alexander-Schwartz-Developments";
        public const string Repo = "VesperAppCode";

        /// <summary>The wiki's home in a browser, for the "Open in browser" affordance.</summary>
        public static readonly Uri WikiHomeUrl = new($"https://github.com/{Owner}/{Repo}/wiki");

        private static readonly Uri RawBase = new($"https://raw.githubusercontent.com/wiki/{Owner}/{Repo}/");
        private static readonly HttpClient SharedHttp = new() { Timeout = TimeSpan.FromSeconds(15) };

        private readonly HttpClient _http;

        public WikiHelpService(HttpClient? http = null) => _http = http ?? SharedHttp;

        private static string CacheDirectory => Path.Combine(SettingsService.ConfigDirectory, "helpcache");

        // Wiki page names as used in links/URLs: letters, digits, '-' and '_' only.
        private static readonly Regex SafePageName = new(@"^[A-Za-z0-9_-]+$", RegexOptions.Compiled);

        // "[Title](Page)" — target without a scheme/anchor is an in-wiki page link.
        private static readonly Regex MdLink = new(@"\[(?<title>[^\]]+)\]\((?<target>[^)#\s]+)\)", RegexOptions.Compiled);

        // "**Section**" line (that is not itself a link) in _Sidebar.md.
        private static readonly Regex SectionHeader = new(@"^\*\*(?<title>[^\[\]*]+)\*\*$", RegexOptions.Compiled);

        /// <summary>
        /// Fetch one wiki page (by wiki page name, e.g. <c>Getting-Started</c>). Resolution order:
        /// live wiki → on-disk cache of a previous fetch → embedded copy shipped with the app.
        /// Returns <c>null</c> only when all three are unavailable.
        /// </summary>
        public async Task<HelpPage?> GetPageAsync(string pageName, CancellationToken ct = default)
        {
            if (!SafePageName.IsMatch(pageName))
                return null;

            try
            {
                string md = await _http.GetStringAsync(new Uri(RawBase, pageName + ".md"), ct).ConfigureAwait(false);
                TryWriteCache(pageName, md);
                return new HelpPage(md, FromCache: false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                // Offline, wiki not yet published, or page missing — fall through to local copies.
            }

            string? cached = TryReadCache(pageName) ?? TryReadEmbedded(pageName);
            return cached is null ? null : new HelpPage(cached, FromCache: true);
        }

        /// <summary>
        /// Build the Help navigation from the wiki's <c>_Sidebar.md</c> (same file GitHub renders
        /// as the wiki sidebar), so docs authors control the in-app nav without an app release.
        /// </summary>
        public async Task<IReadOnlyList<HelpNavItem>> GetNavigationAsync(CancellationToken ct = default)
        {
            HelpPage? sidebar = await GetPageAsync("_Sidebar", ct).ConfigureAwait(false);
            List<HelpNavItem> items = sidebar is null ? new List<HelpNavItem>() : ParseSidebar(sidebar.Markdown);
            return items.Count > 0 ? items : FallbackNavigation;
        }

        internal static List<HelpNavItem> ParseSidebar(string sidebarMarkdown)
        {
            var items = new List<HelpNavItem>();
            foreach (string rawLine in sidebarMarkdown.Split('\n'))
            {
                string line = rawLine.Trim().TrimStart('-').Trim();
                if (line.Length == 0) continue;

                Match link = MdLink.Match(line);
                if (link.Success)
                {
                    string target = link.Groups["target"].Value;
                    if (SafePageName.IsMatch(target))
                        items.Add(new HelpNavItem(link.Groups["title"].Value, target));
                    continue;
                }

                Match section = SectionHeader.Match(line);
                if (section.Success)
                    items.Add(new HelpNavItem(section.Groups["title"].Value.Trim(), null));
            }
            return items;
        }

        /// <summary>Mirrors the published wiki structure; used when even _Sidebar is unreachable.</summary>
        private static readonly IReadOnlyList<HelpNavItem> FallbackNavigation = new[]
        {
            new HelpNavItem("Home", "Home"),
            new HelpNavItem("Setup", null),
            new HelpNavItem("Getting Started", "Getting-Started"),
            new HelpNavItem("Docking Station", "Docking-Station"),
            new HelpNavItem("Supported Devices", "Supported-Devices"),
            new HelpNavItem("Working with data", null),
            new HelpNavItem("Recordings", "Recordings"),
            new HelpNavItem("GNSS Decoding", "GNSS-Decoding"),
            new HelpNavItem("Device management", null),
            new HelpNavItem("Configuration Editor", "Configuration-Editor"),
            new HelpNavItem("Device Tests", "Device-Tests"),
            new HelpNavItem("Firmware Updates", "Firmware-Updates"),
            new HelpNavItem("Application", null),
            new HelpNavItem("Software Updates and Plugins", "Software-Updates-and-Plugins"),
            new HelpNavItem("Settings", "Settings"),
            new HelpNavItem("Support", null),
            new HelpNavItem("Troubleshooting and FAQ", "Troubleshooting-and-FAQ"),
        };

        // ── Images ──
        //
        // Pages reference images RELATIVELY ("images/foo.png") so the same markdown renders on
        // the GitHub wiki and in-app. In-app resolution is all local: PrefetchImagesAsync pulls
        // referenced images into the cache when online, and the Help view's bitmap loader serves
        // them from cache → embedded copy — so images work identically for live, cached and
        // embedded pages.

        private static string ImageCacheDirectory => Path.Combine(CacheDirectory, "images");

        // "![alt](target …)" — image references in a page.
        private static readonly Regex MdImage = new(@"!\[[^\]]*\]\((?<target>[^)\s]+)[^)]*\)", RegexOptions.Compiled);

        // In-wiki image path as written in the markdown: images/<name.ext>.
        private static readonly Regex SafeImagePath = new(@"^images/[A-Za-z0-9._-]+$", RegexOptions.Compiled);

        /// <summary>Cache file for an image reference; null when the reference isn't cacheable.</summary>
        internal static string? ImageCacheFileFor(string target)
        {
            if (SafeImagePath.IsMatch(target))
                return Path.Combine(ImageCacheDirectory, Path.GetFileName(target));

            // Absolute web images are cached under a URL-derived name.
            if (Uri.TryCreate(target, UriKind.Absolute, out Uri? abs) &&
                (abs.Scheme == Uri.UriSchemeHttp || abs.Scheme == Uri.UriSchemeHttps))
            {
                string ext = Path.GetExtension(abs.AbsolutePath);
                if (ext.Length is 0 or > 8) ext = ".img";
                string hash = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(target)))[..12];
                return Path.Combine(ImageCacheDirectory, "ext-" + hash + ext);
            }

            return null;
        }

        /// <summary>
        /// Download the images a page references into the cache (skipping ones already there).
        /// Returns true when anything new arrived, so the caller can re-render the page.
        /// </summary>
        public async Task<bool> PrefetchImagesAsync(string markdown, CancellationToken ct = default)
        {
            bool gotNew = false;
            foreach (string target in MdImage.Matches(markdown).Select(m => m.Groups["target"].Value).Distinct())
            {
                string? cachePath = ImageCacheFileFor(target);
                if (cachePath is null || File.Exists(cachePath))
                    continue;

                Uri url = SafeImagePath.IsMatch(target) ? new Uri(RawBase, target) : new Uri(target);
                try
                {
                    byte[] bytes = await _http.GetByteArrayAsync(url, ct).ConfigureAwait(false);
                    Directory.CreateDirectory(ImageCacheDirectory);
                    await File.WriteAllBytesAsync(cachePath, bytes, ct).ConfigureAwait(false);
                    gotNew = true;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch { /* offline or missing on the wiki — the loader falls back to the embedded copy */ }
            }
            return gotNew;
        }

        /// <summary>Open a local copy of an image reference: cache first, then the embedded
        /// <c>docs/wiki/images</c> resource. Null when neither exists (e.g. offline, never fetched).</summary>
        public static Stream? OpenLocalImage(string target)
        {
            try
            {
                string? cachePath = ImageCacheFileFor(target);
                if (cachePath is not null && File.Exists(cachePath))
                    return File.OpenRead(cachePath);
            }
            catch { /* fall through to embedded */ }

            if (SafeImagePath.IsMatch(target))
            {
                try
                {
                    var uri = new Uri($"avares://VesperApp/docs/wiki/{target}");
                    if (AssetLoader.Exists(uri))
                        return AssetLoader.Open(uri);
                }
                catch { }
            }

            return null;
        }

        private static void TryWriteCache(string pageName, string markdown)
        {
            try
            {
                Directory.CreateDirectory(CacheDirectory);
                File.WriteAllText(Path.Combine(CacheDirectory, pageName + ".md"), markdown);
            }
            catch { /* cache is best effort */ }
        }

        private static string? TryReadCache(string pageName)
        {
            try
            {
                string path = Path.Combine(CacheDirectory, pageName + ".md");
                return File.Exists(path) ? File.ReadAllText(path) : null;
            }
            catch { return null; }
        }

        private static string? TryReadEmbedded(string pageName)
        {
            try
            {
                var uri = new Uri($"avares://VesperApp/docs/wiki/{pageName}.md");
                if (!AssetLoader.Exists(uri)) return null;
                using var stream = AssetLoader.Open(uri);
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd();
            }
            catch { return null; }
        }
    }
}
