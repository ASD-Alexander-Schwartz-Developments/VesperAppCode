using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using VesperApp.Models;

namespace VesperApp.Services
{
    /// <summary>
    /// Flashes the Nanotag (Microchip SAM L21) through its Harmony USB-HID bootloader,
    /// implementing the exact AN1388 protocol from the nanotag-bootloader firmware
    /// (bootloader.c): SOH/EOT/DLE framing, CRC-16/CCITT, commands Read-Boot-Info / Erase /
    /// Program (binary Intel-HEX record) / Read-CRC / Jump-to-App, over 64-byte HID reports.
    /// The running device is told to enter the bootloader (VND_CMD_SET_BOOT); it re-enumerates
    /// as 0x04D8/0x003C and we erase + program + jump. Windows-only (uses Win32HidDevice).
    /// </summary>
    public static class NanotagFlasher
    {
        public const int Vid = 0x04D8;
        public const int BootloaderPid = 0x003C;

        private const byte SOH = 0x01, EOT = 0x04, DLE = 0x10;
        private enum Cmd : byte { ReadBootInfo = 1, EraseFlash = 2, ProgramFlash = 3, ReadCrc = 4, JmpToApp = 5 }

        // CRC-16/CCITT nibble table — identical to bootloader_CalculateCrc().
        private static readonly ushort[] CrcTable =
        {
            0x0000, 0x1021, 0x2042, 0x3063, 0x4084, 0x50a5, 0x60c6, 0x70e7,
            0x8108, 0x9129, 0xa14a, 0xb16b, 0xc18c, 0xd1ad, 0xe1ce, 0xf1ef
        };

        public static async Task<bool> FlashAsync(LoggerDevice runtime, string hexPath,
            IProgress<FlashProgress>? progress, CancellationToken ct = default)
        {
            if (!Path.GetExtension(hexPath).Equals(".hex", StringComparison.OrdinalIgnoreCase))
                throw new NotSupportedException("Nanotag firmware must be an Intel HEX (.hex) file.");

            var records = File.ReadAllLines(hexPath)
                .Where(l => l.TrimStart().StartsWith(":"))
                .Select(HexLineToBytes)
                .ToList();
            if (records.Count == 0)
                throw new InvalidDataException("No Intel HEX records found in the firmware file.");

            // 1. Ask the running firmware to jump to the bootloader.
            progress?.Report(new FlashProgress { Percent = 0, Status = "Entering Nanotag bootloader…" });
            await runtime.Bootloader();   // VND_CMD_SET_BOOT (0x0F)

            // 2. Drive the HID flash sequence off the UI thread.
            return await Task.Run(() => FlashCore(records, progress, ct), ct);
        }

        private static bool FlashCore(List<byte[]> records, IProgress<FlashProgress>? progress, CancellationToken ct)
        {
            // Wait for the bootloader HID device to re-enumerate.
            IHidReportTransport? hid = null;
            for (int i = 0; i < 40 && hid == null; i++)
            {
                ct.ThrowIfCancellationRequested();
                hid = HidReportTransport.Open(Vid, BootloaderPid);
                if (hid == null) Thread.Sleep(250);
            }
            if (hid == null)
                throw new InvalidOperationException(
                    $"Nanotag bootloader ({Vid:X4}:{BootloaderPid:X4}) did not appear after the boot command.");

            try
            {
                Transact(hid, new[] { (byte)Cmd.ReadBootInfo }, expectResponse: true);  // sanity / handshake

                progress?.Report(new FlashProgress { Percent = 0, Status = "Erasing…" });
                Transact(hid, new[] { (byte)Cmd.EraseFlash }, expectResponse: true);

                for (int i = 0; i < records.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    byte[] rec = records[i];

                    // Record type is the 4th byte of a hex record. The firmware replies to
                    // data/address records (00/02/04/05) but NOT to End-Of-File (01).
                    byte recType = rec.Length >= 4 ? rec[3] : (byte)0;
                    bool eof = recType == 0x01;

                    byte[] payload = new byte[1 + rec.Length];
                    payload[0] = (byte)Cmd.ProgramFlash;
                    Array.Copy(rec, 0, payload, 1, rec.Length);
                    Transact(hid, payload, expectResponse: !eof);

                    progress?.Report(new FlashProgress { Percent = (i + 1) * 100 / records.Count, Status = "Programming…" });
                }

                progress?.Report(new FlashProgress { Percent = 100, Status = "Starting application…" });
                Transact(hid, new[] { (byte)Cmd.JmpToApp }, expectResponse: false); // device resets into the app
                return true;
            }
            finally { hid.Dispose(); }
        }

        // One AN1388 transaction: frame + send the payload over HID, optionally read the echoed reply.
        private static byte[]? Transact(IHidReportTransport hid, byte[] payload, bool expectResponse)
        {
            byte[] frame = Frame(payload);

            int size = hid.ReportSize;
            for (int off = 0; off < frame.Length; off += size)
            {
                byte[] rpt = new byte[size]; // zeroed; trailing bytes after EOT are ignored by the bootloader
                Array.Copy(frame, off, rpt, 0, Math.Min(size, frame.Length - off));
                hid.WriteReport(rpt);
            }

            if (!expectResponse) { Thread.Sleep(60); return null; }

            byte[] inBuf = new byte[hid.ReportSize];
            if (!hid.ReadReport(inBuf, 5000))
                throw new IOException($"No response to bootloader command 0x{payload[0]:X2}.");

            byte[]? reply = Unframe(inBuf, 0, inBuf.Length);
            if (reply == null || reply.Length < 1 || reply[0] != payload[0])
                throw new IOException($"Bad response to bootloader command 0x{payload[0]:X2}.");
            return reply;
        }

        private static byte[] Frame(byte[] payload)
        {
            ushort crc = Crc16(payload, payload.Length);
            byte[] body = new byte[payload.Length + 2];
            Array.Copy(payload, body, payload.Length);
            body[payload.Length] = (byte)(crc & 0xFF);
            body[payload.Length + 1] = (byte)(crc >> 8);

            var outBytes = new List<byte>(body.Length + 4) { SOH };
            foreach (byte b in body)
            {
                if (b == SOH || b == EOT || b == DLE) outBytes.Add(DLE);
                outBytes.Add(b);
            }
            outBytes.Add(EOT);
            return outBytes.ToArray();
        }

        private static byte[]? Unframe(byte[] buf, int start, int len)
        {
            var data = new List<byte>();
            bool inFrame = false, esc = false;

            for (int i = start; i < start + len && i < buf.Length; i++)
            {
                byte b = buf[i];
                if (!inFrame) { if (b == SOH) inFrame = true; continue; }
                if (esc) { data.Add(b); esc = false; continue; }
                if (b == DLE) { esc = true; continue; }
                if (b == EOT)
                {
                    if (data.Count < 2) return null;
                    int payLen = data.Count - 2;
                    ushort crc = (ushort)(data[payLen] | (data[payLen + 1] << 8));
                    byte[] payload = data.GetRange(0, payLen).ToArray();
                    return Crc16(payload, payLen) == crc ? payload : null;
                }
                data.Add(b);
            }
            return null;
        }

        private static ushort Crc16(byte[] data, int len)
        {
            ushort crc = 0;
            for (int j = 0; j < len; j++)
            {
                int i = (crc >> 12) ^ (data[j] >> 4);
                crc = (ushort)(CrcTable[i & 0x0F] ^ (crc << 4));
                i = (crc >> 12) ^ (data[j] & 0x0F);
                crc = (ushort)(CrcTable[i & 0x0F] ^ (crc << 4));
            }
            return (ushort)(crc & 0xFFFF);
        }

        // ":10010000....FF" → the record's raw bytes (bytecount, addr, type, data, checksum).
        private static byte[] HexLineToBytes(string line)
        {
            line = line.Trim();
            string hex = line.Substring(line.IndexOf(':') + 1);
            int n = hex.Length / 2;
            byte[] bytes = new byte[n];
            for (int i = 0; i < n; i++)
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            return bytes;
        }
    }
}
