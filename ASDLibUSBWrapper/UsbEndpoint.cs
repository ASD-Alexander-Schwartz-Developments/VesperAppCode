using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASDLibUSBWrapper
{
    /// <summary>
    /// Endpoint members common to Read, Write, Bulk, and Interrupt <see cref="T:LibUsbDotNet.Main.EndpointType"/>.
    /// </summary>
    public abstract class UsbEndpointBase
    {
        private readonly byte mEpNum;
        private readonly IUsbDevice mUsbDevice;
        private readonly byte alternateInterfaceID;
        private UsbEndpointInfo mUsbEndpointInfo;
        private EndpointType mEndpointType;
        private UsbInterfaceInfo mUsbInterfacetInfo;

        internal UsbEndpointBase(IUsbDevice usbDevice, byte alternateInterfaceID, byte epNum, EndpointType endpointType)
        {
            this.mUsbDevice = usbDevice;
            this.alternateInterfaceID = alternateInterfaceID;
            this.mEpNum = epNum;
            this.mEndpointType = endpointType;
        }

        /// <summary>
        /// Gets the <see cref="UsbDevice"/> class this endpoint belongs to.
        /// </summary>
        public IUsbDevice Device
        {
            get { return this.mUsbDevice; }
        }

        /// <summary>
        /// Gets the endpoint ID for this <see cref="UsbEndpointBase"/> class.
        /// </summary>
        public byte EpNum
        {
            get
            {
                return this.mEpNum;
            }
        }

        /// <summary>
        /// Gets the <see cref="EndpointType"/> for this endpoint.
        /// </summary>
        public EndpointType Type
        {
            get { return this.mEndpointType; }
        }

        /// <summary>
        /// Gets the <see cref="UsbEndpointInfo"/> descriptor for this endpoint.
        /// </summary>
        public UsbEndpointInfo EndpointInfo
        {
            get
            {
                if (ReferenceEquals(this.mUsbEndpointInfo, null))
                {
                    if (!LookupEndpointInfo(this.Device.Configs[0], this.alternateInterfaceID, this.mEpNum, out this.mUsbInterfacetInfo, out this.mUsbEndpointInfo))
                    {
                        // throw new UsbException(this, String.Format("Failed locating endpoint {0} for the current usb configuration.", mEpNum));
                        return null;
                    }
                }

                return this.mUsbEndpointInfo;
            }
        }

        /// <summary>
        /// Synchronous bulk/interrupt transfer function.
        /// </summary>
        /// <param name="buffer">An <see cref="IntPtr"/> to a caller-allocated buffer.</param>
        /// <param name="offset">Position in buffer that transferring begins.</param>
        /// <param name="length">Number of bytes, starting from thr offset parameter to transfer.</param>
        /// <param name="timeout">Maximum time to wait for the transfer to complete.</param>
        /// <param name="transferLength">Number of bytes actually transferred.</param>
        /// <returns>True on success.</returns>
        public virtual unsafe LibUsbError Transfer(IntPtr buffer, int offset, int length, int timeout, out int transferLength)
        {
            int transferred = 0;
            LibUsbError returnValue = 0;

            switch (this.mEndpointType)
            {
                case EndpointType.Bulk:
                    returnValue = NativeImport.BulkTransfer(this.Device.DeviceHandle, this.mEpNum, (byte*)buffer + offset, length, ref transferred, (uint)timeout);
                    transferLength = transferred;
                    return returnValue;

                case EndpointType.Interrupt:
                    returnValue = NativeImport.InterruptTransfer(this.Device.DeviceHandle, this.mEpNum, (byte*)buffer + offset, length, ref transferred, (uint)timeout);
                    transferLength = transferred;
                    return returnValue;

                case EndpointType.Isochronous:
                case EndpointType.Control:
                default:
                    return AsyncTransfer.TransferAsync(this.Device.DeviceHandle, this.mEpNum, this.mEndpointType, buffer, offset, length, timeout, out transferLength);
            }
        }

        /// <summary>
        /// Looks up endpoint/interface information in a configuration.
        /// </summary>
        /// <param name="currentConfigInfo">The config to seach.</param>
        /// <param name="altInterfaceID">Alternate interface id the endpoint exists in, or -1 for any alternate interface id.</param>
        /// <param name="endpointAddress">The endpoint address to look for.</param>
        /// <param name="usbInterfaceInfo">On success, the <see cref="UsbInterfaceInfo"/> class for this endpoint.</param>
        /// <param name="usbEndpointInfo">On success, the <see cref="UsbEndpointInfo"/> class for this endpoint.</param>
        /// <returns>True of the endpoint was found, otherwise false.</returns>
        public static bool LookupEndpointInfo(UsbConfigInfo currentConfigInfo, int altInterfaceID, byte endpointAddress, out UsbInterfaceInfo usbInterfaceInfo, out UsbEndpointInfo usbEndpointInfo)
        {
            bool found = false;

            usbInterfaceInfo = null;
            usbEndpointInfo = null;
            foreach (UsbInterfaceInfo interfaceInfo in currentConfigInfo.Interfaces)
            {
                if (altInterfaceID == -1 || altInterfaceID == interfaceInfo.AlternateSetting)
                {
                    foreach (UsbEndpointInfo endpointInfo in interfaceInfo.Endpoints)
                    {
                        if ((endpointAddress & UsbConstants.EndpointNumberMask) == 0)
                        {
                            // find first read/write endpoint
                            if ((endpointAddress & UsbConstants.EndpointDirectionMask) == 0 &&
                                (endpointInfo.EndpointAddress & UsbConstants.EndpointDirectionMask) == 0)
                            {
                                // first write endpoint
                                found = true;
                            }

                            if ((endpointAddress & UsbConstants.EndpointDirectionMask) != 0 &&
                                (endpointInfo.EndpointAddress & UsbConstants.EndpointDirectionMask) != 0)
                            {
                                // first read endpoint
                                found = true;
                            }
                        }
                        else if (endpointInfo.EndpointAddress == endpointAddress)
                        {
                            found = true;
                        }

                        if (found)
                        {
                            usbInterfaceInfo = interfaceInfo;
                            usbEndpointInfo = endpointInfo;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Looks up endpoint/interface information in a configuration.
        /// </summary>
        /// <param name="currentConfigInfo">The config to seach.</param>
        /// <param name="endpointAddress">The endpoint address to look for.</param>
        /// <param name="usbInterfaceInfo">On success, the <see cref="UsbInterfaceInfo"/> class for this endpoint.</param>
        /// <param name="usbEndpointInfo">On success, the <see cref="UsbEndpointInfo"/> class for this endpoint.</param>
        /// <returns>True of the endpoint was found, otherwise false.</returns>
        public static bool LookupEndpointInfo(UsbConfigInfo currentConfigInfo, byte endpointAddress, out UsbInterfaceInfo usbInterfaceInfo, out UsbEndpointInfo usbEndpointInfo)
        {
            return LookupEndpointInfo(currentConfigInfo, -1, endpointAddress, out usbInterfaceInfo, out usbEndpointInfo);
        }

        /// <summary>
        /// Synchronous bulk/interrupt transfer function.
        /// </summary>
        /// <param name="buffer">A caller-allocated buffer for the transfer data. This object is pinned using <see cref="PinnedHandle"/>.</param>
        /// <param name="offset">Position in buffer that transferring begins.</param>
        /// <param name="length">Number of bytes, starting from thr offset parameter to transfer.</param>
        /// <param name="timeout">Maximum time to wait for the transfer to complete.</param>
        /// <param name="transferLength">Number of bytes actually transferred.</param>
        /// <returns>True on success.</returns>
        public LibUsbError Transfer(object buffer, int offset, int length, int timeout, out int transferLength)
        {
            PointerHandle pinned = new PointerHandle(buffer);
            LibUsbError eReturn = this.Transfer(pinned.Handle, offset, length, timeout, out transferLength);
            pinned.Dispose();
            return eReturn;
        }

        #region Nested type: TransferDelegate

        internal delegate int TransferDelegate(IntPtr pBuffer, int bufferLength, out int lengthTransferred, int isoPacketSize, IntPtr pOverlapped);

        #endregion
    }




    /// <summary>
    /// Contains methods for retrieving data from a <see cref="EndpointType.Bulk"/> or <see cref="EndpointType.Interrupt"/> endpoint using the overloaded <see cref="Read(byte[],int,out int)"/> functions or a <see cref="DataReceived"/> event.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Before using the <see cref="DataReceived"/> event, the <see cref="DataReceivedEnabled"/> property must be set to true.</item>
    /// <item>While the <see cref="DataReceivedEnabled"/> property is True, the overloaded <see cref="Read(byte[],int,out int)"/> functions cannot be used.</item>
    /// </list>
    /// </remarks>
    public class UsbEndpointReader : UsbEndpointBase
    {
        private int mReadBufferSize;

        public UsbEndpointReader(IUsbDevice usbDevice, int readBufferSize, byte alternateInterfaceID, ReadEndpointID readEndpointID, EndpointType endpointType)
            : base(usbDevice, alternateInterfaceID, (byte)readEndpointID, endpointType)
        {
            this.mReadBufferSize = readBufferSize;
        }

        /// <summary>
        /// Default read buffer size when using the <see cref="DataReceived"/> event.
        /// </summary>
        /// <remarks>
        /// This value can be bypassed using the second parameter of the <see cref="UsbDevice.OpenEndpointReader(LibUsbDotNet.Main.ReadEndpointID,int)"/> method.
        /// The default is 4096.
        /// </remarks>
        public static int DefReadBufferSize { get; set; } = 4096;

        /// <summary>
        /// Reads data from the current <see cref="UsbEndpointReader"/>.
        /// </summary>
        /// <param name="buffer">The buffer to store the recieved data in.</param>
        /// <param name="timeout">Maximum time to wait for the transfer to complete.  If the transfer times out, the IO operation will be cancelled.</param>
        /// <param name="transferLength">Number of bytes actually transferred.</param>
        /// <returns>
        /// <see cref="LibUsbError"/>.<see cref="Error.None"/> on success.
        /// </returns>
        public virtual LibUsbError Read(byte[] buffer, int timeout, out int transferLength)
        {
            return this.Read(buffer, 0, buffer.Length, timeout, out transferLength);
        }

        /// <summary>
        /// Reads data from the current <see cref="UsbEndpointReader"/>.
        /// </summary>
        /// <param name="buffer">The buffer to store the recieved data in.</param>
        /// <param name="offset">The position in buffer to start storing the data.</param>
        /// <param name="count">The maximum number of bytes to receive.</param>
        /// <param name="timeout">Maximum time to wait for the transfer to complete.  If the transfer times out, the IO operation will be cancelled.</param>
        /// <param name="transferLength">Number of bytes actually transferred.</param>
        /// <returns>
        /// <see cref="LibUsbError"/>.<see cref="Error.None"/> on success.
        /// </returns>
        public virtual LibUsbError Read(IntPtr buffer, int offset, int count, int timeout, out int transferLength)
        {
            return this.Transfer(buffer, offset, count, timeout, out transferLength);
        }

        /// <summary>
        /// Reads data from the current <see cref="UsbEndpointReader"/>.
        /// </summary>
        /// <param name="buffer">The buffer to store the recieved data in.</param>
        /// <param name="offset">The position in buffer to start storing the data.</param>
        /// <param name="count">The maximum number of bytes to receive.</param>
        /// <param name="timeout">Maximum time to wait for the transfer to complete.  If the transfer times out, the IO operation will be cancelled.</param>
        /// <param name="transferLength">Number of bytes actually transferred.</param>
        /// <returns>
        /// <see cref="LibUsbError"/>.<see cref="Error.None"/> on success.
        /// </returns>
        public virtual LibUsbError Read(byte[] buffer, int offset, int count, int timeout, out int transferLength)
        {
            return this.Transfer(buffer, offset, count, timeout, out transferLength);
        }

        /// <summary>
        /// Reads data from the current <see cref="UsbEndpointReader"/>.
        /// </summary>
        /// <param name="buffer">The buffer to store the recieved data in.</param>
        /// <param name="offset">The position in buffer to start storing the data.</param>
        /// <param name="count">The maximum number of bytes to receive.</param>
        /// <param name="timeout">Maximum time to wait for the transfer to complete.  If the transfer times out, the IO operation will be cancelled.</param>
        /// <param name="transferLength">Number of bytes actually transferred.</param>
        /// <returns>
        /// <see cref="LibUsbError"/>.<see cref="Error.None"/> on success.
        /// </returns>
        public virtual LibUsbError Read(object buffer, int offset, int count, int timeout, out int transferLength)
        {
            return this.Transfer(buffer, offset, count, timeout, out transferLength);
        }

        /// <summary>
        /// Reads data from the current <see cref="UsbEndpointReader"/>.
        /// </summary>
        /// <param name="buffer">The buffer to store the recieved data in.</param>
        /// <param name="timeout">Maximum time to wait for the transfer to complete.  If the transfer times out, the IO operation will be cancelled.</param>
        /// <param name="transferLength">Number of bytes actually transferred.</param>
        /// <returns>
        /// <see cref="LibUsbError"/>.<see cref="Error.None"/> on success.
        /// </returns>
        public virtual LibUsbError Read(object buffer, int timeout, out int transferLength)
        {
            return this.Transfer(buffer, 0, Marshal.SizeOf(buffer), timeout, out transferLength);
        }

        /// <summary>
        /// Reads/discards data from the enpoint until no more data is available.
        /// </summary>
        /// <returns>Alwats returns <see cref="LibUsbError.None"/> </returns>
        public virtual LibUsbError ReadFlush()
        {
            byte[] bufDummy = new byte[64];
            int iTransferred;
            int iBufCount = 0;
            while (this.Read(bufDummy, 10, out iTransferred) == LibUsbError.Success && iBufCount < 128)
            {
                iBufCount++;
            }

            return LibUsbError.Success;
        }
    }




    /// <summary>Contains methods for writing data to a <see cref="EndpointType.Bulk"/> or <see cref="EndpointType.Interrupt"/> endpoint using the overloaded <see cref="Write(byte[],int,out int)"/> functions.
    /// </summary>
    public class UsbEndpointWriter : UsbEndpointBase
    {
        public UsbEndpointWriter(IUsbDevice usbDevice, byte alternateInterfaceID, WriteEndpointID writeEndpointID, EndpointType endpointType)
            : base(usbDevice, alternateInterfaceID, (byte)writeEndpointID, endpointType)
        {
        }

        /// <summary>
        /// Writes data to the current <see cref="UsbEndpointWriter"/>.
        /// </summary>
        /// <param name="buffer">The buffer storing the data to write.</param>
        /// <param name="timeout">Maximum time to wait for the transfer to complete.  If the transfer times out, the IO operation will be cancelled.</param>
        /// <param name="transferLength">Number of bytes actually transferred.</param>
        /// <returns>
        /// <see cref="LibUsbError"/>.<see cref="Error.None"/> on success.
        /// </returns>
        public virtual LibUsbError Write(byte[] buffer, int timeout, out int transferLength)
        {
            return this.Write(buffer, 0, buffer.Length, timeout, out transferLength);
        }

        /// <summary>
        /// Writes data to the current <see cref="UsbEndpointWriter"/>.
        /// </summary>
        /// <param name="pBuffer">The buffer storing the data to write.</param>
        /// <param name="offset">The position in buffer to start writing the data from.</param>
        /// <param name="count">The number of bytes to write.</param>
        /// <param name="timeout">Maximum time to wait for the transfer to complete.  If the transfer times out, the IO operation will be cancelled.</param>
        /// <param name="transferLength">Number of bytes actually transferred.</param>
        /// <returns>
        /// <see cref="LibUsbError"/>.<see cref="Error.None"/> on success.
        /// </returns>
        public virtual LibUsbError Write(IntPtr pBuffer, int offset, int count, int timeout, out int transferLength)
        {
            return this.Transfer(pBuffer, offset, count, timeout, out transferLength);
        }

        /// <summary>
        /// Writes data to the current <see cref="UsbEndpointWriter"/>.
        /// </summary>
        /// <param name="buffer">The buffer storing the data to write.</param>
        /// <param name="offset">The position in buffer to start writing the data from.</param>
        /// <param name="count">The number of bytes to write.</param>
        /// <param name="timeout">Maximum time to wait for the transfer to complete.  If the transfer times out, the IO operation will be cancelled.</param>
        /// <param name="transferLength">Number of bytes actually transferred.</param>
        /// <returns>
        /// <see cref="LibUsbError"/>.<see cref="Error.None"/> on success.
        /// </returns>
        public virtual LibUsbError Write(byte[] buffer, int offset, int count, int timeout, out int transferLength)
        {
            return this.Transfer(buffer, offset, count, timeout, out transferLength);
        }

        /// <summary>
        /// Writes data to the current <see cref="UsbEndpointWriter"/>.
        /// </summary>
        /// <param name="buffer">The buffer storing the data to write.</param>
        /// <param name="offset">The position in buffer to start writing the data from.</param>
        /// <param name="count">The number of bytes to write.</param>
        /// <param name="timeout">Maximum time to wait for the transfer to complete.  If the transfer times out, the IO operation will be cancelled.</param>
        /// <param name="transferLength">Number of bytes actually transferred.</param>
        /// <returns>
        /// <see cref="LibUsbError"/>.<see cref="Error.None"/> on success.
        /// </returns>
        public virtual LibUsbError Write(object buffer, int offset, int count, int timeout, out int transferLength)
        {
            return this.Transfer(buffer, offset, count, timeout, out transferLength);
        }

        /// <summary>
        /// Writes data to the current <see cref="UsbEndpointWriter"/>.
        /// </summary>
        /// <param name="buffer">The buffer storing the data to write.</param>
        /// <param name="timeout">Maximum time to wait for the transfer to complete.  If the transfer times out, the IO operation will be cancelled.</param>
        /// <param name="transferLength">Number of bytes actually transferred.</param>
        /// <returns>
        /// <see cref="LibUsbError"/>.<see cref="Error.None"/> on success.
        /// </returns>
        public virtual LibUsbError Write(object buffer, int timeout, out int transferLength)
        {
            return this.Write(buffer, 0, Marshal.SizeOf(buffer), timeout, out transferLength);
        }
    }

}
