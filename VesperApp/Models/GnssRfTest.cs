using System;
using Avalonia.Media;
using ReactiveUI;

namespace VesperApp.Models
{
    // The VESPER_GPS_SELFTEST wire contract (GnssSelfTest / GnssSelfTestResult /
    // GnssSelfTestStatus) lives in ASD.DeviceCore.Protocol so the firmware, the app,
    // and the factory suite share one definition. This file keeps only the
    // app-specific (UI / positioning) helpers.

    /// <summary>Bindable result row for the GNSS RF-test view (tone go/no-go or a fix).</summary>
    public class GnssTestRow : ReactiveObject
    {
        public string Label { get; }
        public GnssTestRow(string label) { _label = label; Label = label; }

        private readonly string _label;
        private string _status = "Not tested";
        public string Status { get => _status; set => this.RaiseAndSetIfChanged(ref _status, value); }

        private string _detail = string.Empty;
        public string Detail { get => _detail; set => this.RaiseAndSetIfChanged(ref _detail, value); }

        private IBrush _statusBrush = Brushes.Gray;
        public IBrush StatusBrush { get => _statusBrush; set => this.RaiseAndSetIfChanged(ref _statusBrush, value); }
    }

    /// <summary>Great-circle distance helpers for positioning accuracy.</summary>
    public static class GeoUtil
    {
        public static double HaversineMeters(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371000.0;
            double p1 = lat1 * Math.PI / 180, p2 = lat2 * Math.PI / 180;
            double dp = (lat2 - lat1) * Math.PI / 180, dl = (lon2 - lon1) * Math.PI / 180;
            double a = Math.Sin(dp / 2) * Math.Sin(dp / 2)
                     + Math.Cos(p1) * Math.Cos(p2) * Math.Sin(dl / 2) * Math.Sin(dl / 2);
            return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        }
    }
}
