using VesperApp.Models;
using Xunit;

namespace VesperApp.Tests.Models;

/// <summary>Config editor labels carry the unit the logger firmware uses for Sample Rate (checked against
/// VesperU5 / KOL / Nanotag 2026-11-04): a period in ms or a rate in Hz, per sensor.</summary>
public class EditorLabelTests
{
    [Theory]
    [InlineData(typeof(ConfigALS3001DDriver), "Sample Period [1] (ms)")]
    [InlineData(typeof(ConfigTPRH31Driver), "Sample Period [1] (ms)")]
    [InlineData(typeof(ConfigADS1115Driver), "Sample Period [1] (ms)")]
    [InlineData(typeof(ConfigLeptonDriver), "Sample Period [1] (ms)")]
    [InlineData(typeof(ConfigACLYSDriver), "Sample Period [1] (ms)")]
    [InlineData(typeof(ConfigLEDDriver), "Sample Period [1] (ms)")]
    [InlineData(typeof(ConfigProxtitDriver), "Sample Period [1] (ms)")]
    [InlineData(typeof(ConfigIMU10Driver), "Sample Rate [1] (Hz)")]
    [InlineData(typeof(ConfigNanotagAcc), "Sample Rate [1] (Hz)")]
    public void SampleRate_ShowsTheFirmwareUnit(System.Type t, string name)
    {
        var d = (ConfigurationDeviceDriver)System.Activator.CreateInstance(t)!;
        var l = d.EditorLabel(nameof(ConfigurationDeviceDriver.SampleRate1));
        Assert.NotNull(l);
        Assert.Equal(name, l!.Value.Name);
        Assert.StartsWith("Config 1: ", l.Value.Description);
        Assert.Equal(name.Replace("[1]", "[2]"), d.EditorLabel(nameof(ConfigurationDeviceDriver.SampleRate2))!.Value.Name);
    }

    [Fact]
    public void Led_WindowLengthIsTheFlashLength_AndGpsIgnoresFileSize()
    {
        Assert.Equal("Flash Length [2] (ms)", new ConfigLEDDriver().EditorLabel(nameof(ConfigurationDeviceDriver.WindowLength2))!.Value.Name);
        Assert.StartsWith("Not used", new ConfigACLYSDriver().EditorLabel(nameof(ConfigurationDeviceDriver.FileSize))!.Value.Description);
        Assert.Null(new ConfigIMU10HTDriver().EditorLabel(nameof(ConfigurationDeviceDriver.SampleRate1)));   // own "(Hz)" attribute
    }
}
