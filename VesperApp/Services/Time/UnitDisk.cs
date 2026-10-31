using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace VesperApp.Services.Time
{
    /// <summary>
    /// The per-unit RTC calibration on the unit's own disk (asd-interfaces interfaces/rtc-time-sync.md).
    /// The RTC backup registers lose it with power, so it is also kept in two files there:
    /// <list type="bullet">
    /// <item><c>RTCCAL.TXT</c> — <c>{"uid","cerr","ctref",...}</c>, read at boot by KOL 1.8 / Vesper 1.22 and
    /// later; ignored by a unit whose UID differs (disk content copied from another unit).</item>
    /// <item><c>config.json</c> <c>"cdrift"</c> — the same correction as the raw CALR value older firmware
    /// applies at boot. A deployment config.json copied onto the unit later overwrites it; RTCCAL.TXT stays.</item>
    /// </list>
    /// The unit's drive is the one whose firmware-written UID.TXT holds the unit's serial.
    /// </summary>
    public static class UnitDisk
    {
        public const string CalibrationFile = "RTCCAL.TXT";
        public const string ConfigFile = "config.json";
        private const string UidFile = "UID.TXT";

        private const double PpbPerCalm = 1e9 / 1048576.0;          // 2^-20
        private const int CorrMaxPpb = 488281, CorrMinPpb = -487329;

        public sealed record DiskCalibration(int ErrorPpb, double? ReferenceC);

        /// <summary>Root of the mounted drive whose UID.TXT names <paramref name="uid"/>; null when not mounted.</summary>
        public static string? FindRoot(string uid)
        {
            foreach (DriveInfo d in DriveInfo.GetDrives())
            {
                try
                {
                    if (!d.IsReady || d.DriveType == DriveType.Network || d.DriveType == DriveType.CDRom) continue;
                    string? file = FindFile(d.RootDirectory.FullName, UidFile);
                    if (file == null) continue;
                    string line = File.ReadLines(file).FirstOrDefault(l => l.Trim().Length > 0) ?? "";
                    if (UidMatches(line, uid))
                        return d.RootDirectory.FullName;
                }
                catch { /* drive vanished or unreadable */ }
            }
            return null;
        }

        /// <summary>The first line of UID.TXT names <paramref name="uid"/>: the value alone or <c>key=value</c>
        /// (as the import dialog reads the same file), case and surrounding blanks ignored.</summary>
        public static bool UidMatches(string uidFileLine, string uid)
        {
            string line = uidFileLine.Trim();
            if (line.Contains('=')) line = line.Split('=', 2)[1].Trim();
            return line.Length > 0 && string.Equals(line, uid.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>The calibration in RTCCAL.TXT when it belongs to <paramref name="uid"/>.</summary>
        public static DiskCalibration? Read(string root, string uid)
        {
            try
            {
                string? file = FindFile(root, CalibrationFile);
                if (file == null) return null;
                using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(file));
                JsonElement r = doc.RootElement;
                if (!string.Equals(r.GetProperty("uid").GetString(), uid.Trim(), StringComparison.OrdinalIgnoreCase)) return null;
                int err = r.GetProperty("cerr").GetInt32();
                double? tref = r.TryGetProperty("ctref", out JsonElement t) && t.GetInt32() != short.MinValue ? t.GetInt32() / 100.0 : null;
                return new DiskCalibration(err, tref);
            }
            catch { return null; }
        }

        /// <summary>Write RTCCAL.TXT. Only "uid", "cerr" and "ctref" are read by the firmware; the rest is
        /// for people and kept as strings (the firmware's parser skips them).</summary>
        public static void Write(string root, string uid, CalibrationEstimate est)
        {
            var o = new JsonObject
            {
                ["uid"] = uid.Trim(),
                ["cerr"] = est.ErrorPpb,
            };
            if (est.ReferenceC.HasValue) o["ctref"] = (int)Math.Round(est.ReferenceC.Value * 100.0);
            o["date"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);
            o["span"] = est.SpanHours.ToString("0.0 'h'", CultureInfo.InvariantCulture);
            o["unc"] = (est.UncertaintyPpb / 1000.0).ToString("0.000 'ppm'", CultureInfo.InvariantCulture);
            File.WriteAllText(Path.Combine(root, CalibrationFile), o.ToJsonString());
        }

        public static bool Delete(string root)
        {
            string? file = FindFile(root, CalibrationFile);
            if (file == null) return false;
            File.Delete(file);
            return true;
        }

        /// <summary>Crystal error at the turnover temperature: what the legacy cdrift stands for
        /// (firmware rc_crystal_error: e(T) = e_ref - k[(T - T0)^2 - (Tref - T0)^2]).</summary>
        public static int ErrorAtTurnover(int errorPpb, double? referenceC, int curvaturePpbPerC2, double turnoverC)
        {
            if (!referenceC.HasValue) return errorPpb;
            double d = referenceC.Value - turnoverC;
            return (int)Math.Round(errorPpb + curvaturePpbPerC2 * d * d);
        }

        /// <summary>Raw RTC_CALR for a correction in ppb (+ = RTC sped up) — firmware rc_calr_for:
        /// bit 15 CALP (+512 pulses per 2^20), bits 0..8 CALM (pulses masked).</summary>
        public static uint CalrFor(int correctionPpb)
        {
            int c = Math.Clamp(correctionPpb, CorrMinPpb, CorrMaxPpb);
            int calp = c > 0 ? 1 : 0;
            int calm = c > 0 ? 512 - (int)(c / PpbPerCalm + 0.5) : (int)(-c / PpbPerCalm + 0.5);
            return (uint)((calp << 15) | Math.Clamp(calm, 0, 511));
        }

        /// <summary>Correction a raw cdrift/CALR value applies, in ppb.</summary>
        public static int CorrectionOf(uint calr) =>
            (int)Math.Round((((calr & 0x8000u) != 0 ? 512 : 0) - (int)(calr & 0x1FFu)) * PpbPerCalm);

        /// <summary>Set "cdrift" in the config.json on the unit's disk, leaving everything else as it is.
        /// Returns the previous value; throws when there is no config.json or it is not a JSON object.
        /// The file is not rewritten when it already holds <paramref name="cdrift"/>.</summary>
        public static uint? PatchConfigCdrift(string root, uint cdrift)
        {
            string file = FindFile(root, ConfigFile) ?? throw new FileNotFoundException("no config.json on the unit");
            JsonObject o = JsonNode.Parse(File.ReadAllText(file)) as JsonObject
                           ?? throw new InvalidDataException("config.json is not a JSON object");
            uint? previous = o["cdrift"] is JsonValue v && v.TryGetValue(out uint p) ? p : null;
            if (previous == cdrift) return previous;
            o["cdrift"] = cdrift;
            File.WriteAllText(file, o.ToJsonString(new JsonSerializerOptions { WriteIndented = false }));
            return previous;
        }

        /// <summary>FAT is case-insensitive, but a Linux mount may show the 8.3 name in either case.</summary>
        private static string? FindFile(string root, string name)
        {
            string direct = Path.Combine(root, name);
            if (File.Exists(direct)) return direct;
            return Directory.EnumerateFiles(root, "*", new EnumerationOptions { MatchCasing = MatchCasing.CaseInsensitive })
                            .FirstOrDefault(f => string.Equals(Path.GetFileName(f), name, StringComparison.OrdinalIgnoreCase));
        }
    }
}
