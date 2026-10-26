using System.Diagnostics;
using VesperApp.Models;
using VesperApp.Services;
using Xunit;

namespace VesperApp.Tests.Services;

/// <summary>
/// COM-port discovery against fake serial ports: the GET_VER reply search, the probe exchange, and the time budget
/// that keeps a port blocked in the driver from hanging the scan (KOL bring-up 2026-10-26, B3: the scan hung while
/// the KOL was in Live Audio).
/// </summary>
public class ComPortScanTests
{
    private sealed class FakePort : IProbePort
    {
        public string PortName { get; init; } = "COM_FAKE";
        public ManualResetEventSlim? BlockOpen, BlockClose;
        public Exception? OpenError;
        public readonly Queue<byte[]> Incoming = new();
        public readonly List<byte[]> Written = new();
        public int Opens, Closes;

        public void Open()
        {
            Interlocked.Increment(ref Opens);
            BlockOpen?.Wait();
            if (OpenError != null) throw OpenError;
        }

        public void Write(byte[] buffer, int offset, int count) => Written.Add(buffer.Skip(offset).Take(count).ToArray());

        public int Read(byte[] buffer, int offset, int count)
        {
            lock (Incoming)
            {
                if (Incoming.Count == 0) throw new TimeoutException();
                byte[] chunk = Incoming.Dequeue();
                int n = Math.Min(count, chunk.Length);
                Array.Copy(chunk, 0, buffer, offset, n);
                if (n < chunk.Length)
                {
                    var rest = new Queue<byte[]>(new[] { chunk.Skip(n).ToArray() }.Concat(Incoming));
                    Incoming.Clear();
                    foreach (byte[] c in rest) Incoming.Enqueue(c);
                }
                return n;
            }
        }

        public void Close()
        {
            Interlocked.Increment(ref Closes);
            BlockClose?.Wait();
        }
    }

    /// <summary>The GET_VER reply frame of a KOL 1.8, UID 0x4762505A.</summary>
    private static byte[] KolReply(byte minor = 8) => SerialMessage.PROTO_MsgBuild((byte)MessageTypes.VESPER_GET_VER, 8,
        new byte[] { 1, minor, 0x5A, 0x50, 0x62, 0x47, (byte)DeviceTypes.Kol, 0 }, 2);

    /// <summary>Another frame that starts with SOH (e.g. an asynchronous status frame of a unit in Live Audio).</summary>
    private static byte[] OtherFrame() => SerialMessage.PROTO_MsgBuild((byte)MessageTypes.VESPER_TIME_SYNC, 12, new byte[12], 2);

    private static readonly GetVerReply Kol18 = new(1, 8, 0x4762505A, DeviceTypes.Kol);

    // ---- reply search ----

    [Fact]
    public void FindGetVer_ParsesTheReply()
    {
        byte[] r = KolReply();
        Assert.Equal(Kol18, ComPortProbe.FindGetVer(r, r.Length));
    }

    [Fact]
    public void FindGetVer_SkipsFramesAndBytesBeforeTheReply()
    {
        /* the original scan looked at the first SOH only, so a frame before the reply hid the unit */
        byte[] b = new byte[] { 0x00, 0x13 }.Concat(OtherFrame()).Concat(KolReply()).ToArray();
        Assert.Equal(Kol18, ComPortProbe.FindGetVer(b, b.Length));
    }

    [Fact]
    public void FindGetVer_IncompleteOrAbsent_IsNull()
    {
        byte[] r = KolReply();
        Assert.Null(ComPortProbe.FindGetVer(r, 10));                 // device type byte not yet received
        Assert.NotNull(ComPortProbe.FindGetVer(r, 11));
        Assert.Null(ComPortProbe.FindGetVer(OtherFrame(), OtherFrame().Length));
        Assert.Null(ComPortProbe.FindGetVer(new byte[64], 64));
    }

    // ---- exchange ----

    [Fact]
    public void Exchange_ChunkedReply_IsAssembled_AndThePortClosed()
    {
        byte[] r = KolReply();
        var port = new FakePort();
        port.Incoming.Enqueue(r[..3]);
        port.Incoming.Enqueue(r[3..7]);
        port.Incoming.Enqueue(r[7..]);

        Assert.Equal(Kol18, ComPortProbe.Exchange(port, TimeSpan.FromSeconds(2)));
        Assert.Equal(SerialMessage.PROTO_MsgBuild((byte)MessageTypes.VESPER_GET_VER, 0, Array.Empty<byte>(), 0), Assert.Single(port.Written));
        Assert.Equal(1, port.Closes);
    }

    [Fact]
    public void Exchange_ReplyBehindMoreThanABufferOfOtherData_IsFound()
    {
        var port = new FakePort();
        for (int i = 0; i < 20; i++) port.Incoming.Enqueue(OtherFrame());   // 340 bytes before the reply
        port.Incoming.Enqueue(KolReply());
        Assert.Equal(Kol18, ComPortProbe.Exchange(port, TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void Exchange_NoReply_EndsAfterTheWindow_AndCloses()
    {
        var port = new FakePort();
        var sw = Stopwatch.StartNew();
        Assert.Null(ComPortProbe.Exchange(port, TimeSpan.FromMilliseconds(200)));
        Assert.InRange(sw.ElapsedMilliseconds, 150, 2000);
        Assert.Equal(1, port.Closes);
    }

    [Fact]
    public void Exchange_PortInUse_IsNoLogger()
    {
        var port = new FakePort { OpenError = new UnauthorizedAccessException("in use") };
        Assert.Null(ComPortProbe.Exchange(port, TimeSpan.FromSeconds(1)));
        Assert.Empty(port.Written);
        Assert.Equal(1, port.Closes);
    }

    // ---- budget ----

    [Fact]
    public async Task Probe_PortBlockedInTheDriver_GivesUpAfterTheBudget_AndSkipsItUntilItReturns()
    {
        using var gate = new ManualResetEventSlim(false);
        var port = new FakePort { BlockOpen = gate };
        port.Incoming.Enqueue(KolReply());
        var probe = new ComPortProbe(_ => port, budget: TimeSpan.FromMilliseconds(300), replyWindow: TimeSpan.FromMilliseconds(500));

        var sw = Stopwatch.StartNew();
        Assert.Null(await probe.ProbeAsync("COM7"));
        Assert.InRange(sw.ElapsedMilliseconds, 250, 3000);
        Assert.True(probe.IsStuck("com7"));

        sw.Restart();
        Assert.Null(await probe.ProbeAsync("COM7"));                     // not opened a second time
        Assert.InRange(sw.ElapsedMilliseconds, 0, 200);
        Assert.Equal(1, port.Opens);

        gate.Set();                                                     // the driver call returns
        await WaitUntil(() => !probe.IsStuck("COM7"));
        Assert.Equal(1, port.Closes);                                   // the worker closed the port itself

        port.Incoming.Enqueue(KolReply());
        Assert.Equal(Kol18, await probe.ProbeAsync("COM7"));            // probed again
        Assert.Equal(2, port.Opens);
    }

    [Fact]
    public async Task Probe_CloseBlocks_DoesNotHangTheCaller()
    {
        using var gate = new ManualResetEventSlim(false);
        var port = new FakePort { BlockClose = gate };
        var probe = new ComPortProbe(_ => port, budget: TimeSpan.FromMilliseconds(400), replyWindow: TimeSpan.FromMilliseconds(100));

        var sw = Stopwatch.StartNew();
        Assert.Null(await probe.ProbeAsync("COM7"));
        Assert.InRange(sw.ElapsedMilliseconds, 300, 3000);
        Assert.True(probe.IsStuck("COM7"));
        gate.Set();
    }

    [Fact]
    public async Task Probe_PortFactoryThrows_IsNoLogger()
    {
        var probe = new ComPortProbe(_ => throw new IOException("gone"), budget: TimeSpan.FromSeconds(1));
        Assert.Null(await probe.ProbeAsync("COM7"));
        Assert.False(probe.IsStuck("COM7"));
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        var until = DateTime.UtcNow.AddSeconds(5);
        while (!condition() && DateTime.UtcNow < until) await Task.Delay(10);
        Assert.True(condition());
    }

    // ---- adapter scan ----

    [Fact]
    public async Task Scan_BlockedPortDoesNotHideOrHangTheOthers()
    {
        using var gate = new ManualResetEventSlim(false);
        var ports = new Dictionary<string, FakePort>
        {
            ["COM5"] = new FakePort { PortName = "COM5", BlockOpen = gate },  // e.g. a unit streaming Live Audio
            ["COM9"] = new FakePort { PortName = "COM9" },
        };
        ports["COM9"].Incoming.Enqueue(KolReply());
        string[] listed = { "COM5", "COM9" };
        var probe = new ComPortProbe(n => ports[n], budget: TimeSpan.FromMilliseconds(300), replyWindow: TimeSpan.FromMilliseconds(300));
        var adapter = new DeviceUsbAdapter(() => listed, probe);
        try
        {
            var sw = Stopwatch.StartNew();
            List<LoggerDevice> found = (await adapter.ScanComPortsAsync(true)).ToList();
            Assert.InRange(sw.ElapsedMilliseconds, 0, 4000);

            LoggerDevice kol = Assert.Single(found);
            Assert.Equal("COM9", kol.ComPort);
            Assert.Equal("4762505A", kol.SerialNumber);
            Assert.Equal(DeviceTypes.Kol, kol.DeviceType);
            Assert.Equal("1.8", kol.FwId);

            /* the next periodic scan skips the blocked port at once and keeps the KOL */
            ports["COM9"].Incoming.Enqueue(KolReply());
            sw.Restart();
            Assert.Single(await adapter.ScanComPortsAsync(false));
            Assert.InRange(sw.ElapsedMilliseconds, 0, 2000);
            Assert.Equal(1, ports["COM5"].Opens);

            /* unplugged: the port is no longer listed, the device goes */
            listed = new[] { "COM5" };
            Assert.Empty(await adapter.ScanComPortsAsync(false));
        }
        finally
        {
            gate.Set();
            adapter.Dispose();
        }
    }
}
