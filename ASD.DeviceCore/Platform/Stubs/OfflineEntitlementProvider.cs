using System;
using System.Collections.Generic;
using ASD.Contracts;

namespace ASD.Platform.Stubs
{
    /// <summary>
    /// Default <see cref="IEntitlementProvider"/> for the open-source / offline build.
    /// Grants only always-on core functionality and denies every proprietary
    /// entitlement, so cloud + GNSS-plugin + remote-download modules stay hidden when
    /// no backend session and no plugins are present. When a real
    /// <see cref="IPlmClient"/> is bound, replace this with a provider that derives
    /// entitlements from the session tier + claims. See docs/ARCHITECTURE.md.
    /// </summary>
    public sealed class OfflineEntitlementProvider : IEntitlementProvider
    {
        public AccountTier Tier => AccountTier.Free;

        public PortalRole Role => PortalRole.Viewer;

        // Offline build holds no entitlements — local device tooling is always on and
        // is not gated, so it is not represented here.
        public bool Has(string entitlement) => false;

        public bool CanWrite => Role == PortalRole.Admin; // false offline

        public IReadOnlyCollection<string> All => Array.Empty<string>();

        // Never changes in the offline build; declared to satisfy the contract.
        public event EventHandler? Changed { add { } remove { } }
    }
}
