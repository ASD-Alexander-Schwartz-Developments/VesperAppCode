namespace ASDLibUSBWrapper
{
    public partial class LibUsbDeviceHandle : Microsoft.Win32.SafeHandles.SafeHandleZeroOrMinusOneIsInvalid
    {
        private string creationStackTrace;

        /// <summary>
        /// Initializes a new instance of the <see cref="LibUsbDeviceHandle"/> class.
        /// </summary>
        protected LibUsbDeviceHandle() :
                base(true)
        {
            this.creationStackTrace = Environment.StackTrace;
        }


        /// <inheritdoc/>
        protected override bool ReleaseHandle()
        {
            NativeImport.Close(this.handle);
            return true;
        }
        

        /// <summary>
        /// Initializes a new instance of the <see cref="LibUsbDeviceHandle"/> class, specifying whether the handle is to be reliably released.
        /// </summary>
        /// <param name="ownsHandle">
        /// <see langword="true"/> to reliably release the handle during the finalization phase; <see langword="false"/> to prevent reliable release (not recommended).
        /// </param>
        protected LibUsbDeviceHandle(bool ownsHandle) :
                base(ownsHandle)
        {
            this.creationStackTrace = Environment.StackTrace;
        }

        /// <summary>
        /// Gets a value which represents a pointer or handle that has been initialized to zero.
        /// </summary>
        public static LibUsbDeviceHandle Zero
        {
            get
            {
                return LibUsbDeviceHandle.DangerousCreate(IntPtr.Zero);
            }
        }

        /// <summary>
        /// Creates a new <see cref="LibUsbDeviceHandle"/> from a <see cref="IntPtr"/>.
        /// </summary>
        /// <param name="unsafeHandle">
        /// The underlying <see cref="IntPtr"/>
        /// </param>
        /// <param name="ownsHandle">
        /// <see langword="true"/> to reliably release the handle during the finalization phase; <see langword="false"/> to prevent reliable release (not recommended).
        /// </param>
        /// <returns>
        /// </returns>
        public static LibUsbDeviceHandle DangerousCreate(IntPtr unsafeHandle, bool ownsHandle)
        {
            LibUsbDeviceHandle safeHandle = new LibUsbDeviceHandle(ownsHandle);
            safeHandle.SetHandle(unsafeHandle);
            return safeHandle;
        }

        /// <summary>
        /// Creates a new <see cref="LibUsbDeviceHandle"/> from a <see cref="IntPtr"/>.
        /// </summary>
        /// <param name="unsafeHandle">
        /// The underlying <see cref="IntPtr"/>
        /// </param>
        /// <returns>
        /// </returns>
        public static LibUsbDeviceHandle DangerousCreate(IntPtr unsafeHandle)
        {
            return LibUsbDeviceHandle.DangerousCreate(unsafeHandle, true);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return string.Format("{0} ({1})", this.handle, "LibUsbDeviceHandle");
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            if (obj != null && obj.GetType() == typeof(LibUsbDeviceHandle))
            {
                return ((LibUsbDeviceHandle)obj).handle.Equals(this.handle);
            }
            else
            {
                return false;
            }
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return this.handle.GetHashCode();
        }

        /// <summary>
        /// Determines whether two specified instances of <see cref="LibUsbDeviceHandle"/> are equal.
        /// </summary>
        /// <param name="value1">
        /// The first pointer or handle to compare.
        /// </param>
        /// <param name="value2">
        /// The second pointer or handle to compare.
        /// </param>
        /// <returns>
        /// <see langword="true"/> if <paramref name="value1"/> equals <paramref name="value2"/>; otherwise, <see langword="false"/>.
        /// </returns>
        public static bool operator == (LibUsbDeviceHandle value1, LibUsbDeviceHandle value2)
        {
            if (object.Equals(value1, null) && object.Equals(value2, null))
            {
                return true;
            }

            if (object.Equals(value1, null) || object.Equals(value2, null))
            {
                return false;
            }

            return value1.handle == value2.handle;
        }

        /// <summary>
        /// Determines whether two specified instances of <see cref="LibUsbDeviceHandle"/> are not equal.
        /// </summary>
        /// <param name="value1">
        /// The first pointer or handle to compare.
        /// </param>
        /// <param name="value2">
        /// The second pointer or handle to compare.
        /// </param>
        /// <returns>
        /// <see langword="true"/> if <paramref name="value1"/> does not equal <paramref name="value2"/>; otherwise, <see langword="false"/>.
        /// </returns>
        public static bool operator != (LibUsbDeviceHandle value1, LibUsbDeviceHandle value2)
        {
            if (object.Equals(value1, null) && object.Equals(value2, null))
            {
                return false;
            }

            if (object.Equals(value1, null) || object.Equals(value2, null))
            {
                return true;
            }

            return value1.handle != value2.handle;
        }
    }
}
