using ASD.Contracts;
using ASD.Platform;
using VesperApp.Services;
using Xunit;

namespace VesperApp.Tests.Services;

/// <summary>Plugins page: which feed entries are offered, and why an installed pack can't load.</summary>
public class PluginUpdateServiceTests
{
    private static ReleaseEntry E(string v, string target, int abi, string? asset = "x.zip")
        => new() { Version = v, Target = target, Abi = abi, Asset = asset };

    [Fact]
    public void PackWithoutAbi_IsNotOffered()
    {
        // pluto-gnss 0.1.0 on the CDN: tool bundle, no ASD.PlutoGnss adapter, no "abi" in the feed.
        var check = PluginUpdateService.Pick(new[] { E("0.1.0", "win-x64", 0) }, "win-x64");
        Assert.Null(check.Installable);
        Assert.Equal("0.1.0", check.Newest?.Version);
    }

    [Fact]
    public void NewestInstallableWins_OverNewerUnloadable()
    {
        var check = PluginUpdateService.Pick(new[]
        {
            E("0.3.0", "win-x64", ContractsAbi.Version + 1),
            E("0.2.0", "win-x64", ContractsAbi.Version),
            E("0.1.0", "win-x64", 0),
            E("0.9.0", "linux-x64", ContractsAbi.Version),
        }, "win-x64");
        Assert.Equal("0.2.0", check.Installable?.Version);
        Assert.Equal("0.3.0", check.Newest?.Version);
    }

    [Fact]
    public void EntryWithoutAsset_IsNotInstallable()
        => Assert.False(PluginUpdateService.IsInstallable(E("1.0.0", "win-x64", ContractsAbi.Version, asset: null)));

    [Fact]
    public void ManifestProblems()
    {
        using var tmp = new TempDir();
        Assert.Contains("no plugin ABI", PluginUpdateService.ManifestProblem(new PluginManifest { Name = "pluto-gnss" }, tmp.Path));
        Assert.Contains("entryDll", PluginUpdateService.ManifestProblem(new PluginManifest { Abi = ContractsAbi.Version }, tmp.Path));
        var m = new PluginManifest { Abi = ContractsAbi.Version, EntryDll = "ASD.PlutoGnss.dll" };
        Assert.Contains("missing its adapter", PluginUpdateService.ManifestProblem(m, tmp.Path));
        File.WriteAllBytes(tmp.File("ASD.PlutoGnss.dll"), new byte[] { 0 });
        Assert.Null(PluginUpdateService.ManifestProblem(m, tmp.Path));
        Assert.NotNull(PluginUpdateService.ManifestProblem(null, tmp.Path));
    }
}
