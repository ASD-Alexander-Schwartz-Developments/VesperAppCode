using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ASD.DeviceCore.Transport;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ASD.DeviceCore.Ble.Bgapi
{
    /// <summary>
    /// <see cref="IBleCentral"/> over a Silicon Labs BGAPI NCP adapter on a serial port
    /// (the host never does native BLE; same pattern as proxtit-downloader-py). Frames
    /// BGAPI commands, runs a background reader that correlates command responses and
    /// dispatches events, and maps the GATT flow onto the small central surface the
    /// modem client needs.
    ///
    /// <para><b>Status:</b> the framing (<see cref="BgapiFrame"/>) is stable, but the
    /// command class/method ids and payload field layouts (<see cref="BgapiIds"/>)
    /// target the <c>sl_bt_*</c> API and must be validated against the installed
    /// Simplicity SDK on the bench. Until then, use <see cref="SimulatedBleCentral"/>
    /// (the daemon's default).</para>
    /// </summary>
    public sealed class BgapiNcpBleCentral : IBleCentral
    {
        private readonly ISerialLink _link;
        private readonly ILogger _log;
        private readonly SemaphoreSlim _cmdLock = new(1, 1);
        private readonly List<EventWaiter> _waiters = new();
        private readonly object _waitersGate = new();

        private CancellationTokenSource? _readCts;
        private Task? _readTask;
        private TaskCompletionSource<byte[]>? _pendingResponse;
        private (byte cls, byte method) _pendingKey;

        private byte _connection;
        private readonly Dictionary<Guid, ushort> _charHandles = new();

        public int Mtu { get; private set; } = 23;

        public BgapiNcpBleCentral(ISerialLink link, ILogger? log = null)
        {
            _link = link ?? throw new ArgumentNullException(nameof(link));
            _log = log ?? NullLogger.Instance;
        }

        public Task OpenAsync(CancellationToken ct = default)
        {
            _link.Open();
            _readCts = new CancellationTokenSource();
            _readTask = Task.Run(() => ReadLoopAsync(_readCts.Token));
            _log.LogWarning(
                "BgapiNcpBleCentral: BGAPI opcode table is pending validation against the " +
                "installed Simplicity SDK (sl_bt_api.h). Verify on the bench before trusting traffic.");
            return Task.CompletedTask;
        }

        public async Task<IReadOnlyList<BleScanResult>> ScanAsync(
            TimeSpan duration, Guid serviceFilter, CancellationToken ct = default)
        {
            var found = new Dictionary<BleAddress, BleScanResult>();
            byte[] filterBytes = serviceFilter.ToByteArray(); // 16 bytes, BGAPI little-endian order

            void OnReport(byte[] p)
            {
                // scan_report: [event_flags?][address(6)][addr_type][...][data_len][data[]]
                if (p.Length < 7) return;
                var addr = new BleAddress(p.AsSpan(p.Length >= 12 ? 1 : 0, 6).ToArray(), false);
                int rssi = (sbyte)p[^1];
                // Accept any advertiser whose payload carries the target 128-bit service uuid.
                if (ContainsServiceUuid(p, filterBytes))
                    found[addr] = new BleScanResult(addr, null, rssi);
            }

            using var sub = Subscribe(BgapiIds.ClassScanner, BgapiIds.EvtScannerScanReport, OnReport);

            await SendCommandAsync(BgapiIds.ClassScanner, BgapiIds.ScannerStart,
                new byte[] { 1 /*phy*/, 2 /*discover_generic*/ }, ct).ConfigureAwait(false);
            try { await Task.Delay(duration, ct).ConfigureAwait(false); }
            finally
            {
                await SendCommandAsync(BgapiIds.ClassScanner, BgapiIds.ScannerStop,
                    Array.Empty<byte>(), ct).ConfigureAwait(false);
            }
            return found.Values.ToList();
        }

        public async Task ConnectAsync(BleAddress address, CancellationToken ct = default)
        {
            _charHandles.Clear();

            var openPayload = new byte[8];
            address.Bytes.CopyTo(openPayload, 0);
            openPayload[6] = (byte)(address.IsRandom ? 1 : 0);
            openPayload[7] = 1; // initiating phy = 1M

            var opened = WaitEventAsync(BgapiIds.ClassConnection, BgapiIds.EvtConnectionOpened, ct);
            await SendCommandAsync(BgapiIds.ClassConnection, BgapiIds.ConnectionOpen, openPayload, ct)
                .ConfigureAwait(false);
            byte[] ev = await opened.ConfigureAwait(false);
            _connection = ev[^1]; // connection handle (layout pending validation)

            // Discover the PRX service, then its characteristics, capturing handles.
            await DiscoverServiceAndCharsAsync(ProxTit.ProxTitGatt.Service,
                new[] { ProxTit.ProxTitGatt.DataCp, ProxTit.ProxTitGatt.Log }, ct).ConfigureAwait(false);
        }

        public async Task<int> NegotiateMtuAsync(int desiredMtu, CancellationToken ct = default)
        {
            await SendCommandAsync(BgapiIds.ClassConnection, BgapiIds.ConnectionSetMaxMtu,
                new[] { (byte)(desiredMtu & 0xFF), (byte)(desiredMtu >> 8) }, ct).ConfigureAwait(false);
            // The peer's agreed mtu arrives via a connection-parameters event.
            try
            {
                byte[] ev = await WaitEventAsync(
                    BgapiIds.ClassConnection, BgapiIds.EvtConnectionParameters, ct, TimeSpan.FromSeconds(2))
                    .ConfigureAwait(false);
                if (ev.Length >= 3)
                    Mtu = BinaryPrimitives.ReadUInt16LittleEndian(ev.AsSpan(1, 2));
            }
            catch (TimeoutException) { Mtu = desiredMtu; }
            return Mtu;
        }

        public async Task<byte[]> ReadAsync(Guid characteristic, CancellationToken ct = default)
        {
            ushort handle = HandleFor(characteristic);
            var valuePayload = new byte[3];
            valuePayload[0] = _connection;
            BinaryPrimitives.WriteUInt16LittleEndian(valuePayload.AsSpan(1, 2), handle);

            var valueEvt = WaitEventAsync(BgapiIds.ClassGatt, BgapiIds.EvtGattCharacteristicValue, ct);
            await SendCommandAsync(BgapiIds.ClassGatt, BgapiIds.GattReadCharacteristicValue, valuePayload, ct)
                .ConfigureAwait(false);
            byte[] ev = await valueEvt.ConfigureAwait(false);
            await WaitEventAsync(BgapiIds.ClassGatt, BgapiIds.EvtGattProcedureCompleted, ct).ConfigureAwait(false);
            return ExtractCharacteristicValue(ev);
        }

        public async Task WriteAsync(Guid characteristic, byte[] value, bool withResponse = true, CancellationToken ct = default)
        {
            ushort handle = HandleFor(characteristic);
            var payload = new byte[4 + value.Length];
            payload[0] = _connection;
            BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(1, 2), handle);
            payload[3] = (byte)value.Length;
            value.CopyTo(payload, 4);

            var done = WaitEventAsync(BgapiIds.ClassGatt, BgapiIds.EvtGattProcedureCompleted, ct);
            await SendCommandAsync(BgapiIds.ClassGatt, BgapiIds.GattWriteCharacteristicValue, payload, ct)
                .ConfigureAwait(false);
            await done.ConfigureAwait(false);
        }

        public async Task DisconnectAsync(CancellationToken ct = default)
        {
            try
            {
                await SendCommandAsync(BgapiIds.ClassConnection, BgapiIds.ConnectionClose,
                    new[] { _connection }, ct).ConfigureAwait(false);
            }
            catch { /* best effort */ }
        }

        public async ValueTask DisposeAsync()
        {
            _readCts?.Cancel();
            if (_readTask != null) { try { await _readTask.ConfigureAwait(false); } catch { } }
            await _link.DisposeAsync().ConfigureAwait(false);
            _cmdLock.Dispose();
        }

        // ---- GATT discovery ----

        private async Task DiscoverServiceAndCharsAsync(Guid service, Guid[] chars, CancellationToken ct)
        {
            uint serviceHandle = 0;
            void OnService(byte[] p)
            {
                if (p.Length >= 5) serviceHandle = BinaryPrimitives.ReadUInt32LittleEndian(p.AsSpan(1, 4));
            }
            using (Subscribe(BgapiIds.ClassGatt, BgapiIds.EvtGattService, OnService))
            {
                byte[] uuid = service.ToByteArray();
                var payload = new byte[2 + uuid.Length];
                payload[0] = _connection;
                payload[1] = (byte)uuid.Length;
                uuid.CopyTo(payload, 2);
                await SendCommandAsync(BgapiIds.ClassGatt, BgapiIds.GattDiscoverPrimaryByUuid, payload, ct).ConfigureAwait(false);
                await WaitEventAsync(BgapiIds.ClassGatt, BgapiIds.EvtGattProcedureCompleted, ct).ConfigureAwait(false);
            }

            foreach (Guid c in chars)
            {
                ushort handle = 0;
                void OnChar(byte[] p)
                {
                    if (p.Length >= 3) handle = BinaryPrimitives.ReadUInt16LittleEndian(p.AsSpan(1, 2));
                }
                using (Subscribe(BgapiIds.ClassGatt, BgapiIds.EvtGattCharacteristic, OnChar))
                {
                    byte[] uuid = c.ToByteArray();
                    var payload = new byte[6 + uuid.Length];
                    payload[0] = _connection;
                    BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(1, 4), serviceHandle);
                    payload[5] = (byte)uuid.Length;
                    uuid.CopyTo(payload, 6);
                    await SendCommandAsync(BgapiIds.ClassGatt, BgapiIds.GattDiscoverCharacteristicsByUuid, payload, ct).ConfigureAwait(false);
                    await WaitEventAsync(BgapiIds.ClassGatt, BgapiIds.EvtGattProcedureCompleted, ct).ConfigureAwait(false);
                }
                _charHandles[c] = handle;
            }
        }

        private ushort HandleFor(Guid c)
            => _charHandles.TryGetValue(c, out var h) && h != 0
               ? h
               : throw new InvalidOperationException($"Characteristic {c} not discovered. Connect first.");

        private static byte[] ExtractCharacteristicValue(byte[] ev)
        {
            // gatt_characteristic_value: connection(1) char(2) att_opcode(1) offset(2) value_len(1) value[]
            if (ev.Length < 7) return Array.Empty<byte>();
            int len = ev[6];
            int avail = Math.Min(len, ev.Length - 7);
            return ev.AsSpan(7, Math.Max(0, avail)).ToArray();
        }

        private static bool ContainsServiceUuid(byte[] report, byte[] uuid16)
        {
            // Brute-force scan the advertising payload for the 16-byte service uuid.
            if (report.Length < uuid16.Length) return false;
            for (int i = 0; i + uuid16.Length <= report.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < uuid16.Length; j++)
                    if (report[i + j] != uuid16[j]) { match = false; break; }
                if (match) return true;
            }
            return false;
        }

        // ---- command / event plumbing ----

        private async Task<byte[]> SendCommandAsync(byte cls, byte method, byte[] payload, CancellationToken ct)
        {
            await _cmdLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var tcs = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
                _pendingResponse = tcs;
                _pendingKey = (cls, method);
                byte[] frame = BgapiFrame.EncodeCommand(cls, method, payload);
                await _link.WriteAsync(frame, ct).ConfigureAwait(false);
                using (ct.Register(() => tcs.TrySetCanceled(ct)))
                    return await tcs.Task.ConfigureAwait(false);
            }
            finally
            {
                _pendingResponse = null;
                _cmdLock.Release();
            }
        }

        private async Task ReadLoopAsync(CancellationToken ct)
        {
            var header = new byte[BgapiFrame.HeaderLen];
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    await ReadExactAsync(header, BgapiFrame.HeaderLen, ct).ConfigureAwait(false);
                    BgapiHeader h = BgapiFrame.ParseHeader(header);
                    var payload = new byte[h.PayloadLength];
                    await ReadExactAsync(payload, h.PayloadLength, ct).ConfigureAwait(false);

                    if (h.IsEvent)
                        DispatchEvent(h, payload);
                    else if (_pendingResponse != null && _pendingKey == (h.ClassId, h.MethodId))
                        _pendingResponse.TrySetResult(payload);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { _log.LogError(ex, "BGAPI read loop terminated."); }
        }

        private async Task ReadExactAsync(byte[] buffer, int count, CancellationToken ct)
        {
            int got = 0;
            while (got < count)
            {
                int n = await _link.ReadAsync(buffer.AsMemory(got, count - got), ct).ConfigureAwait(false);
                if (n <= 0) throw new EndOfStreamException("BGAPI link closed.");
                got += n;
            }
        }

        private void DispatchEvent(BgapiHeader h, byte[] payload)
        {
            EventWaiter[] snapshot;
            lock (_waitersGate) snapshot = _waiters.ToArray();
            foreach (EventWaiter w in snapshot)
                if (w.ClassId == h.ClassId && w.MethodId == h.MethodId)
                    w.Deliver(payload);
        }

        private Task<byte[]> WaitEventAsync(byte cls, byte method, CancellationToken ct, TimeSpan? timeout = null)
        {
            var waiter = new EventWaiter(cls, method, oneShot: true);
            lock (_waitersGate) _waiters.Add(waiter);
            return waiter.WaitAsync(this, ct, timeout);
        }

        private IDisposable Subscribe(byte cls, byte method, Action<byte[]> handler)
        {
            var waiter = new EventWaiter(cls, method, oneShot: false) { Handler = handler };
            lock (_waitersGate) _waiters.Add(waiter);
            return new Unsubscriber(this, waiter);
        }

        private void Remove(EventWaiter w)
        {
            lock (_waitersGate) _waiters.Remove(w);
        }

        private sealed class EventWaiter
        {
            public byte ClassId { get; }
            public byte MethodId { get; }
            public bool OneShot { get; }
            public Action<byte[]>? Handler { get; set; }
            private readonly TaskCompletionSource<byte[]> _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public EventWaiter(byte cls, byte method, bool oneShot)
            {
                ClassId = cls; MethodId = method; OneShot = oneShot;
            }

            public void Deliver(byte[] payload)
            {
                Handler?.Invoke(payload);
                if (OneShot) _tcs.TrySetResult(payload);
            }

            public async Task<byte[]> WaitAsync(BgapiNcpBleCentral owner, CancellationToken ct, TimeSpan? timeout)
            {
                try
                {
                    using var reg = ct.Register(() => _tcs.TrySetCanceled(ct));
                    if (timeout is { } t)
                    {
                        Task done = await Task.WhenAny(_tcs.Task, Task.Delay(t, ct)).ConfigureAwait(false);
                        if (done != _tcs.Task) throw new TimeoutException();
                    }
                    return await _tcs.Task.ConfigureAwait(false);
                }
                finally { owner.Remove(this); }
            }
        }

        private sealed class Unsubscriber : IDisposable
        {
            private readonly BgapiNcpBleCentral _owner;
            private readonly EventWaiter _waiter;
            public Unsubscriber(BgapiNcpBleCentral owner, EventWaiter waiter) { _owner = owner; _waiter = waiter; }
            public void Dispose() => _owner.Remove(_waiter);
        }
    }
}
