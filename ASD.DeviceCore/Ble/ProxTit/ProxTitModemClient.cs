using System;
using System.Buffers.Binary;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ASD.DeviceCore.Ble.ProxTit
{
    /// <summary>Raised when a modem op returns a non-OK status (denied, no-file, busy).</summary>
    public sealed class ProxTitModemException : Exception
    {
        public byte Status { get; }
        public ProxTitModemException(string op, byte status)
            : base($"ProxTit modem op '{op}' failed with status 0x{status:X2} " +
                   $"({DescribeStatus(status)}).")
            => Status = status;

        private static string DescribeStatus(byte s) => s switch
        {
            ProxTitGatt.StEof => "EOF",
            ProxTitGatt.StBusy => "busy/recording",
            ProxTitGatt.StDenied => "denied (auth required or operation disabled)",
            ProxTitGatt.StNoFile => "no such file",
            _ => "unknown",
        };
    }

    /// <summary>
    /// Drives a remote VT04 data download through a ProxTit tag acting as a BLE modem
    /// (scenario 3). Pure protocol logic over <see cref="IBleCentral"/> — works
    /// identically against real hardware (BGAPI NCP) or the simulator, and is reused by
    /// both the desktop app and the Linux base-station daemon.
    ///
    /// Each op writes a command to the control point (prx_datacp) then reads the result
    /// chunk from prx_log, exactly as the firmware delivers it.
    /// </summary>
    public sealed class ProxTitModemClient
    {
        private readonly IBleCentral _ble;
        private readonly ILogger _log;

        public ProxTitModemClient(IBleCentral ble, ILogger? log = null)
        {
            _ble = ble ?? throw new ArgumentNullException(nameof(ble));
            _log = log ?? NullLogger.Instance;
        }

        /// <summary>Whether the last <see cref="OpenAsync"/> authenticated (a deployer key
        /// was supplied). Delete is only permitted on an authenticated session.</summary>
        public bool Authenticated { get; private set; }

        /// <summary>
        /// Open a modem session and return the file catalog. When <paramref name="deployerKey"/>
        /// is non-null, an HMAC auth blob is sent with <paramref name="nonce"/> (which must
        /// be strictly greater than any previously used — the caller persists it). When null,
        /// the session is unauthenticated (only allowed if the deployment configured no key).
        /// </summary>
        public async Task<ProxTitCatalog> OpenAsync(
            byte[]? deployerKey, uint nonce, CancellationToken ct = default)
        {
            byte[] payload = deployerKey != null
                ? ModemAuth.BuildOpenBlob(deployerKey, nonce)
                : Array.Empty<byte>();

            ModemChunk chunk = await OpAsync(
                ProxTitGatt.CmdModemOpen, payload, "MODEM_OPEN", ct).ConfigureAwait(false);

            Authenticated = deployerKey != null;
            var catalog = ProxTitCatalog.Parse(chunk.Data);
            _log.LogInformation(
                "ProxTit modem open: {Streams} stream(s), {FreeKb} KiB free, authenticated={Auth}.",
                catalog.Streams.Count, catalog.FreeKiloBytes, Authenticated);
            return catalog;
        }

        /// <summary>Query one file's size in bytes (MODEM_FILEINFO).</summary>
        public async Task<uint> GetFileSizeAsync(byte sensor, uint index, CancellationToken ct = default)
        {
            byte[] payload = BuildSensorIndex(sensor, index);
            ModemChunk chunk = await OpAsync(
                ProxTitGatt.CmdModemFileInfo, payload, "MODEM_FILEINFO", ct).ConfigureAwait(false);
            if (chunk.Data.Length < 4)
                throw new FormatException("FILEINFO reply did not contain a u32 size.");
            return BinaryPrimitives.ReadUInt32LittleEndian(chunk.Data);
        }

        /// <summary>
        /// Download a complete file (MODEM_CHUNK loop) into <paramref name="dest"/>.
        /// Returns the number of bytes written. Requests are sized to the negotiated MTU.
        /// </summary>
        public async Task<long> DownloadFileAsync(
            byte sensor, uint index, Stream dest,
            IProgress<long>? progress = null, CancellationToken ct = default)
        {
            int chunkLen = MaxChunkLen();
            long total = 0;
            uint offset = 0;

            while (true)
            {
                ct.ThrowIfCancellationRequested();
                byte[] payload = BuildChunkRequest(sensor, index, offset, (ushort)chunkLen);
                ModemChunk chunk = await OpAsync(
                    ProxTitGatt.CmdModemGetChunk, payload, "MODEM_CHUNK", ct, allowEof: true)
                    .ConfigureAwait(false);

                if (chunk.Data.Length > 0)
                {
                    await dest.WriteAsync(chunk.Data.AsMemory(0, chunk.Data.Length), ct).ConfigureAwait(false);
                    total += chunk.Data.Length;
                    offset += (uint)chunk.Data.Length;
                    progress?.Report(total);
                }

                // EOF, or a short read (fewer bytes than requested) ⇒ end of file.
                if (chunk.IsEof || chunk.Data.Length < chunkLen)
                    break;
            }

            _log.LogInformation("Downloaded {Stream}[{Index}] = {Bytes} bytes.",
                ProxTitGatt.StreamName(sensor), index, total);
            return total;
        }

        /// <summary>Download config.json (the dedicated config stream).</summary>
        public Task<long> DownloadConfigAsync(Stream dest, IProgress<long>? progress = null, CancellationToken ct = default)
            => DownloadFileAsync(ProxTitGatt.StreamConfig, 0, dest, progress, ct);

        /// <summary>Delete a complete (non-live) file (MODEM_DELETE). Requires an
        /// authenticated session; config.json can never be deleted.</summary>
        public async Task DeleteFileAsync(byte sensor, uint index, CancellationToken ct = default)
        {
            if (!Authenticated)
                throw new InvalidOperationException(
                    "Delete requires an authenticated modem session (open with a deployer key).");
            byte[] payload = BuildSensorIndex(sensor, index);
            await OpAsync(ProxTitGatt.CmdModemDelete, payload, "MODEM_DELETE", ct).ConfigureAwait(false);
        }

        // ---- internals ----

        /// <summary>Write a control-point command then read + validate the result chunk.</summary>
        private async Task<ModemChunk> OpAsync(
            byte cmd, byte[] payload, string name, CancellationToken ct, bool allowEof = false)
        {
            // gapp_cp_cmd_t = { u8 cmd; u8 data[27] }. Send cmd + payload.
            var frame = new byte[1 + payload.Length];
            frame[0] = cmd;
            payload.CopyTo(frame, 1);
            await _ble.WriteAsync(ProxTitGatt.DataCp, frame, withResponse: true, ct).ConfigureAwait(false);

            byte[] raw = await _ble.ReadAsync(ProxTitGatt.Log, ct).ConfigureAwait(false);
            ModemChunk chunk = ModemChunk.Parse(raw);

            if (chunk.IsOk || (allowEof && chunk.IsEof))
                return chunk;

            throw new ProxTitModemException(name, chunk.Status);
        }

        /// <summary>Largest chunk payload that fits the negotiated MTU (att_mtu - 1 for the
        /// read header, minus the 8-byte chunk header), capped at the firmware max.</summary>
        private int MaxChunkLen()
        {
            int budget = Math.Max(1, _ble.Mtu - 1 - ProxTitGatt.ChunkHeaderLen);
            return Math.Min(ProxTitGatt.ModemChunkMax, budget);
        }

        private static byte[] BuildSensorIndex(byte sensor, uint index)
        {
            var b = new byte[5];
            b[0] = sensor;
            BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(1, 4), index);
            return b;
        }

        private static byte[] BuildChunkRequest(byte sensor, uint index, uint offset, ushort len)
        {
            // { u8 sensor, u32 index, u32 offset, u16 len }
            var b = new byte[11];
            b[0] = sensor;
            BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(1, 4), index);
            BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(5, 4), offset);
            BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(9, 2), len);
            return b;
        }
    }
}
