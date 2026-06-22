using System;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace ASD.DeviceCore.Ble.ProxTit
{
    /// <summary>
    /// Builds the MODEM_OPEN authentication blob the base station sends to prove it
    /// holds the deployer key. Mirrors the verifier in VT04 <c>proxtit.c</c>
    /// (<c>prox_auth_open</c>): the VT04 recomputes <c>HMAC-SHA256(key, "OPEN" ||
    /// LE32(nonce))[:16]</c> and requires the nonce to be strictly greater than the
    /// last one it honoured (monotonic-counter replay protection). The key lives in
    /// the deployer's config.json on both sides; it never touches the ProxTit tag.
    /// </summary>
    public static class ModemAuth
    {
        private static readonly byte[] OpenContext = Encoding.ASCII.GetBytes("OPEN");

        /// <summary>Parse a 32-hex-char deployer key into 16 bytes, or null if absent/invalid
        /// (matching VT04 <c>prox_load_key</c>: no/short/non-hex key ⇒ modem is open).</summary>
        public static byte[]? ParseKey(string? hex)
        {
            if (string.IsNullOrWhiteSpace(hex) || hex.Length < 32)
                return null;
            var key = new byte[16];
            for (int i = 0; i < 16; i++)
            {
                int hi = HexVal(hex[2 * i]);
                int lo = HexVal(hex[2 * i + 1]);
                if (hi < 0 || lo < 0)
                    return null;
                key[i] = (byte)((hi << 4) | lo);
            }
            return key;
        }

        /// <summary>
        /// Build the 20-byte blob { u32 nonce (LE); u8 mac[16] } for a MODEM_OPEN.
        /// The caller supplies a strictly-increasing <paramref name="nonce"/> and must
        /// persist it across sessions (see the daemon's nonce store).
        /// </summary>
        public static byte[] BuildOpenBlob(byte[] key, uint nonce)
        {
            if (key is null || key.Length != 16)
                throw new ArgumentException("Deployer key must be 16 bytes.", nameof(key));

            var blob = new byte[ProxTitGatt.AuthBlobLen];
            BinaryPrimitives.WriteUInt32LittleEndian(blob.AsSpan(0, 4), nonce);

            // message = "OPEN" || LE32(nonce)
            Span<byte> msg = stackalloc byte[8];
            OpenContext.CopyTo(msg);
            BinaryPrimitives.WriteUInt32LittleEndian(msg.Slice(4, 4), nonce);

            Span<byte> mac = stackalloc byte[32];
            using var hmac = new HMACSHA256(key);
            hmac.TryComputeHash(msg, mac, out _);

            mac.Slice(0, 16).CopyTo(blob.AsSpan(4, 16));
            return blob;
        }

        private static int HexVal(char c) => c switch
        {
            >= '0' and <= '9' => c - '0',
            >= 'a' and <= 'f' => c - 'a' + 10,
            >= 'A' and <= 'F' => c - 'A' + 10,
            _ => -1,
        };
    }
}
