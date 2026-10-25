using System;

namespace VesperApp.Services
{
    /// <summary>
    /// Which Velopack channel the self-updater reads, and whether it may go to a lower version.
    /// Pure logic (no Velopack types) so it is unit-tested; <c>UpdateCheckerViewModel</c> feeds it
    /// the runtime id and the channel the installed package was built for
    /// (<c>VelopackLocator.Current.Channel</c>, e.g. <c>win-x64-beta</c>).
    ///
    /// Rules (docs/CDN-HARDENING.md, Part 3):
    ///  • A normal update check follows the channel the app was INSTALLED from — a beta install
    ///    gets newer betas, a stable install gets newer stables — and never offers a lower version.
    ///  • A dev / not-installed build (no channel) falls back to <c>&lt;rid&gt;-stable</c>.
    ///  • Changing ring (e.g. "Leave beta") is only ever an explicit user action; only then may the
    ///    target be lower than the running version (1.2.0 beta → 1.1.0 stable).
    /// </summary>
    public static class UpdateChannelPolicy
    {
        public const string StableRing = "stable";
        public const string BetaRing = "beta";

        /// <summary>The channel Velopack should read plus whether a downgrade is acceptable.</summary>
        public readonly record struct Decision(string Channel, bool AllowDowngrade, bool IsRingSwitch);

        /// <summary>The release ring encoded in a channel name (<c>win-x64-beta</c> → <c>beta</c>);
        /// <c>stable</c> for null/empty or unknown suffixes.</summary>
        public static string RingOf(string? channel)
        {
            if (string.IsNullOrWhiteSpace(channel))
                return StableRing;
            return channel.EndsWith("-" + BetaRing, StringComparison.OrdinalIgnoreCase) ? BetaRing : StableRing;
        }

        /// <summary>Channel name for a runtime id and ring: <c>win-x64</c> + <c>beta</c> → <c>win-x64-beta</c>.</summary>
        public static string ChannelFor(string rid, string ring) => $"{rid}-{ring}";

        /// <summary>
        /// Decide the update channel.
        /// </summary>
        /// <param name="rid">Runtime id of this process (win-x64 / win-x86 / linux-x64 / osx-arm64).</param>
        /// <param name="installedChannel">Channel the installed package was built for; null in a dev build.</param>
        /// <param name="requestedRing">Null for a normal check; a ring name only when the USER asked to switch.</param>
        public static Decision Decide(string rid, string? installedChannel, string? requestedRing = null)
        {
            string current = string.IsNullOrWhiteSpace(installedChannel)
                ? ChannelFor(rid, StableRing)
                : installedChannel!;

            if (string.IsNullOrWhiteSpace(requestedRing))
                return new Decision(current, AllowDowngrade: false, IsRingSwitch: false);

            string target = ChannelFor(rid, requestedRing!.Trim().ToLowerInvariant());
            bool isSwitch = !string.Equals(target, current, StringComparison.OrdinalIgnoreCase);
            // Downgrade only as part of an explicit ring switch the user asked for.
            return new Decision(target, AllowDowngrade: isSwitch, IsRingSwitch: isSwitch);
        }

        /// <summary>
        /// Guard applied to whatever the feed returned: a normal check never offers a version that
        /// is not strictly newer; an explicit ring switch may offer any version.
        /// </summary>
        public static bool ShouldOffer<TVersion>(TVersion? current, TVersion? target, Decision decision)
            where TVersion : class, IComparable<TVersion>
        {
            if (target is null)
                return false;
            if (decision.AllowDowngrade || current is null)
                return true;
            return target.CompareTo(current) > 0;
        }
    }
}
