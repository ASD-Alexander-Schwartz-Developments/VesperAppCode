using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ASD.Contracts
{
    /// <summary>One decoded GNSS fix produced from a raw snapshot.</summary>
    public sealed record GnssFix(
        DateTimeOffset Time,
        double Latitude,
        double Longitude,
        double? AltitudeMeters,
        double? HorizontalAccuracyMeters,
        int SatellitesUsed);

    /// <summary>Inputs to a snapshot-GNSS decode job: a folder of raw snapshot
    /// captures (today: <c>snap.*.dat</c>) plus where to write decoded output.</summary>
    public sealed record GnssDecodeRequest(
        string DownloadPath,
        string DecodeOutputPath,
        string FilePattern = "snap.*.dat");

    /// <summary>Result of a decode job.</summary>
    public sealed record GnssDecodeResult(
        bool Succeeded,
        IReadOnlyList<GnssFix> Fixes,
        string? OutputPath,
        string? Message);

    /// <summary>
    /// Snapshot-GNSS decoder abstraction. All GNSS decode in the shell and daemon flows
    /// through this interface; <c>RecordingParsingViewModel</c> calls
    /// <see cref="DecodeAsync"/> and never references a concrete decoder.
    /// <para>
    /// The real implementation is the proprietary <c>ASD.Gnss</c> plugin, which drives
    /// cg-gnss's cross-platform <c>geotag-cli</c>. It ships as a closed plugin loaded at
    /// runtime from the gitignored <c>plugins/</c> folder; the cg-gnss source/binaries
    /// never enter this repo, and the formerly bundled Windows-only <c>CG\</c> GeoTag
    /// payload has been removed.
    /// </para>
    /// <para>
    /// With no plugin installed the open-source build binds <c>StubGnssDecoder</c>, which
    /// reports unavailable rather than decoding. See docs/ARCHITECTURE.md.
    /// </para>
    /// </summary>
    public interface IGnssDecoder
    {
        /// <summary>True when a real decoder plugin is bound. The stub returns false so
        /// callers can surface "GNSS plugin not installed" or hide the feature.</summary>
        bool IsAvailable { get; }

        /// <summary>Decode a folder of raw GNSS snapshots into fixes.</summary>
        Task<GnssDecodeResult> DecodeAsync(
            GnssDecodeRequest request,
            IProgress<string>? progress = null,
            CancellationToken ct = default);
    }
}
