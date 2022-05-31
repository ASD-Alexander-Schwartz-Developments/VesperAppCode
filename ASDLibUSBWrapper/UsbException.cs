using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASDLibUSBWrapper
{
    [Serializable]
    public class UsbException : Exception
    {
        public UsbException()
            { }

        public UsbException(LibUsbError errorCode) : this(GetErrorMessage(errorCode))
        {
            this.ErrorCode = errorCode;
            this.HResult = (int)errorCode;
        }

        public UsbException(string message) : base(message)
        {
        }

        public UsbException(string message, Exception inner)
            : base(message, inner)
        {
        }

        protected UsbException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }

        public LibUsbError ErrorCode
        {
            get;
            private set;
        }

        private static string GetErrorMessage(LibUsbError errorCode)
        {
            IntPtr errorString = NativeImport.StrError(errorCode);

            if (errorString != IntPtr.Zero)
            {
                // From the documentation: 'The caller must not free() the returned string.'
#pragma warning disable CS8603 // Possible null reference return.
                return Marshal.PtrToStringAnsi(errorString);
#pragma warning restore CS8603 // Possible null reference return.
            }
            else
            {
                return $"An unknown error with code {(int)errorCode} has occurred.";
            }
        }
    }



    /// <summary>
    /// Provides extension methods for the <see cref="LibUsbError"/> enumeration.
    /// </summary>
    public static class ErrorExtensions
    {
        /// <summary>
        /// Throws a <see cref="UsbException"/> if the value of <paramref name="error"/> is not <see cref="Error.Success"/>.
        /// </summary>
        /// <param name="error">
        /// The error code based on which to throw an exception.
        /// </param>
        public static void ThrowOnError(this LibUsbError error)
        {
            if (error != LibUsbError.Success)
            {
                throw new UsbException(error);
            }
        }

        /// <summary>
        /// Gets the function's return value (if ret &gt;= 0), or throws an error if the return value was negative
        /// and indicated an error.
        /// </summary>
        /// <param name="error">
        /// The return value to inspect.
        /// </param>
        /// <returns>
        /// The function's return value (if ret &gt;= 0);.
        /// </returns>
        public static int GetValueOrThrow(this LibUsbError error)
        {
            int value = (int)error;

            if (value < 0)
            {
                throw new UsbException(error);
            }
            else
            {
                return value;
            }
        }

        public static LibUsbError ToError(TransferStatus transferStatus)
        {
            switch (transferStatus)
            {
                case TransferStatus.Completed:
                    return LibUsbError.Success;
                case TransferStatus.Error:
                    return LibUsbError.Pipe;
                case TransferStatus.TimedOut:
                    return LibUsbError.Timeout;
                case TransferStatus.Cancelled:
                    return LibUsbError.Io;
                case TransferStatus.Stall:
                    return LibUsbError.Pipe;
                case TransferStatus.NoDevice:
                    return LibUsbError.NoDevice;
                case TransferStatus.Overflow:
                    return LibUsbError.Overflow;
                default:
                    return LibUsbError.Other;
            }
        }
    }
}
