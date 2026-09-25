using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VesperApp.Models;

namespace VesperApp.Services
{
    /// <summary>
    /// Flashes the Nanotag (Microchip SAM L21) through its Harmony USB-HID bootloader,
    /// implementing the exact AN1388 protocol from the nanotag-bootloader firmware
    /// (bootloader.c / bootloader_nvm_interface.c): SOH/EOT/DLE framing, CRC-16/CCITT, commands
    /// Read-Boot-Info / Erase / Program (binary Intel-HEX record) / Read-CRC / Jump-to-App, over
    /// 64-byte HID reports. The running device is told to enter the bootloader (VND_CMD_SET_BOOT);
    /// it re-enumerates as 0x04D8/0x003C and we erase + program + verify + jump.
    ///
    /// Protocol facts that matter (verified against the firmware source):
    ///  - ERASE blocks in the bootloader while it row-erases the whole application region
    ///    (0x8000..0x40000, 896 rows, each waited for) and only then replies: several seconds.
    ///  - EVERY accepted record type (00/01/02/04/05) is answered, INCLUDING End-Of-File. Replies must
    ///    always be read: on Linux/macOS the libusb transport only receives what we ask for, and an
    ///    unread reply leaves the bootloader waiting for its transmit to complete forever.
    ///  - JUMP is answered too; the device resets right after that reply is sent.
    ///  - A record that the bootloader does not accept is never answered (its state machine stalls),
    ///    so a missing reply is fatal, not something to retry.
    /// </summary>
    public static class NanotagFlasher
    {
        public const int Vid = 0x04D8;
        public const int BootloaderPid = 0x003C;

        /// <summary>Application region programmed by the bootloader (APP_START_ADDRESS .. FLASH_END_ADDRESS).</summary>
        public const uint AppStart = 0x8000;
        public const uint FlashEnd = 0x40000;

        /// <summary>Transport factory (vid, pid): replaceable by a simulated bootloader in tests.</summary>
        internal static Func<int, int, IHidReportTransport?> OpenTransport = HidReportTransport.Open;

        private const byte SOH = 0x01, EOT = 0x04, DLE = 0x10;
        private enum Cmd : byte { ReadBootInfo = 1, EraseFlash = 2, ProgramFlash = 3, ReadCrc = 4, JmpToApp = 5 }

        // Largest data record we send: keeps every framed command (worst case fully escaped) inside
        // one 64-byte report, so a frame never straddles a report boundary.
        private const int MaxDataBytesPerRecord = 16;

        private const int ReadBootInfoTimeoutMs = 3000;
        private const int EraseTimeoutMs        = 60000;  // 896 blocking row erases, about 5-6 s
        private const int ProgramTimeoutMs      = 5000;
        private const int ReadCrcTimeoutMs      = 20000;  // CRC over up to 224 KB on a slow bootloader clock
        private const int JumpTimeoutMs         = 2000;

        // CRC-16/CCITT nibble table, identical to bootloader_CalculateCrc().
        private static readonly ushort[] CrcTable =
        {
            0x0000, 0x1021, 0x2042, 0x3063, 0x4084, 0x50a5, 0x60c6, 0x70e7,
            0x8108, 0x9129, 0xa14a, 0xb16b, 0xc18c, 0xd1ad, 0xe1ce, 0xf1ef
        };

        /// <summary>True when a Nanotag that is already sitting in its bootloader (e.g. after an
        /// interrupted update) is attached. Such a tag is invisible to the normal device list.</summary>
        public static bool IsBootloaderPresent()
        {
            try
            {
                using var hid = OpenTransport(Vid, BootloaderPid);
                return hid != null;
            }
            catch { return false; }
        }

        /// <summary>
        /// Flash <paramref name="hexPath"/>. With <paramref name="runtime"/> set, the running firmware is
        /// first commanded into the bootloader; with <c>null</c> the bootloader must already be attached
        /// (recovery of an interrupted update).
        /// </summary>
        public static async Task<bool> FlashAsync(LoggerDevice? runtime, string hexPath,
            IProgress<FlashProgress>? progress, CancellationToken ct = default)
        {
            if (!Path.GetExtension(hexPath).Equals(".hex", StringComparison.OrdinalIgnoreCase))
                throw new NotSupportedException("Nanotag firmware must be an Intel HEX (.hex) file.");

            HexImage image = HexImage.Load(hexPath);
            if (image.Records.Count == 0 || image.Bytes.Length == 0)
                throw new InvalidDataException("No Intel HEX data records found in the firmware file.");
            if (image.FirstAddress < AppStart || image.LastAddressExclusive > FlashEnd)
                throw new InvalidDataException(
                    $"Firmware image spans 0x{image.FirstAddress:X}..0x{image.LastAddressExclusive - 1:X}, outside the " +
                    $"Nanotag application region 0x{AppStart:X}..0x{FlashEnd - 1:X}.");

            if (runtime != null)
            {
                // 1. Ask the running firmware to jump to the bootloader.
                progress?.Report(new FlashProgress { Percent = 0, Status = "Entering Nanotag bootloader…" });
                await runtime.Bootloader();   // VND_CMD_SET_BOOT (0x0F)
            }

            // 2. Drive the HID flash sequence off the UI thread.
            return await Task.Run(() => FlashCore(image, progress, ct), ct);
        }

        private static bool FlashCore(HexImage image, IProgress<FlashProgress>? progress, CancellationToken ct)
        {
            // Wait for the bootloader HID device to (re-)enumerate.
            IHidReportTransport? hid = null;
            for (int i = 0; i < 40 && hid == null; i++)
            {
                ct.ThrowIfCancellationRequested();
                hid = OpenTransport(Vid, BootloaderPid);
                if (hid == null) Thread.Sleep(250);
            }
            if (hid == null)
                throw new InvalidOperationException(
                    $"Nanotag bootloader ({Vid:X4}:{BootloaderPid:X4}) did not appear after the boot command.");

            try
            {
                Drain(hid);

                byte[] info = Transact(hid, new[] { (byte)Cmd.ReadBootInfo }, ReadBootInfoTimeoutMs, ct);
                string blVer = info.Length >= 3 ? $" (bootloader {info[1]}.{info[2]})" : string.Empty;

                progress?.Report(new FlashProgress { Percent = 0, Status = $"Erasing application flash{blVer}, this takes several seconds…" });
                Transact(hid, new[] { (byte)Cmd.EraseFlash }, EraseTimeoutMs, ct);

                var records = image.Records;
                for (int i = 0; i < records.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    byte[] rec = records[i];

                    byte[] payload = new byte[1 + rec.Length];
                    payload[0] = (byte)Cmd.ProgramFlash;
                    Array.Copy(rec, 0, payload, 1, rec.Length);
                    Transact(hid, payload, ProgramTimeoutMs, ct);   // every record type we send is answered

                    if ((i & 15) == 0 || i == records.Count - 1)
                        progress?.Report(new FlashProgress { Percent = (i + 1) * 100 / records.Count, Status = "Programming…" });
                }

                // 3. Verify: the bootloader's CRC-16 over the programmed region must match the image
                //    (erased bytes read 0xFF, matching the 0xFF fill of the host image).
                progress?.Report(new FlashProgress { Percent = 100, Status = "Verifying…" });
                uint vStart = AppStart, vLen = image.LastAddressExclusive - AppStart;
                byte[] crcReq = new byte[9];
                crcReq[0] = (byte)Cmd.ReadCrc;
                BitConverter.GetBytes(vStart).CopyTo(crcReq, 1);
                BitConverter.GetBytes(vLen).CopyTo(crcReq, 5);
                byte[] crcReply = Transact(hid, crcReq, ReadCrcTimeoutMs, ct);
                if (crcReply.Length < 3)
                    throw new IOException("Short reply to the bootloader CRC request.");
                ushort deviceCrc = (ushort)(crcReply[1] | (crcReply[2] << 8));
                ushort hostCrc = Crc16(image.Bytes, (int)(vStart - image.BaseAddress), (int)vLen);
                if (deviceCrc != hostCrc)
                    throw new IOException(
                        $"Verification failed: device CRC 0x{deviceCrc:X4} differs from image CRC 0x{hostCrc:X4} " +
                        $"over 0x{vStart:X}..0x{vStart + vLen - 1:X}. The tag is still in bootloader mode: press Flash again.");

                // 4. Jump: the bootloader answers, then resets into the application.
                progress?.Report(new FlashProgress { Percent = 100, Status = "Starting application…" });
                try { Transact(hid, new[] { (byte)Cmd.JmpToApp }, JumpTimeoutMs, ct); }
                catch (IOException) { /* the reset can race the reply; the application starts either way */ }
                return true;
            }
            finally { hid.Dispose(); }
        }

        /// <summary>Discard any input reports queued from an earlier, aborted session.</summary>
        private static void Drain(IHidReportTransport hid)
        {
            byte[] buf = new byte[hid.ReportSize];
            for (int i = 0; i < 8 && hid.ReadReport(buf, 30); i++) { }
        }

        // One AN1388 transaction: frame + send the payload over HID, then wait for the reply that echoes
        // the command byte. Frames that do not belong to this command (stale) are skipped.
        private static byte[] Transact(IHidReportTransport hid, byte[] payload, int timeoutMs, CancellationToken ct)
        {
            byte[] frame = Frame(payload);

            int size = hid.ReportSize;
            for (int off = 0; off < frame.Length; off += size)
            {
                byte[] rpt = new byte[size]; // zero padding after EOT is ignored by the bootloader
                Array.Copy(frame, off, rpt, 0, Math.Min(size, frame.Length - off));
                hid.WriteReport(rpt);
            }

            long deadline = Environment.TickCount64 + timeoutMs;
            byte[] inBuf = new byte[hid.ReportSize];
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                int remaining = (int)Math.Max(1, deadline - Environment.TickCount64);
                if (!hid.ReadReport(inBuf, Math.Min(remaining, 1000)))
                {
                    if (Environment.TickCount64 >= deadline)
                        throw new IOException($"No response to bootloader command 0x{payload[0]:X2} within {timeoutMs / 1000} s.");
                    continue;
                }

                byte[]? reply = Unframe(inBuf, 0, inBuf.Length);
                if (reply == null || reply.Length < 1)
                    continue;                         // not a complete/valid frame: keep waiting
                if (reply[0] != payload[0])
                    continue;                         // stale reply from another command: skip it
                return reply;
            }
        }

        private static byte[] Frame(byte[] payload)
        {
            ushort crc = Crc16(payload, 0, payload.Length);
            byte[] body = new byte[payload.Length + 2];
            Array.Copy(payload, body, payload.Length);
            body[payload.Length] = (byte)(crc & 0xFF);
            body[payload.Length + 1] = (byte)(crc >> 8);

            var outBytes = new List<byte>(body.Length * 2 + 2) { SOH };
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
                    return Crc16(payload, 0, payLen) == crc ? payload : null;
                }
                data.Add(b);
            }
            return null;
        }

        private static ushort Crc16(byte[] data, int offset, int len)
        {
            ushort crc = 0;
            for (int j = offset; j < offset + len; j++)
            {
                int i = (crc >> 12) ^ (data[j] >> 4);
                crc = (ushort)(CrcTable[i & 0x0F] ^ (crc << 4));
                i = (crc >> 12) ^ (data[j] & 0x0F);
                crc = (ushort)(CrcTable[i & 0x0F] ^ (crc << 4));
            }
            return (ushort)(crc & 0xFFFF);
        }

        /// <summary>
        /// The Intel HEX file as (a) the raw records to send, data records re-cut to at most
        /// <see cref="MaxDataBytesPerRecord"/> bytes, and (b) a 0xFF-filled byte image for verification.
        /// </summary>
        private sealed class HexImage
        {
            public List<byte[]> Records { get; } = new();
            public byte[] Bytes { get; private set; } = Array.Empty<byte>();
            public uint BaseAddress { get; private set; }
            public uint FirstAddress { get; private set; } = uint.MaxValue;
            public uint LastAddressExclusive { get; private set; }

            public static HexImage Load(string path)
            {
                var img = new HexImage();
                var data = new List<(uint addr, byte[] bytes)>();
                uint ext = 0;

                foreach (string raw in File.ReadAllLines(path))
                {
                    string line = raw.Trim();
                    if (!line.StartsWith(":")) continue;
                    byte[] rec = HexLineToBytes(line);
                    if (rec.Length < 5 || rec.Length != rec[0] + 5)
                        throw new InvalidDataException("Malformed Intel HEX record: " + line);
                    if ((byte)rec.Sum(b => b) != 0)
                        throw new InvalidDataException("Intel HEX checksum error: " + line);

                    byte type = rec[3];
                    switch (type)
                    {
                        case 0x00:
                        {
                            uint addr = ext + (uint)((rec[1] << 8) | rec[2]);
                            int count = rec[0];
                            for (int off = 0; off < count; off += MaxDataBytesPerRecord)
                            {
                                int n = Math.Min(MaxDataBytesPerRecord, count - off);
                                uint a = addr + (uint)off;
                                byte[] piece = new byte[n];
                                Array.Copy(rec, 4 + off, piece, 0, n);
                                img.Records.Add(MakeDataRecord((ushort)(a & 0xFFFF), piece));
                                data.Add((a, piece));
                                img.FirstAddress = Math.Min(img.FirstAddress, a);
                                img.LastAddressExclusive = Math.Max(img.LastAddressExclusive, a + (uint)n);
                            }
                            break;
                        }
                        case 0x02: ext = (uint)(((rec[4] << 8) | rec[5]) << 4);  img.Records.Add(rec); break;
                        case 0x04: ext = (uint)(((rec[4] << 8) | rec[5]) << 16); img.Records.Add(rec); break;
                        case 0x01:
                        case 0x03:
                        case 0x05: img.Records.Add(rec); break;
                        default:
                            throw new InvalidDataException($"Unsupported Intel HEX record type 0x{type:X2}.");
                    }
                }

                if (img.Records.Count > 0 && img.Records[^1][3] != 0x01)
                    img.Records.Add(new byte[] { 0x00, 0x00, 0x00, 0x01, 0xFF });   // ensure an EOF record

                if (data.Count > 0)
                {
                    img.BaseAddress = img.FirstAddress;
                    img.Bytes = new byte[img.LastAddressExclusive - img.FirstAddress];
                    Array.Fill(img.Bytes, (byte)0xFF);
                    foreach (var (addr, bytes) in data)
                        Array.Copy(bytes, 0, img.Bytes, addr - img.BaseAddress, bytes.Length);
                }
                return img;
            }

            private static byte[] MakeDataRecord(ushort addr, byte[] data)
            {
                byte[] rec = new byte[data.Length + 5];
                rec[0] = (byte)data.Length;
                rec[1] = (byte)(addr >> 8);
                rec[2] = (byte)addr;
                rec[3] = 0x00;
                Array.Copy(data, 0, rec, 4, data.Length);
                int sum = 0;
                for (int i = 0; i < rec.Length - 1; i++) sum += rec[i];
                rec[^1] = (byte)(-sum);
                return rec;
            }

            // ":10010000....FF" -> the record's raw bytes (bytecount, addr, type, data, checksum).
            private static byte[] HexLineToBytes(string line)
            {
                string hex = line.Substring(line.IndexOf(':') + 1);
                int n = hex.Length / 2;
                byte[] bytes = new byte[n];
                for (int i = 0; i < n; i++)
                    bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
                return bytes;
            }
        }
    }
}
