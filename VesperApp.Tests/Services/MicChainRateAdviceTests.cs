using VesperApp.Models;
using VesperApp.Services;
using Xunit;

namespace VesperApp.Tests.Services;

/// <summary>The Solve window points out rates without the reshape (anti-aliasing) filter and suggests one that has it
/// (asd-interfaces projects/u585-bench-2026-11-08.md: 4.8 MHz / D needs D = 4 x CIC for the reshape filter).</summary>
public class MicChainRateAdviceTests
{
    [Theory]
    [InlineData(96000u, 100000u)]
    [InlineData(192000u, 200000u)]
    [InlineData(32000u, 48000u)]
    [InlineData(250000u, 300000u)]
    [InlineData(44100u, 48000u)]
    [InlineData(400000u, 300000u)]   // nothing higher has the full filter: the closest lower rate
    public void RecommendedRate_IsTheNextExactRateWithTheFullFilter(uint fs, uint want)
    {
        Assert.Equal(want, MicChainSolver.RecommendedRate(fs));
        Assert.True(MicChainSolver.Solve(want, 0) is { Reshape: true, Inexact: false });
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(48000u)]
    [InlineData(100000u)]
    [InlineData(200000u)]
    public void RecommendedRate_NoneWhenTheFullFilterIsUsed(uint fs) => Assert.Null(MicChainSolver.RecommendedRate(fs));

    [Theory]
    [InlineData(96000u, 0.40, -15, -13)]
    [InlineData(96000u, 0.45, -8, -6)]
    [InlineData(192000u, 0.45, -10, -8)]
    public void AliasRejection_MatchesTheCicImages(uint fs, double fraction, double lo, double hi)
    {
        var c = MicChainSolver.Solve(fs, 0)!;
        Assert.InRange(MicChainSolver.AliasRejectionDb(c, fraction), lo, hi);
    }

    [Fact]
    public void Report_WarnsAndRecommends_For96k()
    {
        var r = MicChainSolver.BuildReport(96000, 0, 9, MicChainSolver.Product.VesperPipistrelle, hpf: 4);
        string text = r.ToString();
        Assert.Contains("No anti-aliasing filter at this rate", text);
        Assert.Contains("Use 100 kHz instead", text);
        Assert.NotNull(r.RateRecommendation);
        Assert.Contains("100 kHz instead of 96 kHz", r.RateRecommendation);
    }

    [Fact]
    public void Report_NoRateAdvice_For100k()
    {
        var r = MicChainSolver.BuildReport(100000, 0, 9, MicChainSolver.Product.VesperPipistrelle, hpf: 4);
        Assert.Null(r.RateRecommendation);
        Assert.Contains("Full anti-aliasing filter", r.ToString());
    }

    [Fact]
    public void ApplyRecommendedRate_MovesBothRatesAndKeepsTheLoudness()
    {
        var d = new ConfigSPH0641Driver();
        d.Rate1 = KOLSampleRate.CreateFromValue(96000);
        d.Rate2 = KOLSampleRate.CreateFromValue(192000);
        d.GAIN = MicChainSolver.GainValue(MicChainSolver.Recommended(96000));   // a realistic 96 kHz gain (19 would clamp at 100 kHz)
        int before = MicChainSolver.LevelCdb(96000, MicChainSolver.GainCode(d.GAIN));

        ((IMicChainSolvable)d).ApplyRecommendedRate();

        Assert.Equal(100000u, d.SampleRate[1]);
        Assert.Equal(200000u, d.SampleRate[2]);
        int after = MicChainSolver.LevelCdb(100000, MicChainSolver.GainCode(d.GAIN));
        Assert.InRange(after - before, -176, 176);              // within half a SCALE step
        Assert.Null(d.MicChainReport().RateRecommendation);
    }
}
