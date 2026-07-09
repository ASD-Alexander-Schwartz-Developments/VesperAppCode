using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Ports;
using System.Threading;
using System.Diagnostics;

namespace VesperApp.Services
{
    public enum MessageTypes : byte
    {
        NACK = 0,
        ACK = 1,
        ABORT = 2,

        PIC_BASE_GET_FW_ID = 3,
        PIC_BASE_GET_HW_ID = 4,
        PIC_BASE_GET_PROTOCOL_ID = 5,
        PIC_BASE_GET_ERROR = 6,
        PIC_BASE_SET_LED = 7,
        PIC_BASE_GET_LASTCOMMAND = 8,
        PIC_BASE_DEBUG = 9,
        PIC_BASE_RST_DEVICE,
        PIC_BASE_BOOT0,
        PIC_BASE_ENVESPER,
        PIC_BASE_STATUS,
        PIC_BASE_LASTCMD,

        VESPER_GET_VER = 15,
        VESPER_GET_RTC,
        VESPER_SET_RTC,
        VESPER_SLEEP,
        VESPER_FORMATDISK,
        VESPER_MOUNTUSBDISK,
        VESPER_GETDISKSIZE,
        VESPER_GETIO,
        VESPER_SETIO,
        VESPER_START_GPS,
        VESPER_START_ADC,
        VESPER_START_IMU,
        VESPER_START_EADC,
        VESPER_GET_READINGS,

        VESPER_GET_PARAMSI,
        VESPER_SET_PARAMSI,
        VESPER_GET_PARAMS1,
        VESPER_SET_PARAMS1,
        VESPER_GET_PARAMS2,
        VESPER_SET_PARAMS2,
        VESPER_GET_SCHEDULE,
        VESPER_SET_SCHEDULE,
        VESPER_SET_EPHEMERIS,

        VESPER_START_EEG,
        VESPER_UNMOUNT_DISK,
        VESPER_GET_FLAGS,
        VESPER_SW_RESET,

        VESPER_STOP_GPS,
        VESPER_STOP_ADC,
        VESPER_STOP_IMU,
        VESPER_STOP_EADC,
        VESPER_STOP_EEG,

        VESPER_LEPTON_SNAPSHOP,

        VESPER_SET_MEMORY_LAYOUT,
        VESPER_SET_FILESIZES,
        VESPER_BURN_CONFIG,

        // Dock/USB bench-test commands. VESPER_TEST_AUDIO captures one mic and
        // returns per-tone SNR go/no-go (KOL pins it at 60; see audio_bench_format.md).
        VESPER_TEST_AUDIO = 60,

        // GNSS front-end bench test (VESPER_TEST_GPS): device captures a short
        // snapshot to RAM, detects the injected CW tone, and returns front-end
        // go/no-go (gps_test_resp_t). Must match the VesperU5 FW MSG enum
        // (feature/dock-bench-test) — see VesperU5 docs/GNSS-BENCH-BRINGUP.md.
        VESPER_TEST_GPS = 61,

        UDSP_GET_VER = 200,
        UDSP_SLEEP,
        UDSP_GET_CONFIG,
        UDSP_SET_CONFIG,
        UDSP_RUN,

        GET_VOLTAGE = 250,
        GET_LOG,
        RESET,
        GETLASTCMD,
        GETLASTERR,
        TOTAL_MSGS
    };

    public enum ErrorTypes : byte
    {
        PORT_OK = 0,
        PORT_CLOSED,

        TOTAL_MSGS
    };

    public class SerialMessage : IDisposable
    {
        public const byte STATE_WAIT_FOR_CHAR = 0;
        public const byte STATE_WAIT_FOR_ID = 1;
        public const byte STATE_WAIT_FOR_TYPE = 2;
        public const byte STATE_WAIT_FOR_LEN = 3;
        public const byte STATE_WAIT_FOR_DATA = 4;
        public const byte STATE_WAIT_FOR_CHK_SUM = 5;

        //Start of Header const
        public const byte SOH = 0x5A;
        public const byte UART_MSG_LEN = 150;

        private byte DataLen;
        private byte MsgID;
        private byte MsgType;
        private byte DataCounter;
        private byte chksum;
        private byte State;

        private object llock;

        private bool GotAck;
        private bool WaitAck;
        private byte OutMsgID;

        private System.IO.Ports.SerialPort serialPort;
        private System.Threading.Thread? workingt;
        private System.Threading.ThreadStart DataReadFunc;

        private System.Threading.Thread? working_send;
        private System.Threading.ThreadStart DataSendFunc;

        private System.Threading.Thread? working_message_processor;
        private System.Threading.ThreadStart MsgSendFunc;

        private bool IsStarted;
        private byte Sequence;

        private System.Collections.Generic.Queue<MessageEventArgs> MessagesBuffer;
        private System.Collections.Generic.Queue<MessageOutEventArgs> sendMessageQueue;

        private CancellationTokenSource cancelSend;
        private CancellationTokenSource cancelRead;
        private CancellationTokenSource cancelMsg;

        private static ManualResetEvent sendEvent = new ManualResetEvent(false);

        AsyncCallback callBack_message_done;

        private byte[] Data_Buff;

        public SerialMessage()
        {
            this.serialPort = new System.IO.Ports.SerialPort();
            this.serialPort.BaudRate = 19200;
            this.serialPort.ReadTimeout = -1;
            this.serialPort.WriteTimeout = -1;
            // STM32 CDC firmware may hold TX until the host asserts DTR; Windows
            // drivers often mask this, Linux cdc_acm does not.
            this.serialPort.DtrEnable = true;

            callBack_message_done = new AsyncCallback(ProcessMessageDone);

            MessagesBuffer = new Queue<MessageEventArgs>();
            sendMessageQueue = new Queue<MessageOutEventArgs>();

            this.DataReadFunc = new ThreadStart(this.port_DataRead);
            this.DataSendFunc = new ThreadStart(this.port_DataSend);
            this.MsgSendFunc = new ThreadStart(this.que_ProcessMessages);

            cancelSend = new CancellationTokenSource();
            cancelRead = new CancellationTokenSource();
            cancelMsg = new CancellationTokenSource();

            this.llock = new object();

            Data_Buff = new byte[0];
        }

        public SerialMessage(String Port, int baudrate) : this()
        {
            this.serialPort.PortName = Port;
            this.serialPort.BaudRate = baudrate;
        }


        public bool IsRunning => IsStarted;

        public void Start()
        {
            try
            {
                serialPort.Open();

                this.workingt = new Thread(DataReadFunc);
                this.working_send = new Thread(DataSendFunc);
                this.working_message_processor = new Thread(MsgSendFunc);

                this.workingt.Priority = ThreadPriority.BelowNormal;
                this.working_message_processor.Priority = ThreadPriority.Lowest;
                this.working_send.Priority = ThreadPriority.BelowNormal;

                workingt.Name = "SerialRead" + workingt.ManagedThreadId;
                working_send.Name = "SerialSend" + working_send.ManagedThreadId;
                working_message_processor.Name = "SerialMessage" + working_message_processor.ManagedThreadId;

                IsStarted = true;

                workingt.Start();
                working_send.Start();
                working_message_processor.Start();
            }
            catch
            {
                IsStarted = false;
                serialPort.Close();

                ErrorEventArgs err = new ErrorEventArgs();
                err.DebugMessage = "Could not open port and start threads";
                err.typeOfMessage = ErrorTypes.PORT_CLOSED;
                OnErrorEvent(err);
            }
        }


        public void Stop()
        {
            try
            {
                if (this.IsStarted == true)
                {
                    this.IsStarted = false;             // Stop the thread
                    sendEvent.Set();
                    cancelRead.Cancel();
                    cancelSend.Cancel();
                    cancelMsg.Cancel();
                    this.working_message_processor?.Join();
                    this.working_send?.Join();
                    this.workingt?.Join();
                    Thread.Sleep(1000);
                }
            }
            catch
            {
            }
            finally
            {
                if (serialPort.IsOpen == true)
                    serialPort.Close();
            }
        }


        void que_ProcessMessages()
        {
            int br;

            while (IsStarted == true && this.cancelMsg.IsCancellationRequested == false)
            {
                Monitor.Enter(this.MessagesBuffer);

                try
                {
                    br = this.MessagesBuffer.Count;
                    if (br > 0)
                        OnMessageEvent(this.MessagesBuffer.Dequeue());
                }
                catch (Exception eeee)
                {

                }
                finally
                {
                    Monitor.Exit(this.MessagesBuffer);
                }

                Thread.Sleep(10);
            }
        }

        void port_DataSend()
        {
            int messages = 0;

            while (IsStarted == true && cancelSend.IsCancellationRequested == false)
            {
                Monitor.Enter(sendMessageQueue);

                try
                {
                    messages = sendMessageQueue.Count;
                    if (messages > 0)
                    {
                        SendMessage(sendMessageQueue.Dequeue());
                    }
                }
                catch (Exception eee)
                {

                }
                finally
                {
                    Monitor.Exit(sendMessageQueue);
                }

                sendEvent.WaitOne(5000);
                sendEvent.Reset();
                //Thread.Sleep(500);
            }
        }

        void port_DataRead()
        {
            int br = 0;

            while (IsStarted == true && cancelRead.IsCancellationRequested == false)
            {
                try
                {
                    if (this.serialPort.IsOpen == true)
                    {
                        br = (int)this.serialPort.BytesToRead;
                    }
                    else
                    {
                        IsStarted = false;
                        ErrorEventArgs err = new ErrorEventArgs();
                        err.DebugMessage = "Port closed";
                        err.typeOfMessage = ErrorTypes.PORT_CLOSED;
                        OnErrorEvent(err);
                    }
                }
                catch
                {
                    ErrorEventArgs err = new ErrorEventArgs();
                    err.DebugMessage = "Port closed";
                    err.typeOfMessage = ErrorTypes.PORT_CLOSED;
                    OnErrorEvent(err);

                    serialPort.Close();
                    IsStarted = false;
                }

                if (br > 0)
                {
                    try
                    {
                        byte[] buf = new byte[br];
                        br = this.serialPort.Read(buf, 0, br);

                        for (int i = 0; i < br; i++)
                            ProcessByte(buf[i]);
                    }
                    catch (TimeoutException toe)
                    {

                    }
                }
                else
                {
                    Thread.Sleep(10);
                }

                //System.Windows.Forms.Application.DoEvents();
                Thread.Sleep(25);
            }
        }


        public static byte BCD2BIN(byte bcdNumber)
        {
            byte digit1 = (byte)(bcdNumber >> 4);
            byte digit2 = (byte)(bcdNumber & 0x0f);

            return (byte)(digit1 * 10 + digit2);
        }


        public void SendMessage(MessageOutEventArgs mo)
        {
            bool failed = true;

            Monitor.Enter(this.serialPort);

            try
            {
                if (this.serialPort.IsOpen == true && mo.MessageData != null)
                    this.serialPort.Write(mo.MessageData, mo.offset, mo.MessageData.Length);

                failed = false;
            }
            catch (ArgumentNullException ane)
            {
                //                this.Log("Bad arguments error - " + ane.Message, 0, false);
            }
            catch (InvalidOperationException ioe)
            {
                ErrorEventArgs err = new ErrorEventArgs();
                err.DebugMessage = ioe.Message; ;
                err.typeOfMessage = ErrorTypes.PORT_CLOSED;
                OnErrorEvent(err);
            }
            catch (ArgumentOutOfRangeException aor)
            {
                //                this.Log("Bad arguments error (AOR) - " + aor.Message, 0, false);
            }
            catch (ArgumentException ae)
            {
                //                this.Log("Bad arguments error (AE) - " + ae.Message, 0, false);
            }
            catch (TimeoutException toe)
            {
                ErrorEventArgs err = new ErrorEventArgs();
                err.DebugMessage = toe.Message;
                err.typeOfMessage = ErrorTypes.PORT_CLOSED;
                OnErrorEvent(err);
            }
            catch (System.IO.IOException IOEx)
            {
                ErrorEventArgs err = new ErrorEventArgs();
                err.DebugMessage = IOEx.Message;
                err.typeOfMessage = ErrorTypes.PORT_CLOSED;
                OnErrorEvent(err);
            }
            finally
            {
                if (failed == true)
                    this.Stop();

                Monitor.Exit(this.serialPort);
            }

        }

        public void SendToDevice(byte[] buffer, int offset, int count)
        {
            MessageOutEventArgs mo = new MessageOutEventArgs();
            mo.MessageData = new byte[count];
            mo.offset = offset;
            buffer.CopyTo(mo.MessageData, 0);

            Monitor.Enter(sendMessageQueue);

            try
            {
                sendMessageQueue.Enqueue(mo);
                sendEvent.Set();
            }
            catch (Exception)
            {
            }
            finally
            {
                Monitor.Exit(sendMessageQueue);
            }
        }

        #region IDisposable Methods
        public void Dispose()
        {
            Stop();

            if (serialPort != null)
            {
                serialPort.Dispose();
                serialPort = null;
            }
        }
        #endregion

        private void ProcessByte(byte data)
        {
            switch (this.State)
            {
                case STATE_WAIT_FOR_CHAR:
                    if (data == SOH)
                    {
                        this.State = STATE_WAIT_FOR_ID;
                        this.DataLen = 0;
                        this.DataCounter = 0;
                    }
                    break;

                case STATE_WAIT_FOR_ID:
                    this.MsgID = data;
                    this.State = STATE_WAIT_FOR_TYPE;
                    break;

                case STATE_WAIT_FOR_TYPE:
                    this.MsgType = data;
                    this.State = STATE_WAIT_FOR_LEN;
                    break;

                case STATE_WAIT_FOR_LEN:
                    this.DataLen = data;
                    if (this.DataLen == 0)
                    {
                        this.State = STATE_WAIT_FOR_CHK_SUM;
                    }
                    else
                    {
                        this.State = STATE_WAIT_FOR_DATA;
                    }

                    this.Data_Buff = new byte[this.DataLen];
                    this.chksum = (byte)(SOH + this.MsgID + this.MsgType + this.DataLen);
                    break;

                case STATE_WAIT_FOR_DATA:
                    this.Data_Buff[this.DataCounter] = data;
                    this.chksum += data;
                    this.DataCounter++;

                    if (this.DataLen == this.DataCounter)
                    {
                        this.State = STATE_WAIT_FOR_CHK_SUM;
                        //this.Data_Buff[this.DataCounter] = 0;
                    }
                    break;

                case STATE_WAIT_FOR_CHK_SUM:
                    if (this.MsgType == (byte)MessageTypes.ACK)
                    { //we don't need to check the ACK
                        MessageEventArgs args = new MessageEventArgs();
                        args.typeOfMessage = MessageTypes.ACK;
                        args.MessageData = new byte[0];

                        Monitor.Enter(this.MessagesBuffer);
                        this.MessagesBuffer.Enqueue(args);
                        Monitor.Exit(this.MessagesBuffer);

                        this.State = STATE_WAIT_FOR_CHAR;
                    }
                    else if (this.MsgType == (byte)MessageTypes.NACK)
                    {
                        MessageEventArgs args = new MessageEventArgs();
                        args.typeOfMessage = MessageTypes.ACK;
                        args.MessageData = new byte[0];

                        Monitor.Enter(this.MessagesBuffer);
                        this.MessagesBuffer.Enqueue(args);
                        Monitor.Exit(this.MessagesBuffer);

                        this.State = STATE_WAIT_FOR_CHAR;
                    }
                    //check validity of checksum
                    else
                    {
                        if ((this.chksum & 0xFF) == data)
                        {
                            MessageEventArgs args = new MessageEventArgs();
                            args.typeOfMessage = (MessageTypes)this.MsgType;
                            args.MessageData = new byte[this.Data_Buff.Length];

                            if (args.MessageData.Length > 0)
                                this.Data_Buff.CopyTo(args.MessageData, 0);

                            Monitor.Enter(this.MessagesBuffer);
                            this.MessagesBuffer.Enqueue(args);
                            Monitor.Exit(this.MessagesBuffer);
                        }
                        else
                        {
                            //lastError = CHKSUM_ERROR;
                        }
                    }

                    this.State = STATE_WAIT_FOR_CHAR;
                    //bCharReady = 1;
                    break;
            }

        }

        public string PortName
        {
            get
            {
                return this.serialPort.PortName;
            }
            set
            {
                this.serialPort.PortName = value;
            }
        }

        private void ProcessMessageDone(IAsyncResult result)
        {
            if (result.IsCompleted == true)
            {
            }
        }

        private readonly object someEventLock = new object();
        private readonly object errorEventLock = new object();

        protected virtual void OnMessageEvent(MessageEventArgs e)
        {
            EventHandler<MessageEventArgs>? handler;

            lock (this.someEventLock)
            {
                handler = this.msgEvent;
            }
            if (handler != null)
            {
                handler.Invoke(this, e);
            }
        }

        protected virtual void OnErrorEvent(ErrorEventArgs e)
        {
            EventHandler<ErrorEventArgs>? handler;

            lock (this.errorEventLock)
            {
                handler = this.errEvent;
            }
            if (handler != null)
            {
                //handler.BeginInvoke(this, e, callBack_message_done, null);
                handler.Invoke(this, e);
            }
        }


        private EventHandler<MessageEventArgs>? msgEvent;
        private EventHandler<ErrorEventArgs>? errEvent;

        public event EventHandler<MessageEventArgs> MessageEvent
        {
            add
            {
                lock (this.someEventLock)
                {
                    this.msgEvent += value;
                }
            }

            remove
            {
                lock (this.someEventLock)
                {
                    this.msgEvent -= value;
                }
            }
        }

        public event EventHandler<ErrorEventArgs> ErrorEvent
        {
            add
            {
                lock (this.errorEventLock)
                {
                    this.errEvent += value;
                }
            }

            remove
            {
                lock (this.errorEventLock)
                {
                    this.errEvent -= value;
                }
            }
        }


        public static byte[] DateTimeToBytes(DateTime dt)
        {
            byte[] bt = new byte[8];

            bt[0] = (byte)dt.Second;
            bt[1] = (byte)dt.Minute;
            bt[2] = (byte)dt.Hour;
            bt[3] = (byte)dt.Day;
            bt[4] = (byte)dt.Month;
            bt[5] = (byte)(dt.Year & 0xFF);
            bt[6] = (byte)((dt.Year >> 8) & 0xFF);
            bt[7] = (byte)(dt.Kind);

            return bt;
        }

        public static DateTime BytesToDateTime(byte[] buf)
        {
            int year = buf[5] + (int)(buf[6] << 8);

            DateTime dt = new DateTime(year, buf[4], buf[3], buf[2], buf[1], buf[0], (byte)DateTimeKind.Utc);

            return dt;
        }

        public static MessageTypes ExtractMessageType(byte[] buffer)
        {
            return (MessageTypes)buffer[2];
        }

        public static byte[] ExtractMessagePayload(byte[] buffer, int len)
        {
            byte[] buf = new byte[len];

            for (int i = 0; i < len; i++)
                buf[i] = buffer[i + 4];

            return buf;
        }


        public static byte[] PROTO_MsgBuild(byte msgType, byte msgLen, byte[] msgBuff, byte OutMsgID)
        {
            byte[] outMsg = new byte[5 + msgLen];

            byte i = 0, checksum = 0;

            if (msgLen > UART_MSG_LEN - 5)
                return new byte[0];

            outMsg[0] = SOH;                    //start of header
            outMsg[1] = OutMsgID;                   //MSG ID 0 = dbg, 1 = multisensor, 2 = vesper or other
            outMsg[2] = msgType;                    //MSG TYPE
            outMsg[3] = msgLen;

            checksum = (byte)(SOH + OutMsgID + msgType + msgLen);

            for (i = 0; i < msgLen; i++)
            {
                outMsg[i + 4] = msgBuff[i];         //payload len
                checksum += msgBuff[i];
            }

            outMsg[i + 4] = (checksum);

            return outMsg;
        }

        public static string ByteArrayToString(byte[] ba)
        {
            StringBuilder hex = new StringBuilder(ba.Length * 2);
            foreach (byte b in ba)
                hex.AppendFormat("{0:X2}", b);
            return hex.ToString();
        }
    }






    public class MessageEventArgs : EventArgs
    {
        public String? DebugMessage { get; set; }
        public byte[]? MessageData { get; set; }
        public MessageTypes? typeOfMessage { get; set; }
    }

    public class MessageOutEventArgs : EventArgs
    {
        public byte[]? MessageData { get; set; }
        public int offset;
    }

    public class ErrorEventArgs : EventArgs
    {
        public String ?DebugMessage { get; set; }
        public ErrorTypes? typeOfMessage { get; set; }
    }
}
