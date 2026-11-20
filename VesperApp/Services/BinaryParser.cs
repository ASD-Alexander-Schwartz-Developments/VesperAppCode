using HarfBuzzSharp;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Text;
using System.Threading.Tasks;
using VesperApp.Models;

namespace VesperApp.Services
{
    public class BinaryParser
    {
        /// <summary>Snapshot files from a G.BIN. With <paramref name="clock"/> (CLOCK.TXT of the unit) a snapshot whose
        /// clock base is known is named in UTC (GNSS decoding needs it); otherwise in the unit's own time.</summary>
        public static Task<int> ExtractVesperSnap(String BinaryFileName, String OutputFolder, TimeSpan gpsTimeOffset,
                                                  VesperApp.Services.Time.ClockFile? clock = null)
        {
            int result;
            byte lastbyte = 0;
            UInt32 word = 0;
            BinHeader? binHeader = null;
            int snap_offset = 1024;


            try
            {
                byte[] databuffer = File.ReadAllBytes(BinaryFileName);

                if (databuffer.Length > 1024)
                {
                    for (int idx = 0; idx < 1024; idx++)
                    {
                        lastbyte = databuffer[idx];
                        word = (UInt32)(((UInt32)word >> 8) + ((UInt32)lastbyte << 24));
                        if (word == BinHeader.PreambleHeader)
                        {
                            binHeader = new BinHeader(databuffer, idx-3, 'G');
                            idx += 1024;
                            snap_offset = 1024;
                            break;
                        }
                    }
                }

                if (binHeader == null)                   ///// unknown file - finish here
                {
                    result = -1;
                }
                else
                {
                    DateTime timestamp = binHeader.HeaderTimestamp;
                    if (clock != null)
                        timestamp = clock.BaseAt(timestamp).ToUtc(timestamp) ?? timestamp;
                    timestamp += gpsTimeOffset;

                    string wn = String.Format("{0}{1}{2,4:D4}_{3,2:D2}_{4,2:D2}_{5,2:D2}_{6,2:D2}_{7,2:D2}_GC0.dat",
                        new object[] {
                                    OutputFolder + Path.DirectorySeparatorChar, "snap.", timestamp.Year, timestamp.Month, 
                            timestamp.Day, timestamp.Hour, timestamp.Minute, timestamp.Second});

                    if (!File.Exists(wn))
                    {
                        for (int i = snap_offset; i < databuffer.Length; i += 4)
                        {
                            byte l0 = databuffer[i];
                            byte l1 = databuffer[i + 1];
                            byte l2 = databuffer[i + 2];
                            byte l3 = databuffer[i + 3];

                            UInt32 temp = (UInt32)(((UInt32)(l0) + (UInt32)(l1 << 8) + (UInt32)(l2 << 16) + (UInt32)(l3 << 24)));

                            temp = SwapWords(temp);

                            databuffer[i] = ((byte)(temp & 0xFF));
                            databuffer[i + 1] = ((byte)(temp >> 8));
                            databuffer[i + 2] = ((byte)(temp >> 16));
                            databuffer[i + 3] = ((byte)(temp >> 24));
                        }

                        FileStream datsnap = File.Create(wn);
                        datsnap.Write(databuffer, snap_offset, (int)(databuffer.Length - snap_offset));
                        datsnap.Close();
                        result = (int)(databuffer.Length - snap_offset);
                    }
                    else
                    {
                        result = 1;
                    }
                }

            }
            catch
            {
                result = (int)-1;
            }

            return Task.FromResult(result);
        }



        static UInt32 SwapBytes(UInt32 v)
        {
            return (((0xFF00FF00 & v) >> 8) | ((0x00FF00FF & v) << 8));
        }

        static UInt32 SwapWords(UInt32 v)
        {
            return (((0xFFFF0000 & v) >> 16) | ((0x0000FFFF & v) << 16));
        }

        static UInt32 SwapBytesAndWords(UInt32 v)
        {
            return SwapWords(SwapBytes(v));
        }

        public static Task<int> StripSplit(String BinaryFileName, String? OutputFolder, int offset)
        {
            return StripSplitEx(BinaryFileName, null, OutputFolder, offset);
        }

        public static Task<int> StripSplitEx(String BinaryFileName, char? channel, String? OutputFolder, int offset)
        {
            bool isStarted = false;
            bool isFinished = false;
            UInt32 word = 0;
            byte lastbyte;
            String wn = "";

            BinaryTypeHeader? binaryTypeHeader = null;
            List<BinTimestamp> binTimestamps = new List<BinTimestamp>();
            BinHeader? binHeader = null;

            string filename = Path.GetFileNameWithoutExtension(BinaryFileName);
            char first_letter = 'Z';

            string check = filename.ToUpper();
            
            if(check.Contains('U'))
            {
                first_letter = 'U';
            }
            else if (check.Contains('A'))
            {
                first_letter = 'A';
            }
            else if (check.Contains('M'))
            {
                first_letter = 'M';
            }
            else if (check.Contains('E'))
            {
                first_letter = 'E';
            }
            else if (check.Contains('S'))
            {
                first_letter = 'S';
            }
            else if (check.Contains('O'))           /// log
            {
                first_letter = 'O';
            }
            else if (check.Contains('R'))
            {
                first_letter = 'R';
            }
            else if (check.Contains('L'))
            {
                first_letter = 'L';
            }
            else if (check.Contains('X'))
            {
                first_letter = 'X';
            }
            else if (check.Contains('C'))
            {
                first_letter = 'C';
            }
            else if (check.Contains('H'))           /// health logger (LOGGER pseudo-sensor)
            {
                first_letter = 'H';
            }
            else
            {
                return Task.FromResult(-1);
            }

            string? folder = (OutputFolder == null || OutputFolder?.Length == 0) ? Path.GetDirectoryName(BinaryFileName) : OutputFolder;
            BinTimestamp? activeTimestamp = null;

            try
            {
                using (BinaryReader reader = new BinaryReader(File.Open(BinaryFileName, FileMode.Open)))
                {
                    reader.BaseStream.Position = offset;
                    UInt32 preamble = reader.ReadUInt32();
                    reader.BaseStream.Position = offset;

                    if (preamble == BinaryTypeHeader.FILE_HEADER_PREAMBLE)
                    {
                        byte[] buffer = new byte[BinaryTypeHeader.FILE_HEADER_LENGTH];
                        reader.Read(buffer, offset, BinaryTypeHeader.FILE_HEADER_LENGTH);

                        binaryTypeHeader = new BinaryTypeHeader(buffer);
                        reader.BaseStream.Position = offset + BinaryTypeHeader.FILE_HEADER_LENGTH;
                    }
                    else
                    {
                        reader.BaseStream.Position = offset;
                    }

                    // Exactly the bytes after the file header (a buffer of the whole file length left
                    // 128 zero bytes at the end, which a file without footer copied into its intermediate).
                    byte[] databuffer = reader.ReadBytes((int)(reader.BaseStream.Length - reader.BaseStream.Position));

                    isStarted = false;
                    isFinished = false;
                    for (int idx = 0; idx < databuffer.Length; idx++)
                    {
                        lastbyte = databuffer[idx];
                        word = (UInt32)(((UInt32)word >> 8) + ((UInt32)lastbyte << 24));
                        if ((word == BinHeader.PreambleHeader || word == BinHeader.PreambleFooter || word == BinHeader.PreambleSync)
                            && !BinHeader.IsValidStamp(databuffer, idx + 1))
                        {
                            continue;       // the magic's bytes in PCM, not a block (one used to drop the whole file)
                        }
                        if (word == BinHeader.PreambleHeader)
                        {
                            if(isFinished == false && activeTimestamp != null)
                            {
                                binTimestamps.Add(activeTimestamp);
                            }

                            isFinished = false;
                            isStarted = true;
                            binHeader = new BinHeader(databuffer, idx-3, first_letter);
                            activeTimestamp = new BinTimestamp(filename);
                            activeTimestamp.StartHeader = binHeader;
                        }
                        else if(word == BinHeader.PreambleFooter)
                        {
                            if(isStarted == true && activeTimestamp != null)
                            {
                                isFinished = true;
                                binHeader = new BinHeader(databuffer, idx - 3, first_letter);
                                activeTimestamp.EndHeader = binHeader;
                                binTimestamps.Add(activeTimestamp);
                                binHeader = null;
                                isFinished = false;
                                isStarted = false;
                            }
                        }
                        else if(word == BinHeader.PreambleSync)
                        {
                            if(isStarted == true && isFinished == false && activeTimestamp != null)
                            {
                                binHeader = new BinHeader(databuffer, idx - 3, first_letter);
                                activeTimestamp.SyncTimestamps.Add(binHeader);
                            }
                        }
                    }

                    if (binTimestamps.Count > 0)
                    {
                        foreach (BinTimestamp timestamp in binTimestamps)
                        {
                            if (timestamp != null)
                            {
                                if (timestamp.StartHeader != null && timestamp.EndHeader != null)
                                {
                                    if (folder == null)
                                        wn = "";
                                    else
                                        wn = folder + Path.DirectorySeparatorChar;

                                    wn += timestamp.StartHeader.ToString();
                                    wn += '-' + timestamp.EndHeader.ToString();

                                    if (channel != null)
                                    {
                                        wn += '_';
                                        wn += first_letter;
                                        wn += channel;
                                        wn += '_';
                                    }

                                    wn += "." + first_letter + "BN";
                                    string metadata_filename = wn + ".txt";

                                    string metadata = string.Empty;

                                    if (File.Exists(wn) == false)
                                    {
                                        WriteSegment(databuffer, timestamp, timestamp.EndHeader.StartPosition, wn, metadata_filename,
                                                     binaryTypeHeader, first_letter, null);
                                    }
                                }
                            }
                        }
                    }
                    else if(activeTimestamp != null)           /// We don't have full timestamps so let's check if we have partial one
                    {
                        if (activeTimestamp.StartHeader != null)
                        {
                            if (folder == null)
                                wn = "";
                            else
                                wn = folder + Path.DirectorySeparatorChar;

                            if (channel != null)
                            {
                                wn += '_';
                                wn += first_letter;
                                wn += channel;
                                wn += '_';
                            }

                            wn += activeTimestamp.StartHeader.ToString();
                            wn += "-0000_00_00_00_00_00.000";
                            if (channel != null)
                            {
                                wn += '_';
                                wn += first_letter;
                                wn += channel;
                                wn += '_';
                            }
                            wn += "." + first_letter + "BN";
                            string metadata_filename = wn + ".txt";

                            string metadata = string.Empty;

                            if (File.Exists(wn) == false)
                            {
                                // No FOOTER: the unit stopped without closing the file (reset - watchdog, low
                                // supply - or power loss). Everything in the file is extracted; what was recorded
                                // after its last write is not on the card (kol-recording-format.md "Reset while recording").
                                WriteSegment(databuffer, activeTimestamp, databuffer.Length, wn, metadata_filename, binaryTypeHeader, first_letter,
                                             "Unclosed:no FOOTER - recording ended by a reset or power loss; data up to the end of the file");
                            }
                        }
                    }
                }
            }
            catch { }
            finally { }

            return Task.FromResult(0);
        }

        /// <summary>
        /// Writes one segment (HEADER .. <paramref name="endPos"/>) of a raw file: the payload without its blocks to
        /// <paramref name="wn"/> (GAP blocks handled by <see cref="KolGapWriter"/>, which keeps the sample timeline),
        /// then the .txt metadata. A "Sync:" line holds the SYNC time and the position of that SYNC in the written file
        /// in 2-byte units - the sample number in the WAV for audio (it used to be the raw-file position, which also
        /// counted the 16-byte blocks: 8 more per earlier SYNC). Audio files also get the rate measured from the SYNCs
        /// (<see cref="SampleClockFit"/>, backlog F6).
        /// </summary>
        private static void WriteSegment(byte[] databuffer, BinTimestamp ts, int endPos, string wn, string metadataFilename,
                                         BinaryTypeHeader? hdr, char firstLetter, string? closingLine)
        {
            if (File.Exists(metadataFilename))
                File.Delete(metadataFilename);

            var syncAt = new List<(long Sample, DateTime Time)>();
            List<string> notes;
            int L = BinHeader.BIN_HEADER_LENGTH;
            using (FileStream fs = File.OpenWrite(wn))
            {
                var gw = new KolGapWriter(databuffer, fs);
                var syncs = ts.SyncTimestamps;
                int from = ts.StartHeader!.StartPosition;
                if (syncs != null)
                {
                    foreach (BinHeader s in syncs)
                    {
                        gw.Write(from, s.StartPosition - from);
                        syncAt.Add((fs.Position / 2, s.HeaderTimestamp));
                        from = s.StartPosition + L;
                    }
                }
                gw.Write(from, endPos - from);
                notes = gw.Notes;
            }

            if (hdr == null)
                return;

            var m = new StringBuilder();
            m.Append("DeviceID:" + hdr.UId.ToString("X") + Environment.NewLine);
            m.Append("HWID:" + hdr.HwId.ToString("X") + Environment.NewLine);
            m.Append("FWID:" + hdr.FwId.ToString("X") + Environment.NewLine);
            m.Append("Sensor:" + hdr.DeviceDriverName + Environment.NewLine);
            m.Append("SampleRate:" + hdr.SamplingRate + Environment.NewLine);
            m.Append("WinRate:" + hdr.WindowRate + Environment.NewLine);
            m.Append("WinLen:" + hdr.WindowLength + Environment.NewLine);
            m.Append("Config0:" + hdr.Configuration0.ToString("X") + Environment.NewLine);
            m.Append("Config1:" + hdr.Configuration1.ToString("X") + Environment.NewLine);
            m.Append("Config2:" + hdr.Configuration2.ToString("X") + Environment.NewLine);
            m.Append("Config3:" + hdr.Configuration3.ToString("X") + Environment.NewLine);
            m.Append("Bitmask:" + hdr.Bitmask.ToString("X") + Environment.NewLine);
            foreach (var (sample, time) in syncAt)
                m.Append("Sync:" + time.ToString("dd/MM/yyyy HH:mm:ss.FFF") + ":" + sample + Environment.NewLine);
            if (firstLetter == 'U')
            {
                var fit = SampleClockFit.Fit(syncAt, hdr.SamplingRate);
                if (fit != null)
                    m.Append(SampleClockFit.MetadataLines(fit));
            }
            if (closingLine != null)
                m.Append(closingLine + Environment.NewLine);
            if (notes.Count > 0)
                m.Append(string.Join(Environment.NewLine, notes) + Environment.NewLine);
            File.WriteAllText(metadataFilename, m.ToString(), Encoding.UTF8);
        }
    }
}
