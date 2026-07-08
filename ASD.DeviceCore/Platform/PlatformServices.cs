using System.Diagnostics;
using ASD.Contracts;
using ASD.Platform.Stubs;

namespace ASD.Platform
{
    /// <summary>
    /// Composition root for the open-core platform seam, shared by the desktop shell
    /// and the headless base-station daemon. Holds the bound platform services; the
    /// open-source build binds inert stubs, and a future proprietary plugin loader
    /// (or the daemon's startup) calls <see cref="Bind"/> to swap in real clients
    /// before <see cref="Initialize"/> runs. Callers read services only through these
    /// properties, never the concrete types. UI concerns (module navigation) live in
    /// the shell's own module host, not here. See docs/ARCHITECTURE.md.
    /// </summary>
    public static class PlatformServices
    {
        public static IPlmClient Plm { get; private set; } = new StubPlmClient();
        public static IGnssDecoder Gnss { get; private set; } = new StubGnssDecoder();
        public static IEntitlementProvider Entitlements { get; private set; } = new OfflineEntitlementProvider();

        /// <summary>The bound services as a context object, for handing to modules.</summary>
        public static IPlatformContext Context { get; private set; } =
            new PlatformContext(new StubPlmClient(), new StubGnssDecoder(), new OfflineEntitlementProvider());

        private static bool _initialised;

        /// <summary>
        /// Swap in real platform services. The proprietary plugin loader (desktop) or
        /// the daemon's startup calls this once before <see cref="Initialize"/>. Pass
        /// null to keep the current (stub) binding for a given service.
        /// </summary>
        public static void Bind(
            IPlmClient? plm = null,
            IGnssDecoder? gnss = null,
            IEntitlementProvider? entitlements = null)
        {
            if (plm != null) Plm = plm;
            if (gnss != null) Gnss = gnss;
            if (entitlements != null) Entitlements = entitlements;
        }

        /// <summary>
        /// Finalise the bound services into <see cref="Context"/>. Idempotent; call
        /// once at startup after any <see cref="Bind"/> calls.
        /// </summary>
        public static void Initialize()
        {
            if (_initialised)
                return;
            _initialised = true;

            Context = new PlatformContext(Plm, Gnss, Entitlements);

            Debug.WriteLine(
                $"[PlatformServices] initialised. " +
                $"Plm.IsConfigured={Plm.IsConfigured}, Gnss.IsAvailable={Gnss.IsAvailable}, " +
                $"Tier={Entitlements.Tier}.");
        }

        private sealed class PlatformContext : IPlatformContext
        {
            public PlatformContext(IPlmClient plm, IGnssDecoder gnss, IEntitlementProvider entitlements)
            {
                Plm = plm;
                Gnss = gnss;
                Entitlements = entitlements;
            }

            public IPlmClient Plm { get; }
            public IGnssDecoder Gnss { get; }
            public IEntitlementProvider Entitlements { get; }
        }
    }
}
