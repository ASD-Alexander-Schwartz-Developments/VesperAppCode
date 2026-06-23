using System.Collections.Generic;

namespace VesperApp.Services
{
    /// <summary>
    /// A release feed (<c>index.json</c>) published to the CDN by CI. Plain data the client GETs
    /// over HTTPS — no credentials. One feed per channel (e.g. firmware, GNSS plugin packs).
    /// </summary>
    public sealed class ReleaseFeed
    {
        /// <summary>Feed schema version (currently 1).</summary>
        public int Schema { get; set; } = 1;

        /// <summary>When CI last wrote the feed (ISO-8601), informational.</summary>
        public string? Updated { get; set; }

        /// <summary>Available releases, newest first by convention.</summary>
        public List<ReleaseEntry> Releases { get; set; } = new();
    }

    /// <summary>One downloadable release in a <see cref="ReleaseFeed"/>.</summary>
    public sealed class ReleaseEntry
    {
        /// <summary>Semantic version, e.g. "1.2.3".</summary>
        public string? Version { get; set; }

        /// <summary>Display name, e.g. "Firmware 1.2.3".</summary>
        public string? Name { get; set; }

        /// <summary>Release notes / changelog (from the tag annotation or CHANGELOG).</summary>
        public string? Description { get; set; }

        /// <summary>Publish timestamp (ISO-8601).</summary>
        public string? Published { get; set; }

        /// <summary>Target key the entry is for: device type (firmware) or os-arch (plugin pack).</summary>
        public string? Target { get; set; }

        /// <summary>Asset location: absolute URL, or a path relative to the feed's own URL.</summary>
        public string? Asset { get; set; }

        /// <summary>Asset size in bytes (0 = unknown).</summary>
        public long Size { get; set; }

        /// <summary>Hex SHA-256 of the asset for integrity verification (optional).</summary>
        public string? Sha256 { get; set; }

        /// <summary>Contract ABI for plugin packs (mirrors plugin.json); 0 = not applicable.</summary>
        public int Abi { get; set; }
    }
}
