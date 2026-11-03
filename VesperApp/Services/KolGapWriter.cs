using System;
using System.Collections.Generic;
using System.IO;
using VesperApp.Models;

namespace VesperApp.Services
{
    /// <summary>
    /// KOL audio data accounting (asd-interfaces interfaces/kol-recording-format.md, KOL firmware 1.8): the
    /// recorder writes a 512-byte GAP block wherever samples were lost or are unreliable, and a summary
    /// block before every FOOTER. Counts are whole ring nodes of <c>node_samples</c> samples.
    /// Writes the PCM of the segments between HEADER / SYNC / FOOTER blocks to the output and handles the
    /// GAP blocks at the start and the end of a segment:
    /// <list type="bullet">
    /// <item>OVERRUN (1): the exact number of missing samples is inserted as zeros, so the output keeps
    /// the sample timeline;</item>
    /// <item>INVALID (2): of the last <c>span</c> nodes written before the block, the oldest <c>nodes</c>
    /// were overwritten while the firmware wrote them: they are set to zero in the output. <c>span</c> 0
    /// (VesperU5): the last <c>nodes × node_samples</c> samples before the block;</item>
    /// <item>SUMMARY (3): counts for the file, compared with what was found.</item>
    /// </list>
    /// KOL writes its blocks next to a SYNC / FOOTER; VesperU5 also between the two halves of its buffer, where no
    /// SYNC is, so a block is found anywhere in a segment. Every event is listed in <see cref="Notes"/> for the .txt metadata.
    /// </summary>
    public sealed class KolGapWriter
    {
        public const uint PreambleGap = 0xA55A3CC3;
        public const int BlockLength = 512;
        public const int ReasonOverrun = 1, ReasonInvalid = 2, ReasonSummary = 3;

        private readonly byte[] _buf;
        private readonly Stream _out;
        private readonly List<(long Offset, int Length)> _nodes = new();

        public long LostSamples { get; private set; }
        public long InvalidSamples { get; private set; }
        public long LostNodes { get; private set; }
        public long InvalidNodes { get; private set; }
        public List<string> Notes { get; } = new();

        public KolGapWriter(byte[] buffer, Stream output)
        {
            _buf = buffer;
            _out = output;
        }

        /// <summary>A GAP block at <paramref name="pos"/>, or null.</summary>
        public static (int Reason, int Mic, int NodeSamples, int Nodes, int Span, DateTime Time)? TryParse(byte[] d, int pos)
        {
            if (pos < 0 || pos + 32 > d.Length || BitConverter.ToUInt32(d, pos) != PreambleGap) return null;
            return (BitConverter.ToUInt16(d, pos + 16), BitConverter.ToUInt16(d, pos + 18), (int)BitConverter.ToUInt32(d, pos + 20),
                    (int)BitConverter.ToUInt32(d, pos + 24), (int)BitConverter.ToUInt32(d, pos + 28), BinHeader.ExtractDateTime(d, pos + 4));
        }

        /// <summary>A whole GAP block: magic, a valid RTC stamp, a known reason and the 0xFF fill - PCM that happens
        /// to hold the magic is not one.</summary>
        private bool IsGap(int pos)
        {
            if (pos < 0 || pos + BlockLength > _buf.Length || BitConverter.ToUInt32(_buf, pos) != PreambleGap) return false;
            int reason = BitConverter.ToUInt16(_buf, pos + 16);
            if (reason < ReasonOverrun || reason > ReasonSummary || !BinHeader.IsValidStamp(_buf, pos + 4)) return false;
            for (int k = pos + 32; k < pos + BlockLength; k++)
                if (_buf[k] != 0xFF) return false;
            return true;
        }

        /// <summary>Writes the segment [start, start + length) of the file: the PCM is copied, GAP blocks in it
        /// (2-byte aligned, as samples) are handled where they are.</summary>
        public void Write(int start, int length)
        {
            int end = start + length;
            int pcm = start;
            for (int p = start; p + BlockLength <= end; p += 2)
            {
                if (BitConverter.ToUInt32(_buf, p) != PreambleGap || !IsGap(p)) continue;
                Copy(pcm, p);
                Handle(p);
                pcm = p + BlockLength;
                p = pcm - 2;
            }
            Copy(pcm, end);
        }

        private void Copy(int from, int to)
        {
            if (to <= from) return;
            _nodes.Add((_out.Position, to - from));
            _out.Write(_buf, from, to - from);
        }

        private void Handle(int pos)
        {
            var g = TryParse(_buf, pos);
            if (g == null) return;
            var (reason, mic, ns, nodes, span, time) = g.Value;
            long at = _out.Position / 2;
            switch (reason)
            {
                case ReasonOverrun:
                {
                    long samples = (long)nodes * ns;
                    var zeros = new byte[Math.Min(samples * 2, 1 << 20)];
                    for (long left = samples * 2; left > 0; left -= zeros.Length)
                        _out.Write(zeros, 0, (int)Math.Min(left, zeros.Length));
                    LostNodes += nodes;
                    LostSamples += samples;
                    Notes.Add($"Gap:{time:dd/MM/yyyy HH:mm:ss.FFF}:{at}:{samples}:lost ({nodes} nodes, mic {mic})");
                    break;
                }
                case ReasonInvalid when span == 0:
                {
                    long end = _out.Position;
                    long bytes = Math.Min((long)nodes * ns * 2, end);
                    _out.Position = end - bytes;
                    _out.Write(new byte[bytes], 0, (int)bytes);
                    InvalidNodes += nodes;
                    InvalidSamples += bytes / 2;
                    Notes.Add($"Gap:{time:dd/MM/yyyy HH:mm:ss.FFF}:{(end - bytes) / 2}:{bytes / 2}:unreliable, set to zero ({nodes} nodes, mic {mic})");
                    break;
                }
                case ReasonInvalid:
                {
                    int first = Math.Max(0, _nodes.Count - span);
                    long zeroed = 0;
                    long end = _out.Position;
                    for (int k = first; k < Math.Min(_nodes.Count, first + nodes); k++)
                    {
                        _out.Position = _nodes[k].Offset;
                        _out.Write(new byte[_nodes[k].Length], 0, _nodes[k].Length);
                        zeroed += _nodes[k].Length / 2;
                    }
                    _out.Position = end;
                    long from = first < _nodes.Count ? _nodes[first].Offset / 2 : at;
                    InvalidNodes += nodes;
                    InvalidSamples += zeroed;
                    Notes.Add($"Gap:{time:dd/MM/yyyy HH:mm:ss.FFF}:{from}:{zeroed}:unreliable, set to zero ({nodes} nodes, mic {mic})");
                    break;
                }
                case ReasonSummary:
                    Notes.Add($"Summary:{time:dd/MM/yyyy HH:mm:ss.FFF}:lost nodes {nodes}, unreliable nodes {span}" +
                              (nodes == LostNodes && span == InvalidNodes ? "" : $" (found lost {LostNodes}, unreliable {InvalidNodes})"));
                    break;
            }
        }
    }
}
