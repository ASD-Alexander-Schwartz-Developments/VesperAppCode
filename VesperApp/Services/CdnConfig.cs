using System;
using System.Linq;
using System.Reflection;

namespace VesperApp.Services
{
    /// <summary>
    /// Single source of truth for the CDN origin the client READS from — the Velopack
    /// self-update root plus the firmware / plugin release feeds and on-demand scenario
    /// assets. The origin is deliberately NOT a literal in the (public) source: CI injects
    /// it at publish time via <c>-p:CdnBaseUrl=…</c> (from a repo secret) which the SDK
    /// emits as an <see cref="AssemblyMetadataAttribute"/>. Resolution order, most specific
    /// first:
    /// <list type="number">
    ///   <item>a per-asset environment override (e.g. <c>VESPERAPP_FIRMWARE_FEED</c>) — handled at the call site;</item>
    ///   <item><c>VESPERAPP_CDN_BASE</c> — the whole origin, for local dev / CI test rigs;</item>
    ///   <item>the build-time <c>[AssemblyMetadata("CdnBaseUrl")]</c> value;</item>
    ///   <item>empty — callers surface a clear "no update source configured" state instead of hitting a bogus URL.</item>
    /// </list>
    /// <para>
    /// This is NOT a security control: the origin is public-read and is trivially recoverable
    /// from a shipped binary (it is a .NET assembly) or by watching one network request.
    /// Keeping it out of the public repo only reduces casual scraping of the distribution /
    /// bucket. Actual abuse limiting is done at the edge (CloudFront WAF rate-limit + S3 OAC).
    /// See docs/CDN-HARDENING.md.
    /// </para>
    /// </summary>
    public static class CdnConfig
    {
        /// <summary>The resolved origin with any trailing slash stripped, or "" when unconfigured.</summary>
        public static string BaseUrl { get; } = Resolve();

        /// <summary>True when an origin was supplied by env override or the build-time value.</summary>
        public static bool IsConfigured => !string.IsNullOrWhiteSpace(BaseUrl);

        private static string Resolve()
        {
            string? env = Environment.GetEnvironmentVariable("VESPERAPP_CDN_BASE");
            if (!string.IsNullOrWhiteSpace(env)) return env.Trim().TrimEnd('/');

            string? meta = typeof(CdnConfig).Assembly
                .GetCustomAttributes<AssemblyMetadataAttribute>()
                .FirstOrDefault(a => string.Equals(a.Key, "CdnBaseUrl", StringComparison.Ordinal))?.Value;

            return string.IsNullOrWhiteSpace(meta) ? string.Empty : meta.Trim().TrimEnd('/');
        }

        /// <summary>Combine the origin with a feed/asset path (e.g. "plugins/gnss/index.json").
        /// Returns <c>null</c> when no origin is configured, so callers can degrade gracefully.</summary>
        public static Uri? FeedUri(string relativePath)
        {
            if (!IsConfigured) return null;
            if (string.IsNullOrWhiteSpace(relativePath))
                throw new ArgumentException("Relative path is required.", nameof(relativePath));
            return new Uri($"{BaseUrl}/{relativePath.TrimStart('/')}");
        }
    }
}
