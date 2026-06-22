using System;
using System.Buffers.Binary;
using System.Collections.Generic;

namespace ASD.DeviceCore.Ble.ProxTit
{
    /// <summary>One parsed modem chunk reply (proxtit_modem_chunk_t), read from prx_log.</summary>
    public sealed class ModemChunk
    {
        public byte Status { get; init; }
        public byte Op { get; init; }
        public uint Offset { get; init; }
        public ushort Len { get; init; }
        public byte[] Data { get; init; } = Array.Empty<byte>();

        public bool IsOk => Status == ProxTitGatt.StOk;
        public bool IsEof => Status == ProxTitGatt.StEof;

        /// <summary>Parse the 8-byte header + payload returned by a prx_log read.</summary>
        public static ModemChunk Parse(ReadOnlySpan<byte> raw)
        {
            if (raw.Length < ProxTitGatt.ChunkHeaderLen)
                throw new FormatException($"Modem chunk too short: {raw.Length} bytes.");

            byte status = raw[0];
            byte op = raw[1];
            uint offset = BinaryPrimitives.ReadUInt32LittleEndian(raw.Slice(2, 4));
            ushort len = BinaryPrimitives.ReadUInt16LittleEndian(raw.Slice(6, 2));

            int avail = raw.Length - ProxTitGatt.ChunkHeaderLen;
            int n = Math.Min(len, avail);
            var data = raw.Slice(ProxTitGatt.ChunkHeaderLen, n).ToArray();

            return new ModemChunk { Status = status, Op = op, Offset = offset, Len = (ushort)n, Data = data };
        }
    }

    /// <summary>One per-sensor entry in the MODEM_OPEN catalog.</summary>
    public sealed record CatalogStream(byte SensorId, uint FileCount, bool Recording)
    {
        public string Name => ProxTitGatt.StreamName(SensorId);

        /// <summary>Highest index that is safe to download — the live (being-recorded)
        /// file is the last one, so skip it while recording. Iterate
        /// [0 .. SafeMaxIndexExclusive).</summary>
        public uint SafeMaxIndexExclusive => Recording && FileCount > 0 ? FileCount - 1 : FileCount;
    }

    /// <summary>The MODEM_OPEN result: free space + per-sensor file counts.</summary>
    public sealed class ProxTitCatalog
    {
        public uint FreeKiloBytes { get; init; }
        public IReadOnlyList<CatalogStream> Streams { get; init; } = Array.Empty<CatalogStream>();

        /// <summary>Parse the catalog payload (chunk.data of a MODEM_OPEN reply):
        /// u32 free_kb, u8 nStreams, { u8 sensorId, u32 fileCount, u8 flags } x n.</summary>
        public static ProxTitCatalog Parse(ReadOnlySpan<byte> data)
        {
            if (data.Length < 5)
                throw new FormatException($"Catalog too short: {data.Length} bytes.");

            uint freeKb = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(0, 4));
            byte n = data[4];
            var streams = new List<CatalogStream>(n);

            int p = 5;
            for (int i = 0; i < n; i++)
            {
                if (p + 6 > data.Length)
                    throw new FormatException("Catalog truncated mid-stream.");
                byte sensor = data[p];
                uint count = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(p + 1, 4));
                byte flags = data[p + 5];
                streams.Add(new CatalogStream(sensor, count, (flags & ProxTitGatt.CatFlagRecording) != 0));
                p += 6;
            }

            return new ProxTitCatalog { FreeKiloBytes = freeKb, Streams = streams };
        }
    }
}
