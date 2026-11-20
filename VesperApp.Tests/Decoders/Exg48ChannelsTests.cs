using VesperApp.Services;
using Xunit;

namespace VesperApp.Tests.Decoders;

/// <summary>
/// EXG48 CSV (backlog V2): only the board's channels (4 / 6 / 8; absent channels read 0 in every sample), and the
/// lead-off flags from the ADS129x status word as the datasheet gives it: 1100, LOFF_STATP[7:0], LOFF_STATN[7:0],
/// GPIO[7:4], bit 0 = channel 1.
/// </summary>
public class Exg48ChannelsTests
{
    private const string Name = "E2026_11_20_09_30_15_000-E2026_11_20_09_31_15_000.EBN";

    private static byte[] S24(int code) => new[] { (byte)(code >> 16), (byte)(code >> 8), (byte)code };

    private static byte[] Record(int loffP, int loffN, params int[] ch)
    {
        int status = (0xC << 20) | (loffP << 12) | (loffN << 4);
        var r = new List<byte> { (byte)(status >> 16), (byte)(status >> 8), (byte)status };
        for (int k = 0; k < 8; k++) r.AddRange(S24(k < ch.Length ? ch[k] : 0));
        return r.ToArray();
    }

    private static string[] Decode(byte[] data, ulong? cfg = null)
    {
        using var dir = new TempDir();
        string f = dir.File(Name);
        using (var p = new EXG48Parser(f, data, new DateTime(2026, 11, 20, 9, 30, 15), 1, 2000, cfg)) p.WriteFile();
        return System.IO.File.ReadAllLines(f + ".csv");
    }

    [Fact]
    public void FourChannelBoard_WritesFourChannels()
    {
        string[] lines = Decode(Raw.Concat(Record(0, 0, 10, 20, 30, 40), Record(0, 0, 11, 21, 31, 41)));
        Assert.Equal(3, lines.Length);
        Assert.Equal(2 + 4 * 3, lines[0].Split(',').Length);
        Assert.EndsWith("CH4[uV],CH4+ off,CH4- off", lines[0]);
        Assert.Equal(2 + 4 * 3, lines[1].Split(',').Length);       // no trailing separator
    }

    [Fact]
    public void SixAndEightChannelBoards()
    {
        Assert.Equal(2 + 6 * 3, Decode(Raw.Concat(Record(0, 0, 1, 2, 3, 4, 5, 6), Record(0, 0, 1, 2, 3, 4, 0, 6)))[0].Split(',').Length);
        Assert.Equal(2 + 8 * 3, Decode(Raw.Concat(Record(0, 0, 1, 2, 3, 4, 5, 6, 0, 0), Record(0, 0, 1, 2, 3, 4, 5, 6, 0, -1)))[0].Split(',').Length);
    }

    [Fact]
    public void LeadOffFlags_FollowTheStatusWord()
    {
        // electrode off: CH1 positive, CH6 positive (byte 0 is then 0xC2, not 0xC0), CH3 negative
        string[] lines = Decode(Raw.Concat(Record(0x21, 0x04, 1, 2, 3, 4, 5, 6), Record(0, 0, 1, 2, 3, 4, 5, 6)));
        Assert.Equal(3, lines.Length);                              // the record with CH6 off is kept
        string[] c = lines[1].Split(',');
        Assert.Equal("True", c[3]);  Assert.Equal("False", c[4]);   // CH1 +, -
        Assert.Equal("False", c[9]); Assert.Equal("True", c[10]);   // CH3 +, -
        Assert.Equal("True", c[18]); Assert.Equal("False", c[19]);  // CH6 +, -
        Assert.Equal("False", lines[2].Split(',')[3]);
    }

    [Fact]
    public void LeadOffNotEnabled_LeavesTheFlagBlank()
    {
        // Config1: CH1 byte = loffp (bit 3) only; CH2..CH4 none. Config2: nothing.
        ulong cfg = 0x08;
        string[] c = Decode(Raw.Concat(Record(0x03, 0x03, 1, 2, 3, 4), Record(0, 0, 1, 2, 3, 4)), cfg)[1].Split(',');
        Assert.Equal("True", c[3]);
        Assert.Equal("", c[4]);
        Assert.Equal("", c[6]);
        Assert.Equal("", c[7]);
    }

    [Fact]
    public void ChannelConfig_FromTheSidecar()
    {
        using var dir = new TempDir();
        string s = dir.File("a.EBN.txt");
        Assert.Null(EXG48Parser.ChannelConfigFromSidecar(s));
        System.IO.File.WriteAllLines(s, new[] { "Sensor:EXG48", "Config1:18181818", "Config2:8" });
        Assert.Equal(0x0000000818181818UL, EXG48Parser.ChannelConfigFromSidecar(s));
    }

    [Fact]
    public void Resync_AfterGarbage_NeedsTwoRecordsInARow()
    {
        var garbage = new byte[] { 0xC5, 1, 2, 3, 4 };              // a 0xC_ byte that is not a record
        string[] lines = Decode(Raw.Concat(Record(0, 0, 1, 2, 3, 4), garbage, Record(0, 0, 5, 6, 7, 8), Record(0, 0, 9, 9, 9, 9)));
        Assert.Equal(4, lines.Length);
    }
}
