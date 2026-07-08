using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ASD.Platform;

namespace VesperApp.Services
{
    /// <summary>Outcome of a plugin update attempt.</summary>
    public sealed record PluginUpdateResult(bool Success, string Message, string? StagedPath = null);

    /// <summary>
    /// Updates a proprietary plugin pack (e.g. the GNSS pack) on its OWN channel, independent of the
    /// shell self-update and device firmware. It reads the pack's release feed from the CDN, picks the
    /// entry for THIS platform, and — when the account is entitled — downloads and stages it into the
    /// loader's per-user plugins folder (<see cref="PluginLoader.DataPluginsDir"/>). Staged packs bind
    /// on the NEXT launch (the loader runs at startup); a loaded native decoder is never hot-swapped.
    /// No credentials: the feed and assets are read over plain HTTPS. See docs/ARCHITECTURE.md.
    /// </summary>
    public sealed class PluginUpdateService
    {
        private readonly ReleaseFeedService _feed;
        private readonly string _packName;             // sub-folder under plugins/, e.g. "gnss"
        private readonly string? _requiredEntitlement; // e.g. "gnss.postprocess"; null = always allowed

        public PluginUpdateService(
            Uri feedUrl,
            string packName,
            string? requiredEntitlement = null,
            Func<Uri, Uri>? downloadUrlResolver = null)
        {
            _feed = new ReleaseFeedService(feedUrl, downloadUrlResolver: downloadUrlResolver);
            _packName = packName;
            _requiredEntitlement = requiredEntitlement;
        }

        /// <summary>True when the signed-in account may receive this pack (no requirement = always).</summary>
        public bool IsEntitled =>
            string.IsNullOrEmpty(_requiredEntitlement) ||
            PlatformServices.Entitlements.Has(_requiredEntitlement!);

        /// <summary>The highest-version feed entry matching this host's platform, or null. Does not
        /// rely on feed ordering — entries are compared by semantic version so the true newest wins.</summary>
        public async Task<ReleaseEntry?> CheckLatestAsync(CancellationToken ct = default)
        {
            string plat = PluginLoader.CurrentPlatform();
            var releases = await _feed.GetReleasesAsync(ct).ConfigureAwait(false);
            // Accept an entry with no Target (any) or one matching this platform.
            return releases
                .Where(r => string.IsNullOrWhiteSpace(r.Target) ||
                            string.Equals(r.Target, plat, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(r => ParseVersion(r.Version))
                .ThenByDescending(r => r.Version, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        }

        /// <summary>The version of the currently-installed pack (from its <c>plugin.json</c>), or
        /// null when the pack isn't installed. Checks the per-user data folder first, then the
        /// bundled folder next to the binary.</summary>
        public string? InstalledVersion()
        {
            foreach (string root in new[] { PluginLoader.DataPluginsDir, PluginLoader.BundledPluginsDir })
            {
                string packDir = Path.Combine(root, _packName);
                if (!Directory.Exists(packDir)) continue;
                try
                {
                    string? manifest = Directory
                        .EnumerateFiles(packDir, "plugin.json", SearchOption.AllDirectories)
                        .FirstOrDefault();
                    if (manifest is null) continue;
                    PluginManifest? m = PluginManifest.TryLoad(manifest);
                    if (!string.IsNullOrWhiteSpace(m?.Version)) return m!.Version;
                }
                catch { /* unreadable pack — treat as not installed */ }
            }
            return null;
        }

        /// <summary>True when <paramref name="feedVersion"/> is strictly newer than
        /// <paramref name="installedVersion"/> (or nothing is installed). Falls back to a
        /// case-insensitive string difference when a version isn't a parseable dotted number.</summary>
        public static bool IsNewer(string? feedVersion, string? installedVersion)
        {
            if (string.IsNullOrWhiteSpace(feedVersion)) return false;
            if (string.IsNullOrWhiteSpace(installedVersion)) return true;
            Version? f = ParseVersion(feedVersion), i = ParseVersion(installedVersion);
            if (f is not null && i is not null) return f > i;
            return !string.Equals(feedVersion.Trim(), installedVersion.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private static Version? ParseVersion(string? v) =>
            Version.TryParse((v ?? string.Empty).Trim().TrimStart('v', 'V'), out Version? parsed) ? parsed : null;

        /// <summary>
        /// Download, verify and stage the pack into the per-user plugins folder (applied next launch).
        /// Extracts to a fresh folder then swaps, so a half-written pack never binds. Refuses when the
        /// account is not entitled.
        /// </summary>
        public async Task<PluginUpdateResult> DownloadAndStageAsync(
            ReleaseEntry entry, IProgress<double>? progress = null, CancellationToken ct = default)
        {
            if (!IsEntitled)
                return new PluginUpdateResult(false, "This account is not entitled to this plugin.");
            if (string.IsNullOrWhiteSpace(entry.Asset))
                return new PluginUpdateResult(false, "Release has no downloadable asset.");

            string tempZip = Path.Combine(Path.GetTempPath(), $"vesper-plugin-{_packName}-{Guid.NewGuid():N}.zip");
            string targetDir = Path.Combine(PluginLoader.DataPluginsDir, _packName);
            string stagingDir = targetDir + ".staging-" + Guid.NewGuid().ToString("N");

            try
            {
                await _feed.DownloadAssetAsync(entry, tempZip, progress, ct).ConfigureAwait(false);

                Directory.CreateDirectory(stagingDir);
                ZipFile.ExtractToDirectory(tempZip, stagingDir, overwriteFiles: true);

                // Swap staging into place; move the old aside first so a locked DLL doesn't block us.
                Directory.CreateDirectory(Path.GetDirectoryName(targetDir)!);
                if (Directory.Exists(targetDir))
                {
                    string trash = targetDir + ".old-" + Guid.NewGuid().ToString("N");
                    Directory.Move(targetDir, trash);
                    TryDeleteDir(trash);
                }
                Directory.Move(stagingDir, targetDir);

                return new PluginUpdateResult(true,
                    $"Plugin '{_packName}' v{entry.Version} staged. Restart to apply.", targetDir);
            }
            catch (Exception ex)
            {
                TryDeleteDir(stagingDir);
                return new PluginUpdateResult(false, "Plugin update failed: " + ex.Message);
            }
            finally
            {
                try { if (File.Exists(tempZip)) File.Delete(tempZip); } catch { /* best effort */ }
            }
        }

        private static void TryDeleteDir(string dir)
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
            catch { /* best effort */ }
        }
    }
}
