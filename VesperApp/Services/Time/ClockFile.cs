using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace VesperApp.Services.Time
{
    /// <summary>
    /// CLOCK.TXT written by a Vesper / Pipistrelle / KOL at every armed start: the unit's time and its clock base
    /// (asd-interfaces rtc-time-sync.md § Clock base). The base of a recording is the line at or before its start.
    /// <code>
    /// # recording start (unit time), clock: UTC / local UTC+hh:mm (UTC = time - offset) / unknown
    /// 2026-11-01 09:14:48 UTC
    /// 2026-11-02 11:00:05 local UTC+02:00
    /// </code>
    /// </summary>
    public sealed class ClockFile
    {
        public const string FileName = "CLOCK.TXT";

        private readonly List<(DateTime Start, DeviceClockBase Base)> _entries;

        private ClockFile(List<(DateTime, DeviceClockBase)> entries) { _entries = entries; }

        public IReadOnlyList<(DateTime Start, DeviceClockBase Base)> Entries => _entries;

        public static ClockFile Parse(IEnumerable<string> lines)
        {
            var list = new List<(DateTime, DeviceClockBase)>();
            foreach (string raw in lines)
            {
                string l = raw.Trim();
                if (l.Length < 20 || l.StartsWith("#")) continue;
                if (!DateTime.TryParseExact(l.Substring(0, 19), "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out DateTime start))
                    continue;
                list.Add((start, ParseBase(l.Substring(19).Trim())));
            }
            list.Sort((a, b) => a.Item1.CompareTo(b.Item1));
            return new ClockFile(list);
        }

        /// <summary>"UTC", "local UTC+02:00" / "local UTC-03:30", anything else unknown.</summary>
        internal static DeviceClockBase ParseBase(string s)
        {
            if (s.Equals("UTC", StringComparison.OrdinalIgnoreCase)) return DeviceClockBase.Utc;
            const string pre = "local UTC";
            if (s.StartsWith(pre, StringComparison.OrdinalIgnoreCase) && s.Length >= pre.Length + 6)
            {
                string z = s.Substring(pre.Length);                 // +hh:mm
                int sign = z[0] == '-' ? -1 : z[0] == '+' ? 1 : 0;
                if (sign != 0 && int.TryParse(z.Substring(1, 2), out int h) && int.TryParse(z.Substring(4, 2), out int m) && m < 60)
                    return DeviceClockBase.FromWire((byte)ClockBaseKind.Local, (short)(sign * (h * 60 + m)));
            }
            return DeviceClockBase.Unknown;
        }

        /// <summary>The base in force at <paramref name="deviceTime"/> (the last start at or before it), or unknown.</summary>
        public DeviceClockBase BaseAt(DateTime deviceTime)
        {
            DeviceClockBase b = DeviceClockBase.Unknown;
            foreach (var (start, bs) in _entries)
            {
                if (start > deviceTime) break;
                b = bs;
            }
            return b;
        }

        /// <summary>CLOCK.TXT in the folder of <paramref name="path"/> or up to four folders above it (an import
        /// copies the whole drive: the recordings sit below the copied root), or null.</summary>
        public static ClockFile? FindFor(string path)
        {
            try
            {
                DirectoryInfo? d = File.Exists(path) ? new FileInfo(path).Directory : new DirectoryInfo(path);
                for (int i = 0; i < 5 && d != null; i++, d = d.Parent)
                {
                    FileInfo? f = d.EnumerateFiles(FileName, new EnumerationOptions { MatchCasing = MatchCasing.CaseInsensitive })
                                   .FirstOrDefault();
                    if (f != null) return Parse(File.ReadAllLines(f.FullName));
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            return null;
        }
    }
}
