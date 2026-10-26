using VesperApp.Models;
using VesperApp.Services.Time;
using Xunit;

namespace VesperApp.Tests.Services;

/// <summary>Per-unit clock history (<see cref="RtcCalibrationStore"/>), the crystal-error estimate over it, and the
/// sync / apply / clear flows of <see cref="RtcSyncService"/> against a simulated unit.</summary>
public class RtcCalibrationTests
{
    private static readonly DateTime T0 = new(2026, 10, 20, 8, 0, 0, DateTimeKind.Utc);

    private static OffsetMeasurement M(double hours, double offset, double? temp = 24.0, int applied = 0, double zone = 0) =>
        new(T0.AddHours(hours), offset, zone, 0.0008, 0.0003, 30, temp, applied, RtcCalFlags.UnitCalibration);

    // ---- store ----

    [Fact]
    public void Store_RecordsPoints_AfterSyncStartsANewSegment()
    {
        using var dir = new TempDir();
        var store = new RtcCalibrationStore(dir.File("rtc.json"));
        var rc = ReferenceClock.ForTest(0, 0.002);

        SyncPoint a = store.Record("47625051", M(0, 0.250), SyncPointKind.Measured, rc);
        SyncPoint b = store.Record("47625051", M(0.01, 0.0004), SyncPointKind.AfterSync, rc);
        SyncPoint c = store.Record("47625051", M(24, 0.0170), SyncPointKind.Measured, rc);

        Assert.Equal(new[] { 0, 1, 1 }, new[] { a.Segment, b.Segment, c.Segment });
        Assert.Equal(0.8, a.MinDelayMs, 9);
        Assert.Equal(0.3, a.SpreadMs, 9);
        Assert.Equal(2.0, a.ReferenceUncertaintyMs, 9);
        Assert.Equal("NTP test", a.Reference);
        Assert.Equal(SyncPointKind.AfterSync, b.Kind);
    }

    [Fact]
    public void Store_PersistsAndReloads_KeyIsCaseInsensitive()
    {
        using var dir = new TempDir();
        string path = dir.File("sub/rtc.json");                      // directory created on the first save
        var store = new RtcCalibrationStore(path);
        store.Record("4762505a", M(0, 0.1), SyncPointKind.Measured, ReferenceClock.ForTest(0, 0.001));
        store.SetCalibration("4762505A", new StoredCalibration { ErrorPpb = -203731, ReferenceC = 23.5, SpanHours = 20 });

        var again = new RtcCalibrationStore(path);
        UnitClockRecord r = again.Get("4762505A");
        Assert.Single(r.Points);
        Assert.Equal(1, r.Segment);                                    // SetCalibration starts a segment
        Assert.Equal(-203731, r.Calibration!.ErrorPpb);
        Assert.Equal(23.5, r.Calibration.ReferenceC);
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public void Store_ClearingTheCalibration_AlsoStartsASegment()
    {
        using var dir = new TempDir();
        var store = new RtcCalibrationStore(dir.File("rtc.json"));
        store.SetCalibration("U", new StoredCalibration { ErrorPpb = 5 });
        store.SetCalibration("U", null);
        Assert.Null(store.Get("U").Calibration);
        Assert.Equal(2, store.Get("U").Segment);
    }

    [Fact]
    public void Store_KeepsTheNewest500Points()
    {
        using var dir = new TempDir();
        var store = new RtcCalibrationStore(dir.File("rtc.json"));
        var rc = ReferenceClock.ForTest(0, 0.001);
        for (int i = 0; i < 503; i++) store.Record("U", M(i, 0), SyncPointKind.Measured, rc);

        List<SyncPoint> pts = new RtcCalibrationStore(dir.File("rtc.json")).Get("U").Points;
        Assert.Equal(500, pts.Count);
        Assert.Equal(T0.AddHours(3), pts[0].TrueUtc);
        Assert.Equal(T0.AddHours(502), pts[^1].TrueUtc);
    }

    [Fact]
    public void Store_DamagedFile_StartsEmpty_AndKeepsABadCopy()
    {
        using var dir = new TempDir();
        string path = dir.File("rtc.json");
        File.WriteAllText(path, "[{\"UnitKey\": broken");
        var store = new RtcCalibrationStore(path);
        Assert.Empty(store.Get("U").Points);
        Assert.Equal("[{\"UnitKey\": broken", File.ReadAllText(path + ".bad"));
    }

    // ---- estimate ----

    private static SyncPoint P(double hours, double offsetSeconds, int segment, int applied = 0, double? temp = 25.0,
                               string reference = "NTP x", double zone = 0) => new()
    {
        TrueUtc = T0.AddHours(hours), OffsetSeconds = offsetSeconds, ZoneOffsetHours = zone, SpreadMs = 0.3,
        ReferenceUncertaintyMs = 2, TemperatureC = temp, AppliedPpb = applied, Segment = segment, Reference = reference,
    };

    private static UnitClockRecord Rec(params SyncPoint[] pts) => new() { UnitKey = "U", Points = pts.ToList() };

    /// <summary>Points over <paramref name="hours"/> of a clock running <paramref name="ppb"/> fast.</summary>
    private static IEnumerable<SyncPoint> Line(double fromHours, double hours, double ppb, int segment, int applied = 0) =>
        Enumerable.Range(0, 5).Select(i => P(fromHours + hours * i / 4, hours * 3600 * i / 4 * ppb * 1e-9, segment, applied));

    [Fact]
    public void Estimate_NewestSegmentWithOnePoint_FallsBackToTheOlderOne()
    {
        var rec = Rec(Line(0, 20, 1500, segment: 1).Append(P(30, 0.0, segment: 2)).ToArray());
        CalibrationEstimate e = RtcSyncService.Estimate(rec)!;
        Assert.Equal(1, e.Segment);
        Assert.Equal(1500, e.ErrorPpb);
    }

    [Fact]
    public void Estimate_ImplausibleNewestSegment_FallsBackToTheOlderOne()
    {
        /* segment 2: the calendar was re-initialised inside (several seconds in two hours) */
        var rec = Rec(Line(0, 20, -800, segment: 1).Concat(Line(30, 2, 5_000_000, segment: 2)).ToArray());
        Assert.Equal(1, RtcSyncService.Estimate(rec)!.Segment);
        Assert.Equal(-800, RtcSyncService.Estimate(rec)!.ErrorPpb);
    }

    [Fact]
    public void Estimate_MinHoursIsHonoured()
    {
        var rec = Rec(Line(0, 2, 100, segment: 1).ToArray());
        Assert.NotNull(RtcSyncService.Estimate(rec, minHours: 2));
        Assert.Null(RtcSyncService.Estimate(rec, minHours: 2.5));
    }

    [Fact]
    public void Estimate_UncertaintyFromWorstPointAndSpan()
    {
        /* per point sqrt(0.5 ms² + 2 ms²) (spread below 0.5 ms counts as the 1/1024 s stamp step), √2 over 24 h */
        CalibrationEstimate e = RtcSyncService.Estimate(Rec(Line(0, 24, 0, segment: 1).ToArray()))!;
        double expected = Math.Sqrt(2) * Math.Sqrt(0.0005 * 0.0005 + 0.002 * 0.002) / 86400 * 1e9;
        Assert.Equal(expected, e.UncertaintyPpb, 6);
    }

    [Fact]
    public void Estimate_DaylightSavingChangeInsideTheSegment_IsNotDrift()
    {
        /* device in local time: when the zone falls from +2 h to +1 h, device-minus-local-time jumps by +3600 s */
        var rec = Rec(P(0, 0.010, 1, zone: 2), P(6, 0.010, 1, zone: 2), P(12, 3600.010, 1, zone: 1), P(24, 3600.010, 1, zone: 1));
        CalibrationEstimate e = RtcSyncService.Estimate(rec)!;
        Assert.Equal(0, e.ErrorPpb);
    }

    [Fact]
    public void Estimate_Warnings()
    {
        var pts = new[]
        {
            P(0, 0, 1, applied: -205000, temp: 22.0, reference: "PC clock (not NTP-verified)"),
            P(13, 0, 1, applied: -201000, temp: 27.5),
        };
        CalibrationEstimate e = RtcSyncService.Estimate(Rec(pts))!;

        Assert.Equal(-203000, e.AppliedAveragePpb);
        Assert.Equal(203000, e.ErrorPpb);                              // flat offset: rate 0 minus the average applied
        Assert.Equal(24.75, e.ReferenceC);
        Assert.Equal("the applied correction changed during the interval (temperature compensation); the average was used; " +
                     "temperature varied by 5.5 °C; calibrate at a stable temperature; " +
                     "some points used the bare PC clock as reference", e.Warning);
    }

    [Fact]
    public void Estimate_NoTemperatures_HasNoReference()
    {
        var rec = Rec(P(0, 0, 1, temp: null), P(13, 0.0001, 1, temp: null));
        Assert.Null(RtcSyncService.Estimate(rec)!.ReferenceC);
    }

    [Fact]
    public void Estimate_EmptyHistory_IsNull() => Assert.Null(RtcSyncService.Estimate(new UnitClockRecord()));

    // ---- flows on a simulated unit ----

    private static CalibrationEstimate Est => new(-203731, 23.5, 20, 30, 7, -205000, 1, null);

    [Fact]
    public async Task Apply_StoresTheMasterCopy_OnlyWhenTheUnitTookIt()
    {
        using var dir = new TempDir();
        var store = new RtcCalibrationStore(dir.File("rtc.json"));
        var dev = new FakeRtcDevice(ReferenceClock.ForTest(0, 0.001)) { CalReply = _ => FakeRtcDevice.Status(0) };

        await RtcSyncService.ApplyAsync(dev, Est, store);
        Assert.Null(store.Get("SIM").Calibration);                       // reply without UnitCalibration

        dev.CalReply = _ => FakeRtcDevice.Status(RtcCalFlags.UnitCalibration, -203731, 23.5);
        RtcCalStatus st = (await RtcSyncService.ApplyAsync(dev, Est, store))!;

        Assert.True(st.HasUnitCalibration);
        Assert.Equal(RtcCalStatus.PackSet(-203731, 23.5), dev.CalPayloads[^1]);
        StoredCalibration cal = store.Get("SIM").Calibration!;
        Assert.Equal(-203731, cal.ErrorPpb);
        Assert.Equal(23.5, cal.ReferenceC);
        Assert.Equal(20, cal.SpanHours);
        Assert.Equal(30, cal.UncertaintyPpb);
    }

    [Fact]
    public async Task Clear_ForgetsTheMasterCopy_OnlyWhenTheUnitAnswered()
    {
        using var dir = new TempDir();
        var store = new RtcCalibrationStore(dir.File("rtc.json"));
        store.SetCalibration("SIM", new StoredCalibration { ErrorPpb = -1 });
        var dev = new FakeRtcDevice(ReferenceClock.ForTest(0, 0.001));

        Assert.Null(await RtcSyncService.ClearAsync(dev, store));
        Assert.NotNull(store.Get("SIM").Calibration);

        dev.CalReply = _ => FakeRtcDevice.Status(0);
        Assert.NotNull(await RtcSyncService.ClearAsync(dev, store));
        Assert.Null(store.Get("SIM").Calibration);
        Assert.Equal(new byte[] { 2 }, dev.CalPayloads[^1]);
    }

    [Fact]
    public async Task Sync_LargeOffset_SetsOnTheSecond_ThenShifts()
    {
        using var dir = new TempDir();
        var store = new RtcCalibrationStore(dir.File("rtc.json"));
        var rc = ReferenceClock.ForTest(0, 0.001);
        var dev = new FakeRtcDevice(rc) { OffsetSeconds = 2.3 };

        SyncResult r = await RtcSyncService.SyncAsync(dev, rc, deviceUtc: true, store);

        Assert.True(r.Ok, r.Message);
        Assert.Equal(2.3, r.Before!.OffsetSeconds, 3);
        Assert.InRange(Math.Abs(r.After!.OffsetSeconds), 0, 0.005);
        Assert.Equal("set", dev.Calls[0]);
        Assert.StartsWith("Clock synchronised: was +2300.0 ms", r.Message);

        UnitClockRecord rec = store.Get("SIM");
        Assert.Equal(new[] { SyncPointKind.Measured, SyncPointKind.AfterSync }, rec.Points.Select(p => p.Kind));
        Assert.Equal(new[] { 0, 1 }, rec.Points.Select(p => p.Segment));
        Assert.DoesNotContain(dev.Calls, c => c.StartsWith("cal"));       // the unit kept its calibration
    }

    [Fact]
    public async Task Sync_SmallOffset_OnlyShifts()
    {
        var rc = ReferenceClock.ForTest(0, 0.001);
        var dev = new FakeRtcDevice(rc) { OffsetSeconds = -0.040 };
        SyncResult r = await RtcSyncService.SyncAsync(dev, rc, true, store: null);
        Assert.True(r.Ok, r.Message);
        Assert.DoesNotContain("set", dev.Calls);
        Assert.StartsWith("shift ", dev.Calls[0]);
        Assert.InRange(int.Parse(dev.Calls[0].Substring(6)), 39000, 41000);
    }

    [Fact]
    public async Task Sync_UnitLostItsCalibration_StoredCopyIsReapplied()
    {
        using var dir = new TempDir();
        var store = new RtcCalibrationStore(dir.File("rtc.json"));
        store.SetCalibration("SIM", new StoredCalibration { ErrorPpb = -203731, ReferenceC = 23.5 });
        var rc = ReferenceClock.ForTest(0, 0.001);
        var dev = new FakeRtcDevice(rc) { ExchangeFlags = 0, CalReply = _ => FakeRtcDevice.Status(RtcCalFlags.UnitCalibration) };

        await RtcSyncService.SyncAsync(dev, rc, true, store);

        Assert.Equal(RtcCalStatus.PackSet(-203731, 23.5), Assert.Single(dev.CalPayloads));
    }

    [Fact]
    public async Task Sync_NoAnswer_Fails()
    {
        var rc = ReferenceClock.ForTest(0, 0.001);
        SyncResult r = await RtcSyncService.SyncAsync(new FakeRtcDevice(rc) { Answers = false }, rc, true, null);
        Assert.False(r.Ok);
        Assert.Null(r.Before);
        Assert.Contains("does not answer", r.Message);
    }

    [Fact]
    public async Task Sync_Nanotag_SetsWholeSeconds_AndKeepsTheSubSecondOffset()
    {
        var rc = ReferenceClock.ForTest(0, 0.001);
        var dev = new FakeRtcDevice(rc) { OffsetSeconds = 3.2, CanShiftValue = false };

        SyncResult r = await RtcSyncService.SyncAsync(dev, rc, true, null);

        Assert.True(r.Ok, r.Message);
        Assert.Equal(new[] { "set" }, dev.Calls);
        Assert.Equal(0.2, r.After!.OffsetSeconds, 2);
        Assert.StartsWith("Clock set to the whole second", r.Message);
    }

    [Theory]
    [InlineData(1000.3, 3.2)]
    [InlineData(1000.9, -2.6)]
    [InlineData(1000.0, 0.49)]
    public void PlanWholeSecondSet_ArrivesMidSecond_ResidualWithinHalfASecond(double now, double offset)
    {
        var plan = RtcSyncService.PlanWholeSecondSet(now, offset);
        Assert.Equal(0.5, plan.ArriveDeviceSeconds - Math.Floor(plan.ArriveDeviceSeconds), 9);
        Assert.True(plan.ArriveDeviceSeconds >= now + offset + 0.25 + 0.5 - 1e-9);
        Assert.InRange(plan.Residual, -0.5, 0.5);
        /* the value written is the device's own second minus the whole-second part of the offset */
        Assert.Equal(Math.Floor(plan.ArriveDeviceSeconds) - Math.Round(offset, MidpointRounding.AwayFromZero), plan.SetSeconds, 9);
    }
}
