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
    public void Timestamps_ComeFromEachRecordsStamp()
    {
        // the fixture records are all stamped mm:ss 30:15.499; the hour and date come from the start time
        string[] lines = Decode("imu-imu10.MBN", "Sensor:IMU10");
        Assert.StartsWith("10/25/2026 10:30:15.499,", lines[1]);
        Assert.StartsWith("10/25/2026 10:30:15.499,", lines[4]);
    }

    /// <summary>A baro-only IMU10 record (flags 0x08) stamped at the given minute, second and ms.</summary>
    private static byte[] BaroRecord(int minute, int second, int ms)
    {
        ushort subsec = (ushort)(1023 - (int)Math.Round(ms * 1024 / 1000.0));
        var r = new List<byte> { 0x55, 0x08, (byte)minute, (byte)second, (byte)(subsec & 0xFF), (byte)(subsec >> 8) };
        r.AddRange(BitConverter.GetBytes((short)2100));
        r.AddRange(BitConverter.GetBytes((ushort)943));
        return r.ToArray();
    }

    [Fact]
    public void Timestamps_At26Hz_FollowTheStamps_NotAnIntegerMsPeriod()
    {
        // Bench 2026-11-10 (Pipistrelle, baro 26 Hz): 1000 / 26 = 38 ms per row put the last row of a
        // 3-min file 2.25 s early. 26 Hz for 180 s: the last row must be at its stamp.
        var data = new List<byte>();
        var t0 = new DateTime(2026, 11, 10, 14, 0, 30, 302);
        int n = 26 * 180;
        for (int k = 0; k < n; k++)
        {
            DateTime t = t0.AddSeconds(k / 26.0);
            data.AddRange(BaroRecord(t.Minute, t.Second, t.Millisecond));
        }
        using var dir = new TempDir();
        string mbn = dir.File("M2026_11_10_14_00_30_264-M2026_11_10_14_03_30_015.MBN");
        File.WriteAllBytes(mbn, data.ToArray());
        using (var p = new IMU10Parser(mbn, data.ToArray(), new DateTime(2026, 11, 10, 14, 0, 30, 264), 1023, 1000 / 26))
            p.WriteFile();
        string[] lines = Fixtures.WaitForLines(mbn + ".csv", n + 1);
        DateTime last = DateTime.ParseExact(lines[^1].Split(',')[0], "MM/dd/yyyy HH:mm:ss.FFF", CultureInfo.InvariantCulture);
        Assert.InRange((last - t0.AddSeconds((n - 1) / 26.0)).TotalMilliseconds, -2, 2);
    }

    [Fact]
    public void Timestamps_StaleSeconds_Fw120_AreMonotonicAndFollowTheCounter()
    {
        // Firmware up to 1.20: the stamped minute / second is refreshed only every 0.68 s (41 rows at
        // 59.89 Hz), the sub-second counter is live (customer files 73M-75M.BIN, 2026-06-04). Across an
        // hour change, 60 s of rows: every row must be at its true time, none stepping back.
        var data = new List<byte>();
        var t0 = new DateTime(2026, 6, 4, 3, 59, 30, 900);
        const double period = 1.0 / 59.89;
        int n = 3600;
        DateTime stale = t0;
        var truth = new List<DateTime>();
        for (int k = 0; k < n; k++)
        {
            DateTime t = t0.AddSeconds(k * period);
            if (k % 41 == 0) stale = t;                                   // the seconds are read fresh here only
            truth.Add(t);
            data.AddRange(BaroRecord(stale.Minute, stale.Second, t.Millisecond));
        }
        using var dir = new TempDir();
        string mbn = dir.File("M2026_06_04_03_59_30_900-M2026_06_04_04_00_31_000.MBN");
        File.WriteAllBytes(mbn, data.ToArray());
        using (var p = new IMU10Parser(mbn, data.ToArray(), t0, 1023, 1000 / 60))
            p.WriteFile();
        string[] lines = Fixtures.WaitForLines(mbn + ".csv", n + 1);
        DateTime prev = DateTime.MinValue;
        for (int k = 0; k < n; k++)
        {
            string[] col = lines[k + 1].Split(',');
            DateTime got = DateTime.ParseExact(col[0], "MM/dd/yyyy HH:mm:ss.FFF", CultureInfo.InvariantCulture);
            Assert.InRange((got - truth[k]).TotalMilliseconds, -2, 2);
            Assert.True(got >= prev, $"row {k} steps back");
            Assert.Equal(got.Minute.ToString(), col[ColMinute]);          // Minute / Second columns agree with Time
            Assert.Equal(got.Second.ToString(), col[ColSecond]);
            prev = got;
        }
    }

    [Fact]
    public void StampTime_StaleSecond_IsMovedForward_AlsoOverTheHour()
    {
        DateTime last = new(2026, 6, 4, 3, 59, 59, 990);
        // the counter wrapped, the second is still the old one: 59:59.006 means 04:00:00.006
        Assert.Equal(new DateTime(2026, 6, 4, 4, 0, 0, 6), IMU10Parser.StampTime(ref last, 59, 59, 6));
        // still stale after the hour changed
        Assert.Equal(new DateTime(2026, 6, 4, 4, 0, 0, 23), IMU10Parser.StampTime(ref last, 59, 59, 23));
        // two seconds stale
        last = new DateTime(2026, 6, 4, 4, 0, 2, 100);
        Assert.Equal(new DateTime(2026, 6, 4, 4, 0, 2, 117), IMU10Parser.StampTime(ref last, 0, 0, 117));
        // a refreshed stamp ahead of the previous record is taken as it is (a gap in the recording)
        Assert.Equal(new DateTime(2026, 6, 4, 4, 5, 0, 0), IMU10Parser.StampTime(ref last, 5, 0, 0));
    }

    [Fact]
    public void StampTime_RollsIntoTheNextHour_AndRejectsBadStamps()
    {
        DateTime last = new(2026, 11, 10, 13, 59, 59, 900);
        Assert.Equal(new DateTime(2026, 11, 10, 13, 59, 59, 950), IMU10Parser.StampTime(ref last, 59, 59, 950));
        Assert.Equal(new DateTime(2026, 11, 10, 14, 0, 0, 10), IMU10Parser.StampTime(ref last, 0, 0, 10));
        Assert.Equal(new DateTime(2026, 11, 10, 14, 0, 0, 5), IMU10Parser.StampTime(ref last, 0, 0, 5));   // a small step back stays in the hour
        Assert.Null(IMU10Parser.StampTime(ref last, 60, 0, 0));
        Assert.Null(IMU10Parser.StampTime(ref last, 0, -1, 0));
        Assert.Equal(new DateTime(2026, 11, 10, 14, 0, 0, 5), last);                                  // unchanged by a bad stamp
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
        // hour and date from the file name, minute / second / ms from the record's stamp (30:15.499)
        Assert.StartsWith("10/25/2026 10:30:15.499,", lines[1]);
    }

    [Fact]
    public void EmptyData_Throws()
    {
        Assert.Throws<ArgumentException>(() => new IMU10Parser("x.MBN", Array.Empty<byte>(), DateTime.Now, 1023, 1));
        Assert.Throws<ArgumentNullException>(() => new IMU10Parser("x.MBN", null!, DateTime.Now, 1023, 1));
    }
}
