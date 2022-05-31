using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASDLibUSBWrapper
{

    
    public partial class LibUsbContext : Microsoft.Win32.SafeHandles.SafeHandleZeroOrMinusOneIsInvalid
    {
        private string creationStackTrace;

        /// <summary>
        /// Initializes a new instance of the <see cref="LibUsbContext"/> class.
        /// </summary>
        protected LibUsbContext() :
                base(true)
        {
            this.creationStackTrace = Environment.StackTrace;
        }


        /// <inheritdoc/>
        protected override bool ReleaseHandle()
        {
            NativeImport.Exit(this.handle);
            return true;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LibUsbContext"/> class, specifying whether the handle is to be reliably released.
        /// </summary>
        /// <param name="ownsHandle">
        /// <see langword="true"/> to reliably release the handle during the finalization phase; <see langword="false"/> to prevent reliable release (not recommended).
        /// </param>
        protected LibUsbContext(bool ownsHandle) :
                base(ownsHandle)
        {
            this.creationStackTrace = Environment.StackTrace;
        }

        /// <summary>
        /// Gets a value which represents a pointer or handle that has been initialized to zero.
        /// </summary>
        public static LibUsbContext Zero
        {
            get
            {
                return LibUsbContext.DangerousCreate(IntPtr.Zero);
            }
        }

        /// <summary>
        /// Creates a new <see cref="LibUsbContext"/> from a <see cref="IntPtr"/>.
        /// </summary>
        /// <param name="unsafeHandle">
        /// The underlying <see cref="IntPtr"/>
        /// </param>
        /// <param name="ownsHandle">
        /// <see langword="true"/> to reliably release the handle during the finalization phase; <see langword="false"/> to prevent reliable release (not recommended).
        /// </param>
        /// <returns>
        /// </returns>
        public static LibUsbContext DangerousCreate(IntPtr unsafeHandle, bool ownsHandle)
        {
            LibUsbContext safeHandle = new LibUsbContext(ownsHandle);
            safeHandle.SetHandle(unsafeHandle);
            return safeHandle;
        }

        /// <summary>
        /// Creates a new <see cref="LibUsbContext"/> from a <see cref="IntPtr"/>.
        /// </summary>
        /// <param name="unsafeHandle">
        /// The underlying <see cref="IntPtr"/>
        /// </param>
        /// <returns>
        /// </returns>
        public static LibUsbContext DangerousCreate(IntPtr unsafeHandle)
        {
            return LibUsbContext.DangerousCreate(unsafeHandle, true);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return string.Format("{0} ({1})", this.handle, "LibUsbContext");
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            if (obj != null && obj.GetType() == typeof(LibUsbContext))
            {
                return ((LibUsbContext)obj).handle.Equals(this.handle);
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
        /// Determines whether two specified instances of <see cref="LibUsbContext"/> are equal.
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
        public static bool operator ==(LibUsbContext value1, LibUsbContext value2)
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
        /// Determines whether two specified instances of <see cref="LibUsbContext"/> are not equal.
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
        public static bool operator !=(LibUsbContext value1, LibUsbContext value2)
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
