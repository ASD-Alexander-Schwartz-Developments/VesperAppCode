using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ASD.DeviceCore.Ble.ProxTit;

namespace ASD.DeviceCore.Ble
{
    /// <summary>
    /// An in-memory fake ProxTit tag + VT04 backing store. Lets the modem client and
    /// the base-station daemon run end-to-end without hardware, and doubles as the
    /// reference for the firmware contract: it verifies the MODEM_OPEN HMAC + monotonic
    /// nonce exactly like VT04 <c>prox_auth_open</c>, gates non-OPEN ops behind
    /// authentication, refuses to serve/delete the live (being-recorded) file, and
    /// serves chunks bounded by the negotiated MTU.
    /// </summary>
    public sealed class SimulatedBleCentral : IBleCentral
    {
        private readonly byte[]? _key;                 // null ⇒ deployment configured no key (open)
        private uint _lastNonce;                       // monotonic replay guard
        private bool _authed;
        private byte[] _pending = Array.Empty<byte>(); // last op's chunk (header+data), served by next Log read

        // Backing store: (sensor, index) -> bytes. Per-sensor "recording" marks the live file.
        private readonly Dictionary<(byte sensor, uint index), byte[]> _files = new();
        private readonly Dictionary<byte, bool> _recording = new();
        private byte[] _config = Encoding.UTF8.GetBytes("{\"demo\":true}");
        private uint _freeKb = 512u * 1024u;           // 512 MiB free

        public int Mtu { get; private set; } = 23;
        public BleAddress Address { get; }

        /// <param name="deployerKeyHex">32-hex deployer key, or null for an open (keyless) tag.</param>
        public SimulatedBleCentral(string? deployerKeyHex = null, string address = "C0:FF:EE:00:00:01")
        {
            _key = ModemAuth.ParseKey(deployerKeyHex);
            Address = BleAddress.Parse(address);
            SeedDemoData();
        }

        /// <summary>Add a file the fake tag will serve. Set <paramref name="recording"/> on the
        /// highest index of a sensor to mark it live (it will be refused with BUSY).</summary>
        public void AddFile(byte sensor, uint index, byte[] data, bool recording = false)
        {
            _files[(sensor, index)] = data;
            if (recording) _recording[sensor] = true;
        }

        public void SetConfig(byte[] json) => _config = json;

        private void SeedDemoData()
        {
            // Two GPS files and one IMU file; GPS index 1 is "live" (being recorded).
            AddFile(0, 0, RandomBytes(4096));
            AddFile(0, 1, RandomBytes(1024), recording: true);
            AddFile(4, 0, RandomBytes(8192));
        }

        private static byte[] RandomBytes(int n)
        {
            var b = new byte[n];
            RandomNumberGenerator.Fill(b);
            return b;
        }

        public Task OpenAsync(CancellationToken ct = default) => Task.CompletedTask;

        public Task<IReadOnlyList<BleScanResult>> ScanAsync(
            TimeSpan duration, Guid serviceFilter, CancellationToken ct = default)
        {
            IReadOnlyList<BleScanResult> r = new[] { new BleScanResult(Address, "ProxTit-SIM", -52) };
            return Task.FromResult(r);
        }

        public Task ConnectAsync(BleAddress address, CancellationToken ct = default)
        {
            _authed = false;
            return Task.CompletedTask;
        }

        public Task<int> NegotiateMtuAsync(int desiredMtu, CancellationToken ct = default)
        {
            Mtu = Math.Clamp(desiredMtu, 23, 247);
            return Task.FromResult(Mtu);
        }

        public Task DisconnectAsync(CancellationToken ct = default)
        {
            _authed = false;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        /// <summary>Control-point write: decode the command and stage the result chunk.</summary>
        public Task WriteAsync(Guid characteristic, byte[] value, bool withResponse = true, CancellationToken ct = default)
        {
            if (characteristic != ProxTitGatt.DataCp || value.Length < 1)
                return Task.CompletedTask;

            byte cmd = value[0];
            ReadOnlySpan<byte> p = value.AsSpan(1);

            _pending = cmd switch
            {
                ProxTitGatt.CmdModemOpen => HandleOpen(p),
                ProxTitGatt.CmdModemFileInfo => RequireAuth(ProxTitGatt.OpFileInfo) ?? HandleFileInfo(p),
                ProxTitGatt.CmdModemGetChunk => RequireAuth(ProxTitGatt.OpChunk) ?? HandleChunk(p),
                ProxTitGatt.CmdModemDelete => RequireAuth(ProxTitGatt.OpDelete) ?? HandleDelete(p),
                _ => MakeChunk(ProxTitGatt.StNoFile, 0, 0, ReadOnlySpan<byte>.Empty),
            };
            return Task.CompletedTask;
        }

        /// <summary>prx_log read: hand back the staged chunk, MTU-bounded.</summary>
        public Task<byte[]> ReadAsync(Guid characteristic, CancellationToken ct = default)
        {
            if (characteristic != ProxTitGatt.Log)
                return Task.FromResult(Array.Empty<byte>());
            int cap = Math.Max(ProxTitGatt.ChunkHeaderLen, Mtu - 1);
            byte[] outp = _pending.Length <= cap ? _pending : _pending.AsSpan(0, cap).ToArray();
            return Task.FromResult(outp);
        }

        // ---- op handlers (mirror VT04 proxtit.c) ----

        private byte[]? RequireAuth(byte op)
            => _authed ? null : MakeChunk(ProxTitGatt.StDenied, op, 0, ReadOnlySpan<byte>.Empty);

        private byte[] HandleOpen(ReadOnlySpan<byte> blob)
        {
            if (_key == null)
            {
                _authed = true; // keyless deployment ⇒ open
                return BuildCatalog();
            }
            if (blob.Length < ProxTitGatt.AuthBlobLen)
                return MakeChunk(ProxTitGatt.StDenied, ProxTitGatt.OpCatalog, 0, ReadOnlySpan<byte>.Empty);

            uint nonce = BinaryPrimitives.ReadUInt32LittleEndian(blob.Slice(0, 4));
            if (nonce <= _lastNonce)
                return MakeChunk(ProxTitGatt.StDenied, ProxTitGatt.OpCatalog, 0, ReadOnlySpan<byte>.Empty);

            byte[] expect = ModemAuth.BuildOpenBlob(_key, nonce); // recompute (nonce + mac)
            if (!blob.Slice(4, 16).SequenceEqual(expect.AsSpan(4, 16)))
                return MakeChunk(ProxTitGatt.StDenied, ProxTitGatt.OpCatalog, 0, ReadOnlySpan<byte>.Empty);

            _lastNonce = nonce;
            _authed = true;
            return BuildCatalog();
        }

        private byte[] BuildCatalog()
        {
            // u32 free_kb, u8 nStreams, { u8 sensor, u32 count, u8 flags } x n
            var sensors = _files.Keys.Select(k => k.sensor).Distinct().OrderBy(x => x).ToList();
            var data = new List<byte>();
            data.AddRange(Le32(_freeKb));
            data.Add((byte)sensors.Count);
            foreach (byte s in sensors)
            {
                uint count = (uint)_files.Keys.Count(k => k.sensor == s);
                data.Add(s);
                data.AddRange(Le32(count));
                data.Add(_recording.GetValueOrDefault(s) ? ProxTitGatt.CatFlagRecording : (byte)0);
            }
            return MakeChunk(ProxTitGatt.StOk, ProxTitGatt.OpCatalog, 0, data.ToArray());
        }

        private byte[] HandleFileInfo(ReadOnlySpan<byte> p)
        {
            (byte sensor, uint index) = ReadSensorIndex(p);
            byte st = CheckIndex(sensor, index, forSize: true);
            if (st != ProxTitGatt.StOk)
                return MakeChunk(st, ProxTitGatt.OpFileInfo, 0, ReadOnlySpan<byte>.Empty);
            uint size = (uint)GetBytes(sensor, index)!.Length;
            return MakeChunk(ProxTitGatt.StOk, ProxTitGatt.OpFileInfo, 0, Le32(size));
        }

        private byte[] HandleChunk(ReadOnlySpan<byte> p)
        {
            byte sensor = p[0];
            uint index = BinaryPrimitives.ReadUInt32LittleEndian(p.Slice(1, 4));
            uint offset = BinaryPrimitives.ReadUInt32LittleEndian(p.Slice(5, 4));
            ushort len = BinaryPrimitives.ReadUInt16LittleEndian(p.Slice(9, 2));

            byte st = CheckIndex(sensor, index, forSize: false);
            if (st != ProxTitGatt.StOk)
                return MakeChunk(st, ProxTitGatt.OpChunk, offset, ReadOnlySpan<byte>.Empty);

            byte[] file = GetBytes(sensor, index)!;
            if (offset >= file.Length)
                return MakeChunk(ProxTitGatt.StEof, ProxTitGatt.OpChunk, offset, ReadOnlySpan<byte>.Empty);

            int want = Math.Min((int)len, ProxTitGatt.ModemChunkMax);
            int avail = (int)Math.Min(want, file.Length - offset);
            byte status = (offset + (uint)avail >= file.Length) ? ProxTitGatt.StEof : ProxTitGatt.StOk;
            return MakeChunk(status, ProxTitGatt.OpChunk, offset, file.AsSpan((int)offset, avail));
        }

        private byte[] HandleDelete(ReadOnlySpan<byte> p)
        {
            (byte sensor, uint index) = ReadSensorIndex(p);
            if (sensor == ProxTitGatt.StreamConfig)
                return MakeChunk(ProxTitGatt.StDenied, ProxTitGatt.OpDelete, 0, ReadOnlySpan<byte>.Empty);
            byte st = CheckIndex(sensor, index, forSize: false);
            if (st != ProxTitGatt.StOk)
                return MakeChunk(st, ProxTitGatt.OpDelete, 0, ReadOnlySpan<byte>.Empty);
            _files.Remove((sensor, index));
            return MakeChunk(ProxTitGatt.StOk, ProxTitGatt.OpDelete, 0, ReadOnlySpan<byte>.Empty);
        }

        // Live (highest-index while recording) file is refused for chunk/delete (BUSY),
        // but its size is still reported (forSize=true), mirroring prox_check_index.
        private byte CheckIndex(byte sensor, uint index, bool forSize)
        {
            if (sensor == ProxTitGatt.StreamConfig)
                return _config.Length > 0 ? ProxTitGatt.StOk : ProxTitGatt.StNoFile;
            if (!_files.ContainsKey((sensor, index)))
                return ProxTitGatt.StNoFile;
            if (_recording.GetValueOrDefault(sensor))
            {
                uint live = _files.Keys.Where(k => k.sensor == sensor).Max(k => k.index);
                if (index == live)
                    return forSize ? ProxTitGatt.StOk : ProxTitGatt.StBusy;
            }
            return ProxTitGatt.StOk;
        }

        private byte[]? GetBytes(byte sensor, uint index)
            => sensor == ProxTitGatt.StreamConfig ? _config
               : _files.TryGetValue((sensor, index), out var b) ? b : null;

        private static (byte, uint) ReadSensorIndex(ReadOnlySpan<byte> p)
            => (p[0], BinaryPrimitives.ReadUInt32LittleEndian(p.Slice(1, 4)));

        private static byte[] MakeChunk(byte status, byte op, uint offset, ReadOnlySpan<byte> data)
        {
            var b = new byte[ProxTitGatt.ChunkHeaderLen + data.Length];
            b[0] = status;
            b[1] = op;
            BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(2, 4), offset);
            BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(6, 2), (ushort)data.Length);
            data.CopyTo(b.AsSpan(ProxTitGatt.ChunkHeaderLen));
            return b;
        }

        private static byte[] Le32(uint v)
        {
            var b = new byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(b, v);
            return b;
        }
    }
}
