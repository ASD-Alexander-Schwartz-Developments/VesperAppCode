using System.Diagnostics;
using VesperApp.Models;
using VesperApp.Services.Time;
using Xunit;

namespace VesperApp.Tests.Services;

/// <summary>RTC sync / calibration maths (asd-interfaces interfaces/rtc-time-sync.md) without hardware.</summary>
public class TimeSyncTests
{
    /// <summary>Simulated logger: its calendar runs <see cref="OffsetSeconds"/> ahead of true time (device in UTC);
    /// each exchange takes <see cref="OneWay"/> each direction and <see cref="Processing"/> inside the device.</summary>
    private sealed class FakeRtc : IRtcDevice
    {
        private readonly ReferenceClock _rc;
        public double OffsetSeconds;
        public double OneWay = 0.0004, Processing = 0.0002;
        public double ExtraDelayEvery3rd;                         // asymmetric outliers the filter must drop
        private int _n;

        public FakeRtc(ReferenceClock rc) => _rc = rc;
        public string UnitKey => "SIM";

        private static long Ticks(double s) => (long)(s * Stopwatch.Frequency);

        public Task<RtcExchange?> TimeSyncAsync(uint seq, int timeoutMs = 1000)
        {
            long t1 = Stopwatch.GetTimestamp();
            double extra = (++_n % 3 == 0) ? ExtraDelayEvery3rd : 0;          // only on the way in: biases offset
            double trueRx = RtcSyncService.DeviceSecondsOfTrue(_rc.TrueUtcAt(t1 + Ticks(OneWay + extra)), true);
            long t4 = t1 + Ticks(2 * OneWay + extra + Processing);
            double rx = trueRx + OffsetSeconds;
            return Task.FromResult<RtcExchange?>(new RtcExchange(seq, t1, t4, rx, rx + Processing, 24.5, 3, -205000, RtcCalFlags.UnitCalibration));
        }

        public Task<bool> SetRtcAsync(DateTime deviceTime, int timeoutMs = 2000) => Task.FromResult(true);
        public Task<bool> ShiftAsync(int microseconds, int timeoutMs = 2000) => Task.FromResult(true);
        public Task<RtcCalStatus?> CalAsync(byte[] payload, int timeoutMs = 2000) => Task.FromResult<RtcCalStatus?>(null);
    }

    [Fact]
    public async Task Measure_RecoversDeviceOffset_SymmetricPath()
    {
        var rc = ReferenceClock.ForTest(0, 0.001);
        var dev = new FakeRtc(rc) { OffsetSeconds = 0.25 };

        OffsetMeasurement m = (await RtcSyncService.MeasureAsync(dev, rc, deviceUtc: true, exchanges: 12))!;

        Assert.NotNull(m);
        Assert.Equal(0.25, m.OffsetSeconds, 4);                   // processing time and path cancel out
        Assert.Equal(12, m.Samples);
        Assert.Equal(0.0008, m.MinDelaySeconds, 4);               // round trip minus device processing
        Assert.Equal(0.0, m.ZoneOffsetHours);
        Assert.Equal(24.5, m.TemperatureC);
        Assert.Equal(-205000, m.AppliedPpb);
    }

    [Fact]
    public async Task Measure_KeepsShortestRoundTrips_DropsDelayedOutliers()
    {
        var rc = ReferenceClock.ForTest(0, 0.001);
        var dev = new FakeRtc(rc) { OffsetSeconds = -1.5, ExtraDelayEvery3rd = 0.040 };

        OffsetMeasurement m = (await RtcSyncService.MeasureAsync(dev, rc, true, exchanges: 24))!;

        // outliers would pull the offset by +20 ms; the shortest-round-trip quarter contains none
        Assert.Equal(-1.5, m.OffsetSeconds, 4);
        Assert.True(m.SpreadSeconds < 0.001, $"spread {m.SpreadSeconds}");
    }

    [Fact]
    public void ReferenceClock_AppliesPcOffset()
    {
        var rc = ReferenceClock.ForTest(3600, 0.002);
        double diff = (rc.TrueUtcNow() - DateTime.UtcNow).TotalSeconds;
        Assert.InRange(diff, 3599.9, 3600.1);
        Assert.True(rc.IsNtp);
        Assert.Equal(0.002, rc.UncertaintySeconds);
        Assert.False(ReferenceClock.FromPcClock().IsNtp);
    }

    private static UnitClockRecord Record(double hours, double ratePpb, int appliedPpb, int segment = 1, int points = 7, double? temp = 25.0)
    {
        var rec = new UnitClockRecord { UnitKey = "SIM", Segment = segment };
        var t0 = new DateTime(2026, 10, 20, 8, 0, 0, DateTimeKind.Utc);
        for (int i = 0; i < points; i++)
        {
            double s = hours * 3600.0 * i / (points - 1);
            rec.Points.Add(new SyncPoint
            {
                TrueUtc = t0.AddSeconds(s), OffsetSeconds = 0.010 + s * ratePpb * 1e-9, SpreadMs = 0.3,
                ReferenceUncertaintyMs = 2, TemperatureC = temp, AppliedPpb = appliedPpb, Segment = segment, Reference = "NTP time.google.com",
            });
        }
        return rec;
    }

    [Fact]
    public void Estimate_CrystalErrorIsClockRateMinusAppliedCorrection()
    {
        // device clock observed 1 ppm fast while it applied -205 ppm: the crystal itself is +206 ppm
        CalibrationEstimate e = RtcSyncService.Estimate(Record(hours: 24, ratePpb: 1000, appliedPpb: -205000))!;
        Assert.Equal(206000, e.ErrorPpb);
        Assert.Equal(24.0, e.SpanHours, 6);
        Assert.Equal(25.0, e.ReferenceC);
        Assert.Equal(7, e.Points);
        Assert.Null(e.Warning);
        Assert.InRange(e.UncertaintyPpb, 1, 100);
    }

    [Fact]
    public void Estimate_ShortSpanWarns_TooShortIsNull_ImplausibleIsSkipped()
    {
        CalibrationEstimate e = RtcSyncService.Estimate(Record(hours: 2, ratePpb: -3000, appliedPpb: 0))!;
        Assert.Equal(-3000, e.ErrorPpb);
        Assert.Contains("only 2.0 h of data", e.Warning);

        Assert.Null(RtcSyncService.Estimate(Record(hours: 0.5, ratePpb: 0, appliedPpb: 0)));
        Assert.Null(RtcSyncService.Estimate(Record(hours: 24, ratePpb: 5_000_000, appliedPpb: 0)));   // clock reset inside
    }

    [Fact]
    public void Estimate_UsesNewestUsableSegment()
    {
        UnitClockRecord rec = Record(hours: 24, ratePpb: 1000, appliedPpb: 0, segment: 1);
        rec.Points.AddRange(Record(hours: 13, ratePpb: -2000, appliedPpb: 0, segment: 2).Points
                                .Select(p => { p.TrueUtc = p.TrueUtc.AddDays(2); return p; }));
        CalibrationEstimate e = RtcSyncService.Estimate(rec)!;
        Assert.Equal(2, e.Segment);
        Assert.Equal(-2000, e.ErrorPpb);
    }

    [Theory]
    [InlineData(0, 0x0000u)]
    [InlineData(-205000, 0x00D7u)]        // 215 pulses masked per 2^20
    [InlineData(1000000, 0x8000u)]        // clamped to +488281: CALP, no CALM
    [InlineData(-1000000, 0x01FFu)]       // clamped
    [InlineData(100000, 0x8197u)]         // CALP (+512) - 105
    public void CalrFor_FirmwareEncoding(int ppb, uint calr) => Assert.Equal(calr, UnitDisk.CalrFor(ppb));

    [Fact]
    public void CalrFor_CorrectionOf_RoundTripWithinOneStep()
    {
        for (int ppb = -480000; ppb <= 480000; ppb += 12345)
            Assert.InRange(UnitDisk.CorrectionOf(UnitDisk.CalrFor(ppb)) - ppb, -477, 477);   // half of 953.7 ppb
    }

    [Fact]
    public void ErrorAtTurnover_FollowsParabola()
    {
        Assert.Equal(-201600, UnitDisk.ErrorAtTurnover(-205000, 35.0, 34, 25.0));
        Assert.Equal(-205000, UnitDisk.ErrorAtTurnover(-205000, 25.0, 34, 25.0));
        Assert.Equal(-205000, UnitDisk.ErrorAtTurnover(-205000, null, 34, 25.0));
    }

    [Fact]
    public void UnitDisk_WriteRead_AndPatchConfigCdrift()
    {
        using var dir = new TempDir();
        var est = new CalibrationEstimate(-206123, 23.456, 14.2, 812, 9, -205000, 3, null);
        UnitDisk.Write(dir.Path, " 47625051 ", est);

        UnitDisk.DiskCalibration c = UnitDisk.Read(dir.Path, "47625051")!;
        Assert.Equal(-206123, c.ErrorPpb);
        Assert.Equal(23.46, c.ReferenceC!.Value, 6);
        Assert.Null(UnitDisk.Read(dir.Path, "47085037"));         // copied from another unit: ignored

        File.WriteAllText(dir.File("config.json"), """{"name":"KOL","cdrift":32999,"devices":[{"name":"KOL"}]}""");
        Assert.Equal(32999u, UnitDisk.PatchConfigCdrift(dir.Path, 215));
        string patched = File.ReadAllText(dir.File("config.json"));
        Assert.Contains("\"cdrift\":215", patched);
        Assert.Contains("\"devices\":[{\"name\":\"KOL\"}]", patched);

        Assert.True(UnitDisk.Delete(dir.Path));
        Assert.False(UnitDisk.Delete(dir.Path));
    }

    [Fact]
    public void RtcExchange_ParsesReply()
    {
        var b = new byte[26];
        BitConverter.GetBytes(77u).CopyTo(b, 0);
        BitConverter.GetBytes(846235800u).CopyTo(b, 4); BitConverter.GetBytes((ushort)32768).CopyTo(b, 8);
        BitConverter.GetBytes(846235801u).CopyTo(b, 10); BitConverter.GetBytes((ushort)16384).CopyTo(b, 14);
        BitConverter.GetBytes((short)-512).CopyTo(b, 16);
        BitConverter.GetBytes((ushort)0xFFFF).CopyTo(b, 18);
        BitConverter.GetBytes(-205000).CopyTo(b, 20);
        b[24] = 0x23;

        RtcExchange x = RtcExchange.Parse(b, 10, 20)!;
        Assert.Equal(77u, x.Seq);
        Assert.Equal(846235800.5, x.DeviceReceiveSeconds, 9);
        Assert.Equal(846235801.25, x.DeviceReplySeconds, 9);
        Assert.Equal(-5.12, x.TemperatureC);
        Assert.Equal(-1, x.TemperatureAgeSeconds);
        Assert.Equal(-205000, x.AppliedPpb);
        Assert.Equal(RtcCalFlags.UnitCalibration | RtcCalFlags.TemperatureComp | RtcCalFlags.TemperatureValid, x.Flags);
        Assert.Null(RtcExchange.Parse(new byte[25], 0, 0));

        BitConverter.GetBytes(short.MinValue).CopyTo(b, 16);
        Assert.Null(RtcExchange.Parse(b, 0, 0)!.TemperatureC);
    }

    [Fact]
    public void RtcCalStatus_PackSetAndParse()
    {
        Assert.Equal(new byte[] { 1, 0x38, 0xDF, 0xFC, 0xFF, 0x2A, 0x09 }, RtcCalStatus.PackSet(-205000, 23.46));
        byte[] none = RtcCalStatus.PackSet(5, null);
        Assert.Equal(short.MinValue, BitConverter.ToInt16(none, 5));

        var b = new byte[23];
        b[0] = 0x81;
        BitConverter.GetBytes(-206000).CopyTo(b, 1);
        BitConverter.GetBytes((short)2346).CopyTo(b, 5);
        BitConverter.GetBytes(short.MinValue).CopyTo(b, 7);
        BitConverter.GetBytes((ushort)12).CopyTo(b, 9);
        BitConverter.GetBytes(-204000).CopyTo(b, 11);
        BitConverter.GetBytes((ushort)0x00D6).CopyTo(b, 15);
        BitConverter.GetBytes((ushort)34).CopyTo(b, 17);
        BitConverter.GetBytes((short)2500).CopyTo(b, 19);
        BitConverter.GetBytes((ushort)600).CopyTo(b, 21);

        RtcCalStatus s = RtcCalStatus.Parse(b)!;
        Assert.True(s.HasUnitCalibration);
        Assert.False(s.TemperatureCompensation);
        Assert.Equal(RtcCalFlags.UnitCalibration | RtcCalFlags.FromDisk, s.Flags);
        Assert.Equal(-206000, s.StoredErrorPpb);
        Assert.Equal(23.46, s.ReferenceTemperatureC);
        Assert.Null(s.TemperatureC);
        Assert.Equal(12, s.TemperatureAgeSeconds);
        Assert.Equal(-204000, s.AppliedPpb);
        Assert.Equal((ushort)0xD6, s.Calr);
        Assert.Equal(34, s.CurvaturePpbPerC2);
        Assert.Equal(25.0, s.TurnoverC);
        Assert.Equal(600, s.PeriodSeconds);
        Assert.Null(RtcCalStatus.Parse(new byte[22]));
    }

    [Fact]
    public void DeviceEpoch_SecondsSince2000()
    {
        Assert.Equal(846235800.0, DeviceEpoch.ToSeconds(new DateTime(2026, 10, 25, 9, 30, 0)));
        Assert.Equal(new DateTime(2026, 10, 25, 9, 30, 0, 500), DeviceEpoch.FromSeconds(846235800.5));
        Assert.Equal(846235800.0, RtcSyncService.DeviceSecondsOfTrue(new DateTime(2026, 10, 25, 9, 30, 0, DateTimeKind.Utc), deviceUtc: true));
    }
    private static NtpSample N(string server, double offsetMs, double delayMs) =>
        new(server, offsetMs / 1000.0, delayMs / 1000.0, DateTime.UtcNow);

    [Fact]
    public void NtpConsensus_OneServerOff_DoesNotMoveTheResult()
    {
        /* bench 2026-10-26: one answer 31 ms away from the others */
        var c = NtpClient.Combine(new[]
        {
            N("a", -561.0, 92), N("a", -559.0, 120),
            N("b", -592.8, 96),
            N("c", -560.0, 88),
            N("d", -562.5, 101),
        })!;
        Assert.Equal(-0.56175, c.OffsetSeconds, 5);         // median of -592.8, -562.5, -561, -560
        Assert.Equal(4, c.Used.Count);
        Assert.Equal(0, c.Rejected);
    }

    [Fact]
    public void NtpConsensus_RejectsWideRoundTrips_AndKeepsTheBestReplyPerServer()
    {
        var c = NtpClient.Combine(new[]
        {
            N("a", -10.0, 10), N("a", -40.0, 60),            // a's best reply is the 10 ms one
            N("b", -11.0, 14),
            N("c", +80.0, 200),                              // wide: rejected
        })!;
        Assert.Equal(1, c.Rejected);
        Assert.Equal(-0.0105, c.OffsetSeconds, 4);
        Assert.Equal(0.006, c.UncertaintySeconds, 4);         // median half round trip of 10 and 14 ms
    }

    [Fact]
    public void NtpConsensus_UncertaintyCoversTheSpread_AndEmptyIsNull()
    {
        var c = NtpClient.Combine(new[] { N("a", 0.0, 2), N("b", 30.0, 2), N("c", 10.0, 2) })!;
        Assert.Equal(0.010, c.OffsetSeconds, 4);
        Assert.Equal(0.015, c.UncertaintySeconds, 4);
        Assert.Null(NtpClient.Combine(Array.Empty<NtpSample>()));
    }
}
