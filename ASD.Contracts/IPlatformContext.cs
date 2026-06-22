namespace ASD.Contracts
{
    /// <summary>
    /// The set of platform services handed to a module at initialisation. UI-free, so
    /// it can live in a future standalone ASD.Contracts assembly. A module never new's
    /// up a client; it receives whatever the host bound (real plugin or stub) and
    /// degrades gracefully when <see cref="IPlmClient.IsConfigured"/> is false.
    /// </summary>
    public interface IPlatformContext
    {
        IPlmClient Plm { get; }
        IGnssDecoder Gnss { get; }
        IEntitlementProvider Entitlements { get; }
    }
}
