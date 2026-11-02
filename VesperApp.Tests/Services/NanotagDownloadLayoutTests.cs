using VesperApp.Models;
using VesperApp.Services;
using VesperApp.Services.Time;
using Xunit;

namespace VesperApp.Tests.Services;

/// <summary>
/// Nanotag download output (asd-interfaces projects/nanotag-data-ux-proposal.md, items 3 and 9): snapshot pages
/// listed in one GPS/snapshots.csv instead of a .txt per page, and a folder of snapshots decodable whatever its
/// name (the download writes GPS/, a Vesper parse DAT/).
/// </summary>
public class NanotagDownloadLayoutTests
{
    [Fact]
    public void Snapshot_csv_row_lists_page_time_clock_and_activity()
    {
        string row = LoggerDevice.SnapshotCsvRow("snap.2026_11_01_20_07_24_GC0.dat",
            new DateTime(2026, 11, 1, 20, 7, 24, 250), DeviceClockBase.For(deviceUtc: true, trueUtc: DateTime.UtcNow),
            snapId: 399, pageIndex: 0, pages: 16, odba: 1886, vedba: 1316041);

        string[] f = row.Split(',');
        Assert.Equal(LoggerDevice.SnapshotCsvHeader.Split(',').Length, f.Length);
        Assert.Equal("snap.2026_11_01_20_07_24_GC0.dat", f[0]);
        Assert.Equal("2026-11-01 20:07:24.250", f[1]);
        Assert.Equal("399", f[3]);
        Assert.Equal("1", f[4]);                     // pages counted from 1
        Assert.Equal("16", f[5]);
        Assert.Equal("43.428", f[6]);                // sqrt of the stored sum, as the per-page .txt had it
        Assert.Equal("1147.188", f[7]);
    }

    [Fact]
    public void Unknown_clock_base_says_device_time()
    {
        string row = LoggerDevice.SnapshotCsvRow("snap.x_GC0.dat", new DateTime(2026, 1, 1), DeviceClockBase.Unknown, 1, 0, 1, 0, 0);
        Assert.Equal("device time", row.Split(',')[2]);
    }

    [Fact]
    public void Folder_with_snapshots_is_decodable_whatever_its_name()
    {
        string root = Path.Combine(Path.GetTempPath(), "vesperapp-test-" + Guid.NewGuid().ToString("N"));
        string gps = Path.Combine(root, "GPS");
        string empty = Path.Combine(root, "ACC");
        Directory.CreateDirectory(gps);
        Directory.CreateDirectory(empty);
        try
        {
            Assert.False(RecordingPipeline.IsGnssSnapFolder(gps));
            File.WriteAllBytes(Path.Combine(gps, "snap.2026_11_01_18_59_58_GC0.dat"), new byte[16]);
            Assert.True(RecordingPipeline.IsGnssSnapFolder(gps));
            Assert.False(RecordingPipeline.IsGnssSnapFolder(empty));
            Assert.True(RecordingPipeline.IsGnssSnapFolder(Path.Combine(root, "DAT")));   // Vesper name, as before
        }
        finally { Directory.Delete(root, true); }
    }
}
