using VesperApp.Models;
using Xunit;

namespace VesperApp.Tests.Models;

/// <summary>Format button on a Vesper / Pipistrelle: what is offered, and the VESPER_FORMATDISK request / reply
/// (VesperU5 fs_bench.h FsBench_FormatDisk).</summary>
public class DiskFormatTests
{
    private static readonly Version Fw122 = new(1, 22);
    private const ulong Gb16 = 15_634_268_160;      // 16 GB eMMC user area (about 14.6 GiB)
    private const ulong Gb32 = 31_268_536_320;
    private const ulong Gb64 = 62_537_072_640;

    [Fact]
    public void Sixteen_gb_unit_gets_fat32_only()
        => Assert.Equal(new[] { DiskFileSystem.Fat32 }, DiskFormat.Choices(Fw122, Gb16));

    [Theory]
    [InlineData(Gb32)]
    [InlineData(Gb64)]
    public void Larger_units_get_fat32_first_then_exfat(ulong bytes)
        => Assert.Equal(new[] { DiskFileSystem.Fat32, DiskFileSystem.ExFat }, DiskFormat.Choices(Fw122, bytes));

    [Fact]
    public void Old_or_unknown_firmware_or_size_gets_fat32_only()
    {
        Assert.Single(DiskFormat.Choices(new Version(1, 20), Gb64));
        Assert.Single(DiskFormat.Choices(null, Gb64));
        Assert.Single(DiskFormat.Choices(Fw122, null));
    }

    [Fact]
    public void Request_is_fs_then_cluster_kb()
    {
        Assert.Equal(new byte[] { 1, 32 }, DiskFormat.BuildRequest(DiskFileSystem.ExFat, 32));
        Assert.Equal(new byte[] { 0, 0 }, DiskFormat.BuildRequest(DiskFileSystem.Fat32));
        Assert.Equal(new byte[] { 0, 0 }, DiskFormat.BuildRequest(DiskFileSystem.Fat32, 256));   // out of range = default
    }

    [Fact]
    public void Disk_size_is_blocks_times_block_bytes()
    {
        byte[] r = new byte[8];
        BitConverter.GetBytes(122_142_720u).CopyTo(r, 0);
        BitConverter.GetBytes(512u).CopyTo(r, 4);
        Assert.Equal(122_142_720UL * 512, DiskFormat.ParseDiskSize(r));
        Assert.Null(DiskFormat.ParseDiskSize(new byte[4]));
        Assert.Null(DiskFormat.ParseDiskSize(new byte[8]));
    }

    [Fact]
    public void Reply_status_maps_to_outcome()
    {
        var done = DiskFormat.ParseReply(new byte[] { 0, 0, 0, 4, 0x00, 0x20, 0, 0 });
        Assert.Equal(DiskFormatOutcome.Done, done.Outcome);
        Assert.Equal("exFAT", done.TypeText);
        Assert.Equal(0x2000u, done.DataStartSector);

        Assert.Equal(DiskFormatOutcome.BadRequest, DiskFormat.ParseReply(new byte[] { 4, 0, 0, 0, 0, 0, 0, 0 }).Outcome);
        Assert.Equal(DiskFormatOutcome.Failed, DiskFormat.ParseReply(new byte[] { 3, 0x90, 0x02, 0, 0, 0, 0, 0 }).Outcome);
        Assert.Equal(DiskFormatOutcome.NoAnswer, DiskFormat.ParseReply(new byte[3]).Outcome);
    }
}
