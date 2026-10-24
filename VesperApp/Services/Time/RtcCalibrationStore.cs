using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace VesperApp.Services.Time
{
    public enum SyncPointKind { Measured, AfterSync }

    /// <summary>One offset measurement of a unit against true time.</summary>
    public sealed class SyncPoint
    {
        public DateTime TrueUtc { get; set; }
        /// <summary>Device clock minus true time in the device's own calendar (UTC or local).</summary>
        public double OffsetSeconds { get; set; }
        /// <summary>Calendar offset of the device clock from UTC at that moment (0 when the device runs UTC).
        /// Offset + zone * 3600 is continuous across daylight-saving changes.</summary>
        public double ZoneOffsetHours { get; set; }
        public double MinDelayMs { get; set; }
        public double SpreadMs { get; set; }
        public double ReferenceUncertaintyMs { get; set; }
        public double? TemperatureC { get; set; }
        public int AppliedPpb { get; set; }
        public int Segment { get; set; }
        public SyncPointKind Kind { get; set; }
        public string Reference { get; set; } = "";

        public double ContinuousOffsetSeconds => OffsetSeconds + ZoneOffsetHours * 3600.0;
    }

    /// <summary>The calibration last written to a unit (the master copy - the device keeps it in
    /// backup registers that are lost together with its RTC time).</summary>
    public sealed class StoredCalibration
    {
        public int ErrorPpb { get; set; }
        public double? ReferenceC { get; set; }
        public DateTime AppliedUtc { get; set; }
        public double SpanHours { get; set; }
        public double UncertaintyPpb { get; set; }
    }

    public sealed class UnitClockRecord
    {
        public string UnitKey { get; set; } = "";
        /// <summary>Incremented whenever the clock is adjusted; drift is only computed inside a segment.</summary>
        public int Segment { get; set; }
        public List<SyncPoint> Points { get; set; } = new();
        public StoredCalibration? Calibration { get; set; }
    }

    /// <summary>Per-unit clock history and calibration, %LOCALAPPDATA%/VesperApp/rtc-calibration.json.</summary>
    public sealed class RtcCalibrationStore
    {
        private const int MaxPointsPerUnit = 500;
        private readonly object _lock = new();
        private readonly string _path;
        private Dictionary<string, UnitClockRecord> _units = new(StringComparer.OrdinalIgnoreCase);

        private static readonly Lazy<RtcCalibrationStore> _default = new(() =>
            new RtcCalibrationStore(Path.Combine(SettingsService.ConfigDirectory, "rtc-calibration.json")));

        public static RtcCalibrationStore Default => _default.Value;

        public RtcCalibrationStore(string path)
        {
            _path = path;
            Load();
        }

        public string FilePath => _path;

        private void Load()
        {
            try
            {
                if (!File.Exists(_path)) return;
                var list = JsonSerializer.Deserialize<List<UnitClockRecord>>(File.ReadAllText(_path));
                if (list != null)
                    _units = list.Where(u => !string.IsNullOrWhiteSpace(u.UnitKey))
                                 .ToDictionary(u => u.UnitKey, StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                /* a damaged file must not stop the app: start over, keep the bad copy */
                try { File.Copy(_path, _path + ".bad", true); } catch { }
            }
        }

        private void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            string tmp = _path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(_units.Values.ToList(), new JsonSerializerOptions { WriteIndented = true }));
            File.Move(tmp, _path, true);
        }

        public UnitClockRecord Get(string unitKey)
        {
            lock (_lock)
            {
                if (!_units.TryGetValue(unitKey, out var r))
                {
                    r = new UnitClockRecord { UnitKey = unitKey };
                    _units[unitKey] = r;
                }
                return r;
            }
        }

        public SyncPoint Record(string unitKey, OffsetMeasurement m, SyncPointKind kind, ReferenceClock rc)
        {
            lock (_lock)
            {
                UnitClockRecord r = Get(unitKey);
                if (kind == SyncPointKind.AfterSync) r.Segment++;
                var p = new SyncPoint
                {
                    TrueUtc = m.TrueUtc,
                    OffsetSeconds = m.OffsetSeconds,
                    ZoneOffsetHours = m.ZoneOffsetHours,
                    MinDelayMs = m.MinDelaySeconds * 1000.0,
                    SpreadMs = m.SpreadSeconds * 1000.0,
                    ReferenceUncertaintyMs = rc.UncertaintySeconds * 1000.0,
                    TemperatureC = m.TemperatureC,
                    AppliedPpb = m.AppliedPpb,
                    Segment = r.Segment,
                    Kind = kind,
                    Reference = rc.Source,
                };
                r.Points.Add(p);
                if (r.Points.Count > MaxPointsPerUnit) r.Points.RemoveRange(0, r.Points.Count - MaxPointsPerUnit);
                Save();
                return p;
            }
        }

        /// <summary>Store (or clear) the unit's calibration. The device's clock rate changes with
        /// it, so a new segment starts: drift is never fitted across a calibration change.</summary>
        public void SetCalibration(string unitKey, StoredCalibration? cal)
        {
            lock (_lock)
            {
                UnitClockRecord r = Get(unitKey);
                r.Calibration = cal;
                r.Segment++;
                Save();
            }
        }
    }
}
