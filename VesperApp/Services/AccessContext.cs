using ASD.Contracts;
using ASD.Platform;

namespace VesperApp.Services
{
    /// <summary>The three access tiers the UI projects onto the open-core platform.</summary>
    public enum AccessLevel
    {
        /// <summary>No backend session — the local-only open-source experience.</summary>
        Anonymous,

        /// <summary>Signed in, free org tier.</summary>
        FreeAccount,

        /// <summary>Signed in, paid org tier.</summary>
        Paid,
    }

    /// <summary>
    /// The user's effective access level, derived live from <see cref="PlatformServices"/> — never
    /// stored — so it always reflects the current backend session and org tier:
    /// <list type="bullet">
    /// <item>no session → <see cref="AccessLevel.Anonymous"/></item>
    /// <item>session + free tier → <see cref="AccessLevel.FreeAccount"/></item>
    /// <item>session + paid tier → <see cref="AccessLevel.Paid"/></item>
    /// </list>
    /// The open-source build binds the inert stub client, so this is always
    /// <see cref="AccessLevel.Anonymous"/> until the proprietary <c>ASD.PlmClient</c> plugin and a
    /// sign-in flow are present. Client-side checks here are UX only — privileged actions are
    /// authorised server-side against the Cognito JWT. See docs/ARCHITECTURE.md.
    /// </summary>
    public static class AccessContext
    {
        /// <summary>The current access level, from the live session + tier.</summary>
        public static AccessLevel Current =>
            PlatformServices.Plm.CurrentSession is null ? AccessLevel.Anonymous
            : PlatformServices.Entitlements.Tier == AccountTier.Paid ? AccessLevel.Paid
            : AccessLevel.FreeAccount;

        /// <summary>True when a backend session exists — a registered user, free OR paid.</summary>
        public static bool IsRegistered => Current != AccessLevel.Anonymous;

        /// <summary>True only for a paid account.</summary>
        public static bool IsPaid => Current == AccessLevel.Paid;
    }
}
