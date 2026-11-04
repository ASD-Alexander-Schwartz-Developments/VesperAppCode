using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using ASD.DeviceCore.Protocol;
using ASD.DeviceCore.Transport;
using VesperApp;
using VesperApp.Services;
using Xunit;

namespace VesperApp.Tests.Services;

/// <summary>Lepton live view (asd-interfaces interfaces/lepton-live.md): LVF1 frames split out of the
/// console stream between messages, checked by size and CRC-32, and drawn through the Lepton palettes.</summary>
public class ThermalLiveTests
{
    private static byte[] Lvf(int w, int h, uint frame, Func<int, ushort> pix, byte flags = 0x07, short fpa = 3012, uint? crc = null, uint? len = null)
    {
        var payload = new byte[w * h * 2];
        for (int i = 0; i < w * h; i++) { ushort v = pix(i); payload[2 * i] = (byte)v; payload[2 * i + 1] = (byte)(v >> 8); }
        var hd = new byte[32];
        Encoding.ASCII.GetBytes("LVF1").CopyTo(hd, 0);
        BitConverter.GetBytes((ushort)w).CopyTo(hd, 4);
        BitConverter.GetBytes((ushort)h).CopyTo(hd, 6);
        BitConverter.GetBytes(frame).CopyTo(hd, 8);
        BitConverter.GetBytes(12345u).CopyTo(hd, 12);
        BitConverter.GetBytes(fpa).CopyTo(hd, 16);
        BitConverter.GetBytes((short)2875).CopyTo(hd, 18);
        hd[20] = flags;
        BitConverter.GetBytes(len ?? (uint)payload.Length).CopyTo(hd, 24);
        BitConverter.GetBytes(crc ?? ThermalFrameDemux.Crc32(payload)).CopyTo(hd, 28);
        return hd.Concat(payload).ToArray();
    }

    private static ushort Px(int i) => (ushort)(29315 + i % 1000);   // TLinear 0.01 K: 20.00 .. 29.99 °C

    [Fact]
    public void Crc32_IsZlib() =>
        Assert.Equal(0xCBF43926u, ThermalFrameDemux.Crc32(Encoding.ASCII.GetBytes("123456789")));

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(4096)]
    [InlineData(65536)]
    public void Demux_ReassemblesAFrameInAnyChunking(int chunk)
    {
        var d = new ThermalFrameDemux();
        byte[] bytes = Lvf(160, 120, 42, Px);
        ThermalFrame? got = null;
        int off = 0;
        while (off < bytes.Length)
        {
            int n = Math.Min(chunk, bytes.Length - off);
            int used = 0;
            while (used < n)
            {
                used += d.Feed(bytes.AsSpan(off + used, n - used), out ThermalFrame? f);
                if (f != null) got = f;
            }
            off += n;
        }
        Assert.NotNull(got);
        Assert.Equal(160, got!.Width);
        Assert.Equal(120, got.Height);
        Assert.Equal(42u, got.FrameNumber);
        Assert.Equal(30.12, got.FpaC!.Value, 2);
        Assert.Equal(28.75, got.HousingC!.Value, 2);
        Assert.True(got.IsTLinear);
        Assert.True(got.IsLepton3);
        Assert.Equal(0.01, got.KelvinPerCount);
        Assert.Equal(Px(5 * 160 + 7), got.At(7, 5));
        Assert.Equal(20.0, got.Celsius(29315), 2);
        Assert.False(d.Active);
        Assert.Equal(1, d.Received);
    }

    [Fact]
    public void Demux_Lepton2Size()
    {
        var d = new ThermalFrameDemux();
        byte[] b = Lvf(80, 60, 1, i => (ushort)i, flags: 0);
        Assert.Equal(b.Length, d.Feed(b, out ThermalFrame? f));
        Assert.NotNull(f);
        Assert.Equal(80, f!.Width);
        Assert.False(f.IsTLinear);
        Assert.True(double.IsNaN(f.Celsius(100)));
    }

    [Fact]
    public void Demux_DropsBadCrcAndBadSize()
    {
        var d = new ThermalFrameDemux();
        byte[] bad = Lvf(160, 120, 1, Px, crc: 0x12345678);
        Assert.Equal(bad.Length, d.Feed(bad, out ThermalFrame? f));
        Assert.Null(f);
        byte[] odd = Lvf(100, 100, 1, Px);                       // not a Lepton size: header only consumed
        Assert.Equal(32, d.Feed(odd, out f));
        Assert.Null(f);
        byte[] lenWrong = Lvf(160, 120, 1, Px, len: 100);
        Assert.Equal(32, d.Feed(lenWrong, out f));
        Assert.Equal(3, d.Dropped);
        Assert.False(d.Active);
    }

    [Fact]
    public void Demux_NotAFrame_LeavesTheByteToTheConsole()
    {
        var d = new ThermalFrameDemux();
        byte[] notLvf = { 0x4C, 0x56, 0x5A, 0x02 };              // "LV" then a console SOH
        int used = d.Feed(notLvf, out ThermalFrame? f);
        Assert.Equal(2, used);
        Assert.Null(f);
        Assert.False(d.Active);
    }

    /// <summary>A link that replays a byte script in fixed-size reads.</summary>
    private sealed class ScriptLink : ISerialLink
    {
        private readonly Channel<byte[]> _ch = Channel.CreateUnbounded<byte[]>();
        public ScriptLink(byte[] script, int chunk)
        {
            for (int o = 0; o < script.Length; o += chunk)
                _ch.Writer.TryWrite(script.AsSpan(o, Math.Min(chunk, script.Length - o)).ToArray());
        }
        public bool IsOpen => true;
        public void Open() { }
        public void Close() { }
        public Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default) => Task.CompletedTask;
        private byte[] _rest = Array.Empty<byte>();
        public async Task<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
        {
            byte[] b = _rest.Length > 0 ? _rest : await _ch.Reader.ReadAsync(ct);
            int n = Math.Min(b.Length, buffer.Length);
            b.AsSpan(0, n).CopyTo(buffer.Span);
            _rest = b[n..];
            return n;
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    [Theory]
    [InlineData(64)]
    [InlineData(1000)]
    [InlineData(100000)]
    public async Task ConsoleClient_SeparatesFramesFromMessages(int chunk)
    {
        byte[] reply = AsdFrame.Build(70, new byte[] { 0, 1, 1, 0, 0xC4, 0x0B, 0x3B, 0x0B, 0, 0 }, 2);
        byte[] gnssLike = AsdFrame.Build(0x4C, new byte[] { 0x4C, 0x56, 0x46, 0x31 }, 2);   // 'L'-typed message with LVF1 inside
        byte[] script = reply.Concat(Lvf(160, 120, 0, Px)).Concat(gnssLike).Concat(Lvf(160, 120, 1, Px)).Concat(reply).ToArray();

        await using var client = new AsdConsoleClient(new ScriptLink(script, chunk));
        var frames = new List<ThermalFrame>();
        var msgs = new List<AsdMessage>();
        var done = new TaskCompletionSource();
        client.ThermalFrameReceived += f => { lock (frames) frames.Add(f); };
        client.MessageReceived += m => { lock (msgs) { msgs.Add(m); if (msgs.Count == 3) done.TrySetResult(); } };
        client.Start();
        await Task.WhenAny(done.Task, Task.Delay(5000));

        Assert.Equal(new byte[] { 70, 0x4C, 70 }, msgs.Select(m => m.Type).ToArray());
        Assert.Equal(new uint[] { 0, 1 }, frames.Select(f => f.FrameNumber).ToArray());
        Assert.Equal(0, client.ThermalFramesDropped);
    }

    [Fact]
    public void Renderer_RangeAndPalette()
    {
        ushort[] px = { 100, 200, 300, 400 };
        var (lo, hi) = ThermalRenderer.Range(px);
        Assert.Equal((ushort)100, lo);
        Assert.Equal((ushort)400, hi);
        var bgra = new byte[16];
        ThermalRenderer.ToBgra(px, lo, hi, LeptonFilterType.LEPTON_GRAYSCALE, bgra);
        Assert.Equal(new byte[] { 0, 0, 0, 255 }, bgra[..4]);
        Assert.Equal(new byte[] { 255, 255, 255, 255 }, bgra[12..]);
        Assert.Equal(768, ThermalRenderer.Palette(LeptonFilterType.LEPTON_IRONBLACK).Length);
    }

    [Fact]
    public void Renderer_FormatsAndCsv()
    {
        var d = new ThermalFrameDemux();
        d.Feed(Lvf(80, 60, 3, i => 29815, flags: 0x03), out ThermalFrame? f);   // 25.00 °C everywhere
        Assert.Equal("25.0 °C", ThermalRenderer.Format(f!, 29815));
        string csv = ThermalRenderer.ToCsv(f!);
        string[] lines = csv.TrimEnd('\n').Split('\n');
        Assert.Equal(61, lines.Length);
        Assert.StartsWith("# Lepton 2.x 80x60, frame 3, deg C", lines[0]);
        Assert.Equal(80, lines[1].Split(',').Length);
        Assert.Equal("25.00", lines[60].Split(',')[79]);

        d.Feed(Lvf(80, 60, 4, i => 8123, flags: 0), out f);
        Assert.Equal("8123 (raw)", ThermalRenderer.Format(f!, 8123));
    }
}
