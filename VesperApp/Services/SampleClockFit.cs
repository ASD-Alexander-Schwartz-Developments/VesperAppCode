using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace VesperApp.Services
{
    /// <summary>
    /// The true sample rate of one audio file, measured from its SYNC stamps (backlog F6, asd-interfaces
    /// projects/vesperu5-next-firmware-backlog.md). The file header holds the nominal rate; the microphone clock of
    /// Vesper / Pipistrelle / KOL is locked to the 32.768 kHz crystal (1465 x crystal = +106.7 ppm over nominal) and
    /// so carries the crystal's own error, while the SYNC times come from the calibrated clock. A straight line
    /// through (sample number, SYNC time) gives the samples per second of real time: below 1 ppm for a 10-minute file.
    /// Only the SYNC stamps are used: with firmware 1.22 the HEADER / FOOTER can sit up to ~40 ms off the samples
    /// (backlog F10).
    /// </summary>
    public static class SampleClockFit
    {
        /// <summary>Shorter spans give a rate no better than the nominal one (1 ms stamp steps over a few seconds).</summary>
        public const double MinSpanS = 10;
        public const int MinPoints = 3;

        public sealed record Result(double RateHz, double Ppm, int Points, double SpanS, double MaxDeviationMs);

        /// <summary>Least-squares rate through the SYNC points (sample number in the output file, stamp time), or
        /// null when there are too few points or they span less than <see cref="MinSpanS"/>.</summary>
        public static Result? Fit(IReadOnlyList<(long Sample, DateTime Time)> points, uint nominalHz)
        {
            if (points == null || points.Count < MinPoints || nominalHz == 0) return null;
            long n0 = points[0].Sample;
            DateTime t0 = points[0].Time;
            int k = points.Count;
            double sn = 0, st = 0, snn = 0, snt = 0;
            foreach (var p in points)
            {
                double n = p.Sample - n0, t = (p.Time - t0).TotalSeconds;
                sn += n; st += t; snn += n * n; snt += n * t;
            }
            double den = k * snn - sn * sn;
            if (den <= 0) return null;
            double b = (k * snt - sn * st) / den;          // seconds per sample
            double a = (st - b * sn) / k;
            double spanS = (points[k - 1].Sample - n0) / (double)nominalHz;
            if (b <= 0 || spanS < MinSpanS) return null;
            double maxDev = 0;
            foreach (var p in points)
            {
                double n = p.Sample - n0, t = (p.Time - t0).TotalSeconds;
                maxDev = Math.Max(maxDev, Math.Abs(t - (a + b * n)));
            }
            double rate = 1.0 / b;
            return new Result(rate, (rate / nominalHz - 1.0) * 1e6, k, spanS, maxDev * 1000.0);
        }

        /// <summary>The metadata lines (no trailing blank line): MeasuredRate, ClockPpm and ClockFit, with a note when
        /// the stamps do not lie on one line (a lost clock lock or a crystal stall inside the file).</summary>
        public static string MetadataLines(Result r)
        {
            var c = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.Append("MeasuredRate:").Append(r.RateHz.ToString("0.00", c)).Append(Environment.NewLine);
            sb.Append("ClockPpm:").Append(r.Ppm.ToString("+0.0;-0.0;0.0", c)).Append(Environment.NewLine);
            sb.Append("ClockFit:").Append(r.Points.ToString(c)).Append(" SYNC stamps over ")
              .Append(r.SpanS.ToString("0.0", c)).Append(" s, largest deviation ")
              .Append(r.MaxDeviationMs.ToString("0.0", c)).Append(" ms");
            if (r.MaxDeviationMs > UnevenMs)
                sb.Append(" - uneven: the clock changed inside this file; use the Sync lines (see the recording check)");
            sb.Append(Environment.NewLine);
            return sb.ToString();
        }

        /// <summary>Beyond this the points are not one straight line: the 1 ms stamp steps and the RTC sub-second step
        /// (up to ~4 ms) stay well below it.</summary>
        public const double UnevenMs = 10;
    }
}
