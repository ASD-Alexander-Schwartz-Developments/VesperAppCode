using System.IO;
using System.Text;
using VesperApp.Services;
using Xunit;

namespace VesperApp.Tests.Services;

/// <summary>
/// Nanotag hex checks before anything is sent to the bootloader (check B-11, bench 2026-11-01: a truncated file
/// verified, the jump made bootloader V1.1 mark the half image valid, and the tag needed a debugger).
/// </summary>
public class NanotagFlasherImageTests
{
    // Intel HEX record: count, address, type, data, checksum.
    private static string Rec(ushort addr, byte type, params byte[] data)
    {
        var b = new List<byte> { (byte)data.Length, (byte)(addr >> 8), (byte)addr, type };
        b.AddRange(data);
        b.Add((byte)-b.Sum(x => x));
        return ":" + Convert.ToHexString(b.ToArray());
    }

    private static readonly byte[] Vectors = { 0xF8, 0x7F, 0x00, 0x20, 0x09, 0x72, 0x02, 0x00 };
    private static readonly byte[] ValidWord = BitConverter.GetBytes(NanotagFlasher.AppValidWord);

    // Upper 16 bits 0 (0x8000 / 0x3FFFC sit below 64 KB pages 0 and 3).
    private static string Image(bool vectors = true, bool validWord = true, bool eof = true)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Rec(0, 0x04, 0x00, 0x00));
        if (vectors) sb.AppendLine(Rec(0x8000, 0x00, Vectors));
        if (validWord)
        {
            sb.AppendLine(Rec(0, 0x04, 0x00, 0x03));
            sb.AppendLine(Rec(0xFFFC, 0x00, ValidWord));
        }
        if (eof) sb.AppendLine(Rec(0, 0x01));
        return sb.ToString();
    }

    private static string Write(string content)
    {
        string path = Path.Combine(Path.GetTempPath(), $"nt-{Guid.NewGuid():N}.hex");
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void CompleteImage_IsAccepted()
    {
        string p = Write(Image());
        try { NanotagFlasher.ValidateImage(p); }
        finally { File.Delete(p); }
    }

    [Fact]
    public void MissingEofRecord_IsRefused()
    {
        string p = Write(Image(eof: false));
        try { Assert.Contains("End-Of-File", Assert.Throws<InvalidDataException>(() => NanotagFlasher.ValidateImage(p)).Message); }
        finally { File.Delete(p); }
    }

    [Fact]
    public void MissingAppValidWord_IsRefused()
    {
        string p = Write(Image(validWord: false));
        try { Assert.Contains("app-valid word", Assert.Throws<InvalidDataException>(() => NanotagFlasher.ValidateImage(p)).Message); }
        finally { File.Delete(p); }
    }

    [Fact]
    public void WrongAppValidWord_IsRefused()
    {
        string p = Write(Image().Replace(Rec(0xFFFC, 0x00, ValidWord), Rec(0xFFFC, 0x00, 0, 0, 0, 0)));
        try { Assert.Throws<InvalidDataException>(() => NanotagFlasher.ValidateImage(p)); }
        finally { File.Delete(p); }
    }

    [Fact]
    public void TruncatedRealisticFile_IsRefused()
    {
        // The first half of an image: vectors, no app-valid word, no EOF.
        string p = Write(Image(validWord: false, eof: false));
        try { Assert.Throws<InvalidDataException>(() => NanotagFlasher.ValidateImage(p)); }
        finally { File.Delete(p); }
    }
}
