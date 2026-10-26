using System.Text.Json;
using VesperApp.Models;
using VesperApp.Services.Time;
using Xunit;

namespace VesperApp.Tests.Services;

/// <summary>The calibration on the unit's own disk: RTCCAL.TXT and config.json "cdrift"
/// (asd-interfaces interfaces/rtc-time-sync.md), on a temp directory standing in for the drive.</summary>
public class UnitDiskTests
{
    private static CalibrationEstimate Est(int ppb, double? tref = null) => new(ppb, tref, 14.2, 812, 9, -205000, 3, null);

    [Fact]
    public void Cdrift_ForBenchKol_Minus203731Ppb_Is33066()
    {
        /* KOL bench 2026-10-26: crystal -203.731 ppm, no reference temperature -> CALP + CALM 298 */
        int e0 = UnitDisk.ErrorAtTurnover(-203731, null, 34, 25.0);
        Assert.Equal(-203731, e0);
        uint cdrift = UnitDisk.CalrFor(-e0);
        Assert.Equal(33066u, cdrift);
        Assert.Equal(0x8000u | 298u, cdrift);
        Assert.InRange(UnitDisk.CorrectionOf(cdrift) - 203731, -477, 477);
    }

    [Fact]
    public void Cdrift_UsesTheErrorAtTurnover()
    {
        /* measured at 30 °C: 5 °C from T0 -> 34 * 25 = 850 ppb less negative at T0 */
        int e0 = UnitDisk.ErrorAtTurnover(-203731, 30.0, 34, 25.0);
        Assert.Equal(-202881, e0);
        Assert.Equal(33067u, UnitDisk.CalrFor(-e0));
    }

    [Fact]
    public void Write_HoldsFirmwareKeysAndInformationalStrings()
    {
        using var dir = new TempDir();
        UnitDisk.Write(dir.Path, "47625051", Est(-206123, 23.456));

        using var doc = JsonDocument.Parse(File.ReadAllText(dir.File(UnitDisk.CalibrationFile)));
        JsonElement r = doc.RootElement;
        Assert.Equal("47625051", r.GetProperty("uid").GetString());
        Assert.Equal(-206123, r.GetProperty("cerr").GetInt32());
        Assert.Equal(2346, r.GetProperty("ctref").GetInt32());
        Assert.Equal("14.2 h", r.GetProperty("span").GetString());
        Assert.Equal("0.812 ppm", r.GetProperty("unc").GetString());
        Assert.EndsWith("UTC", r.GetProperty("date").GetString());
    }

    [Fact]
    public void Write_WithoutReferenceTemperature_LeavesCtrefOut_ReadGivesNull()
    {
        using var dir = new TempDir();
        UnitDisk.Write(dir.Path, "47625051", Est(-206123));
        Assert.DoesNotContain("ctref", File.ReadAllText(dir.File(UnitDisk.CalibrationFile)));
        UnitDisk.DiskCalibration c = UnitDisk.Read(dir.Path, "47625051")!;
        Assert.Equal(-206123, c.ErrorPpb);
        Assert.Null(c.ReferenceC);
    }

    [Fact]
    public void Read_CtrefNoneMarker_IsNull_AndUidIsCaseInsensitive()
    {
        using var dir = new TempDir();
        File.WriteAllText(dir.File("rtccal.txt"), """{"uid":"4762505a","cerr":-1000,"ctref":-32768}""");
        UnitDisk.DiskCalibration c = UnitDisk.Read(dir.Path, " 4762505A ")!;
        Assert.Equal(-1000, c.ErrorPpb);
        Assert.Null(c.ReferenceC);
    }

    [Theory]
    [InlineData(null)]                                            // no file
    [InlineData("{not json")]
    [InlineData("""{"uid":"47625051"}""")]                        // no cerr
    [InlineData("""{"uid":"47085037","cerr":5}""")]               // another unit's disk content
    public void Read_UnusableFile_IsNull(string? content)
    {
        using var dir = new TempDir();
        if (content != null) File.WriteAllText(dir.File(UnitDisk.CalibrationFile), content);
        Assert.Null(UnitDisk.Read(dir.Path, "47625051"));
    }

    [Fact]
    public void PatchConfigCdrift_Unchanged_LeavesTheFileByteIdentical()
    {
        using var dir = new TempDir();
        string original = "{\r\n  \"name\": \"KOL\",\r\n  \"cdrift\": 33066,\r\n  \"devices\": [ { \"name\": \"KOL\" } ]\r\n}\r\n";
        File.WriteAllText(dir.File("config.json"), original);
        byte[] before = File.ReadAllBytes(dir.File("config.json"));

        Assert.Equal(33066u, UnitDisk.PatchConfigCdrift(dir.Path, 33066));
        Assert.Equal(before, File.ReadAllBytes(dir.File("config.json")));
    }

    [Fact]
    public void PatchConfigCdrift_Changed_KeepsEveryOtherKey()
    {
        using var dir = new TempDir();
        File.WriteAllText(dir.File("config.json"), """{"name":"KOL","cdrift":32999,"tcomp":true,"devices":[{"name":"KOL","rate":48000}]}""");
        Assert.Equal(32999u, UnitDisk.PatchConfigCdrift(dir.Path, 33066));
        Assert.Equal("""{"name":"KOL","cdrift":33066,"tcomp":true,"devices":[{"name":"KOL","rate":48000}]}""",
                     File.ReadAllText(dir.File("config.json")));
    }

    [Theory]
    [InlineData("""{"name":"KOL"}""")]                    // no cdrift yet
    [InlineData("""{"name":"KOL","cdrift":"x"}""")]       // not a number
    [InlineData("""{"name":"KOL","cdrift":-5}""")]        // not a uint
    public void PatchConfigCdrift_NoUsablePreviousValue_ReturnsNullAndSets(string json)
    {
        using var dir = new TempDir();
        File.WriteAllText(dir.File("config.json"), json);
        Assert.Null(UnitDisk.PatchConfigCdrift(dir.Path, 215));
        using var doc = JsonDocument.Parse(File.ReadAllText(dir.File("config.json")));
        Assert.Equal(215u, doc.RootElement.GetProperty("cdrift").GetUInt32());
    }

    [Fact]
    public void PatchConfigCdrift_NoConfigOrNotAnObject_Throws()
    {
        using var dir = new TempDir();
        Assert.Throws<FileNotFoundException>(() => UnitDisk.PatchConfigCdrift(dir.Path, 1));
        File.WriteAllText(dir.File("config.json"), "[1,2]");
        Assert.Throws<InvalidDataException>(() => UnitDisk.PatchConfigCdrift(dir.Path, 1));
    }

    private static RtcCalStatus Applied => FakeRtcDevice.Status(RtcCalFlags.UnitCalibration, curvature: 34, turnoverC: 25.0);

    [Fact]
    public void PersistToDisk_WritesRtccalAndCdrift()
    {
        using var dir = new TempDir();
        File.WriteAllText(dir.File("config.json"), """{"name":"KOL","cdrift":32999}""");

        string msg = RtcSyncService.PersistToRoot(dir.Path, "47625051", Est(-203731), Applied);

        Assert.Equal($"Saved on the unit's disk ({dir.Path}): RTCCAL.TXT; config.json \"cdrift\" 32999 → 33066 for older firmware.", msg);
        Assert.Equal(-203731, UnitDisk.Read(dir.Path, "47625051")!.ErrorPpb);
    }

    [Fact]
    public void PersistToDisk_NoConfigJson_StillWritesRtccal()
    {
        using var dir = new TempDir();
        string msg = RtcSyncService.PersistToRoot(dir.Path, "47625051", Est(-203731), Applied);
        Assert.Contains("RTCCAL.TXT; config.json not updated (no config.json on the unit)", msg);
        Assert.NotNull(UnitDisk.Read(dir.Path, "47625051"));
    }

    [Fact]
    public void PersistToDisk_DiskNotMounted_SaysSo()
        => Assert.StartsWith("The unit's disk was not found", RtcSyncService.PersistToRoot(null, "47625051", Est(-1), Applied));

    [Fact]
    public void RemoveFromDisk_RemovesOnlyRtccal()
    {
        using var dir = new TempDir();
        File.WriteAllText(dir.File("config.json"), """{"cdrift":33066}""");
        UnitDisk.Write(dir.Path, "47625051", Est(-203731));

        Assert.StartsWith("RTCCAL.TXT removed", RtcSyncService.RemoveFromRoot(dir.Path));
        Assert.False(File.Exists(dir.File(UnitDisk.CalibrationFile)));
        Assert.True(File.Exists(dir.File("config.json")));
        Assert.Equal("There was no RTCCAL.TXT on the unit's disk.", RtcSyncService.RemoveFromRoot(dir.Path));
        Assert.StartsWith("The unit's disk was not found", RtcSyncService.RemoveFromRoot(null));
    }
}
