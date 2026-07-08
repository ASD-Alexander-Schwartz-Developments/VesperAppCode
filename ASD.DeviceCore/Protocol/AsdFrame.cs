using System;
using System.Collections.Generic;

namespace ASD.DeviceCore.Protocol
{
    /// <summary>One decoded console message.</summary>
    public readonly record struct AsdMessage(byte Id, byte Type, byte[] Payload);

    /// <summary>
    /// The ASD device console framing, byte-identical to the firmware / the legacy
    /// <c>SerialMessage.PROTO_MsgBuild</c>:
    /// <code>[SOH=0x5A][ID][TYPE][LEN][payload x LEN][checksum]</code>
    /// where checksum = 8-bit sum of SOH+ID+TYPE+LEN+payload. Pure, allocation-light,
    /// and shared by the async <see cref="AsdConsoleClient"/> (replacing the legacy
    /// three-thread reader).
    /// </summary>
    public static class AsdFrame
    {
        public const byte Soh = 0x5A;
        public const int MaxPayload = 145; // legacy UART_MSG_LEN(150) - 5 header/footer bytes

        public static byte[] Build(byte type, ReadOnlySpan<byte> payload, byte id = 0)
        {
            if (payload.Length > MaxPayload)
                throw new ArgumentException($"Payload exceeds {MaxPayload} bytes.", nameof(payload));

            int len = payload.Length;
            var msg = new byte[5 + len];
            msg[0] = Soh;
            msg[1] = id;
            msg[2] = type;
            msg[3] = (byte)len;

            byte checksum = (byte)(Soh + id + type + len);
            for (int i = 0; i < len; i++)
            {
                msg[4 + i] = payload[i];
                checksum += payload[i];
            }
            msg[4 + len] = checksum;
            return msg;
        }
    }

    /// <summary>
    /// Incremental, allocation-aware parser for the console framing. Feed it bytes as
    /// they arrive; it yields each complete, checksum-valid <see cref="AsdMessage"/>.
    /// Resilient to noise: a bad checksum or stray byte just resyncs on the next SOH.
    /// </summary>
    public sealed class AsdFrameParser
    {
        private enum S { Soh, Id, Type, Len, Data, Checksum }

        private S _state = S.Soh;
        private byte _id, _type, _len, _checksum, _count;
        private byte[] _data = Array.Empty<byte>();

        /// <summary>Feed received bytes; returns any complete messages decoded.</summary>
        public IEnumerable<AsdMessage> Push(ReadOnlyMemory<byte> bytes)
        {
            var outList = new List<AsdMessage>();
            ReadOnlySpan<byte> span = bytes.Span;
            for (int i = 0; i < span.Length; i++)
                Step(span[i], outList);
            return outList;
        }

        private void Step(byte b, List<AsdMessage> outList)
        {
            switch (_state)
            {
                case S.Soh:
                    if (b == AsdFrame.Soh) _state = S.Id;
                    break;
                case S.Id:
                    _id = b; _state = S.Type;
                    break;
                case S.Type:
                    _type = b; _state = S.Len;
                    break;
                case S.Len:
                    _len = b;
                    _checksum = (byte)(AsdFrame.Soh + _id + _type + _len);
                    _count = 0;
                    _data = _len > 0 ? new byte[_len] : Array.Empty<byte>();
                    _state = _len > 0 ? S.Data : S.Checksum;
                    break;
                case S.Data:
                    _data[_count++] = b;
                    _checksum += b;
                    if (_count >= _len) _state = S.Checksum;
                    break;
                case S.Checksum:
                    if (b == _checksum)
                        outList.Add(new AsdMessage(_id, _type, _data));
                    // valid or not, resync for the next frame
                    _state = S.Soh;
                    break;
            }
        }
    }
}
