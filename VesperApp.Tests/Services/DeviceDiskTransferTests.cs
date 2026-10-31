using VesperApp.Services;
using VesperApp.Services.Time;
using Xunit;

namespace VesperApp.Tests.Services;

/// <summary>
/// Upload Config for disk loggers (Vesper / Pipistrelle / KOL): config.json written to the unit's drive root,
/// valid JSON only, an existing file replaced; UID.TXT matching for finding that drive.
/// </summary>
public class DeviceDiskTransferTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "va-disk-" + Guid.NewGuid().ToString("N"));

    public DeviceDiskTransferTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, true); } catch { }
    }

    [Fact]
    public void WritesConfigJsonAsGiven()
    {
        const string json = "{\n  \"General\": { \"Name\": \"bat 7\" }\n}";
        DeviceDiskTransfer.Result r = DeviceDiskTransfer.WriteConfig(_root, json);
        Assert.True(r.Ok, r.Message);
        Assert.Equal(json, File.ReadAllText(Path.Combine(_root, "config.json")));
        Assert.False(File.Exists(Path.Combine(_root, "config.tmp")));
    }

    [Fact]
    public void ReplacesAnExistingConfig()
    {
        File.WriteAllText(Path.Combine(_root, "config.json"), "{\"old\":1}");
        Assert.True(DeviceDiskTransfer.WriteConfig(_root, "{\"new\":2}").Ok);
        Assert.Equal("{\"new\":2}", File.ReadAllText(Path.Combine(_root, "config.json")));
        Assert.Single(Directory.GetFiles(_root));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{ not json")]
    [InlineData("[1, 2]")]
    public void RefusesWhatIsNotAJsonObject(string json)
    {
        File.WriteAllText(Path.Combine(_root, "config.json"), "{\"keep\":1}");
        DeviceDiskTransfer.Result r = DeviceDiskTransfer.WriteConfig(_root, json);
        Assert.False(r.Ok);
        Assert.Equal("{\"keep\":1}", File.ReadAllText(Path.Combine(_root, "config.json")));   // untouched
    }

    [Fact]
    public void NoDriveIsReported()
    {
        DeviceDiskTransfer.Result r = DeviceDiskTransfer.WriteConfig(null, "{}");
        Assert.False(r.Ok);
        Assert.Equal(DeviceDiskTransfer.DriveNotFound, r.Message);
        Assert.False(DeviceDiskTransfer.WriteConfig(Path.Combine(_root, "missing"), "{}").Ok);
    }

    [Theory]
    [InlineData("47715041", "47715041", true)]
    [InlineData("  47715041 \r", "47715041", true)]
    [InlineData("UID=47715041", "47715041", true)]
    [InlineData("uid = abcd1234", "ABCD1234", true)]
    [InlineData("47715042", "47715041", false)]
    [InlineData("", "47715041", false)]
    [InlineData("UID=", "", false)]
    public void UidFileLineMatches(string line, string uid, bool expected)
    {
        Assert.Equal(expected, UnitDisk.UidMatches(line, uid));
    }
}
