using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace VesperApp.Services
{
    /// <summary>
    /// Reads a release feed (<c>index.json</c>) from the CDN and downloads assets over plain HTTPS,
    /// with <b>no embedded credentials</b>. This replaces the GitHub-API path that required a PAT:
    /// CI (running in the private source repo, where the publishing secret lives) builds artifacts
    /// and uploads them plus the feed to S3/CloudFront; the client only ever reads from that origin.
    /// <para>
    /// Downloads are public-read today. If gated downloads are needed later, inject a
    /// <paramref name="downloadUrlResolver"/> that turns a public asset URL into a short-lived
    /// signed URL minted by the backend — no other client change. See docs/ARCHITECTURE.md.
    /// </para>
    /// </summary>
    public sealed class ReleaseFeedService
    {
        private static readonly HttpClient SharedHttp = new() { Timeout = TimeSpan.FromMinutes(5) };

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
        };

        private readonly HttpClient _http;
        private readonly Uri _indexUrl;
        private readonly Func<Uri, Uri> _resolveDownloadUrl;

        /// <param name="indexUrl">Absolute URL of the channel's <c>index.json</c> on the CDN.</param>
        /// <param name="http">Optional shared <see cref="HttpClient"/>.</param>
        /// <param name="downloadUrlResolver">
        /// Optional hook to transform a public asset URL before download (e.g. into a signed URL).
        /// Defaults to identity — the public CDN URL is used as-is.
        /// </param>
        public ReleaseFeedService(Uri indexUrl, HttpClient? http = null, Func<Uri, Uri>? downloadUrlResolver = null)
        {
            _indexUrl = indexUrl ?? throw new ArgumentNullException(nameof(indexUrl));
            _http = http ?? SharedHttp;
            _resolveDownloadUrl = downloadUrlResolver ?? (u => u);
        }

        /// <summary>Fetch and parse the feed; returns its releases (empty on an empty/invalid feed).</summary>
        public async Task<IReadOnlyList<ReleaseEntry>> GetReleasesAsync(CancellationToken ct = default)
        {
            await using Stream s = await _http.GetStreamAsync(_indexUrl, ct).ConfigureAwait(false);
            ReleaseFeed? feed = await JsonSerializer.DeserializeAsync<ReleaseFeed>(s, JsonOpts, ct).ConfigureAwait(false);
            return feed?.Releases ?? new List<ReleaseEntry>();
        }

        /// <summary>Resolve an entry's download URL: absolute as-is, relative against the feed URL,
        /// then passed through the (optional) signer hook.</summary>
        public Uri ResolveAssetUri(ReleaseEntry entry)
        {
            if (entry is null) throw new ArgumentNullException(nameof(entry));
            if (string.IsNullOrWhiteSpace(entry.Asset))
                throw new InvalidOperationException("Release entry has no asset path.");

            Uri assetUri = Uri.TryCreate(entry.Asset, UriKind.Absolute, out Uri? abs)
                ? abs
                : new Uri(_indexUrl, entry.Asset);

            return _resolveDownloadUrl(assetUri);
        }

        /// <summary>Stream an asset to <paramref name="destinationPath"/>, reporting 0..1 progress;
        /// verifies SHA-256 when the entry provides one (and deletes a mismatched file).</summary>
        public async Task DownloadAssetAsync(
            ReleaseEntry entry,
            string destinationPath,
            IProgress<double>? progress = null,
            CancellationToken ct = default)
        {
            Uri url = ResolveAssetUri(entry);

            using HttpResponseMessage resp =
                await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();

            long? total = resp.Content.Headers.ContentLength ?? (entry.Size > 0 ? entry.Size : null);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? ".");

            await using (Stream src = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false))
            await using (var dst = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var buffer = new byte[81920];
                long readTotal = 0;
                int n;
                while ((n = await src.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
                {
                    await dst.WriteAsync(buffer.AsMemory(0, n), ct).ConfigureAwait(false);
                    readTotal += n;
                    if (total is > 0)
                        progress?.Report((double)readTotal / total.Value);
                }
            }

            if (!string.IsNullOrWhiteSpace(entry.Sha256))
                await VerifySha256Async(destinationPath, entry.Sha256!, ct).ConfigureAwait(false);
        }

        private static async Task VerifySha256Async(string path, string expectedHex, CancellationToken ct)
        {
            await using FileStream fs = File.OpenRead(path);
            byte[] hash = await SHA256.HashDataAsync(fs, ct).ConfigureAwait(false);
            string actual = Convert.ToHexString(hash);
            string expected = expectedHex.Replace("0x", "").Trim();

            if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            {
                try { File.Delete(path); } catch { /* best effort */ }
                throw new InvalidDataException(
                    $"Downloaded asset SHA-256 mismatch (expected {expected}, got {actual}). File deleted.");
            }
        }
    }
}
