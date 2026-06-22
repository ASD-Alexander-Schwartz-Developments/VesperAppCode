using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ASD.DeviceCore.Ble
{
    /// <summary>A 6-byte BLE device address + type.</summary>
    public readonly struct BleAddress : IEquatable<BleAddress>
    {
        public byte[] Bytes { get; }
        public bool IsRandom { get; }

        public BleAddress(byte[] bytes, bool isRandom = false)
        {
            if (bytes is null || bytes.Length != 6)
                throw new ArgumentException("BLE address must be 6 bytes.", nameof(bytes));
            Bytes = bytes;
            IsRandom = isRandom;
        }

        /// <summary>Parse "AA:BB:CC:DD:EE:FF" (most-significant byte first).</summary>
        public static BleAddress Parse(string s, bool isRandom = false)
        {
            string[] parts = s.Split(':', '-');
            if (parts.Length != 6)
                throw new FormatException("Expected 6 colon/dash-separated octets.");
            var b = new byte[6];
            for (int i = 0; i < 6; i++)
                b[i] = Convert.ToByte(parts[i], 16);
            return new BleAddress(b, isRandom);
        }

        public override string ToString() =>
            string.Join(":", Array.ConvertAll(Bytes, x => x.ToString("X2")));

        public bool Equals(BleAddress other)
        {
            if (IsRandom != other.IsRandom) return false;
            for (int i = 0; i < 6; i++)
                if (Bytes[i] != other.Bytes[i]) return false;
            return true;
        }

        public override bool Equals(object? obj) => obj is BleAddress a && Equals(a);
        public override int GetHashCode() => BitConverter.ToInt32(Bytes, 0) ^ (IsRandom ? 1 : 0) ^ Bytes[4] ^ (Bytes[5] << 8);
    }

    /// <summary>A device seen during a scan.</summary>
    public sealed record BleScanResult(BleAddress Address, string? Name, int Rssi);

    /// <summary>
    /// Minimal BLE central (GATT client) abstraction — exactly the operations the
    /// ProxTit modem download client needs, and nothing else. Implemented by:
    /// <list type="bullet">
    /// <item><see cref="BgapiNcpBleCentral"/> — real hardware over a Silicon Labs UART
    /// NCP adapter (the host never does native BLE; same approach as
    /// proxtit-downloader-py).</item>
    /// <item><see cref="SimulatedBleCentral"/> — an in-memory fake tag so the client +
    /// daemon are runnable and testable without hardware.</item>
    /// </list>
    /// Keeping the surface this small is what lets a full BGAPI stack stay optional.
    /// </summary>
    public interface IBleCentral : IAsyncDisposable
    {
        /// <summary>Negotiated ATT MTU on the active connection (default 23 until raised).</summary>
        int Mtu { get; }

        /// <summary>Open the underlying link (NCP serial port / sim). Idempotent.</summary>
        Task OpenAsync(CancellationToken ct = default);

        /// <summary>Scan for advertisers exposing <paramref name="serviceFilter"/>.</summary>
        Task<IReadOnlyList<BleScanResult>> ScanAsync(
            TimeSpan duration, Guid serviceFilter, CancellationToken ct = default);

        /// <summary>Connect + discover GATT for the given peer.</summary>
        Task ConnectAsync(BleAddress address, CancellationToken ct = default);

        /// <summary>Request a larger ATT MTU; returns the value the peer agreed to.</summary>
        Task<int> NegotiateMtuAsync(int desiredMtu, CancellationToken ct = default);

        /// <summary>GATT read of a characteristic value (offset 0).</summary>
        Task<byte[]> ReadAsync(Guid characteristic, CancellationToken ct = default);

        /// <summary>GATT write of a characteristic value.</summary>
        Task WriteAsync(Guid characteristic, byte[] value, bool withResponse = true, CancellationToken ct = default);

        /// <summary>Tear down the active connection (the link stays open).</summary>
        Task DisconnectAsync(CancellationToken ct = default);
    }
}
