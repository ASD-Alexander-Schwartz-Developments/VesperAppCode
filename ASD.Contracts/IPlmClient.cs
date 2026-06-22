using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ASD.Contracts
{
    /// <summary>
    /// Client for the plm-cdk SaaS backend (API Gateway REST + Cognito).
    /// <para>
    /// This is the single seam between the open-source shell and the proprietary
    /// backend. The open-source build binds it to <c>StubPlmClient</c>, which is
    /// inert and never touches the network; the real implementation
    /// (<c>ASD.PlmClient</c>) ships as a separate, closed assembly that knows the
    /// API base URL, the Cognito pool/client IDs, and the auth flow. None of those
    /// secrets appear in this repo — see docs/ARCHITECTURE.md.
    /// </para>
    /// <para>
    /// FUTURE FUNCTIONALITY. Wired but inert today; the methods are the contract the
    /// proprietary plugin will fulfil, and the desktop "my devices / my data" views
    /// will bind to. Network methods on the stub throw
    /// <see cref="PlatformNotConfiguredException"/>.
    /// </para>
    /// </summary>
    public interface IPlmClient
    {
        /// <summary>True when a real (configured) backend client is bound. The stub
        /// returns false so the shell can hide cloud UI without catching exceptions.</summary>
        bool IsConfigured { get; }

        /// <summary>The current authenticated session, or null when signed out.</summary>
        PlmSession? CurrentSession { get; }

        /// <summary>Raised when <see cref="CurrentSession"/> changes (sign-in / sign-out / refresh).</summary>
        event EventHandler? SessionChanged;

        /// <summary>Authenticate against the customer Cognito pool (SRP). Returns the
        /// session including the org tier + entitlements distilled from the JWT.</summary>
        Task<PlmSession> SignInAsync(string email, string password, CancellationToken ct = default);

        /// <summary>Drop the current session and any cached tokens.</summary>
        Task SignOutAsync(CancellationToken ct = default);

        /// <summary>Devices owned by the signed-in org. Backend: <c>GET /portal/devices</c>.</summary>
        Task<IReadOnlyList<PlmDevice>> GetMyDevicesAsync(CancellationToken ct = default);

        /// <summary>Telemetry history for one device, time-bounded.
        /// Backend: <c>GET /portal/telemetry/{deviceId}</c>.</summary>
        Task<IReadOnlyList<PlmTelemetryPoint>> GetTelemetryAsync(
            string deviceId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);

        /// <summary>Request a presigned upload URL for field data (used by the desktop's
        /// manual upload and by the base-station daemon). Backend:
        /// <c>POST /documents/upload-url</c>. The caller PUTs the bytes to
        /// <see cref="PlmUploadTicket.Url"/> then calls <see cref="ConfirmUploadAsync"/>.</summary>
        Task<PlmUploadTicket> RequestUploadUrlAsync(
            string filename, string contentType, CancellationToken ct = default);

        /// <summary>Confirm a completed upload so the backend persists its metadata.
        /// Backend: <c>POST /documents/confirm</c>.</summary>
        Task ConfirmUploadAsync(string uploadId, CancellationToken ct = default);
    }

    /// <summary>Thrown by stub/unconfigured platform services when a caller invokes an
    /// operation that requires the (future) proprietary plugin or a network backend.</summary>
    public sealed class PlatformNotConfiguredException : InvalidOperationException
    {
        public PlatformNotConfiguredException(string what)
            : base($"{what} is not available in this build. " +
                   "It requires the proprietary ASD plugin, which is not present. " +
                   "See docs/ARCHITECTURE.md.") { }
    }
}
