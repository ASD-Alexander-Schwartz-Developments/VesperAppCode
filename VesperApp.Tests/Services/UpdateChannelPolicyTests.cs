using VesperApp.Services;
using Xunit;

namespace VesperApp.Tests.Services;

public class UpdateChannelPolicyTests
{
    [Theory]
    [InlineData("win-x64-beta", "win-x64-beta")]
    [InlineData("win-x86-stable", "win-x86-stable")]
    [InlineData("linux-x64-beta", "linux-x64-beta")]
    public void NormalCheck_FollowsInstalledChannel_NoDowngrade(string installed, string expected)
    {
        var d = UpdateChannelPolicy.Decide("win-x64", installed);
        Assert.Equal(expected, d.Channel);
        Assert.False(d.AllowDowngrade);
        Assert.False(d.IsRingSwitch);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void DevBuild_FallsBackToStable(string? installed)
    {
        var d = UpdateChannelPolicy.Decide("win-x86", installed);
        Assert.Equal("win-x86-stable", d.Channel);
        Assert.False(d.AllowDowngrade);
    }

    [Fact]
    public void LeaveBeta_IsExplicitSwitch_AllowsDowngrade()
    {
        var d = UpdateChannelPolicy.Decide("win-x64", "win-x64-beta", UpdateChannelPolicy.StableRing);
        Assert.Equal("win-x64-stable", d.Channel);
        Assert.True(d.AllowDowngrade);
        Assert.True(d.IsRingSwitch);
    }

    [Fact]
    public void RequestingTheInstalledRing_IsNotASwitch()
    {
        var d = UpdateChannelPolicy.Decide("win-x64", "win-x64-stable", "stable");
        Assert.False(d.AllowDowngrade);
        Assert.False(d.IsRingSwitch);
    }

    [Theory]
    [InlineData("win-x64-beta", "beta")]
    [InlineData("win-x64-BETA", "beta")]
    [InlineData("win-x64-stable", "stable")]
    [InlineData(null, "stable")]
    public void RingOf(string? channel, string ring) => Assert.Equal(ring, UpdateChannelPolicy.RingOf(channel));

    [Fact]
    public void BetaInstall_IsNeverOfferedOlderStable_OnANormalCheck()
    {
        // The v1.2.0-beta bug: a beta install was offered 1.1.0 stable as an "update".
        var normal = UpdateChannelPolicy.Decide("win-x64", "win-x64-beta");
        Assert.False(UpdateChannelPolicy.ShouldOffer(V("1.2.0"), V("1.1.0"), normal));
        Assert.False(UpdateChannelPolicy.ShouldOffer(V("1.2.0"), V("1.2.0"), normal));
        Assert.True(UpdateChannelPolicy.ShouldOffer(V("1.2.0"), V("1.2.1"), normal));
        Assert.False(UpdateChannelPolicy.ShouldOffer(V("1.2.0"), null, normal));

        var leave = UpdateChannelPolicy.Decide("win-x64", "win-x64-beta", "stable");
        Assert.True(UpdateChannelPolicy.ShouldOffer(V("1.2.0"), V("1.1.0"), leave));
    }

    private static System.Version V(string s) => System.Version.Parse(s);
}
