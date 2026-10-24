using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace VesperApp.Tests;

/// <summary>Deterministic culture for every test: the CSV writers use Utils.GetSeparator() (the
/// culture's list separator) and culture-dependent number/date formatting.</summary>
internal static class TestCulture
{
    [ModuleInitializer]
    internal static void Init()
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
    }
}

/// <summary>A fresh temp directory per test, removed afterwards.</summary>
public sealed class TempDir : IDisposable
{
    public string Path { get; }

    public TempDir()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "VesperApp.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string File(string name) => System.IO.Path.Combine(Path, name);

    public void Dispose()
    {
        try { Directory.Delete(Path, true); } catch { /* best effort */ }
    }
}

internal static class Fixtures
{
    public static string Dir => Path.Combine(AppContext.BaseDirectory, "Fixtures");

    public static string PathOf(string name) => Path.Combine(Dir, name);

    public static JsonElement Json(string fixture)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(PathOf(fixture + ".json")));
        return doc.RootElement.Clone();
    }

    /// <summary>Copy a fixture into <paramref name="dir"/> under the name its JSON asks for
    /// (VesperApp classifies raw files by the letters in their names).</summary>
    public static string CopyAs(string fixture, TempDir dir)
    {
        string name = Json(fixture).GetProperty("use_as_file_name").GetString()!;
        string dst = dir.File(name);
        File.Copy(PathOf(fixture), dst, true);
        return dst;
    }

    /// <summary>The TPH/ALS/IMU parsers write their CSV with an un-awaitable "async void"
    /// WriteFile: wait until the file holds the expected number of lines.</summary>
    public static string[] WaitForLines(string path, int expectedLines, int timeoutMs = 5000)
    {
        var until = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        string[] lines = Array.Empty<string>();
        while (DateTime.UtcNow < until)
        {
            try
            {
                if (File.Exists(path))
                {
                    lines = File.ReadAllLines(path);
                    if (lines.Length >= expectedLines) return lines;
                }
            }
            catch (IOException) { /* still being written */ }
            Thread.Sleep(20);
        }
        return lines;
    }
}

/// <summary>Builders for the binary layouts the firmware writes.</summary>
internal static class Raw
{
    public static byte Bcd(int v) => (byte)(((v / 10) << 4) | (v % 10));

    /// <summary>128-byte file header (0xDEAFDAC0), name NUL-padded like the firmware's strncpy.</summary>
    public static byte[] FileHeader(string name, uint rate, uint bitmask, uint c0, uint c1, uint c2, uint c3,
                                    uint uid = 0x12345678, ushort fwid = 0x0108, uint winLen = 0, uint winRate = 0)
    {
        var ms = new MemoryStream();
        var w = new BinaryWriter(ms);
        w.Write(0xDEAFDAC0u);
        w.Write(uid);
        var nb = new byte[16];
        System.Text.Encoding.ASCII.GetBytes(name).CopyTo(nb, 0);
        w.Write(nb);
        w.Write(fwid);
        w.Write((ushort)0);
        w.Write(rate);
        w.Write(winLen);
        w.Write(winRate);
        w.Write(bitmask);
        w.Write(c0); w.Write(c1); w.Write(c2); w.Write(c3);
        w.Write(0xFFFFFFFFu);
        while (ms.Length < 128) w.Write((byte)0xFF);
        return ms.ToArray();
    }

    /// <summary>16-byte start (0xA55AA55A) / footer (0xA55A5AA5) / sync block: BCD time + date, subsecond fraction/value.</summary>
    public static byte[] Block(uint marker, DateTime t, ushort subsecFrac = 255, ushort subsec = 255)
    {
        var ms = new MemoryStream();
        var w = new BinaryWriter(ms);
        w.Write(marker);
        w.Write(Bcd(t.Hour)); w.Write(Bcd(t.Minute)); w.Write(Bcd(t.Second)); w.Write((byte)0);
        w.Write((byte)7); w.Write(Bcd(t.Month)); w.Write(Bcd(t.Day)); w.Write(Bcd(t.Year - 2000));
        w.Write(subsecFrac); w.Write(subsec);
        return ms.ToArray();
    }

    public static byte[] Concat(params byte[][] parts) => parts.SelectMany(p => p).ToArray();
}
