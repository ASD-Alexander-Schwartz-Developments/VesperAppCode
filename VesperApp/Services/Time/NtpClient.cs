using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace VesperApp.Services.Time
{
    /// <summary>Result of an SNTP query: how far the PC clock is from NTP time.</summary>
    /// <param name="OffsetSeconds">NTP time minus PC time.</param>
    /// <param name="DelaySeconds">Network round trip minus server processing time.</param>
    public sealed record NtpSample(string Server, double OffsetSeconds, double DelaySeconds, DateTime PcUtcAt);

    /// <summary>
    /// Minimal SNTPv4 client (RFC 4330). The PC clock alone is not a usable reference: the default
    /// Windows time service keeps it only to tens or hundreds of milliseconds. Several servers are
    /// queried a few times each and the reply with the smallest round trip wins, which is also the
    /// one whose offset is least affected by path asymmetry.
    /// </summary>
    public static class NtpClient
    {
        public static readonly string[] DefaultServers =
            { "time.google.com", "time.cloudflare.com", "pool.ntp.org", "time.windows.com" };

        /// <summary>Servers from VESPERAPP_NTP_SERVERS (comma separated) or the defaults.</summary>
        public static IReadOnlyList<string> ConfiguredServers()
        {
            string? env = Environment.GetEnvironmentVariable("VESPERAPP_NTP_SERVERS");
            if (string.IsNullOrWhiteSpace(env)) return DefaultServers;
            return env.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        private static readonly DateTime NtpEpoch = new(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private static void WriteTimestamp(byte[] b, int at, DateTime utc)
        {
            double s = (utc - NtpEpoch).TotalSeconds;
            ulong secs = (ulong)Math.Floor(s);
            ulong frac = (ulong)((s - secs) * 4294967296.0);
            for (int i = 0; i < 4; i++) b[at + i] = (byte)(secs >> (24 - 8 * i));
            for (int i = 0; i < 4; i++) b[at + 4 + i] = (byte)(frac >> (24 - 8 * i));
        }

        private static DateTime ReadTimestamp(byte[] b, int at)
        {
            ulong secs = 0, frac = 0;
            for (int i = 0; i < 4; i++) secs = (secs << 8) | b[at + i];
            for (int i = 0; i < 4; i++) frac = (frac << 8) | b[at + 4 + i];
            return NtpEpoch.AddTicks((long)(secs * TimeSpan.TicksPerSecond + frac * TimeSpan.TicksPerSecond / 4294967296UL));
        }

        public static async Task<NtpSample?> QueryAsync(string server, int timeoutMs = 1500, CancellationToken ct = default)
        {
            try
            {
                IPAddress[] addrs = await Dns.GetHostAddressesAsync(server, ct).ConfigureAwait(false);
                IPAddress? ip = addrs.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork) ?? addrs.FirstOrDefault();
                if (ip == null) return null;

                using var udp = new UdpClient(ip.AddressFamily);
                udp.Connect(new IPEndPoint(ip, 123));

                var req = new byte[48];
                req[0] = 0x23;                              // LI 0, version 4, mode 3 (client)
                DateTime t1 = DateTime.UtcNow;
                WriteTimestamp(req, 40, t1);                // transmit timestamp, echoed as originate
                await udp.SendAsync(req, req.Length).ConfigureAwait(false);

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(timeoutMs);
                UdpReceiveResult res = await udp.ReceiveAsync(cts.Token).ConfigureAwait(false);
                DateTime t4 = DateTime.UtcNow;

                byte[] r = res.Buffer;
                if (r.Length < 48) return null;
                int mode = r[0] & 0x07, stratum = r[1];
                if (mode != 4 || stratum < 1 || stratum > 15) return null;
                for (int i = 0; i < 8; i++) if (r[24 + i] != req[40 + i]) return null;   // not our request

                DateTime t2 = ReadTimestamp(r, 32), t3 = ReadTimestamp(r, 40);
                double offset = ((t2 - t1).TotalSeconds + (t3 - t4).TotalSeconds) / 2.0;
                double delay = (t4 - t1).TotalSeconds - (t3 - t2).TotalSeconds;
                return delay < 0 ? null : new NtpSample(server, offset, delay, t4);
            }
            catch (Exception ex) when (ex is SocketException || ex is OperationCanceledException || ex is ObjectDisposedException)
            {
                return null;
            }
        }

        /// <summary>Best (smallest round trip) sample over <paramref name="perServer"/> queries per server.</summary>
        public static async Task<NtpSample?> BestOfAsync(IEnumerable<string>? servers = null, int perServer = 3, CancellationToken ct = default)
        {
            var tasks = new List<Task<NtpSample?>>();
            foreach (string s in servers ?? ConfiguredServers())
                for (int i = 0; i < perServer; i++)
                    tasks.Add(QueryAsync(s, 1500, ct));
            NtpSample?[] all = await Task.WhenAll(tasks).ConfigureAwait(false);
            return all.Where(x => x != null).OrderBy(x => x!.DelaySeconds).FirstOrDefault();
        }
    }

    /// <summary>
    /// True UTC for any <see cref="Stopwatch"/> timestamp: one (Stopwatch, PC UTC) pair captured
    /// together plus the PC-to-NTP offset. Over a sync session of seconds the Stopwatch rate error
    /// (ppm class) is negligible; build a new instance for each session.
    /// </summary>
    public sealed class ReferenceClock
    {
        private readonly long _ts0;
        private readonly DateTime _pcUtc0;

        public string Source { get; }
        public double PcOffsetSeconds { get; }       // NTP minus PC
        public double UncertaintySeconds { get; }    // half the NTP round trip, or 0.5 s for the bare PC clock
        public bool IsNtp { get; }

        private ReferenceClock(string source, double offset, double uncertainty, bool isNtp)
        {
            _ts0 = Stopwatch.GetTimestamp();
            _pcUtc0 = DateTime.UtcNow;
            Source = source;
            PcOffsetSeconds = offset;
            UncertaintySeconds = uncertainty;
            IsNtp = isNtp;
        }

        public static ReferenceClock FromPcClock() => new("PC clock (not NTP-verified)", 0.0, 0.5, false);

        /// <summary>Test hook: a reference shifted by <paramref name="offsetSeconds"/> (simulates elapsed days).</summary>
        internal static ReferenceClock ForTest(double offsetSeconds, double uncertaintySeconds) =>
            new("NTP test", offsetSeconds, uncertaintySeconds, true);

        /// <summary>NTP-backed reference, or the bare PC clock when no server answers.</summary>
        public static async Task<ReferenceClock> CreateAsync(CancellationToken ct = default)
        {
            NtpSample? s = await NtpClient.BestOfAsync(null, 3, ct).ConfigureAwait(false);
            return s == null
                ? FromPcClock()
                : new ReferenceClock($"NTP {s.Server}", s.OffsetSeconds, s.DelaySeconds / 2.0, true);
        }

        public DateTime TrueUtcAt(long stopwatchTimestamp)
        {
            long ticks = (long)((stopwatchTimestamp - _ts0) * (double)TimeSpan.TicksPerSecond / Stopwatch.Frequency);
            return _pcUtc0.AddTicks(ticks).AddSeconds(PcOffsetSeconds);
        }

        public DateTime TrueUtcNow() => TrueUtcAt(Stopwatch.GetTimestamp());
    }
}
