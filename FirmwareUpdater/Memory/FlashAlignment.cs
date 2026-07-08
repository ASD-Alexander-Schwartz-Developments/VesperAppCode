using System;
using System.Collections.Generic;
using System.Linq;

namespace FirmwareUpdater.Memory
{
    /// <summary>
    /// Prepares a parsed firmware image for flash controllers with write-alignment
    /// requirements. Linker-produced images (Intel HEX) often contain small gaps and
    /// segments starting at odd addresses; flash bootloaders program in fixed-size
    /// words and may silently ignore unaligned writes (observed on the STM32U585 ROM
    /// bootloader, which programs 16-byte quad-words: a segment at 0x08000238 left
    /// its whole region erased while reporting success).
    /// </summary>
    public static class FlashAlignment
    {
        /// <summary>
        /// Merge segments separated by at most <paramref name="maxGap"/> bytes (filled
        /// with <paramref name="filler"/>, the erased-flash state) and pad every run's
        /// start and end to <paramref name="alignment"/> bytes, so each write lands on
        /// an aligned address.
        /// </summary>
        public static RawMemory Normalize(RawMemory memory, uint alignment = 16, uint maxGap = 4096, byte filler = 0xFF)
        {
            if (memory.Segments.Count == 0) return memory;

            var result = new RawMemory();
            List<Segment> ordered = memory.Segments.OrderBy(s => s.StartAddress).ToList();

            int i = 0;
            while (i < ordered.Count)
            {
                ulong runStart = ordered[i].StartAddress & ~(ulong)(alignment - 1);
                ulong runEnd = ordered[i].StartAddress + (ulong)ordered[i].Length;

                int j = i + 1;
                while (j < ordered.Count && ordered[j].StartAddress <= runEnd + maxGap)
                {
                    runEnd = Math.Max(runEnd, ordered[j].StartAddress + (ulong)ordered[j].Length);
                    j++;
                }
                runEnd = (runEnd + alignment - 1) & ~(ulong)(alignment - 1);

                var image = new byte[runEnd - runStart];
                Array.Fill(image, filler);
                for (int k = i; k < j; k++)
                    Array.Copy(ordered[k].Data, 0, image, (long)(ordered[k].StartAddress - runStart), ordered[k].Length);

                result.TryAddSegment(new Segment(runStart, image));
                i = j;
            }

            return result;
        }
    }
}
