using VesperApp.Models;
using VesperApp.Services;
using Xunit;

namespace VesperApp.Tests.Models;

/// <summary>
/// EXG48 sample rate in the configuration editor: config.json "sampleRate" is the ADS129x DR code (VesperU5 writes it
/// as is; 0 = off), read with the HR bit (eegconf1 bit 0). Before 2026-11-20 the editor wrote the list position: "8 kSPS"
/// = 0 turned the ExG off, the others ran 2x / 4x the rate shown. 16 / 32 kSPS are never offered.
/// </summary>
public class Exg48RateOptionsTests
{
    [Theory]
    [InlineData(8000u, true, 2u)]
    [InlineData(1000u, true, 5u)]
    [InlineData(500u, true, 6u)]
    [InlineData(250u, true, 6u)]          // no 250 SPS in high resolution: 500
    [InlineData(8000u, false, 1u)]
    [InlineData(500u, false, 5u)]
    [InlineData(250u, false, 6u)]
    [InlineData(32000u, true, 2u)]        // nothing above 8 kSPS
    [InlineData(0u, true, 0u)]            // off
    public void CodeFor_TheRateInTheMode(uint sps, bool hr, uint code)
    {
        Assert.Equal(code, EXGSampleRateOptions.CodeFor(sps, hr));
        if (code != 0 && sps <= 8000 && (hr ? sps >= 500 : sps >= 250))
            Assert.Equal(sps, EXGSampleRateOptions.SpsFor(code, hr));
    }

    [Fact]
    public void TheList_HasNo16Or32Kilo_AndSelectionsWriteRealCodes()
    {
        Assert.DoesNotContain(EXGSampleRateOptions.ListOfOptions, o => o.Value > 8000);
        Assert.Equal("Off", EXGSampleRateOptions.ListOfOptions[0].ToString());
        Assert.Equal("8 kSPS", EXGSampleRateOptions.ListOfOptions[1].ToString());

        var d = new ConfigEXG48Driver { EnableHR = false };
        d.SampleRate_1 = EXGSampleRateOptions.CreateFromValue(8000);
        Assert.Equal(1u, d.SampleRate1);                     // was 0: the ExG did not start
        Assert.Equal(8000.0, ExgRate.Exg48SamplesPerSecond(d.SampleRate1, false));
        d.SampleRate_2 = EXGSampleRateOptions.CreateFromValue(0);
        Assert.Equal(0u, d.SampleRate2);
        Assert.Equal("Off", d.SampleRate_2.ToString());
    }

    [Fact]
    public void ModeChange_KeepsTheRates()
    {
        var d = new ConfigEXG48Driver { EnableHR = false };
        d.SampleRate_1 = EXGSampleRateOptions.CreateFromValue(1000);
        Assert.Equal(4u, d.SampleRate1);
        d.EnableHR = true;
        Assert.Equal(5u, d.SampleRate1);
        Assert.Equal("1 kSPS", d.SampleRate_1.ToString());
        Assert.Equal(0u, d.SampleRate2);                     // off stays off
        d.EnableHR = false;
        Assert.Equal(4u, d.SampleRate1);
    }

    [Theory]
    [InlineData(5u, true, "1 kSPS")]      // a hand-written bench config (code 5, high resolution)
    [InlineData(5u, false, "500 SPS")]
    [InlineData(6u, false, "250 SPS")]    // was shown as "500 SPS"
    [InlineData(0u, false, "Off")]
    public void LoadedCodes_ShowTheirRate(uint code, bool hr, string label)
    {
        Assert.Equal(label, EXGSampleRateOptions.FromCode(code, hr).ToString());
    }
}
