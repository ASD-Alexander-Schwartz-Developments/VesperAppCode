using System;
using System.Collections.Generic;

namespace ASD.Contracts
{
    /// <summary>
    /// Well-known entitlement keys. Entitlements are plain strings so the backend can
    /// introduce new ones without a client release; these constants name the ones the
    /// shell currently checks. The backend source of truth is the org tier plus a
    /// per-org entitlement list (see docs/plm-backend-additions.md — the self-service
    /// entitlements endpoint is a future backend addition).
    /// </summary>
    public static class Entitlements
    {
        /// <summary>Sync recordings / config with the plm-cdk backend.</summary>
        public const string CloudSync = "cloud.sync";

        /// <summary>Upload downloaded field data to the backend (base-station / desktop).</summary>
        public const string FieldDataUpload = "data.upload";

        /// <summary>Cross-platform snapshot-GNSS post-processing (the ASD.Gnss plugin).</summary>
        public const string GnssPostProcessing = "gnss.postprocess";

        /// <summary>Remote BLE download of ProxTit tags via a UART NCP adapter.</summary>
        public const string RemoteBleDownload = "ble.remote_download";

        /// <summary>Bulk export of historical telemetry beyond the free-tier window.</summary>
        public const string HistoricalExport = "data.history_export";
    }

    /// <summary>
    /// Answers "is the signed-in account allowed to use feature X?". The shell uses
    /// this to show/hide modules and write actions. The open-source build binds
    /// <c>OfflineEntitlementProvider</c>, which grants only the always-on core and
    /// denies every proprietary entitlement. When a real <see cref="IPlmClient"/> is
    /// bound, a backed provider derives entitlements from the session's tier + claims.
    /// </summary>
    public interface IEntitlementProvider
    {
        /// <summary>The account's subscription tier (Free until a paid session is bound).</summary>
        AccountTier Tier { get; }

        /// <summary>The signed-in user's portal role (Viewer until an admin session is bound).</summary>
        PortalRole Role { get; }

        /// <summary>True when the account holds the given entitlement (see <see cref="Entitlements"/>).</summary>
        bool Has(string entitlement);

        /// <summary>True when the user may perform write actions (RMA, delete, config push).
        /// Convenience over <c>Role == PortalRole.Admin</c>.</summary>
        bool CanWrite { get; }

        /// <summary>All entitlements currently held. Empty in the offline build.</summary>
        IReadOnlyCollection<string> All { get; }

        /// <summary>Raised when entitlements change (sign-in / sign-out / tier change).</summary>
        event EventHandler? Changed;
    }
}
