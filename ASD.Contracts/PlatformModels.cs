using System;
using System.Collections.Generic;

namespace ASD.Contracts
{
    // ---------------------------------------------------------------------------
    // Shared DTOs for the open-core platform seam. These types are deliberately
    // UI-free and dependency-free so they can be lifted, verbatim, into a standalone
    // ASD.Contracts assembly when the open/proprietary split happens (see
    // docs/ARCHITECTURE.md). Nothing here talks to the network or the backend; the
    // concrete clients that do live behind the interfaces in this folder and ship as
    // separate (proprietary) plugins in the future.
    // ---------------------------------------------------------------------------

    /// <summary>Subscription tier of the signed-in user's organisation.
    /// Mirrors plm-cdk <c>OrgItem.tier</c> ('free' | 'paid').</summary>
    public enum AccountTier
    {
        Free = 0,
        Paid = 1,
    }

    /// <summary>Per-user portal role. Mirrors the customer Cognito claim
    /// <c>custom:portalRole</c> ('viewer' | 'admin').</summary>
    public enum PortalRole
    {
        Viewer = 0,
        Admin = 1,
    }

    /// <summary>An authenticated backend session. Returned by <see cref="IPlmClient.SignInAsync"/>.
    /// The actual token handling (Cognito SRP, refresh) lives in the proprietary client.</summary>
    public sealed record PlmSession(
        PlmAccount Account,
        DateTimeOffset ExpiresAt);

    /// <summary>The signed-in user's identity + entitlements, distilled from the JWT
    /// claims and the org record. This is what the shell gates features on.</summary>
    public sealed record PlmAccount(
        string UserId,
        string Email,
        string OrgId,
        string OrgName,
        AccountTier Tier,
        PortalRole Role,
        IReadOnlyCollection<string> Entitlements);

    /// <summary>A device owned by the signed-in org. Maps to plm-cdk
    /// <c>GET /portal/devices</c>.</summary>
    public sealed record PlmDevice(
        string CanonicalId,
        string Sku,
        string Status,
        DateTimeOffset? LastSeen);

    /// <summary>One telemetry sample. Maps to plm-cdk
    /// <c>GET /portal/telemetry/{deviceId}</c>.</summary>
    public sealed record PlmTelemetryPoint(
        DateTimeOffset Timestamp,
        double? Latitude,
        double? Longitude,
        string? RawHex);

    /// <summary>A presigned S3 upload grant. Maps to plm-cdk
    /// <c>POST /documents/upload-url</c> (the stop-gap ingest path used by the
    /// base-station daemon until a typed bulk-ingest endpoint exists — see
    /// docs/plm-backend-additions.md).</summary>
    public sealed record PlmUploadTicket(
        string UploadId,
        string Url,
        string S3Key,
        int ExpiresInSeconds);
}
