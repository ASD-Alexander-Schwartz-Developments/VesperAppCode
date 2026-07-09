using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ASDWaveLib;
using VesperApp.Models;

namespace VesperApp.Services
{
    /// <summary>Tally of what an Auto Decode run produced, for a friendly summary.</summary>
    public class DecodeSummary
    {
        public int RawParsed;
        public int Wav;
        public int Csv;
        public int Image;
        public int GnssSets;
        public int Skipped;
        public int Failed;

        public override string ToString()
        {
            var parts = new List<string>();
            if (Wav > 0) parts.Add($"{Wav} WAV");
            if (Csv > 0) parts.Add($"{Csv} CSV");
            if (Image > 0) parts.Add($"{Image} image" + (Image == 1 ? "" : "s"));
            if (GnssSets > 0) parts.Add($"{GnssSets} GNSS fix-set" + (GnssSets == 1 ? "" : "s"));
            string made = parts.Count > 0 ? string.Join(", ", parts) : "nothing to decode";
            string tail = Failed > 0 ? $" · {Failed} failed" : "";
            return $"Decoded {RawParsed} raw file(s) → {made}{tail}.";
        }
    }

    /// <summary>
    /// One-click "Auto Decode": runs the BinaryParser strip/split phase on raw logger
    /// .bin files, then decodes every produced intermediate with the right parser
    /// (chosen by file type + the <c>Sensor:</c> metadata the parser writes). Mirrors
    /// the per-type mapping used by the manual decoders so the output is identical —
    /// it just removes the manual second step. Pure file/CPU work, safe off the UI thread.
    /// </summary>
    public static class RecordingPipeline
    {
        // Intermediate extensions we know how to decode (others, e.g. .SBN/.OBN/.XBN, are left as parsed bins).
        private static readonly string[] DecodableExts = { ".UBN", ".MBN", ".ABN", ".LBN", ".RBN", ".EBN", ".CBN" };

        public static async Task<DecodeSummary> AutoDecodeAsync(IReadOnlyList<string> rawFiles, IProgress<int>? progress = null)
        {
            var sum = new DecodeSummary();
            var touched = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int total = Math.Max(1, rawFiles.Count);
            int done = 0;

            // Phase 1: raw .bin -> intermediates (0..50%)
            foreach (string raw in rawFiles)
            {
                try
                {
                    foreach (string f in await ParseRawAsync(raw)) touched.Add(f);
                    sum.RawParsed++;
                }
                catch { sum.Failed++; }
                progress?.Report((int)(50.0 * ++done / total));
            }

            // Phase 2: decode the produced intermediates (50..100%)
            var datFolders = touched.Where(IsSnapFolder).ToList();
            var binFolders = touched.Where(f => !IsSnapFolder(f)).ToList();

            var intermediates = binFolders
                .Where(Directory.Exists)
                .SelectMany(d => Directory.GetFiles(d))
                .Where(f => DecodableExts.Contains(Path.GetExtension(f).ToUpperInvariant()))
                .ToList();

            int total2 = Math.Max(1, intermediates.Count + datFolders.Count);
            done = 0;
            foreach (string file in intermediates)
            {
                DecodeIntermediate(file, sum);
                progress?.Report(50 + (int)(50.0 * ++done / total2));
            }
            foreach (string dat in datFolders)
            {
                await DecodeSnapFolderAsync(dat, sum);
                progress?.Report(50 + (int)(50.0 * ++done / total2));
            }

            progress?.Report(100);
            return sum;
        }

        /// <summary>Parse raw logger .bin files into intermediates only — the strip/split
        /// half of the pipeline, without decoding. Used by "Parse only" on a browser
        /// selection; mirrors phase 1 of <see cref="AutoDecodeAsync"/>.</summary>
        public static async Task<DecodeSummary> ParseRawFilesAsync(IReadOnlyList<string> rawFiles, IProgress<int>? progress = null)
        {
            var sum = new DecodeSummary();
            int total = Math.Max(1, rawFiles.Count);
            int done = 0;

            foreach (string raw in rawFiles)
            {
                try { await ParseRawAsync(raw); sum.RawParsed++; }
                catch { sum.Failed++; }
                progress?.Report((int)(100.0 * ++done / total));
            }

            progress?.Report(100);
            return sum;
        }

        /// <summary>Decode already-parsed targets: intermediate files (.UBN/.MBN/…) and/or
        /// DAT snap folders. Per-item entry point for "Decode" on a browser selection —
        /// same decoders phase 2 of <see cref="AutoDecodeAsync"/> uses.</summary>
        public static async Task<DecodeSummary> DecodeFilesAsync(IReadOnlyList<string> targets, IProgress<int>? progress = null)
        {
            var sum = new DecodeSummary();
            int total = Math.Max(1, targets.Count);
            int done = 0;

            foreach (string t in targets)
            {
                if (Directory.Exists(t) && IsSnapFolder(t)) await DecodeSnapFolderAsync(t, sum);
                else if (File.Exists(t) && IsDecodableIntermediate(t)) DecodeIntermediate(t, sum);
                else sum.Skipped++;
                progress?.Report((int)(100.0 * ++done / total));
            }

            progress?.Report(100);
            return sum;
        }

        /// <summary>Find raw logger .bin files under a folder (recursively) for Auto Import → Auto Decode.</summary>
        public static List<string> FindRawBinFiles(string folder)
        {
            try
            {
                // Devices write FAT 8.3 names, i.e. uppercase ".BIN" — match case-insensitively
                // (Windows does implicitly, Linux does not).
                return Directory.EnumerateFiles(folder, "*.bin", new EnumerationOptions
                    { RecurseSubdirectories = true, MatchCasing = MatchCasing.CaseInsensitive })
                    .Where(IsRawBin)
                    .ToList();
            }
            catch { return new List<string>(); }
        }

        /// <summary>True for an intermediate file the decode phase knows how to turn
        /// into WAV/CSV/image (.UBN/.MBN/.ABN/.LBN/.RBN/.EBN/.CBN).</summary>
        public static bool IsDecodableIntermediate(string path) =>
            DecodableExts.Contains(Path.GetExtension(path).ToUpperInvariant());

        /// <summary>True for a folder of GNSS snapshot .dat files (named "DAT").</summary>
        public static bool IsGnssSnapFolder(string folder) => IsSnapFolder(folder);

        public static bool IsRawBin(string path)
        {
            // Logger raws end in <type-letter>.bin (G/U/U0-3/M/E/R/L/X/O/S/C). Skip our own
            // intermediates (.UBN etc.) and decode outputs.
            string n = Path.GetFileName(path).ToUpperInvariant();
            return n.EndsWith(".BIN") && !n.EndsWith("BN.BIN");
        }

        private static bool IsSnapFolder(string folder) =>
            string.Equals(Path.GetFileName(folder), "DAT", StringComparison.OrdinalIgnoreCase);

        // ───────────────────────── phase 1: parse ─────────────────────────

        private static async Task<IEnumerable<string>> ParseRawAsync(string rawPath)
        {
            var touched = new List<string>();
            string? dir = Path.GetDirectoryName(rawPath);
            if (dir == null) return touched;

            string name = Path.GetFileName(rawPath).ToUpperInvariant();

            string Sub(string sub)
            {
                string p = Path.Combine(dir, sub);
                Directory.CreateDirectory(p);
                touched.Add(p);
                return p;
            }

            if (name.Contains("G.BIN")) await BinaryParser.ExtractVesperSnap(rawPath, Sub("DAT"), TimeSpan.Zero);
            else if (name.Contains("U0.BIN")) await BinaryParser.StripSplitEx(rawPath, '0', Sub("KOL-AUD"), 0);
            else if (name.Contains("U1.BIN")) await BinaryParser.StripSplitEx(rawPath, '1', Sub("KOL-AUD"), 0);
            else if (name.Contains("U2.BIN")) await BinaryParser.StripSplitEx(rawPath, '2', Sub("KOL-AUD"), 0);
            else if (name.Contains("U3.BIN")) await BinaryParser.StripSplitEx(rawPath, '3', Sub("KOL-AUD"), 0);
            else if (name.Contains("U.BIN")) await BinaryParser.StripSplit(rawPath, Sub("AUD"), 0);
            else if (name.Contains("M.BIN")) await BinaryParser.StripSplit(rawPath, Sub("IMU"), 0);
            else if (name.Contains("E.BIN")) await BinaryParser.StripSplit(rawPath, Sub("EXG"), 0);
            else if (name.Contains("R.BIN")) await BinaryParser.StripSplit(rawPath, Sub("TPRH"), 0);
            else if (name.Contains("L.BIN")) await BinaryParser.StripSplit(rawPath, Sub("ALS"), 0);
            else if (name.Contains("C.BIN")) await BinaryParser.StripSplit(rawPath, Sub("THCAM"), 0);
            else if (name.Contains("X.BIN")) await BinaryParser.StripSplit(rawPath, Sub("PRX"), 0);
            else if (name.Contains("O.BIN")) await BinaryParser.StripSplit(rawPath, Sub("LOG"), 0);
            else if (name.Contains("S.BIN")) await BinaryParser.StripSplit(rawPath, Sub("SNS"), 0);

            return touched;
        }

        // ───────────────────────── phase 2: decode ─────────────────────────

        private static void DecodeIntermediate(string path, DecodeSummary sum)
        {
            try
            {
                string ext = Path.GetExtension(path).ToUpperInvariant();
                string name = Path.GetFileName(path).ToUpperInvariant();
                byte[] data = File.ReadAllBytes(path);

                switch (ext)
                {
                    case ".UBN":
                        using (var wf = new WaveFile(path, ReadMetaText(path)))
                        {
                            wf.Open();
                            wf.WriteWave(data);
                        }
                        sum.Wav++;
                        break;

                    case ".MBN":
                        using (var ip = new IMU10Parser(path, data, StartTime(name, "M"), 1023, RateMsPerSample(path)))
                            ip.WriteFile();
                        sum.Csv++;
                        break;

                    case ".ABN":
                        using (var ip = new NanoAccParser(path, data, StartTime(name, "NACC."), 1023, RateMsPerSample(path)))
                            ip.WriteFile();
                        sum.Csv++;
                        break;

                    case ".LBN":
                        using (var ip = new ALSParser(path, data, StartTime(name, "L"), 1023, MetaUint(path, "SampleRate")))
                            ip.WriteFile();
                        sum.Csv++;
                        break;

                    case ".RBN":
                        using (var ip = new TPHParser(path, data, StartTime(name, "R"), 1023, MetaUint(path, "SampleRate")))
                            ip.WriteFile();
                        sum.Csv++;
                        break;

                    case ".EBN":
                        DecodeExg(path, data, name, sum);
                        break;

                    case ".CBN":
                        var lr = new LeptonReading(path, data, 1024 - 16, DateTime.Now, 0, 0, LeptonFilterType.LEPTON_RAINBOW);
                        lr.SaveAs(OutputFileType.PIC_JPG, path);
                        sum.Image++;
                        break;

                    default:
                        sum.Skipped++;
                        break;
                }
            }
            catch { sum.Failed++; }
        }

        private static void DecodeExg(string path, byte[] data, string name, DecodeSummary sum)
        {
            // .EBN is shared by the EXG48 and the EXG1292 (2-channel) sensors; the
            // Sensor: metadata the parser wrote tells them apart.
            string? sensor = MetaText(path, "Sensor");
            DateTime start = StartTime(name, "E");
            uint sr = MetaUint(path, "SampleRate");

            if (sensor != null && sensor.Trim().Equals("EXG2", StringComparison.OrdinalIgnoreCase))
            {
                using var ip = new EXG1292Parser(path, data, start, 1, Exg1292UsPerSample(sr));
                ip.WriteFile();
            }
            else
            {
                using var ip = new EXG48Parser(path, data, start, 1, sr > 0 ? 1000 / sr : 0);
                ip.WriteFile();
            }
            sum.Csv++;
        }

        private static async Task DecodeSnapFolderAsync(string datFolder, DecodeSummary sum)
        {
            try
            {
                var gnss = ASD.Platform.PlatformServices.Gnss;
                if (gnss == null || !gnss.IsAvailable) { sum.Skipped++; return; }

                // Route through the shared job manager so this decode shows up in the unified
                // Decoding Progress panel with live progress/log; then await its outcome so the
                // batch summary stays accurate.
                DecodeJob job = DecodeJobManager.Instance.StartGnss(datFolder, Path.Combine(datFolder, "decode"));
                DecodeOutcome res = await job.Completion;

                if (res.Succeeded) sum.GnssSets++;
                else sum.Failed++;
            }
            catch { sum.Failed++; }
        }

        // ───────────────────────── metadata helpers ─────────────────────────

        private static string ReadMetaText(string intermediatePath)
        {
            string meta = intermediatePath + ".txt";
            return File.Exists(meta) ? File.ReadAllText(meta, Encoding.UTF8) : string.Empty;
        }

        private static string? MetaText(string intermediatePath, string key)
        {
            string meta = intermediatePath + ".txt";
            if (!File.Exists(meta)) return null;
            foreach (string raw in File.ReadAllLines(meta))
            {
                string line = raw.Trim();
                if (line.StartsWith(key + ":", StringComparison.OrdinalIgnoreCase))
                    return line.Substring(line.IndexOf(':') + 1).Trim();
            }
            return null;
        }

        private static uint MetaUint(string intermediatePath, string key)
            => uint.TryParse(MetaText(intermediatePath, key), out uint v) ? v : 0u;

        private static uint RateMsPerSample(string intermediatePath)
        {
            uint sr = MetaUint(intermediatePath, "SampleRate");
            return sr > 0 ? 1000 / sr : 0;
        }

        private static uint Exg1292UsPerSample(uint sampleRate)
        {
            // Matches the manual EXG1292 decoder's rate table (index = SampleRate-1).
            return (sampleRate == 0 ? 0u : sampleRate - 1) switch
            {
                0 => 1000000u / 125,
                1 => 1000000u / 250,
                2 => 1000000u / 500,
                3 => 1000000u / 1000,
                4 => 1000000u / 2000,
                5 => 1000000u / 4000,
                6 => 1000000u / 8000,
                _ => 1000000u / 125,
            };
        }

        private static DateTime StartTime(string filename, string prefix)
        {
            try
            {
                string pattern = prefix.EndsWith(".")
                    ? prefix + "%d_%d_%d_%d_%d_%d_%d.ABN"
                    : prefix + "%d_%d_%d_%d_%d_%d_%d";

                ArrayList parts = Utils.scan(filename, pattern);
                if (parts.Count >= 6)
                {
                    int Y = Convert.ToInt32(parts[0]);
                    int Mo = Convert.ToInt32(parts[1]);
                    int D = Convert.ToInt32(parts[2]);
                    int H = Convert.ToInt32(parts[3]);
                    int Mi = Convert.ToInt32(parts[4]);
                    int S = Convert.ToInt32(parts[5]);
                    int Ms = parts.Count >= 7 ? Convert.ToInt32(parts[6]) : 0;
                    return new DateTime(Y, Mo, D, H, Mi, S, Ms);
                }
            }
            catch { }
            return DateTime.Now;
        }
    }
}
