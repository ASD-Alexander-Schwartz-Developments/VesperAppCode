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
        public static Task<int> ExtractVesperSnap(String BinaryFileName, String OutputFolder, TimeSpan gpsTimeOffset)
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

                    byte[] databuffer = new byte[reader.BaseStream.Length - offset];

                    reader.Read(databuffer, 0, databuffer.Length);

                    isStarted = false;
                    isFinished = false;
                    for (int idx = 0; idx < databuffer.Length; idx++)
                    {
                        lastbyte = databuffer[idx];
                        word = (UInt32)(((UInt32)word >> 8) + ((UInt32)lastbyte << 24));
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
                                    wn += "." + first_letter + "BN";
                                    string metadata_filename = wn + ".txt";

                                    string metadata = string.Empty;

                                    if (File.Exists(wn) == false)
                                    {
                                        if (File.Exists(metadata_filename) == true)
                                        {
                                            File.Delete(metadata_filename);
                                        }

                                        if (binaryTypeHeader != null)
                                        {
                                            metadata += ("DeviceID:" + binaryTypeHeader.UId.ToString("X") + Environment.NewLine);
                                            metadata += ("HWID:" + binaryTypeHeader.HwId.ToString("X") + Environment.NewLine);
                                            metadata += ("FWID:" + binaryTypeHeader.FwId.ToString("X") + Environment.NewLine);
                                            metadata += ("Sensor:" + binaryTypeHeader.DeviceDriverName + Environment.NewLine);
                                            metadata += ("SampleRate:" + binaryTypeHeader.SamplingRate + Environment.NewLine);
                                            metadata += ("WinRate:" + binaryTypeHeader.WindowRate + Environment.NewLine);
                                            metadata += ("WinLen:" + binaryTypeHeader.WindowLength + Environment.NewLine);
                                            metadata += ("Config0:" + binaryTypeHeader.Configuration0.ToString("X") + Environment.NewLine);
                                            metadata += ("Config1:" + binaryTypeHeader.Configuration1.ToString("X") + Environment.NewLine);
                                            metadata += ("Config2:" + binaryTypeHeader.Configuration2.ToString("X") + Environment.NewLine);
                                            metadata += ("Config3:" + binaryTypeHeader.Configuration3.ToString("X") + Environment.NewLine);
                                            metadata += ("Bitmask:" + binaryTypeHeader.Bitmask.ToString("X") + Environment.NewLine);

                                            if (timestamp.SyncTimestamps != null)
                                            {
                                                foreach (BinHeader binh in timestamp.SyncTimestamps)
                                                {
                                                    metadata += ("Sync:" + binh.HeaderTimestamp.ToString("dd/MM/yyyy hh:mm:ss.FFF") + ":" + (binh.StartPosition / 2).ToString() + Environment.NewLine);       /// shift start position to get it in number of sample
                                                }
                                            }

                                            File.WriteAllText(metadata_filename, metadata, Encoding.UTF8);
                                        }

                                        using (FileStream fs = File.OpenWrite(wn))
                                        {
                                            if (timestamp.SyncTimestamps == null || timestamp.SyncTimestamps.Count == 0)
                                            {
                                                fs.Write(databuffer, timestamp.StartHeader.StartPosition, timestamp.EndHeader.StartPosition - timestamp.StartHeader.StartPosition);
                                            }
                                            else
                                            {
                                                fs.Write(databuffer, timestamp.StartHeader.StartPosition, timestamp.SyncTimestamps[0].StartPosition - timestamp.StartHeader.StartPosition);

                                                for (int i = 0; i < timestamp.SyncTimestamps.Count - 1; i++)
                                                {
                                                    fs.Write(databuffer, timestamp.SyncTimestamps[i].StartPosition + BinHeader.BIN_HEADER_LENGTH, timestamp.SyncTimestamps[i + 1].StartPosition - (timestamp.SyncTimestamps[i].StartPosition + BinHeader.BIN_HEADER_LENGTH));
                                                }

                                                fs.Write(databuffer, timestamp.SyncTimestamps[timestamp.SyncTimestamps.Count - 1].StartPosition + BinHeader.BIN_HEADER_LENGTH, timestamp.EndHeader.StartPosition - (timestamp.SyncTimestamps[timestamp.SyncTimestamps.Count - 1].StartPosition + BinHeader.BIN_HEADER_LENGTH));
                                            }

                                            fs.Close();
                                        }

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

                            wn += activeTimestamp.StartHeader.ToString();
                            wn += "-0000_00_00_00_00_00.000";
                            wn += "." + first_letter + "BN";
                            string metadata_filename = wn + ".txt";

                            string metadata = string.Empty;

                            if (File.Exists(wn) == false)
                            {
                                if (File.Exists(metadata_filename) == true)
                                {
                                    File.Delete(metadata_filename);
                                }

                                if (binaryTypeHeader != null)
                                {
                                    metadata += ("DeviceID:" + binaryTypeHeader.UId.ToString("X") + Environment.NewLine);
                                    metadata += ("HWID:" + binaryTypeHeader.HwId.ToString("X") + Environment.NewLine);
                                    metadata += ("FWID:" + binaryTypeHeader.FwId.ToString("X") + Environment.NewLine);
                                    metadata += ("Sensor:" + binaryTypeHeader.DeviceDriverName + Environment.NewLine);
                                    metadata += ("SampleRate:" + binaryTypeHeader.SamplingRate + Environment.NewLine);
                                    metadata += ("WinRate:" + binaryTypeHeader.WindowRate + Environment.NewLine);
                                    metadata += ("WinLen:" + binaryTypeHeader.WindowLength + Environment.NewLine);
                                    metadata += ("Config0:" + binaryTypeHeader.Configuration0.ToString("X") + Environment.NewLine);
                                    metadata += ("Config1:" + binaryTypeHeader.Configuration1.ToString("X") + Environment.NewLine);
                                    metadata += ("Config2:" + binaryTypeHeader.Configuration2.ToString("X") + Environment.NewLine);
                                    metadata += ("Config3:" + binaryTypeHeader.Configuration3.ToString("X") + Environment.NewLine);
                                    metadata += ("Bitmask:" + binaryTypeHeader.Bitmask.ToString("X") + Environment.NewLine);

                                    if (activeTimestamp.SyncTimestamps != null)
                                    {
                                        foreach (BinHeader binh in activeTimestamp.SyncTimestamps)
                                        {
                                            metadata += ("Sync:" + binh.HeaderTimestamp.ToString("dd/MM/yyyy hh:mm:ss.FFF") + ":" + (binh.StartPosition / 2).ToString() + Environment.NewLine);       /// shift start position to get it in number of sample
                                        }
                                    }

                                    File.WriteAllText(metadata_filename, metadata, Encoding.UTF8);
                                }

                                using (FileStream fs = File.OpenWrite(wn))
                                {
                                    if (activeTimestamp.SyncTimestamps == null || activeTimestamp.SyncTimestamps.Count == 0)
                                    {
                                        fs.Write(databuffer, activeTimestamp.StartHeader.StartPosition, databuffer.Length - activeTimestamp.StartHeader.StartPosition);
                                    }
                                    else
                                    {
                                        fs.Write(databuffer, activeTimestamp.StartHeader.StartPosition, activeTimestamp.SyncTimestamps[0].StartPosition - activeTimestamp.StartHeader.StartPosition);

                                        for (int i = 0; i < activeTimestamp.SyncTimestamps.Count - 1; i++)
                                        {
                                            fs.Write(databuffer, activeTimestamp.SyncTimestamps[i].StartPosition + BinHeader.BIN_HEADER_LENGTH, activeTimestamp.SyncTimestamps[i + 1].StartPosition - (activeTimestamp.SyncTimestamps[i].StartPosition + BinHeader.BIN_HEADER_LENGTH));
                                        }
                                        fs.Write(databuffer, activeTimestamp.SyncTimestamps[activeTimestamp.SyncTimestamps.Count - 1].StartPosition + BinHeader.BIN_HEADER_LENGTH, databuffer.Length - (activeTimestamp.SyncTimestamps[activeTimestamp.SyncTimestamps.Count - 1].StartPosition + BinHeader.BIN_HEADER_LENGTH));
                                    }
                                    fs.Close();
                                }
                            }
                        }
                    }
                }
            }
            catch { }
            finally { }

            return Task.FromResult(0);
        }
    }
}
