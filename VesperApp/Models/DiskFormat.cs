using System;
using System.Collections.Generic;

namespace VesperApp.Models
{
    /// <summary>File system the Format button asks a Vesper / Pipistrelle for (VESPER_FORMATDISK [0]).</summary>
    public enum DiskFileSystem : byte
    {
        Fat32 = 0,
        ExFat = 1,
    }

    /// <summary>Outcome of VESPER_FORMATDISK.</summary>
    public sealed record DiskFormatResult(DiskFormatOutcome Outcome, byte Status = 0, byte FormatCode = 0, byte OpenCode = 0,
                                          byte TypeNow = 0, uint DataStartSector = 0)
    {
        public static readonly DiskFormatResult NoAnswer = new(DiskFormatOutcome.NoAnswer);
        public static readonly DiskFormatResult Refused = new(DiskFormatOutcome.Refused);
        public static readonly DiskFormatResult LegacyStarted = new(DiskFormatOutcome.LegacyStarted);

        /// <summary>File system the unit reports after the format ([3]: 3 FAT32, 4 exFAT).</summary>
        public string TypeText => TypeNow switch { 3 => "FAT32", 4 => "exFAT", 1 => "FAT12", 2 => "FAT16", _ => "unknown" };
    }

    public enum DiskFormatOutcome
    {
        /// <summary>Formatted and the new volume opened; the unit resets now.</summary>
        Done,
        /// <summary>The unit tried and FileX failed (codes in the result); the unit resets now.</summary>
        Failed,
        /// <summary>The request was not valid for this unit (exFAT on a 16 GB unit, cluster size); nothing was written.</summary>
        BadRequest,
        /// <summary>NACK: armed, in Live Audio, or KOL firmware (no format).</summary>
        Refused,
        /// <summary>ACK: firmware before 1.22 - it formats FAT32 / 32 KB now and resets, with no result.</summary>
        LegacyStarted,
        /// <summary>No reply in time (or no console).</summary>
        NoAnswer,
    }

    /// <summary>The Vesper / Pipistrelle format command (VesperU5 ux_device_cdc_acm.c VESPER_FORMATDISK, fs_bench.h
    /// FsBench_FormatDisk) and what the Format button offers.</summary>
    public static class DiskFormat
    {
        /// <summary>First VesperU5 firmware that takes the file-system choice and answers with the result.</summary>
        public static readonly Version ChoiceFirmware = new(1, 22);

        /// <summary>exFAT is offered from this size on: the 32 / 64 GB eMMC (the 16 GB unit is about 14.6 GiB).
        /// Same threshold as the firmware (FSB_EXFAT_MIN_SECTORS, 20 GiB).</summary>
        public const ulong ExFatMinBytes = 20UL * 1024 * 1024 * 1024;

        /// <summary>The cluster size VesperApp formats with: one recorder write (32 KB half-buffer) per cluster.</summary>
        public const int DefaultClusterKb = 32;

        // reply [0] status (fs_bench.h FSB_*)
        private const byte FsbOk = 0, FsbArgs = 4;

        /// <summary>File systems to offer, first = default. FAT32 always; exFAT only with firmware 1.22+ on a
        /// disk of <see cref="ExFatMinBytes"/> or more (unknown size or firmware: FAT32 only).</summary>
        public static IReadOnlyList<DiskFileSystem> Choices(Version? firmware, ulong? diskBytes)
        {
            bool exfat = firmware != null && firmware >= ChoiceFirmware && diskBytes is ulong b && b >= ExFatMinBytes;
            return exfat ? new[] { DiskFileSystem.Fat32, DiskFileSystem.ExFat } : new[] { DiskFileSystem.Fat32 };
        }

        public static byte[] BuildRequest(DiskFileSystem fs, int clusterKb = 0) =>
            new[] { (byte)fs, (byte)(clusterKb is > 0 and <= 128 ? clusterKb : 0) };

        /// <summary>VESPER_GETDISKSIZE reply: [0..3] blocks, [4..7] block bytes (little-endian).</summary>
        public static ulong? ParseDiskSize(byte[]? r)
        {
            if (r == null || r.Length < 8) return null;
            ulong blocks = BitConverter.ToUInt32(r, 0);
            ulong size = BitConverter.ToUInt32(r, 4);
            if (blocks == 0 || size == 0) return null;
            return blocks * size;
        }

        /// <summary>The 8-byte result: [0] status, [1] FileX format code, [2] FileX open code, [3] type now,
        /// [4..7] data start sector.</summary>
        public static DiskFormatResult ParseReply(byte[]? r)
        {
            if (r == null || r.Length < 8) return DiskFormatResult.NoAnswer;
            DiskFormatOutcome o = r[0] == FsbOk ? DiskFormatOutcome.Done
                                : r[0] == FsbArgs ? DiskFormatOutcome.BadRequest
                                : DiskFormatOutcome.Failed;
            return new DiskFormatResult(o, r[0], r[1], r[2], r[3], BitConverter.ToUInt32(r, 4));
        }

        public static string Name(DiskFileSystem fs) => fs == DiskFileSystem.ExFat ? "exFAT" : "FAT32";

        /// <summary>Status-bar text for a result.</summary>
        public static string Message(DiskFormatResult r, DiskFileSystem asked) => r.Outcome switch
        {
            DiskFormatOutcome.Done => $"Format done: {r.TypeText}, {DefaultClusterKb} KB clusters. The unit restarts; upload the configuration again.",
            DiskFormatOutcome.Failed => $"Format failed (status {r.Status}, FileX {r.FormatCode:X2}/{r.OpenCode:X2}). The unit restarts; try again, and report it if it repeats.",
            DiskFormatOutcome.BadRequest => $"The unit refused {Name(asked)} (not offered for its storage size). Nothing was erased.",
            DiskFormatOutcome.Refused => "The unit refused the format: it is recording, armed or in Live Audio. Nothing was erased.",
            DiskFormatOutcome.LegacyStarted => "Format started (FAT32, firmware before 1.22). The unit restarts when done; upload the configuration again.",
            _ => "The unit did not report the end of the format. Wait until it is back in the device list, then check its drive.",
        };
    }
}
