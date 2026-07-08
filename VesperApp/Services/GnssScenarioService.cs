using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace VesperApp.Services
{
    /// <summary>
    /// Manages the gps-sdr-sim positioning scenario as an on-demand asset. The bin lives
    /// on the SAME CDN as the app's own releases/firmware (CloudFront, see
    /// docs/ci-templates) and is cached under the per-user data folder. The UI checks
    /// <see cref="Exists"/> and, when missing, offers <see cref="DownloadAsync"/> with
    /// progress — there are no user-typed scenario paths.
    /// </summary>
    public sealed class GnssScenarioService
    {
        // Asset path on the same client-read CDN origin the firmware/plugin feeds use (CdnConfig).
        // The origin is injected at build time, not a literal here. Override the whole URL with
        // VESPERAPP_GNSS_SCENARIO_URL, or the origin with VESPERAPP_CDN_BASE. See docs/CDN-HARDENING.md.
        public const string ScenarioRelativePath = "scenarios/static_tokyo_s2600k_30s.bin";

        private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(10) };

        /// <summary>The resolved asset URL, or <c>null</c> when no CDN origin is configured in this build.</summary>
        public Uri? Url { get; }
        public string FileName { get; }
        public string LocalPath { get; }

        /// <summary>True when an asset source is configured (build-time origin or an override).</summary>
        public bool IsConfigured => Url is not null;

        public GnssScenarioService(string? url = null)
        {
            string? u = url
                ?? Environment.GetEnvironmentVariable("VESPERAPP_GNSS_SCENARIO_URL")
                ?? CdnConfig.FeedUri(ScenarioRelativePath)?.ToString();
            Url = string.IsNullOrWhiteSpace(u) ? null : new Uri(u);
            // Keep a stable file name (and thus LocalPath) even when unconfigured, so the cache
            // location and Exists check are consistent regardless of how the URL was resolved.
            FileName = Path.GetFileName(Url?.LocalPath ?? ScenarioRelativePath);
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "VesperApp", "gnss-scenarios");
            LocalPath = Path.Combine(dir, FileName);
        }

        /// <summary>True when a non-empty cached copy exists locally.</summary>
        public bool Exists => File.Exists(LocalPath) && new FileInfo(LocalPath).Length > 0;

        /// <summary>
        /// Download the asset to the local cache, reporting 0..1 progress. Streams to a
        /// <c>.part</c> file then moves it into place, so an interrupted download never
        /// leaves a half-written file that looks complete.
        /// </summary>
        public async Task DownloadAsync(IProgress<double>? progress = null, CancellationToken ct = default)
        {
            if (Url is null)
                throw new InvalidOperationException(
                    "No GNSS scenario source configured: set VESPERAPP_GNSS_SCENARIO_URL, " +
                    "or build with -p:CdnBaseUrl=… (see docs/CDN-HARDENING.md).");

            Directory.CreateDirectory(Path.GetDirectoryName(LocalPath)!);
            string tmp = LocalPath + ".part";

            using (HttpResponseMessage resp =
                await Http.GetAsync(Url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false))
            {
                resp.EnsureSuccessStatusCode();
                long? total = resp.Content.Headers.ContentLength;

                await using Stream src = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                await using (var dst = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    var buf = new byte[81920];
                    long got = 0;
                    int n;
                    while ((n = await src.ReadAsync(buf, ct).ConfigureAwait(false)) > 0)
                    {
                        await dst.WriteAsync(buf.AsMemory(0, n), ct).ConfigureAwait(false);
                        got += n;
                        if (total is > 0) progress?.Report((double)got / total.Value);
                    }
                }
            }

            if (File.Exists(LocalPath)) File.Delete(LocalPath);
            File.Move(tmp, LocalPath);
            progress?.Report(1.0);
        }
    }
}
