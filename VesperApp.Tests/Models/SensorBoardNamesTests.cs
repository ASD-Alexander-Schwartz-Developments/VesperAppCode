using Avalonia.Media;
using VesperApp.Models;
using Xunit;

namespace VesperApp.Tests.Models;

/// <summary>Sensors tab: the biopotential board named from the probe and chip id (EEG4 / EXG2 / EEG2-EMG2-ECG2),
/// and the thermal camera's verdict from the console 70 reply.</summary>
public class SensorBoardNamesTests
{
    [Theory]
    [InlineData(3, 0x90, "EEG4 (ADS1294, 4 channels)")]
    [InlineData(3, 0x92, "EEG4 (ADS1298, 8 channels)")]
    [InlineData(3, 0xD1, "EEG4 (ADS1296, 6 channels, R part)")]
    [InlineData(2, 0x53, "EXG2 (ADS1292)")]
    [InlineData(2, 0x72, "EXG2 (ADS1291R)")]
    [InlineData(1, 0x51, "EEG2 / EMG2 / ECG2 (ADS1192)")]
    [InlineData(1, 0x50, "EEG2 / EMG2 / ECG2 (ADS1191)")]
    public void ExgPart_NamesTheBoard(byte variant, ushort id, string expected) =>
        Assert.Equal(expected, SensorBenchTest.ExgPart(variant, id));

    private static byte[] Cam(byte status, byte lep3, short fpaX100, short auxX100) =>
        new byte[] { status, lep3, 1, 0 }.Concat(BitConverter.GetBytes(fpaX100)).Concat(BitConverter.GetBytes(auxX100)).Concat(new byte[2]).ToArray();

    [Fact]
    public void Lepton_Verdicts()
    {
        var (s, b, d) = SensorBenchTest.DescribeLepton(Cam(0, 1, 3012, 2875));
        Assert.Equal("Pass", s);
        Assert.Contains("Lepton 3.x, radiometric, sensor 30.1 °C, housing 28.8 °C", d);

        Assert.Equal("Not found", SensorBenchTest.DescribeLepton(Cam(0xFF, 0, 0, 0)).status);
        Assert.Equal("Check", SensorBenchTest.DescribeLepton(Cam(0, 0, -27315, 0)).status);   // never booted: no temperature
        Assert.Equal("No answer", SensorBenchTest.DescribeLepton(null).status);
    }
}
