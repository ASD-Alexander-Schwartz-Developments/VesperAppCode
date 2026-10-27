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
    /// were overwritten while the firmware wrote them: they are set to zero in the output;</item>
    /// <item>SUMMARY (3): counts for the file, compared with what was found.</item>
    /// </list>
    /// Every event is listed in <see cref="Notes"/> for the .txt metadata.
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

        private bool IsGap(int pos) => pos >= 0 && pos + BlockLength <= _buf.Length && BitConverter.ToUInt32(_buf, pos) == PreambleGap;

        /// <summary>Writes the segment [start, start + length) of the file: GAP blocks at its start and end
        /// are handled, the PCM in between is copied.</summary>
        public void Write(int start, int length)
        {
            int end = start + length;
            while (end - start >= BlockLength && IsGap(start))
            {
                Handle(start);
                start += BlockLength;
            }
            var trailing = new List<int>();
            int tail = end;
            while (tail - start >= BlockLength && IsGap(tail - BlockLength))
            {
                tail -= BlockLength;
                trailing.Insert(0, tail);
            }
            if (tail > start)
            {
                _nodes.Add((_out.Position, tail - start));
                _out.Write(_buf, start, tail - start);
            }
            foreach (int t in trailing) Handle(t);
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
