using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Nodes;
using VesperApp.Models;

namespace VesperApp.Services
{
    /// <summary>
    /// GET_DISKHEALTH 0x0D reply (Nanotag firmware 1.39; asd-interfaces interfaces/nanotag-usb-vendor.md):
    /// 9 x u32 little endian, then free %, flags, format %.
    /// </summary>
    public sealed record NanotagDiskHealth(
        uint BadBlocks, uint GoodBlocks, uint BlocksToRetire,
        uint EccCorrected, uint EccUncorrectable, uint ReadErrors,
        uint FirstDataPage, uint WritePage, uint DataEndPage,
        byte FreePercent, bool Formatting, bool DataAreaFull, byte FormatPercent)
    {
        public const int Length = 40;

        public uint PagesWritten => WritePage > FirstDataPage ? WritePage - FirstDataPage : 0;

        public static NanotagDiskHealth? Parse(byte[]? p)
        {
            if (p == null || p.Length < Length) return null;
            uint U(int i) => BitConverter.ToUInt32(p, i * 4);
            return new NanotagDiskHealth(U(0), U(1), U(2), U(3), U(4), U(5), U(6), U(7), U(8),
                                         p[36], (p[37] & 0x01) != 0, (p[37] & 0x02) != 0, p[38]);
        }
    }

    public static class NanotagService
    {
        /// <summary>GET_DATACHUNK reply byte 1 (firmware 1.39): the read status of the page.</summary>
        public const byte PageOk = 0, PageCorrected = 1, PageCorrectedHigh = 2, PageUncorrectable = 3, PageReadError = 0xEE;

        public static string PageStatusName(byte s) => s switch
        {
            PageOk => "OK",
            PageCorrected => "ECC corrected",
            PageCorrectedHigh => "ECC corrected (at / above the threshold)",
            PageUncorrectable => "uncorrectable ECC error",
            PageReadError => "read error",
            _ => $"status 0x{s:X2}",
        };

        /// <summary>
        /// The config rows read back from a Nanotag (GET_CFGCHUNK_GEN / _DEV / _SCH, 256 B each) as a readable
        /// JSON report: what the tag will run, for checking an upload. Not a config.json to upload again.
        /// Layout: Models/Nanotag.cs NanotagGeneralConfiguration / NanotagDeviceConfigParams / NanotagScheduleEntry.
        /// </summary>
        public static JsonObject DescribeConfig(byte[] general, byte[] devices, byte[] schedule)
        {
            if (general.Length < 64 || devices.Length < 256 || schedule.Length < 256)
                throw new ArgumentException("a config row is shorter than the firmware's");

            int U16(byte[] b, int o) => BitConverter.ToUInt16(b, o);              // plain JSON numbers (int / long)
            long U32(byte[] b, int o) => BitConverter.ToUInt32(b, o);

            ushort type = BitConverter.ToUInt16(general, 8);
            uint poweron = BitConverter.ToUInt32(general, 18);
            var g = new JsonObject
            {
                ["minimum_hw_ver"] = U16(general, 0),
                ["magnet_off"] = general[2] != 0,
                ["battery_capacity_mAh"] = U16(general, 4),
                ["schedule_type"] = ScheduleTypeName(type),
                ["delayed_start"] = poweron == 0 ? "none" : FormatPacked(poweron, (poweron >> 26) == 0 ? PackedKind.Relative : PackedKind.Absolute),
                ["tcomp"] = general[11] != 0,
                ["tck_ppb_per_C2"] = U16(general, 12),
                ["tct0_0p1C"] = (int)BitConverter.ToInt16(general, 14),
                ["tcper_s"] = U16(general, 16),
            };

            var slots = new JsonArray();
            for (int i = 0; i < Nanotag.MAX_LOADED_DEVICES; i++)
            {
                int o = i * 64;
                var name = new StringBuilder();
                for (int c = 0; c < Nanotag.DEVICE_NAME_LENGTH && devices[o + c] != 0 && devices[o + c] != 0xFF; c++)
                    name.Append((char)devices[o + c]);
                if (name.Length == 0) continue;
                slots.Add(new JsonObject
                {
                    ["slot"] = i + 1,
                    ["driver"] = name.ToString(),
                    ["sample_rate"] = new JsonArray(U32(devices, o + 16), U32(devices, o + 20)),
                    ["window_len"] = new JsonArray(U32(devices, o + 24), U32(devices, o + 28)),
                    ["window_rate"] = new JsonArray(U32(devices, o + 32), U32(devices, o + 36)),
                    ["control_bitmask"] = $"0x{U32(devices, o + 44):X8}",
                    ["raw_data"] = new JsonArray($"0x{U32(devices, o + 48):X8}", $"0x{U32(devices, o + 52):X8}",
                                                 $"0x{U32(devices, o + 56):X8}", $"0x{U32(devices, o + 60):X8}"),
                });
            }

            var entries = new JsonArray();
            for (int i = 0; i < Nanotag.MAX_SCHEDULE_SIZE; i++)
            {
                uint t = BitConverter.ToUInt32(schedule, i * 8);
                uint cfg = BitConverter.ToUInt32(schedule, i * 8 + 4);
                if (t == 0xFFFFFFFF) break;
                entries.Add(new JsonObject
                {
                    ["time"] = FormatPacked(t, type == 5 ? PackedKind.Relative : type == 3 ? PackedKind.Daily : PackedKind.Absolute),
                    ["config"] = (int)(cfg & 0xFF),
                });
            }

            return new JsonObject { ["general"] = g, ["drivers"] = slots, ["schedule"] = entries };
        }

        /// <summary>The disk health as lines for the Nanotag page.</summary>
        public static string Describe(NanotagDiskHealth h)
        {
            uint dataPages = h.DataEndPage > h.FirstDataPage ? h.DataEndPage - h.FirstDataPage : 0;
            var sb = new StringBuilder();
            sb.AppendLine($"Blocks: {h.GoodBlocks} good, {h.BadBlocks} bad, {h.BlocksToRetire} to retire at the next format");
            sb.AppendLine($"Data: {h.PagesWritten} of {dataPages} pages written, {h.FreePercent} % free" +
                          (h.DataAreaFull ? " (full: recording stops)" : ""));
            sb.AppendLine($"Page reads since the tag started: {h.EccCorrected} ECC-corrected, {h.EccUncorrectable} uncorrectable, {h.ReadErrors} read errors");
            if (h.Formatting) sb.AppendLine($"Format in progress: {h.FormatPercent} %");
            if (h.EccUncorrectable > 0 || h.ReadErrors > 0)
                sb.AppendLine("Some pages could not be read cleanly: download the data, then format before the next deployment.");
            return sb.ToString().TrimEnd();
        }

        public static string ScheduleTypeName(ushort t) => t switch
        {
            0 => "continuous",
            1 => "triggered (legacy)",
            2 => "dated",
            3 => "daily",
            4 => "weekly (legacy)",
            5 => "relative",
            _ => $"unknown ({t})",
        };

        public enum PackedKind { Absolute, Relative, Daily }

        /// <summary>A packed SAML21 RTC time (year since 2020 / month / day / h:m:s): "yyyy-MM-dd hh:mm:ss";
        /// relative "+d d hh:mm:ss" (day = offset from the start); daily "hh:mm:ss".</summary>
        public static string FormatPacked(uint t, PackedKind kind)
        {
            uint s = t & 0x3F, m = (t >> 6) & 0x3F, h = (t >> 12) & 0x1F, d = (t >> 17) & 0x1F, mo = (t >> 22) & 0x0F, y = (t >> 26) & 0x3F;
            return kind switch
            {
                PackedKind.Relative => $"+{d} d {h:00}:{m:00}:{s:00}",
                PackedKind.Daily => $"{h:00}:{m:00}:{s:00}",
                _ => $"{2020 + y:0000}-{mo:00}-{d:00} {h:00}:{m:00}:{s:00}",
            };
        }
    }

    /// <summary>Command history of the console page (up / down like a terminal).</summary>
    public sealed class ConsoleHistory
    {
        private readonly List<string> _items = new();
        private readonly int _max;
        private int _pos;

        public ConsoleHistory(int max = 50) { _max = max; }

        public IReadOnlyList<string> Items => _items;

        public void Add(string line)
        {
            line = line.Trim();
            if (line.Length > 0 && (_items.Count == 0 || _items[^1] != line))
            {
                _items.Add(line);
                if (_items.Count > _max) _items.RemoveAt(0);
            }
            _pos = _items.Count;
        }

        /// <summary>The previous command (up arrow); the oldest one stays.</summary>
        public string? Previous()
        {
            if (_items.Count == 0) return null;
            if (_pos > 0) _pos--;
            return _items[_pos];
        }

        /// <summary>The next command (down arrow); past the newest: empty line.</summary>
        public string Next()
        {
            if (_pos < _items.Count) _pos++;
            return _pos < _items.Count ? _items[_pos] : string.Empty;
        }
    }
}
