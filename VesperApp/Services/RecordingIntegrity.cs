using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using VesperApp.Models;

namespace VesperApp.Services
{
    /// <summary>
    /// Per-run integrity check of a Vesper / Pipistrelle microphone recording (the raw <c>&lt;n&gt;U.BIN</c> files),
    /// from what the firmware already writes: the sample count between blocks and the crystal (RTC) time in every
    /// HEADER, SYNC (every 65 536 bytes) and FOOTER block. No firmware support needed.
    /// <list type="bullet">
    /// <item>Sample clock off: the samples per crystal second, in ~10 s windows, more than
    /// <see cref="ClockOffPpm"/> from the nominal rate. The microphone clock is locked to the crystal; a lost lock
    /// (STM32U5 erratum ES0499 2.2.27, bench 2026-11-08) runs it ~0.3–0.5 % fast for the rest of the run.</item>
    /// <item>Crystal stall: the crystal time falls behind the samples within a few SYNC intervals (the crystal
    /// slowed or stopped, e.g. when the board is pressed); time stamps after it are late by that much.</item>
    /// <item>Audio lost between files: the gap from one file's FOOTER to the next file's HEADER
    /// (~19 ms at every file change with firmware 1.22; more means more audio lost).</item>
    /// <item>GAP blocks the firmware wrote itself.</item>
    /// </list>
    /// asd-interfaces projects/u585-bench-2026-11-08.md, issue 2 (check 2026-11-09).
    /// <para>ExG runs (<c>&lt;n&gt;E.BIN</c>: EXG2, EEG2 / EMG2 / ECG2, EXG48; backlog V1) are checked the same way from
    /// their HEADER / FOOTER stamps (they have no SYNC): data lost at a file change (backlog F1), records that are not
    /// samples (no 1100 status start: the EXG2 family's merged reads, backlog F3), and the recorded samples per
    /// second. The boards run on the converter's own oscillator (ADS129x / ADS1x9x: +-0.5 % at 25 C, +-2 % over
    /// 0..70 C), so only a rate beyond <see cref="ExgRateTolerancePct"/> is reported; the table shows every file's.</para>
    /// </summary>
    public static class RecordingIntegrity
    {
        public const double ClockOffPpm = 1000;          // locked: crystal error, within a few hundred ppm
        public const double StallMs = 10;                // per SYNC interval, beyond the RTC sub-second step (~4 ms)
        public const double WindowS = 10;
        public const double FileChangeNormalMs = 30;     // firmware 1.22: 18.6–20.5 ms
        public const double PauseS = 2;                  // longer: a recording window or schedule pause, not a loss
        public const string ReportName = "RECORDING-CHECK.TXT";
        public const double ExgRateTolerancePct = 2.5;   // converter oscillator over temperature (+-2 %, industrial +-2.5 %)
        public const double ExgFileChangeMs = 10;        // beyond one sample period: data lost at the file change
        public const double ExgMinClockS = 5;            // shorter: the 1 ms stamps say little about the rate

        private const uint MagHeader = 0xA55AA55A, MagFooter = 0xA55A5AA5, MagSync = 0xABCDEFEF, MagGap = 0xA55A3CC3;
        private const int BlockLen = 16, GapLen = 512;
        private const ushort GapOverrun = 1, GapInvalid = 2;   // GAP block reasons (kol-recording-format.md)
        private static readonly Regex MicRaw = new(@"^(\d+)U\.BIN$", RegexOptions.IgnoreCase);
        private static readonly Regex ExgRaw = new(@"^(\d+)E\.BIN$", RegexOptions.IgnoreCase);

        /// <summary>True for a Vesper / Pipistrelle microphone raw file (<c>12U.BIN</c>; not KOL's <c>U0</c>–<c>U3</c>).</summary>
        public static bool IsMicRaw(string path) => MicRaw.IsMatch(Path.GetFileName(path));

        /// <summary>True for an ExG raw file (<c>7E.BIN</c>).</summary>
        public static bool IsExgRaw(string path) => ExgRaw.IsMatch(Path.GetFileName(path));

        public sealed class Finding
        {
            public string File = "";
            public double AtS;              // seconds from the start of that file's audio
            public string Text = "";
        }

        public sealed class FileResult
        {
            public string File = "";
            public uint Rate;
            public long Samples;
            public double AudioS, ClockS;
            public double? GapBeforeMs;     // FOOTER of the previous file -> this HEADER
            public List<double> WindowPpm = new();
            public int GapBlocks;
            public double? ClockOffAt;      // start of the first ~10 s window with the sample clock off
            public List<double> OffPpm = new();
            public int LockedBeforeOff;     // windows within the lock limit before ClockOffAt (0: off since the start)
            public long Unreliable;         // ExG: records without a valid status start
            public double? RecordedRate;    // ExG: records per clock second (files of >= ExgMinClockS)
        }

        public sealed class Report
        {
            public List<FileResult> Files = new();
            public List<Finding> Findings = new();
            public int ClockOffFiles, Stalls, LostBetweenFiles, GapBlocks, RateOffFiles;
            public long UnreliableRecords;
            /// <summary>"AUD" for a microphone run, "EXG" for an ExG run: the output folder the report goes to.</summary>
            public string Folder = "AUD";
            public bool IsExg => Folder == "EXG";
            public double StallTotalMs, LostTotalMs, LostInFilesMs;
            public bool AnyProblem => Findings.Count > 0;

            /// <summary>One line for the decode summary.</summary>
            public string Headline()
            {
                if (!AnyProblem) return "recording check: no problems found";
                var p = new List<string>();
                if (ClockOffFiles > 0) p.Add($"sample clock off in {ClockOffFiles} file(s)");
                if (Stalls > 0) p.Add($"{Stalls} crystal stall(s), {StallTotalMs / 1000:0.00} s");
                if (LostBetweenFiles > 0) p.Add($"{LostTotalMs:0} ms lost between files");
                if (UnreliableRecords > 0) p.Add($"{UnreliableRecords} unreliable record(s)");
                if (RateOffFiles > 0) p.Add($"sample count off in {RateOffFiles} file(s)");
                if (LostInFilesMs > 0) p.Add($"{LostInFilesMs:0} ms lost inside files");
                else if (GapBlocks > 0) p.Add($"{GapBlocks} GAP block(s)");
                return "recording check: " + string.Join(", ", p);
            }
        }

        private readonly record struct Block(int Pos, uint Magic, double T, ushort Reason, uint NodeSamples);
        private readonly record struct Point(long N, double T);

        /// <summary>Checks one run: the microphone raw files of one session, in file-number order.</summary>
        public static Report Check(IEnumerable<string> micRawFiles)
        {
            var rep = new Report();
            var files = micRawFiles.Where(IsMicRaw)
                .OrderBy(f => long.Parse(MicRaw.Match(Path.GetFileName(f)).Groups[1].Value, CultureInfo.InvariantCulture))
                .ToList();
            double? prevFooter = null;
            foreach (string path in files)
            {
                byte[] b;
                try { b = File.ReadAllBytes(path); } catch { continue; }
                CheckFile(b, Path.GetFileName(path), rep, ref prevFooter);
            }
            Finish(rep);
            return rep;
        }

        internal static void CheckFile(byte[] b, string name, Report rep, ref double? prevFooter)
        {
            uint rate = 0;
            if (b.Length >= BinaryTypeHeader.FILE_HEADER_LENGTH && BitConverter.ToUInt32(b, 0) == BinaryTypeHeader.FILE_HEADER_PREAMBLE)
                rate = new BinaryTypeHeader(b.Take(BinaryTypeHeader.FILE_HEADER_LENGTH).ToArray()).SamplingRate;
            if (rate == 0) return;

            List<Block> blocks = FindBlocks(b);
            // Segments: HEADER .. FOOTER (continuous recording: one per file; windowed: one per window).
            int i = 0;
            while (i < blocks.Count)
            {
                if (blocks[i].Magic != MagHeader) { i++; continue; }
                Block h = blocks[i];
                var pts = new List<Point> { new(0, h.T) };
                long bytes = 0, lost = 0; int last = h.Pos + BlockLen, gaps = 0, overruns = 0, invalid = 0;
                double? lostAt = null;
                Block? footer = null;
                int j = i + 1;
                for (; j < blocks.Count; j++)
                {
                    Block k = blocks[j];
                    if (k.Magic == MagHeader) break;
                    bytes += k.Pos - last;
                    // Sample positions on the timeline count the samples an OVERRUN block stands for (the half the
                    // storage write lost): the sample clock stays comparable with the crystal across the loss.
                    if (k.Magic == MagSync) { pts.Add(new(bytes / 2 + lost, k.T)); last = k.Pos + BlockLen; }
                    else if (k.Magic == MagGap)
                    {
                        gaps++; last = k.Pos + GapLen;
                        if (k.Reason == GapOverrun) { overruns++; lost += k.NodeSamples; lostAt ??= bytes / 2.0 / rate; }
                        else if (k.Reason == GapInvalid) invalid++;
                    }
                    else { footer = k; pts.Add(new(bytes / 2 + lost, k.T)); j++; break; }
                }
                i = j;
                if (footer == null) continue;                      // unfinished segment (cut-off file): nothing to compare

                var fr = new FileResult
                {
                    File = name, Rate = rate, Samples = bytes / 2, AudioS = bytes / 2.0 / rate,
                    ClockS = footer.Value.T - h.T, GapBlocks = gaps,
                };
                if (prevFooter.HasValue)
                {
                    double g = h.T - prevFooter.Value;
                    if (g >= 0 && g < PauseS)
                    {
                        fr.GapBeforeMs = g * 1000;
                        if (g * 1000 > FileChangeNormalMs)
                        {
                            rep.LostBetweenFiles++; rep.LostTotalMs += g * 1000;
                            rep.Findings.Add(new Finding { File = name, AtS = 0,
                                Text = $"{g * 1000:0} ms of audio lost before this file (a normal file change loses about 19 ms)" });
                        }
                    }
                }
                prevFooter = footer.Value.T;
                if (gaps > 0)
                {
                    rep.GapBlocks += gaps;
                    double lostMs = lost * 1000.0 / rate;
                    rep.LostInFilesMs += lostMs;
                    string text = overruns > 0
                        ? $"{lostMs:0} ms of audio lost inside this file ({overruns} buffer(s): a storage write took too long; the logger marked the place)"
                        : $"{gaps} GAP block(s) written by the logger (samples unreliable)";
                    if (invalid > 0) text += $", {invalid} buffer(s) next to it marked unreliable";
                    rep.Findings.Add(new Finding { File = name, AtS = lostAt ?? 0, Text = text });
                }
                AnalyseClock(pts, rate, fr, rep);
                rep.Files.Add(fr);
            }
        }

        private static void AnalyseClock(List<Point> pts, uint rate, FileResult fr, Report rep)
        {
            if (pts.Count < 3) return;
            // Crystal stalls: intervals where the crystal time advances clearly less than the samples (beyond any
            // sample-clock offset, which is <= ~0.5 %). Consecutive ones are one event.
            double runStart = -1, runLost = 0;
            void Close()
            {
                if (runStart < 0) return;
                rep.Stalls++; rep.StallTotalMs += runLost;
                rep.Findings.Add(new Finding { File = fr.File, AtS = runStart,
                    Text = $"crystal clock fell behind by {runLost:0} ms (it slowed or stopped; e.g. the board was pressed). Time stamps after this point are late by that much" });
                runStart = -1; runLost = 0;
            }
            for (int k = 1; k < pts.Count; k++)
            {
                double ds = (pts[k].N - pts[k - 1].N) / (double)rate, dt = pts[k].T - pts[k - 1].T;
                double excessMs = (ds * 0.99 - dt) * 1000;          // > 0: crystal behind even a 1 % fast sample clock
                if (ds > 0.05 && excessMs > StallMs)
                {
                    if (runStart < 0) runStart = pts[k - 1].N / (double)rate;
                    runLost += (ds - dt) * 1000;
                }
                else Close();
            }
            Close();

            // Sample-clock offset in ~10 s windows (stall intervals excluded by measuring between clean points).
            var w = new List<(double at, double ppm)>();
            int a = 0;
            for (int k = 1; k < pts.Count; k++)
            {
                double ds = (pts[k].N - pts[a].N) / (double)rate;
                if (ds < WindowS && k < pts.Count - 1) continue;
                if (ds < WindowS / 2) break;
                double dt = pts[k].T - pts[a].T;
                bool stalled = false;
                for (int q = a + 1; q <= k; q++)
                {
                    double s = (pts[q].N - pts[q - 1].N) / (double)rate, t = pts[q].T - pts[q - 1].T;
                    if (s > 0.05 && (s * 0.99 - t) * 1000 > StallMs) { stalled = true; break; }
                }
                if (!stalled && dt > 0) w.Add((pts[a].N / (double)rate, (ds / dt - 1) * 1e6));
                a = k;
            }
            fr.WindowPpm = w.Select(x => x.ppm).ToList();
            var off = w.Where(x => Math.Abs(x.ppm) > ClockOffPpm).ToList();
            if (off.Count > 0)
            {
                rep.ClockOffFiles++;
                fr.ClockOffAt = off[0].at;
                fr.OffPpm = off.Select(x => x.ppm).ToList();
                fr.LockedBeforeOff = w.Count(x => x.at < off[0].at && Math.Abs(x.ppm) <= ClockOffPpm);
            }
        }

        /// <summary>Adds the sample-clock findings - one per stretch of consecutive files with the clock off (a lost
        /// lock lasts to the end of the run) - and puts the findings in file / time order.</summary>
        internal static void Finish(Report rep)
        {
            var files = rep.Files;
            int i = 0;
            while (i < files.Count)
            {
                if (!files[i].ClockOffAt.HasValue) { i++; continue; }
                int j = i;
                // The stretch goes on through files that are off from their start (or whose first windows were
                // lost to a crystal stall) and through files too short to measure (no window at all).
                while (j + 1 < files.Count &&
                       ((files[j + 1].ClockOffAt.HasValue && files[j + 1].LockedBeforeOff == 0) || files[j + 1].WindowPpm.Count == 0)) j++;
                while (j > i && !files[j].ClockOffAt.HasValue && j < files.Count - 1) j--;   // a short file only bridges
                var all = files.Skip(i).Take(j - i + 1).SelectMany(f => f.OffPpm).OrderBy(x => x).ToList();
                // The first window may straddle the moment the lock went; with more windows, leave it out.
                var use = all.Count > 2 ? files.Skip(i).Take(j - i + 1).SelectMany(f => f.OffPpm).Skip(1).OrderBy(x => x).ToList() : all;
                double med = use[use.Count / 2];
                FileResult a = files[i], b = files[j];
                string where = a.ClockOffAt < 1
                    ? (i == j ? "for this file" : $"from the start of this file to the end of {b.File}")
                    : (i == j ? $"from about {a.ClockOffAt:0}-{a.ClockOffAt + WindowS:0} s to the end of this file"
                              : $"from about {a.ClockOffAt:0}-{a.ClockOffAt + WindowS:0} s in this file to the end of {b.File}");
                if (j == files.Count - 1 && i != j) where += " (the end of the run)";
                rep.Findings.Add(new Finding { File = a.File, AtS = a.ClockOffAt ?? 0,
                    Text = $"sample clock {(med > 0 ? "fast" : "slow")} by {Math.Abs(med) / 1e4:0.00} % ({med:+0;-0} ppm) {where}: " +
                           $"the true rate was about {a.Rate * (1 + med / 1e6):0} Hz, not {a.Rate} Hz - frequencies read " +
                           $"{Math.Abs(med) / 1e4:0.00} % {(med > 0 ? "low" : "high")}, times drift {Math.Abs(med) / 1000:0.0} ms per second" });
                i = j + 1;
            }
            var order = files.Select((f, k) => (f.File, k)).GroupBy(x => x.File).ToDictionary(g => g.Key, g => g.First().k);
            rep.Findings = rep.Findings.OrderBy(f => order.TryGetValue(f.File, out int k) ? k : int.MaxValue).ThenBy(f => f.AtS).ToList();
        }

        private static List<Block> FindBlocks(byte[] b)
        {
            var list = new List<Block>();
            for (int o = 0; o + BlockLen <= b.Length; o += 2)
            {
                uint m = BitConverter.ToUInt32(b, o);
                if (m != MagHeader && m != MagFooter && m != MagSync && m != MagGap) continue;
                if (!BinHeader.IsValidStamp(b, o + 4)) continue;
                double t = StampSeconds(b, o + 4);
                ushort reason = m == MagGap && o + 18 <= b.Length ? BitConverter.ToUInt16(b, o + 16) : (ushort)0;
                uint nodeSamples = m == MagGap && o + 24 <= b.Length ? BitConverter.ToUInt32(b, o + 20) : 0;
                list.Add(new Block(o, m, t, reason, nodeSamples));
                o += (m == MagGap ? GapLen : BlockLen) - 2;
            }
            return list;
        }

        /// <summary>The block's RTC time in seconds (unrounded sub-seconds), as BinHeader.ExtractDateTime reads it.</summary>
        internal static double StampSeconds(byte[] b, int i)
        {
            int hh = BinHeader.BCD2BIN(b[i]), mm = BinHeader.BCD2BIN(b[i + 1]), ss = BinHeader.BCD2BIN(b[i + 2]);
            int mo = BinHeader.BCD2BIN(b[i + 5]), d = BinHeader.BCD2BIN(b[i + 6]), y = 2000 + BinHeader.BCD2BIN(b[i + 7]);
            ushort frac = BitConverter.ToUInt16(b, i + 8), sub = BitConverter.ToUInt16(b, i + 10);
            double day = new DateTime(y, mo, d).Ticks / (double)TimeSpan.TicksPerSecond;
            return day + hh * 3600 + mm * 60 + ss + (frac - (double)sub) / (frac + 1.0);
        }

        // ───────────────────────── ExG runs (backlog V1) ─────────────────────────

        /// <summary>Checks one ExG run: the <c>&lt;n&gt;E.BIN</c> files of one session, in file-number order.</summary>
        public static Report CheckExg(IEnumerable<string> exgRawFiles)
        {
            var rep = new Report { Folder = "EXG" };
            var files = exgRawFiles.Where(IsExgRaw)
                .OrderBy(f => long.Parse(ExgRaw.Match(Path.GetFileName(f)).Groups[1].Value, CultureInfo.InvariantCulture))
                .ToList();
            double? prevFooter = null;
            foreach (string path in files)
            {
                byte[] b;
                try { b = File.ReadAllBytes(path); } catch { continue; }
                CheckExgFile(b, Path.GetFileName(path), rep, ref prevFooter);
            }
            Finish(rep);
            return rep;
        }

        internal static void CheckExgFile(byte[] b, string name, Report rep, ref double? prevFooter)
        {
            if (b.Length < BinaryTypeHeader.FILE_HEADER_LENGTH || BitConverter.ToUInt32(b, 0) != BinaryTypeHeader.FILE_HEADER_PREAMBLE) return;
            var hdr = new BinaryTypeHeader(b.Take(BinaryTypeHeader.FILE_HEADER_LENGTH).ToArray());
            string sensor = hdr.DeviceDriverName;
            bool ads1192 = ExgGain.IsAds1192(sensor), exg2 = ExgGain.IsExg2(sensor);
            double rate = ads1192 || exg2
                ? ExgRate.Exg2SamplesPerSecond(hdr.SamplingRate)
                : ExgRate.Exg48SamplesPerSecond(hdr.SamplingRate, ExgRate.Exg48HighResolution(hdr.Configuration0));
            if (rate <= 0) return;

            List<Block> blocks = FindBlocks(b);
            int i = 0;
            while (i < blocks.Count)
            {
                if (blocks[i].Magic != MagHeader) { i++; continue; }
                Block h = blocks[i];
                // The payload of the segment, without any block inside it (ExG writes none today).
                var data = new List<byte>();
                int last = h.Pos + BlockLen;
                Block? footer = null;
                int j = i + 1;
                for (; j < blocks.Count; j++)
                {
                    Block k = blocks[j];
                    if (k.Magic == MagHeader) break;
                    for (int q = last; q < k.Pos; q++) data.Add(b[q]);
                    last = k.Pos + (k.Magic == MagGap ? GapLen : BlockLen);
                    if (k.Magic == MagFooter) { footer = k; j++; break; }
                }
                i = j;
                if (footer == null) continue;                      // unfinished segment (cut-off file)

                byte[] d = data.ToArray();
                int size = ads1192 ? EXG1192Parser.DetectRecordSize(d) : exg2 ? Exg2RecordSize : EXG48Parser.RecordSize;
                long records = d.Length / size, bad = 0;
                for (long r = 0; r < records; r++)
                {
                    int o = (int)(r * size);
                    bool ok = ads1192 || exg2 ? EXG1192Parser.IsFrameStart(d, o) : (d[o] & 0xF0) == 0xC0;
                    if (!ok) bad++;
                }
                double clockS = footer.Value.T - h.T;
                var fr = new FileResult
                {
                    File = name, Rate = (uint)Math.Round(rate), Samples = records, AudioS = records / rate, ClockS = clockS,
                    Unreliable = bad, RecordedRate = clockS >= ExgMinClockS ? records / clockS : null,
                };

                if (prevFooter.HasValue)
                {
                    double g = h.T - prevFooter.Value;
                    if (g >= 0 && g < PauseS)
                    {
                        fr.GapBeforeMs = g * 1000;
                        double lostMs = g * 1000 - 1000.0 / rate;      // one sample period is the normal step
                        if (lostMs > ExgFileChangeMs)
                        {
                            rep.LostBetweenFiles++; rep.LostTotalMs += lostMs;
                            rep.Findings.Add(new Finding { File = name, AtS = 0,
                                Text = $"{lostMs:0} ms of data lost before this file (about {lostMs * rate / 1000:0} samples): the recording paused while the logger changed files" });
                        }
                    }
                }
                prevFooter = footer.Value.T;

                if (bad > 0)
                {
                    rep.UnreliableRecords += bad;
                    rep.Findings.Add(new Finding { File = name, AtS = 0,
                        Text = $"{bad} of {records} records ({100.0 * bad / Math.Max(1, records):0.00} %) are not valid samples (no status word at their start): " +
                               "the logger read the converter out of step while it wrote to storage. Each one still takes a sample's place in time" });
                }
                if (fr.RecordedRate.HasValue)
                {
                    double off = (fr.RecordedRate.Value / rate - 1) * 100;
                    if (Math.Abs(off) > ExgRateTolerancePct)
                    {
                        rep.RateOffFiles++;
                        rep.Findings.Add(new Finding { File = name, AtS = 0,
                            Text = off < 0
                                ? $"the samples cover {clockS - records / rate:0.00} s less than the file's {clockS:0.00} s between its start and end stamps " +
                                  $"({fr.RecordedRate.Value:0.0} samples per second, {off:+0.0;-0.0} % from {rate:0}; the board's clock is within 2 %): " +
                                  "samples were lost, or sampling started late after the start stamp. The CSV times run from the start stamp"
                                : $"{fr.RecordedRate.Value:0.0} samples per second against the logger's clock, {off:+0.0;-0.0} % from {rate:0}: more than the board's clock tolerance" });
                    }
                }
                rep.Files.Add(fr);
            }
        }

        /// <summary>EXG2 (ADS1292) record: the 9-byte frame (VesperU5 ads1292.c).</summary>
        public const int Exg2RecordSize = 9;

        private static string FormatExg(Report rep, string session)
        {
            var c = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.AppendLine($"Recording check (ExG) - {session}");
            sb.AppendLine($"Checked: {rep.Files.Count} file(s), {rep.Files.Sum(f => f.AudioS).ToString("0.0", c)} s of data.");
            sb.AppendLine("Compares the sample count with the logger's crystal clock (HEADER / FOOTER time stamps).");
            sb.AppendLine();
            if (!rep.AnyProblem) sb.AppendLine("No problems found: no data lost between files, every record a valid sample, sample counts within the board's clock tolerance.");
            else
            {
                sb.AppendLine("Problems:");
                foreach (var f in rep.Findings) sb.AppendLine($"- {f.File}: {f.Text}.");
            }
            sb.AppendLine();
            sb.AppendLine("File      Rate SPS   Data s   Clock s   Gap before ms   Recorded SPS   Off %   Unreliable");
            foreach (var f in rep.Files)
            {
                string rec = f.RecordedRate.HasValue ? f.RecordedRate.Value.ToString("0.0", c) : "-";
                string off = f.RecordedRate.HasValue ? ((f.RecordedRate.Value / f.Rate - 1) * 100).ToString("+0.00;-0.00", c) : "-";
                string gap = f.GapBeforeMs.HasValue ? f.GapBeforeMs.Value.ToString("0.0", c) : "-";
                sb.AppendLine($"{f.File,-9} {f.Rate,8}   {f.AudioS.ToString("0.00", c),7}   {f.ClockS.ToString("0.00", c),7}   {gap,13}   {rec,12}   {off,5}   {f.Unreliable,10}");
            }
            sb.AppendLine();
            sb.AppendLine("Recorded SPS: samples per second of the logger's clock (files of 5 s or more). The board runs on its own clock,");
            sb.AppendLine($"accurate to +-0.5 % at room temperature and +-2 % over 0-70 C: within that the rate is normal; beyond {ExgRateTolerancePct:0.0} % it is reported.");
            sb.AppendLine("Gap before: from the end of the previous file to the start of this one; one sample period is no loss.");
            return sb.ToString();
        }

        /// <summary>The report as text: the findings first, then a per-file table.</summary>
        public static string Format(Report rep, string session)
        {
            if (rep.IsExg) return FormatExg(rep, session);
            var sb = new StringBuilder();
            sb.AppendLine($"Recording check - {session}");
            sb.AppendLine($"Checked: {rep.Files.Count} file(s), {rep.Files.Sum(f => f.AudioS):0.0} s of audio.");
            sb.AppendLine("Compares the sample count with the logger's crystal clock (HEADER / SYNC / FOOTER time stamps).");
            sb.AppendLine();
            if (!rep.AnyProblem) sb.AppendLine("No problems found: sample clock locked, no crystal stalls, normal file changes.");
            else
            {
                sb.AppendLine("Problems:");
                foreach (var f in rep.Findings) sb.AppendLine($"- {f.File} at {f.AtS:0.0} s: {f.Text}.");
            }
            sb.AppendLine();
            sb.AppendLine("File      Rate Hz   Audio s   Clock s   Gap before ms   Sample clock ppm per 10 s");
            foreach (var f in rep.Files)
                sb.AppendLine($"{f.File,-9} {f.Rate,7}   {f.AudioS,7:0.00}   {f.ClockS,7:0.00}   {(f.GapBeforeMs.HasValue ? f.GapBeforeMs.Value.ToString("0.0", CultureInfo.InvariantCulture) : "-"),13}   " +
                              string.Join(" ", f.WindowPpm.Select(p => p.ToString("+0;-0", CultureInfo.InvariantCulture))));
            sb.AppendLine();
            sb.AppendLine("Locked sample clock: within about +-300 ppm (crystal tolerance). A normal file change loses about 19 ms.");
            return sb.ToString();
        }

        /// <summary>Checks the microphone raws among <paramref name="rawFiles"/>, one run per folder, and writes
        /// <see cref="ReportName"/> into that folder's AUD output folder. Returns (folder, report) per run.</summary>
        public static List<(string Folder, Report Report)> CheckAndWrite(IEnumerable<string> rawFiles)
        {
            var result = new List<(string, Report)>();
            foreach (var g in rawFiles.Where(IsMicRaw).GroupBy(f => Path.GetDirectoryName(f) ?? "", StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    Report rep = Check(g);
                    if (rep.Files.Count == 0) continue;
                    string outDir = Path.Combine(g.Key, "AUD");
                    Directory.CreateDirectory(outDir);
                    File.WriteAllText(Path.Combine(outDir, ReportName), Format(rep, g.Key), Encoding.UTF8);
                    result.Add((g.Key, rep));
                }
                catch { /* a check that cannot run must not stop the decode */ }
            }
            foreach (var g in rawFiles.Where(IsExgRaw).GroupBy(f => Path.GetDirectoryName(f) ?? "", StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    Report rep = CheckExg(g);
                    if (rep.Files.Count == 0) continue;
                    string outDir = Path.Combine(g.Key, rep.Folder);
                    Directory.CreateDirectory(outDir);
                    File.WriteAllText(Path.Combine(outDir, ReportName), Format(rep, g.Key), Encoding.UTF8);
                    result.Add((g.Key, rep));
                }
                catch { /* a check that cannot run must not stop the decode */ }
            }
            return result;
        }
    }
}
