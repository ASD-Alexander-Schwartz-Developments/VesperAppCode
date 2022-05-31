using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASDLibUSBWrapper
{
    /// <summary>
    ///  The generic USB transfer structure. The user populates this structure and
    ///  then submits it in order to request a transfer. After the transfer has
    ///  completed, the library populates the transfer with the results and passes
    ///  it back to the user.
    /// </summary>
    [StructLayoutAttribute(LayoutKind.Sequential, Pack = NativeImport.Pack)]
    public unsafe struct Transfer
    {
        /// <summary>
        ///  Handle of the device that this transfer will be submitted to
        /// </summary>
        public IntPtr DevHandle;

        /// <summary>
        ///  A bitwise OR combination of
        /// </summary>
        public byte Flags;

        /// <summary>
        ///  Address of the endpoint where this transfer will be sent.
        /// </summary>
        public byte Endpoint;

        /// <summary>
        ///  Type of the endpoint from
        /// </summary>
        public byte Type;

        /// <summary>
        ///  Timeout for this transfer in milliseconds. A value of 0 indicates no
        ///  timeout.
        /// </summary>
        public uint Timeout;

        /// <summary>
        ///  The status of the transfer. Read-only, and only for use within
        ///  transfer callback function.
        ///  If this is an isochronous transfer, this field may read COMPLETED even
        ///  if there were errors in the frames. Use the
        ///  to determine if errors occurred.
        /// </summary>
        public TransferStatus Status;

        /// <summary>
        ///  Length of the data buffer
        /// </summary>
        public int Length;

        /// <summary>
        ///  Actual length of data that was transferred. Read-only, and only for
        ///  use within transfer callback function. Not valid for isochronous
        ///  endpoint transfers.
        /// </summary>
        public int ActualLength;

        /// <summary>
        ///  Callback function. This will be invoked when the transfer completes,
        ///  fails, or is cancelled.
        /// </summary>
        public IntPtr Callback;

        /// <summary>
        ///  User context data to pass to the callback function.
        /// </summary>
        public IntPtr UserData;

        /// <summary>
        ///  Data buffer
        /// </summary>
        public byte* Buffer;

        /// <summary>
        ///  Number of isochronous packets. Only used for I/O with isochronous
        ///  endpoints.
        /// </summary>
        public int NumIsoPackets;

        /// <summary>
        ///  Isochronous packet descriptors, for isochronous transfers only.
        /// </summary>
        public IntPtr IsoPacketDesc;

    }

    public unsafe delegate void TransferDelegate(Transfer* transfer);


    /// <summary>
    ///  libusb_transfer.flags values 
    /// </summary>
    [Flags]
    public enum TransferFlags : byte
    {
        /// <summary>
        ///  Report short frames as errors 
        /// </summary>
        ShortNotOk = 0x1,

        FreeBuffer = 0x2,

        FreeTransfer = 0x4,

        /// <summary>
        ///  Available since libusb-1.0.9.
        /// </summary>
        AddZeroPacket = 0x8,

        None = 0x0,

    }


    /// <summary>
    ///  Transfer status codes 
    /// </summary>
    [Flags]
    public enum TransferStatus : byte
    {
        Completed = 0,

        /// <summary>
        ///  Transfer failed 
        /// </summary>
        Error = 0x1,

        /// <summary>
        ///  Transfer timed out 
        /// </summary>
        TimedOut = 0x2,

        /// <summary>
        ///  Transfer was cancelled 
        /// </summary>
        Cancelled = 0x3,

        Stall = 0x4,

        /// <summary>
        ///  LibUsbDevice was disconnected 
        /// </summary>
        NoDevice = 0x5,

        /// <summary>
        ///  LibUsbDevice sent more data than requested 
        /// </summary>
        Overflow = 0x6,

    }

    [Flags]
    public enum TransferType : byte
    {
        /// <summary>
        ///  Control endpoint 
        /// </summary>
        Control = 0,

        /// <summary>
        ///  Isochronous endpoint 
        /// </summary>
        Isochronous = 0x1,

        /// <summary>
        ///  Bulk endpoint 
        /// </summary>
        Bulk = 0x2,

        /// <summary>
        ///  Interrupt endpoint 
        /// </summary>
        Interrupt = 0x3,

        /// <summary>
        ///  Stream endpoint 
        /// </summary>
        BulkStream = 0x4,

    }




}
