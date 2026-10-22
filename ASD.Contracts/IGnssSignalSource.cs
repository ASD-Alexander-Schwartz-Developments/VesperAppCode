using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ASD.Contracts
{
    /// <summary>
    /// A simulated GNSS scenario the signal source can transmit. A static scenario has
    /// <see cref="PathRadiusMeters"/> = 0 (every fix should sit on the reference point); a moving one
    /// circles the reference at that radius.
    /// </summary>
    public sealed record GnssScenarioInfo(
        string Id,
        string Name,
        string Description,
        double ReferenceLatitude,
        double ReferenceLongitude,
        double PathRadiusMeters,
        double DurationSeconds,
        bool IsReady);

    /// <summary>The RF generator as last probed: is the radio there, and its station calibration.</summary>
    public sealed record GnssSourceStatus(
        bool RadioFound,
        string Message,
        double? CalibrationHz = null,
        DateTimeOffset? CalibratedOn = null);

    /// <summary>
    /// Bench RF stimulus for the GNSS front end (Device Tests ▸ GNSS / RF): an RF generator plugin
    /// that transmits a CW tone near L1, or a simulated constellation, into a shielded test path. The
    /// open-source build binds none — the tests that need a transmitter are unavailable until the pack
    /// is installed from the Plugins page. The generator owns its own tooling, scenarios and station
    /// calibration; the host only asks it to transmit. Bound by the plugin loader like
    /// <see cref="IGnssDecoder"/>.
    /// <para>SAFETY: L1 is a protected band — implementations transmit only into a shielded or
    /// conducted, attenuated path, and say so in their UI text.</para>
    /// </summary>
    public interface IGnssSignalSource
    {
        /// <summary>Generator name shown in the UI.</summary>
        string Name { get; }

        /// <summary>True when the pack's tooling is present (says nothing about the radio — see
        /// <see cref="ProbeAsync"/>).</summary>
        bool IsAvailable { get; }

        bool IsTransmitting { get; }

        /// <summary>Look for the radio and read the station calibration.</summary>
        Task<GnssSourceStatus> ProbeAsync(CancellationToken ct = default);

        /// <summary>Start a CW tone at L1 + <paramref name="offsetHz"/> (calibrated LO) until
        /// <see cref="Stop"/>. Returns the offset actually transmitted (the generator may round it),
        /// which is what a device-measured offset must be compared against.</summary>
        Task<double> StartToneAsync(double offsetHz, double gainDb, CancellationToken ct = default);

        /// <summary>The scenarios this generator offers, with their current readiness.</summary>
        IReadOnlyList<GnssScenarioInfo> GetScenarios();

        /// <summary>Make a scenario ready to transmit (generate or fetch it into a local cache).
        /// Progress is 0..1. Returns false when it could not be prepared.</summary>
        Task<bool> PrepareScenarioAsync(string id, IProgress<double>? progress = null, CancellationToken ct = default);

        /// <summary>Transmit a prepared scenario (cyclic, calibrated LO) until <see cref="Stop"/>.</summary>
        Task StartScenarioAsync(string id, double gainDb, CancellationToken ct = default);

        /// <summary>Add <paramref name="residualHz"/> (device-observed minus transmitted tone offset)
        /// to the station calibration; returns the new status.</summary>
        Task<GnssSourceStatus> AdjustCalibrationAsync(double residualHz, CancellationToken ct = default);

        void Stop();
    }
}
