using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ASD.Contracts;

namespace ASD.Platform.Stubs
{
    /// <summary>
    /// Inert <see cref="IGnssDecoder"/> for the open-source build: reports unavailable
    /// so callers surface "GNSS plugin not installed" or hide GNSS post-processing
    /// entirely. The real decoder — the proprietary <c>ASD.Gnss</c> plugin over cg-gnss
    /// <c>geotag-cli</c> — is dropped into <c>plugins/</c> at deploy time. See
    /// docs/ARCHITECTURE.md.
    /// </summary>
    public sealed class StubGnssDecoder : IGnssDecoder
    {
        public bool IsAvailable => false;

        public Task<GnssDecodeResult> DecodeAsync(
            GnssDecodeRequest request,
            IProgress<string>? progress = null,
            CancellationToken ct = default)
        {
            progress?.Report("GNSS post-processing plugin not installed.");
            return Task.FromResult(new GnssDecodeResult(
                Succeeded: false,
                Fixes: Array.Empty<GnssFix>(),
                OutputPath: null,
                Message: "The cross-platform GNSS decoder is not available in this build. " +
                         "Use the legacy GeoTag decode path on Windows, or install the ASD.Gnss plugin."));
        }
    }
}
