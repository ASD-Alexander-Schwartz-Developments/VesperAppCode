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

        /// <summary>The newest feed entry matching this host's platform, or null.</summary>
        public async Task<ReleaseEntry?> CheckLatestAsync(CancellationToken ct = default)
        {
            string plat = PluginLoader.CurrentPlatform();
            var releases = await _feed.GetReleasesAsync(ct).ConfigureAwait(false);
            // Feed convention: newest first. Accept an entry with no Target (any) or a matching one.
            return releases.FirstOrDefault(r =>
                string.IsNullOrWhiteSpace(r.Target) ||
                string.Equals(r.Target, plat, StringComparison.OrdinalIgnoreCase));
        }

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
