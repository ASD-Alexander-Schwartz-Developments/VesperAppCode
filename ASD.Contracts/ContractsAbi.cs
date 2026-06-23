namespace ASD.Contracts
{
    /// <summary>
    /// The plugin ABI version — the ONE coupling point between the open VesperApp shell and
    /// the proprietary plugins. A pack declares the ABI it was built against in its
    /// <c>plugin.json</c>; <c>ASD.Platform.PluginLoader</c> refuses to bind a pack whose ABI
    /// the host does not support, so a mismatched plugin fails loudly instead of crashing on a
    /// missing/changed member.
    /// <para>
    /// Bump <see cref="Version"/> ONLY on a breaking change to the contract surface
    /// (<see cref="IGnssDecoder"/>, <c>IPlmClient</c>, <c>IEntitlementProvider</c>, or their
    /// DTOs). Additive, backward-compatible changes keep the same number — that is what lets
    /// cg-gnss (and other plugins) ship on their own cadence without a shell release. A shell
    /// release is required only when this number changes. See docs/ARCHITECTURE.md.
    /// </para>
    /// </summary>
    public static class ContractsAbi
    {
        /// <summary>The current contract ABI the host implements. Plugins built against an ABI
        /// in <c>[<see cref="MinSupported"/>, <see cref="Version"/>]</c> are accepted.</summary>
        public const int Version = 1;

        /// <summary>The oldest plugin ABI the host still accepts. Today only the current ABI is
        /// supported, so this equals <see cref="Version"/>; widen it when we add a
        /// backward-compatible contract revision.</summary>
        public const int MinSupported = 1;
    }
}
