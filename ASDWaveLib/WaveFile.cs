using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASDWaveLib
{
    public class WaveFile : IDisposable
    {
        private readonly char[] ChunkID = { 'R', 'I', 'F', 'F' };
        private UInt32 length;
        private readonly char[] Format = { 'W', 'A', 'V', 'E' };
        private readonly char[] Subchunk1ID = { 'f', 'm', 't', ' ' };
        private readonly UInt32 Subchunk1Size = 16;
        private UInt16 AudioFormat;
        private UInt16 NumOfChannels;
        private UInt32 SampleRate;
        private UInt32 ByteRate;
        private UInt16 BlockAlign;
        private UInt16 bps;

        private readonly char[] Subchunk2ID = { 'd', 'a', 't', 'a' };
        private UInt32 Subchunk2Size;
        private byte[]? DataBuffer;

        private string filename;
        private bool IsOpened;
        private FileStream? fs;
        private BinaryWriter? bwr;
        private string? metadata;

        public WaveFile(string filename, string? meta = null, UInt16 Channels = 1, UInt32 SampleRate = 0, UInt16 bps = 16)
        {
            this.filename = filename;
            this.NumOfChannels = Channels;
            this.SampleRate = SampleRate;
            this.bps = bps;

            this.AudioFormat = 1;
            this.ByteRate = (UInt32)(this.SampleRate * ((this.bps * this.NumOfChannels) / 8));
            this.BlockAlign = (UInt16)((this.bps * this.NumOfChannels) / 8);

            this.Subchunk2Size = 0;
            this.DataBuffer = null;
            this.fs = null;
            this.bwr = null;
            this.metadata = meta;
            this.IsOpened = false;


            if(this.metadata != null && this.metadata.Length > 0)
            {
                if(this.metadata.Contains("SampleRate") == true)
                {
                    int start = this.metadata.IndexOf("SampleRate");
                    int valstart = this.metadata.IndexOf(":", start) + 1;
                    int end = this.metadata.IndexOf(Environment.NewLine, valstart);
                    string value = this.metadata.Substring(valstart, end-valstart);

                    if(UInt32.TryParse(value, out var sr))
                    {
                        this.SampleRate = sr;
                        this.ByteRate = (UInt32)(this.SampleRate * ((this.bps * this.NumOfChannels) / 8));
                    }
                }
            }
        }

        public void Open()
        {
            try
            {
                string fname = filename + ".WAV";
                if(File.Exists(fname) == true) 
                { 
                    File.Delete(fname);
                }

                fs = new FileStream(fname, FileMode.CreateNew, FileAccess.Write);
                bwr = new BinaryWriter(fs);
                IsOpened = true;
            }
            catch (Exception ex)
            {
                IsOpened = false;
            }
        }


        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        // NOTE: Leave out the finalizer altogether if this class doesn't   
        // own unmanaged resources itself, but leave the other methods  
        // exactly as they are.   

        // The bulk of the clean-up code is implemented in Dispose(bool)  
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // free managed resources  

                this.DataBuffer = new byte[0];
                this.DataBuffer = null;

                if (bwr != null)
                {
                    bwr.Dispose();
                    bwr = null;
                }

                if (fs != null)
                {
                    fs.Dispose();
                    fs = null;
                }
            }
        }


        public void WriteWave()
        {
            if (this.IsOpened == true && this.DataBuffer != null && bwr != null)
            {
                try
                {
                    this.Subchunk2Size = (UInt32)this.DataBuffer.Length;
                    this.length = (UInt32)Subchunk2Size + this.Subchunk1Size + 4;

                    if(SampleRate == 0)
                    {
                        SampleRate = (uint)this.DataBuffer.Length / 2;                // 16bit per sample
                        this.ByteRate = (UInt32)(this.SampleRate * ((this.bps * this.NumOfChannels) / 8));
                    }

                    bwr.Write(this.ChunkID);
                    bwr.Write(this.length);
                    bwr.Write(this.Format);
                    bwr.Write(this.Subchunk1ID);
                    bwr.Write(this.Subchunk1Size);
                    bwr.Write(this.AudioFormat);
                    bwr.Write(this.NumOfChannels);
                    bwr.Write(this.SampleRate);
                    bwr.Write(this.ByteRate);
                    bwr.Write(this.BlockAlign);
                    bwr.Write(this.bps);
                    bwr.Write(this.Subchunk2ID);
                    bwr.Write(this.Subchunk2Size);

                    bwr.Write(this.DataBuffer, 0, this.DataBuffer.Length);
                }
                catch
                {

                }
                finally
                {
                    bwr.Flush();
                    bwr.Close();

                    IsOpened = false;
                }
            }
        }


        public void WriteWave(byte[] buffer)
        {
            if (buffer != null)
            {
                if (buffer.Length > 0)
                {
                    this.DataBuffer = new byte[buffer.Length];
                    buffer.CopyTo(this.DataBuffer, 0);

                    
                    Int16[] buf16bit = new Int16[buffer.Length / 2];
                    for (int objIndex = 0; objIndex < buf16bit.Length; objIndex++)
                    {
                        buf16bit[objIndex] = (Int16)(((UInt16)buffer[objIndex * 2]) +
                                                    ((UInt16)buffer[(objIndex * 2) + 1] << 8));
                    }
                    
                    double arr_avg = 0.0;
                    /*for (int i = 0; i < buf16bit.Length; i++)
                    {
                        arr_avg += buf16bit[i];
                    }
                    arr_avg /= (double)buf16bit.Length;
                    */
                    double[] in_data = new double[buffer.Length / 2];
                    for (int i = 0; i < buf16bit.Length; i++)
                    {
                        in_data[i] = (double)((double)((double)buf16bit[i]/* - arr_avg*/) / (double)Int16.MaxValue);
                    }

                    double[] filtered_data = ButterworthFilter.Butterworth(in_data, SampleRate, SampleRate / 2.2);
                    
                    for (int objIndex = 0; objIndex < buf16bit.Length; objIndex++)
                    {
                        buf16bit[objIndex] = (short)(((double)filtered_data[objIndex] * (double)Int16.MaxValue));
                        DataBuffer[objIndex * 2] = (byte)(buf16bit[objIndex] & 0xFF);
                        DataBuffer[objIndex * 2 + 1] = (byte)(buf16bit[objIndex] >> 8);
                    }

                    this.WriteWave();
                }
            }
        }
    }
}
