using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VesperApp.Models;

namespace VesperApp.Services.Time
{
    /// <summary>Device clock against true time, from a burst of VESPER_TIME_SYNC exchanges.</summary>
    public sealed record OffsetMeasurement(
        DateTime TrueUtc, double OffsetSeconds, double ZoneOffsetHours,
        double MinDelaySeconds, double SpreadSeconds, int Samples,
        double? TemperatureC, int AppliedPpb, RtcCalFlags Flags);

    public sealed record SyncResult(bool Ok, string Message, OffsetMeasurement? Before, OffsetMeasurement? After);

    /// <summary>A unit's crystal error derived from two or more measurements without an adjustment between them.</summary>
    public sealed record CalibrationEstimate(
        int ErrorPpb, double? ReferenceC, double SpanHours, double UncertaintyPpb,
        int Points, double AppliedAveragePpb, int Segment, string? Warning);

    /// <summary>
    /// Precise RTC sync and crystal calibration for console loggers (asd-interfaces
    /// interfaces/rtc-time-sync.md).
    ///
    /// Offset: NTP-style four-timestamp exchange. offset = ((t2 - t1) + (t3 - t4)) / 2 removes the
    /// device's processing time and the (nearly symmetric) USB path; of 30-40 exchanges the quarter
    /// with the shortest round trip is kept (nothing delayed them) and their median is the result.
    /// Sync: coarse SET timed to land on a whole second when the error is 0.9 s or more, then the
    /// sub-second rest through the RTC shift register (calendar keeps running, calibration kept).
    /// Calibration: slope of the offset over a segment (no adjustment inside) is the device clock rate,
    /// which is crystal error + the correction the device applied; the difference is the crystal error.
    /// </summary>
    public static class RtcSyncService
    {
        public const double MinCalibrationHours = 1.0;
        public const double RecommendedCalibrationHours = 12.0;
        public const double MaxPlausibleErrorPpb = 2_000_000;   // 2000 ppm

        public static double ZoneHoursAt(DateTime trueUtc, bool deviceUtc) =>
            deviceUtc ? 0.0 : TimeZoneInfo.Local.GetUtcOffset(trueUtc).TotalHours;

        /// <summary>True time expressed in the device's calendar, as seconds since 2000.</summary>
        public static double DeviceSecondsOfTrue(DateTime trueUtc, bool deviceUtc)
        {
            DateTime cal = deviceUtc ? trueUtc : TimeZoneInfo.ConvertTimeFromUtc(trueUtc, TimeZoneInfo.Local);
            return DeviceEpoch.ToSeconds(DateTime.SpecifyKind(cal, DateTimeKind.Unspecified));
        }

        public static async Task<OffsetMeasurement?> MeasureAsync(
            IRtcDevice dev, ReferenceClock rc, bool deviceUtc, int exchanges = 40, CancellationToken ct = default)
        {
            var samples = new List<(double off, double delay, RtcExchange ex)>();
            uint seq = (uint)Environment.TickCount;
            int failures = 0;

            for (int i = 0; i < exchanges; i++)
            {
                ct.ThrowIfCancellationRequested();
                RtcExchange? ex = await dev.TimeSyncAsync(++seq).ConfigureAwait(false);
                if (ex == null)
                {
                    if (++failures > Math.Max(3, exchanges / 2)) return null;
                    continue;
                }

                double t1 = DeviceSecondsOfTrue(rc.TrueUtcAt(ex.HostSendTimestamp), deviceUtc);
                double t4 = DeviceSecondsOfTrue(rc.TrueUtcAt(ex.HostReceiveTimestamp), deviceUtc);
                double off = ((ex.DeviceReceiveSeconds - t1) + (ex.DeviceReplySeconds - t4)) / 2.0;
                double delay = (t4 - t1) - (ex.DeviceReplySeconds - ex.DeviceReceiveSeconds);

                /* device stamps step in 1/1024 s, so a slightly negative delay is quantisation */
                if (delay > -0.002 && delay < 0.5) samples.Add((off, delay, ex));

                await Task.Delay(3, ct).ConfigureAwait(false);   // decorrelate from USB frame phase
            }
            if (samples.Count < 3) return null;

            var best = samples.OrderBy(s => s.delay).Take(Math.Max(5, samples.Count / 4)).ToList();
            var offs = best.Select(b => b.off).OrderBy(x => x).ToList();
            double median = (offs.Count % 2 == 1) ? offs[offs.Count / 2] : (offs[offs.Count / 2 - 1] + offs[offs.Count / 2]) / 2.0;

            RtcExchange last = samples[^1].ex;
            DateTime when = rc.TrueUtcAt(last.HostReceiveTimestamp);
            return new OffsetMeasurement(when, median, ZoneHoursAt(when, deviceUtc),
                Math.Max(0, best[0].delay), offs[^1] - offs[0], samples.Count,
                last.TemperatureC, last.AppliedPpb, last.Flags);
        }

        /// <summary>Send SET_RTC so it lands on a whole second (the device restarts its sub-second
        /// counter when it applies the command).</summary>
        private static async Task SetOnSecondAsync(IRtcDevice dev, ReferenceClock rc, bool deviceUtc, double oneWay, CancellationToken ct)
        {
            double target = Math.Ceiling(DeviceSecondsOfTrue(rc.TrueUtcNow(), deviceUtc) + oneWay + 0.25);
            for (;;)
            {
                double remain = target - oneWay - DeviceSecondsOfTrue(rc.TrueUtcNow(), deviceUtc);
                if (remain <= 0) break;
                if (remain > 0.03) await Task.Delay(TimeSpan.FromSeconds(remain - 0.02), ct).ConfigureAwait(false);
                else Thread.SpinWait(200);
            }
            await dev.SetRtcAsync(DeviceEpoch.FromSeconds(target)).ConfigureAwait(false);
        }

        public static async Task<SyncResult> SyncAsync(
            IRtcDevice dev, ReferenceClock rc, bool deviceUtc, RtcCalibrationStore? store, CancellationToken ct = default)
        {
            OffsetMeasurement? before = await MeasureAsync(dev, rc, deviceUtc, 30, ct).ConfigureAwait(false);
            if (before == null)
                return new SyncResult(false, "The device does not answer the time-sync command (firmware without VESPER_TIME_SYNC?).", null, null);
            store?.Record(dev.UnitKey, before, SyncPointKind.Measured, rc);   // closes the running segment

            double off = before.OffsetSeconds;
            if (Math.Abs(off) >= 0.9)
            {
                await SetOnSecondAsync(dev, rc, deviceUtc, Math.Max(0.0005, before.MinDelaySeconds / 2.0), ct).ConfigureAwait(false);
                await Task.Delay(200, ct).ConfigureAwait(false);
                OffsetMeasurement? m = await MeasureAsync(dev, rc, deviceUtc, 20, ct).ConfigureAwait(false);
                if (m == null) return new SyncResult(false, "The device stopped answering after the clock was set.", before, null);
                off = m.OffsetSeconds;
            }

            for (int pass = 0; pass < 3 && Math.Abs(off) > 0.0015; pass++)
            {
                int us = (int)Math.Clamp(Math.Round(-off * 1e6), -999000, 999000);
                bool ack = await dev.ShiftAsync(us).ConfigureAwait(false);
                if (!ack)
                {
                    await Task.Delay(1200, ct).ConfigureAwait(false);       // a previous shift may be pending
                    if (!await dev.ShiftAsync(us).ConfigureAwait(false)) break;
                }
                await Task.Delay(300, ct).ConfigureAwait(false);
                OffsetMeasurement? m = await MeasureAsync(dev, rc, deviceUtc, 20, ct).ConfigureAwait(false);
                if (m == null) break;
                off = m.OffsetSeconds;
            }

            OffsetMeasurement? after = await MeasureAsync(dev, rc, deviceUtc, 30, ct).ConfigureAwait(false);
            if (after == null) return new SyncResult(false, "The device stopped answering after the adjustment.", before, null);
            store?.Record(dev.UnitKey, after, SyncPointKind.AfterSync, rc);

            /* The device keeps its calibration in backup registers, lost together with its time:
               re-apply the stored master copy when the device came back without it. */
            if (store != null && (after.Flags & RtcCalFlags.UnitCalibration) == 0)
            {
                StoredCalibration? cal = store.Get(dev.UnitKey).Calibration;
                if (cal != null) await dev.CalAsync(RtcCalStatus.PackSet(cal.ErrorPpb, cal.ReferenceC)).ConfigureAwait(false);
            }

            bool ok = Math.Abs(after.OffsetSeconds) < 0.005;
            string msg = $"Clock {(ok ? "synchronised" : "adjusted")}: was {before.OffsetSeconds * 1000:+0.0;-0.0} ms, now " +
                         $"{after.OffsetSeconds * 1000:+0.0;-0.0} ms (± {Math.Max(after.MinDelaySeconds / 2, 0.0005) * 1000:0.0} ms, reference {rc.Source}).";
            return new SyncResult(ok, msg, before, after);
        }

        /// <summary>Crystal error from the most recent segment that spans at least
        /// <paramref name="minHours"/>; null when no such segment exists.</summary>
        public static CalibrationEstimate? Estimate(UnitClockRecord rec, double minHours = MinCalibrationHours)
        {
            foreach (var seg in rec.Points.GroupBy(p => p.Segment).OrderByDescending(g => g.Key))
            {
                var pts = seg.OrderBy(p => p.TrueUtc).ToList();
                if (pts.Count < 2) continue;
                double span = (pts[^1].TrueUtc - pts[0].TrueUtc).TotalSeconds;
                if (span < minHours * 3600.0) continue;

                /* least squares of the continuous offset against true time */
                DateTime t0 = pts[0].TrueUtc;
                double[] x = pts.Select(p => (p.TrueUtc - t0).TotalSeconds).ToArray();
                double[] y = pts.Select(p => p.ContinuousOffsetSeconds).ToArray();
                double mx = x.Average(), my = y.Average();
                double sxx = x.Sum(v => (v - mx) * (v - mx));
                double sxy = x.Zip(y, (a, b) => (a - mx) * (b - my)).Sum();
                double rate = sxy / sxx;                                 // s/s

                double applied = pts.Average(p => p.AppliedPpb);
                /* A 32 kHz crystal is never 0.2 % off: such a slope means the device clock was
                   reset or set inside the period (e.g. firmware re-initialising an unset
                   calendar on boot) - not usable. */
                if (Math.Abs(rate * 1e9 - applied) > MaxPlausibleErrorPpb) continue;
                int errorPpb = (int)Math.Round(rate * 1e9 - applied);
                var temps = pts.Where(p => p.TemperatureC.HasValue).Select(p => p.TemperatureC!.Value).ToList();
                double? tref = temps.Count > 0 ? temps.Average() : null;

                /* per point: measurement spread, 1/1024 s stamp step, reference (NTP) uncertainty */
                double sigY = pts.Max(p => Math.Sqrt(Math.Pow(Math.Max(p.SpreadMs, 0.5) / 1000.0, 2) +
                                                     Math.Pow(p.ReferenceUncertaintyMs / 1000.0, 2)));
                double sigRatePpb = Math.Sqrt(2.0) * sigY / span * 1e9;

                var warnings = new List<string>();
                if (span < RecommendedCalibrationHours * 3600.0)
                    warnings.Add($"only {span / 3600.0:0.0} h of data; {RecommendedCalibrationHours:0} h or more gives a better estimate");
                if (pts.Max(p => p.AppliedPpb) - pts.Min(p => p.AppliedPpb) > 2000)
                    warnings.Add("the applied correction changed during the interval (temperature compensation); the average was used");
                if (temps.Count > 1 && temps.Max() - temps.Min() > 3.0)
                    warnings.Add($"temperature varied by {temps.Max() - temps.Min():0.0} °C; calibrate at a stable temperature");
                if (pts.Any(p => !p.Reference.StartsWith("NTP", StringComparison.Ordinal)))
                    warnings.Add("some points used the bare PC clock as reference");

                return new CalibrationEstimate(errorPpb, tref, span / 3600.0, sigRatePpb, pts.Count, applied, seg.Key,
                    warnings.Count > 0 ? string.Join("; ", warnings) : null);
            }
            return null;
        }

        /// <summary>Remove the unit's calibration (it falls back to config.json "cdrift").</summary>
        public static async Task<RtcCalStatus?> ClearAsync(IRtcDevice dev, RtcCalibrationStore store)
        {
            RtcCalStatus? st = await dev.CalAsync(new byte[] { 2 }).ConfigureAwait(false);
            if (st != null) store.SetCalibration(dev.UnitKey, null);
            return st;
        }

        /// <summary>Write the estimate to the unit and keep it as the master copy.</summary>
        public static async Task<RtcCalStatus?> ApplyAsync(IRtcDevice dev, CalibrationEstimate est, RtcCalibrationStore store)
        {
            RtcCalStatus? st = await dev.CalAsync(RtcCalStatus.PackSet(est.ErrorPpb, est.ReferenceC)).ConfigureAwait(false);
            if (st != null && st.HasUnitCalibration)
            {
                store.SetCalibration(dev.UnitKey, new StoredCalibration
                {
                    ErrorPpb = est.ErrorPpb,
                    ReferenceC = est.ReferenceC,
                    AppliedUtc = DateTime.UtcNow,
                    SpanHours = est.SpanHours,
                    UncertaintyPpb = est.UncertaintyPpb,
                });
            }
            return st;
        }
    }
}
