using VesperApp.Services;
using Xunit;

namespace VesperApp.Tests.Services;

/// <summary>KOL / Vesper microphone chain (mirror of firmware mic_chain.c): PDM 4.8 MHz / D, reshape /4 preferred.</summary>
public class MicChainSolverTests
{
    [Theory]
    // rate,  total, cic, reshape, order, inexact
    [InlineData(48000u, 100, 25, true, 5, false)]
    [InlineData(8000u, 600, 150, true, 3, false)]
    [InlineData(96000u, 50, 50, false, 4, false)]        // 50 is not a multiple of 4: CIC only
    [InlineData(192000u, 25, 25, false, 5, false)]
    [InlineData(200000u, 24, 6, true, 5, false)]
    [InlineData(300000u, 16, 4, true, 5, false)]         // reshape reaches full scale with the measured x3.01 gain
    [InlineData(400000u, 12, 12, false, 5, false)]       // CIC 3 + reshape cannot reach full scale: CIC 12 only
    [InlineData(44100u, 109, 109, false, 3, true)]       // closest divider, 44036.7 Hz
    [InlineData(250000u, 19, 19, false, 5, true)]
    public void Solve_PicksDividerAndFilter(uint fs, int total, int cic, bool reshape, int order, bool inexact)
    {
        MicChainSolver.Chain c = MicChainSolver.Solve(fs, 0)!;
        Assert.Equal(total, c.Total);
        Assert.Equal(cic, c.Cic);
        Assert.Equal(reshape, c.Reshape);
        Assert.Equal(order, c.Order);
        Assert.Equal(inexact, c.Inexact);
        Assert.Equal(4_800_000.0 / total, c.ExactHz, 6);
        Assert.Equal(reshape ? 4 * cic : cic, c.Total);
    }

    [Fact]
    public void Solve_ZeroRate_IsNull() => Assert.Null(MicChainSolver.Solve(0, 0));

    [Fact]
    public void Solve_GainCodeBringsLevelClosestToTarget()
    {
        foreach (uint fs in new uint[] { 16000, 48000, 96000, 200000, 384000 })
        {
            MicChainSolver.Chain c = MicChainSolver.Solve(fs, 600)!;
            Assert.Equal(c.BaseCdb + MicChainSolver.CodeCdb(c.GainCode), c.LevelCdb);
            if (!c.GainClamped)
                Assert.True(Math.Abs(c.LevelCdb - 600) <= 176, $"{fs}: level {c.LevelCdb}");   // half the largest SCALE step
        }
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 352)]
    [InlineData(2, 602)]
    [InlineData(3, 954)]
    [InlineData(24, 7224)]
    [InlineData(-1, -250)]
    [InlineData(-2, -602)]
    [InlineData(-16, -4816)]
    public void CodeCdb_AlternatesSteps(int code, int cdb) => Assert.Equal(cdb, MicChainSolver.CodeCdb(code));

    [Theory]
    [InlineData((ushort)0, 0)]
    [InlineData((ushort)19, 19)]
    [InlineData((ushort)30, 24)]          // clamped
    [InlineData((ushort)0x2F, -1)]        // config: SCALE field 0x20..0x2F = negative
    [InlineData((ushort)0x20, -16)]
    [InlineData((ushort)0xFF, -1)]        // console: int8
    [InlineData((ushort)0xF0, -16)]
    public void GainCode_DecodesConfigAndConsoleEncodings(ushort cfg, int code) => Assert.Equal(code, MicChainSolver.GainCode(cfg));

    [Fact]
    public void GainValue_RoundTripsEveryCode()
    {
        for (int code = MicChainSolver.CodeMin; code <= MicChainSolver.CodeMax; code++)
            Assert.Equal(code, MicChainSolver.GainCode(MicChainSolver.GainValue(code)));
        Assert.Equal((ushort)47, MicChainSolver.GainValue(-1));
        Assert.Equal((ushort)24, MicChainSolver.GainValue(99));
    }

    [Theory]
    [InlineData(48000u, (ushort)0x8100)]
    [InlineData(0u, (ushort)0x8100)]      // off: written for 48 kHz
    [InlineData(400000u, (ushort)0x8101)] // reshape bypassed
    [InlineData(96000u, (ushort)0x8101)]
    public void FilterValue_AutoBitPlusReshapeBits(uint fs, ushort expected) => Assert.Equal(expected, MicChainSolver.FilterValue(fs));

    [Theory]
    [InlineData(96000u, 48000u, 96000u)]
    [InlineData(0u, 48000u, 48000u)]
    [InlineData(0u, 0u, 0u)]
    public void RefRate_FirstConfiguredRate(uint r1, uint r2, uint expected) => Assert.Equal(expected, MicChainSolver.RefRate(r1, r2, 0));

    [Theory]
    [InlineData(0u, 0u)]
    [InlineData(45000u, 44100u)]
    [InlineData(195000u, 192000u)]
    [InlineData(1000000u, 400000u)]
    public void NearestListed(uint fs, uint expected) => Assert.Equal(expected, MicChainSolver.NearestListed(fs));

    [Theory]
    [InlineData(200000u, 4)]              // ultrasonic: highest cut-off <= 2 kHz (0.0095 * 200 kHz = 1.9 kHz)
    [InlineData(384000u, 3)]              // 0.0095 * 384 kHz = 3.6 kHz is too high
    [InlineData(48000u, 1)]               // audio: <= 50 Hz
    [InlineData(0u, 1)]
    public void RecommendedHpf(uint fs, int expected) => Assert.Equal(expected, MicChainSolver.RecommendedHpf(fs));

    [Fact]
    public void HpfCutoff()
    {
        Assert.Equal(500.0, MicChainSolver.HpfCutoffHz(3, 200000), 6);
        Assert.Equal(0.0, MicChainSolver.HpfCutoffHz(0, 200000));
        Assert.Equal(0.0, MicChainSolver.HpfCutoffHz(9, 200000));
    }

    [Theory]
    [InlineData(48000.0, "48 kHz")]
    [InlineData(44100.0, "44.1 kHz")]
    [InlineData(500.0, "500 Hz")]
    public void Hz_Formats(double hz, string expected) => Assert.Equal(expected, MicChainSolver.Hz(hz));

    [Fact]
    public void MigrateLegacy_AutoConfig_KeepsGainAndMovesOffListRates()
    {
        var m = MicChainSolver.MigrateLegacy(45000, 0, MicChainSolver.AutoBit, false, 10);
        Assert.Equal((ushort)10, m.Gain);
        Assert.Equal(44100u, m.Rate1);
        Assert.Equal(0u, m.Rate2);
        Assert.Contains("not offered any more", m.Note);
    }

    [Fact]
    public void MigrateLegacy_OldFilter_KeepsTheOutputLevel()
    {
        // pre-auto config at 48 kHz, reshape on, SINC5, gain 6: the automatic chain is the same filter -> same level
        var legacy = MicChainSolver.SolveLegacy(48000, 0x100, false, 6)!;
        var m = MicChainSolver.MigrateLegacy(48000, 0, 0x100, false, 6);
        var auto = MicChainSolver.Solve(48000, legacy.LevelCdb)!;
        Assert.Equal(MicChainSolver.GainValue(auto.GainCode), m.Gain);
        Assert.Equal(legacy.Total, auto.Total);
        Assert.Equal((ushort)6, m.Gain);
        Assert.Null(m.Note);
    }

    [Fact]
    public void Recommended_PutsFullScaleNearPlus6Dbfs()
    {
        foreach (uint fs in new uint[] { 48000, 200000, 384000 })
        {
            var c = MicChainSolver.Solve(fs, 600)!;
            Assert.Equal(c.GainCode, MicChainSolver.Recommended(fs));
        }
        Assert.Equal(0, MicChainSolver.Recommended(0));
    }

    [Fact]
    public void BuildReport_BothOff_HasNoSections()
    {
        var r = MicChainSolver.BuildReport(0, 0, 0, MicChainSolver.Product.Kol);
        Assert.Empty(r.Sections);
        Assert.Contains("Off", r.Summary);
    }

    [Fact]
    public void BuildReport_TwoRates_OneSectionEachPlusHpf()
    {
        var r = MicChainSolver.BuildReport(200000, 48000, 10, MicChainSolver.Product.Kol, hpf: 0);
        Assert.Equal(3, r.Sections.Count);
        Assert.StartsWith("Sample Rate [1] — 200 kHz", r.Sections[0].Title);
        Assert.StartsWith("Sample Rate [2] — 48 kHz", r.Sections[1].Title);
        Assert.NotNull(r.HpfRecommendation);                      // off is never recommended
        Assert.Equal(2, r.Response!.Count);
    }
}
