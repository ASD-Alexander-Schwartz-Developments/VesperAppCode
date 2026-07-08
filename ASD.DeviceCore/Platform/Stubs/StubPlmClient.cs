using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ASD.Contracts;

namespace ASD.Platform.Stubs
{
    /// <summary>
    /// Inert <see cref="IPlmClient"/> used by the open-source build. It reports itself
    /// unconfigured, holds no session, and throws <see cref="PlatformNotConfiguredException"/>
    /// for any network operation. The real client (<c>ASD.PlmClient</c>) is a closed
    /// plugin loaded at runtime when present. See docs/ARCHITECTURE.md.
    /// </summary>
    public sealed class StubPlmClient : IPlmClient
    {
        public bool IsConfigured => false;

        public PlmSession? CurrentSession => null;

        // Never raised by the stub; declared to satisfy the contract.
        public event EventHandler? SessionChanged { add { } remove { } }

        public Task<PlmSession> SignInAsync(string email, string password, CancellationToken ct = default)
            => throw new PlatformNotConfiguredException("Backend sign-in");

        public Task SignOutAsync(CancellationToken ct = default) => Task.CompletedTask;

        public Task<IReadOnlyList<PlmDevice>> GetMyDevicesAsync(CancellationToken ct = default)
            => throw new PlatformNotConfiguredException("Device list");

        public Task<IReadOnlyList<PlmTelemetryPoint>> GetTelemetryAsync(
            string deviceId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
            => throw new PlatformNotConfiguredException("Telemetry query");

        public Task<PlmUploadTicket> RequestUploadUrlAsync(
            string filename, string contentType, CancellationToken ct = default)
            => throw new PlatformNotConfiguredException("Data upload");

        public Task ConfirmUploadAsync(string uploadId, CancellationToken ct = default)
            => throw new PlatformNotConfiguredException("Data upload");
    }
}
