using System.Text.Json;
using VesperApp.Services.Time;
using Xunit;

namespace VesperApp.Tests;

/// <summary>Nanotag records were keyed with the tag's NUL-padded USB serial ("84F74056\0\0\0") up to VesperApp
/// b2005f7; the store normalises keys and merges a padded record with the clean one.</summary>
public class RtcCalibrationStoreKeyTests
{
    [Theory]
    [InlineData("84F74056\0\0\0", "84F74056")]
    [InlineData(" 84F74056 ", "84F74056")]
    [InlineData("84F74056", "84F74056")]
    [InlineData(null, "")]
    public void NormalizeKey_drops_padding(string? key, string expected) =>
        Assert.Equal(expected, RtcCalibrationStore.NormalizeKey(key));

    [Fact]
    public void Padded_and_clean_records_merge_into_one()
    {
        string dir = Path.Combine(Path.GetTempPath(), "rtcstore-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "rtc-calibration.json");
        try
        {
            var padded = new UnitClockRecord
            {
                UnitKey = "84F74056\0\0\0",
                Points = { new SyncPoint { TrueUtc = new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc) } },
                Calibration = new StoredCalibration { ErrorPpb = -55500, AppliedUtc = new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc) },
            };
            var clean = new UnitClockRecord
            {
                UnitKey = "84F74056",
                Points = { new SyncPoint { TrueUtc = new DateTime(2026, 10, 30, 10, 0, 0, DateTimeKind.Utc) } },
            };
            File.WriteAllText(path, JsonSerializer.Serialize(new List<UnitClockRecord> { padded, clean }));

            var store = new RtcCalibrationStore(path);
            UnitClockRecord r = store.Get("84F74056\0\0\0");

            Assert.Equal("84F74056", r.UnitKey);
            Assert.Equal(2, r.Points.Count);
            Assert.True(r.Points[0].TrueUtc < r.Points[1].TrueUtc);
            Assert.Equal(-55500, r.Calibration?.ErrorPpb);
            Assert.Same(r, store.Get("84F74056"));
        }
        finally { Directory.Delete(dir, true); }
    }
}
