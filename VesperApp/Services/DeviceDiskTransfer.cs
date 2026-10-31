using System;
using System.IO;
using System.Text.Json;
using VesperApp.Services.Time;

namespace VesperApp.Services
{
    /// <summary>
    /// Upload Config / Download Data for loggers that show up as a USB disk (Vesper, Pipistrelle, KOL in Idle).
    /// The Nanotag has no disk: its config and data go over the USB vendor interface (LoggerDevice). For a disk
    /// logger, uploading a config is copying it to <c>config.json</c> in the root of the unit's drive, the file
    /// the firmware reads at boot (KOL with the operating states also when asked to re-read it); downloading
    /// data is the guided import of the drive (RecordingParsingViewModel). The drive is the one whose UID.TXT
    /// names the unit (<see cref="UnitDisk.FindRoot"/>).
    /// </summary>
    public static class DeviceDiskTransfer
    {
        public sealed record Result(bool Ok, string Message);

        public const string DriveNotFound =
            "The unit's drive was not found. Connect it in Idle (USB disk); a KOL in Live Audio has no disk.";

        /// <summary>Write <paramref name="json"/> as config.json in <paramref name="root"/> (the unit's drive).
        /// The text must be valid JSON; it is written as given (no reformatting). An existing config.json is
        /// replaced, whatever the case of its name on the FAT volume.</summary>
        public static Result WriteConfig(string? root, string json)
        {
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
                return new Result(false, DriveNotFound);
            if (string.IsNullOrWhiteSpace(json))
                return new Result(false, "The configuration file is empty.");
            try
            {
                using JsonDocument doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind != JsonValueKind.Object)
                    return new Result(false, "The configuration file is not a JSON object.");
            }
            catch (JsonException ex)
            {
                return new Result(false, "The configuration file is not valid JSON: " + ex.Message);
            }

            try
            {
                string target = Path.Combine(root, UnitDisk.ConfigFile);
                string tmp = Path.Combine(root, "config.tmp");
                File.WriteAllText(tmp, json);
                foreach (string f in Directory.EnumerateFiles(root, "*", new EnumerationOptions { MatchCasing = MatchCasing.CaseInsensitive }))
                {
                    if (string.Equals(Path.GetFileName(f), UnitDisk.ConfigFile, StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(Path.GetFileName(f), UnitDisk.ConfigFile, StringComparison.Ordinal))
                        File.Delete(f);                       // CONFIG.JSON on a case-sensitive mount (Linux)
                }
                File.Move(tmp, target, overwrite: true);
                return new Result(true, $"config.json written to {root}");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return new Result(false, "config.json could not be written: " + ex.Message);
            }
        }
    }
}
