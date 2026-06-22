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
    /// Snapshot-GNSS decoder abstraction.
    /// <para>
    /// TODAY the decode is performed by <c>RecordingParsingViewModel</c> shelling out
    /// to the bundled Windows-only <c>CG\GeoTag\GeoTag.exe</c> + <c>GeoTagEngine.exe</c>.
    /// Those binaries (and the Intel IPP DLLs under <c>CG\</c>) are proprietary and
    /// currently sit inside this open-source repo — the exposure we are eliminating.
    /// </para>
    /// <para>
    /// FUTURE: the decoder moves behind this interface. The cross-platform
    /// implementation (<c>ASD.Gnss</c>, P/Invoke over the <c>cg-gnss</c> C library)
    /// ships as a closed plugin and removes the Windows-only <c>CG\</c> payload from
    /// the public repo. The open-source build binds <c>StubGnssDecoder</c>, which
    /// reports unavailable rather than decoding. See docs/ARCHITECTURE.md.
    /// </para>
    /// </summary>
    public interface IGnssDecoder
    {
        /// <summary>True when a real decoder plugin is bound. The stub returns false so
        /// callers can fall back (e.g. keep the legacy GeoTag.exe path on Windows) or
        /// hide the feature.</summary>
        bool IsAvailable { get; }

        /// <summary>Decode a folder of raw GNSS snapshots into fixes.</summary>
        Task<GnssDecodeResult> DecodeAsync(
            GnssDecodeRequest request,
            IProgress<string>? progress = null,
            CancellationToken ct = default);
    }
}
