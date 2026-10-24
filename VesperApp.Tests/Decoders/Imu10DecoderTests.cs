using System.Globalization;
using System.Text.Json;
using VesperApp.Services;
using Xunit;

namespace VesperApp.Tests.Decoders;

/// <summary>IMU10 / IMU10HT .MBN intermediates against the golden fixtures: IMU10 writes a 4-byte
/// barometer field, IMU10HT 6 bytes (told apart by the "Sensor:" line of the .txt sidecar).</summary>
public class Imu10DecoderTests
{
    // CSV columns (IMU10RecordLine): Time, Minute, Second, Milisecond, Acc XYZ, Gyro XYZ, Mag XYZ, Temperature, Pressure
    private const int ColMinute = 1, ColSecond = 2, ColMs = 3, ColAcc = 4, ColGyro = 7, ColMag = 10, ColTemp = 13, ColHpa = 14;

    private static double D(string s) => double.Parse(s, CultureInfo.InvariantCulture);

    private static void AssertTriple(JsonElement expected, string[] col, int at)
    {
        double[] want = expected.ValueKind == JsonValueKind.Null
            ? new[] { 0.0, 0.0, 0.0 }                             // absent sensor -> zeros
            : expected.EnumerateArray().Select(e => e.GetDouble()).ToArray();
        for (int i = 0; i < 3; i++) Assert.Equal(want[i], D(col[at + i]), 4);
    }

    private static void AssertMatchesFixture(string fixture, string[] lines)
    {
        JsonElement exp = Fixtures.Json(fixture);
        var records = exp.GetProperty("records").EnumerateArray().ToList();

        Assert.StartsWith("Time,Minute,Second,Milisecond,Acc X [mg]", lines[0]);
        Assert.Equal(records.Count + 1, lines.Length);            // every record, incl. the last one

        for (int r = 0; r < records.Count; r++)
        {
            string[] col = lines[r + 1].Split(',');
            Assert.Equal(15, col.Length);
            Assert.Equal("30", col[ColMinute]);
            Assert.Equal("15", col[ColSecond]);
            Assert.Equal("499", col[ColMs]);                     // subsecond 512 of 1023 -> 499 ms

            AssertTriple(records[r].GetProperty("acc_mg"), col, ColAcc);
            AssertTriple(records[r].GetProperty("gyro_mdps"), col, ColGyro);
            AssertTriple(records[r].GetProperty("mag_mgauss"), col, ColMag);

            JsonElement t = records[r].GetProperty("temp_c"), p = records[r].GetProperty("hpa");
            Assert.Equal(t.ValueKind == JsonValueKind.Null ? 0.0 : t.GetDouble(), D(col[ColTemp]), 2);
            Assert.Equal(p.ValueKind == JsonValueKind.Null ? 0.0 : p.GetDouble(), D(col[ColHpa]), 1);
        }
    }

    private static string[] Decode(string fixture, string? sensorLine, int? expectedLines = null)
    {
        using var dir = new TempDir();
        string mbn = Fixtures.CopyAs(fixture, dir);
        if (sensorLine != null) File.WriteAllText(mbn + ".txt", sensorLine + Environment.NewLine);

        byte[] data = File.ReadAllBytes(mbn);
        using (var p = new IMU10Parser(mbn, data, new DateTime(2026, 10, 25, 10, 0, 0), 1023, 100))
            p.WriteFile();
        int expected = expectedLines ?? Fixtures.Json(fixture).GetProperty("records").GetArrayLength() + 1;
        return Fixtures.WaitForLines(mbn + ".csv", expected);
    }

    [Fact]
    public void Imu10_FourByteBarometer_DecodesAllRecords()
        => AssertMatchesFixture("imu-imu10.MBN", Decode("imu-imu10.MBN", "Sensor:IMU10"));

    [Fact]
    public void Imu10_WithoutSidecar_DefaultsToFourByteBarometer()
        => AssertMatchesFixture("imu-imu10.MBN", Decode("imu-imu10.MBN", null));

    [Fact]
    public void Imu10Ht_SixByteBarometer_DecodesAllRecords()
        => AssertMatchesFixture("imu-imu10ht.MBN", Decode("imu-imu10ht.MBN", "Sensor:IMU10HT"));

    [Fact]
    public void Imu10Ht_SpecificValues_NegativeTemperatureGyroMagOnlyBaroOnly()
    {
        string[] lines = Decode("imu-imu10ht.MBN", "Sensor:IMU10HT");
        string[] first = lines[1].Split(',');
        Assert.Equal("-5.12", first[ColTemp]);                    // signed TEMP_OUT, not ~650 degC
        Assert.Equal("1013.0", first[ColHpa]);
        Assert.Equal("-2000.0000", first[ColGyro + 1]);

        string[] gyroMag = lines[3].Split(',');                   // flags 0x06: no accel, no baro
        Assert.Equal(new[] { "0.0000", "0.0000", "0.0000" }, gyroMag[ColAcc..(ColAcc + 3)]);
        Assert.Equal(new[] { "4.0000", "5.0000", "6.0000" }, gyroMag[ColGyro..(ColGyro + 3)]);
        Assert.Equal(new[] { "7.0000", "8.0000", "9.0000" }, gyroMag[ColMag..(ColMag + 3)]);

        string[] baro = lines[4].Split(',');                      // flags 0x08, last record of the file
        Assert.Equal("-1.00", baro[ColTemp]);
        Assert.Equal("1009.0", baro[ColHpa]);
        Assert.Equal("0.0000", baro[ColGyro]);
    }

    [Fact]
    public void Timestamps_AdvanceBySamplePeriodFromStart()
    {
        string[] lines = Decode("imu-imu10.MBN", "Sensor:IMU10");
        Assert.StartsWith("10/25/2026 10:00:00.1,", lines[1]);
        Assert.StartsWith("10/25/2026 10:00:00.4,", lines[4]);
    }

    [Fact]
    public void Imu10Data_ReadAsSixByteBarometer_LosesRecords()
    {
        // Guards the distinction: the 6-byte layout swallows the next record's 0x55 header.
        string[] lines = Decode("imu-imu10.MBN", "Sensor:IMU10HT", expectedLines: 2);
        Assert.True(lines.Length < 5, $"expected records to be lost, got {lines.Length - 1}");
    }

    [Theory]
    [InlineData("Sensor:IMU10HT", 6)]
    [InlineData("sensor: imu10ht ", 6)]
    [InlineData("Sensor:IMU10", 4)]
    [InlineData("Sensor:IMU10HTX", 4)]
    [InlineData(null, 4)]
    public void BaroBytesFor_ReadsSensorLineOfSidecar(string? sidecar, int expected)
    {
        using var dir = new TempDir();
        string f = dir.File("M2026_01_01_00_00_00_000-M2026_01_01_00_00_01_000.MBN");
        File.WriteAllBytes(f, new byte[] { 0x55 });
        if (sidecar != null) File.WriteAllLines(f + ".txt", new[] { "DeviceID:1234", sidecar, "SampleRate:10" });
        Assert.Equal(expected, IMU10Parser.BaroBytesFor(f));
    }

    [Fact]
    public async Task Pipeline_DecodeFiles_Imu10HtIntermediate()
    {
        using var dir = new TempDir();
        string mbn = Fixtures.CopyAs("imu-imu10ht.MBN", dir);
        File.WriteAllLines(mbn + ".txt", new[] { "Sensor:IMU10HT", "SampleRate:10" });

        DecodeSummary sum = await RecordingPipeline.DecodeFilesAsync(new[] { mbn });

        Assert.Equal(1, sum.Csv);
        Assert.Equal(0, sum.Failed);
        string[] lines = Fixtures.WaitForLines(mbn + ".csv", 5);
        AssertMatchesFixture("imu-imu10ht.MBN", lines);
        // start time from the file name, 100 ms per sample (SampleRate 10 Hz)
        Assert.StartsWith("10/25/2026 10:00:00.1,", lines[1]);
    }

    [Fact]
    public void EmptyData_Throws()
    {
        Assert.Throws<ArgumentException>(() => new IMU10Parser("x.MBN", Array.Empty<byte>(), DateTime.Now, 1023, 1));
        Assert.Throws<ArgumentNullException>(() => new IMU10Parser("x.MBN", null!, DateTime.Now, 1023, 1));
    }
}
