using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASDLibUSBWrapper
{
    /// <summary>
    /// Unix Mono.net timeval structure.
    /// </summary>

    [StructLayout(LayoutKind.Sequential)]

    public struct UnixTimeval
    {
        private IntPtr mTvSecInternal;
        private IntPtr mTvUSecInternal;

        /// <summary>
        /// Default <see cref="UnixTimeval"/>.
        /// </summary>
        public static UnixTimeval Default
        {
            get { return new UnixTimeval(2, 0); }
        }

        /// <summary>
        /// Timeval seconds property.
        /// </summary>
        public long tv_sec
        {
            get { return this.mTvSecInternal.ToInt64(); }
            set { this.mTvSecInternal = new IntPtr(value); }
        }

        /// <summary>
        /// Timeval milliseconds property.
        /// </summary>
        public long tv_usec
        {
            get { return this.mTvUSecInternal.ToInt64(); }
            set { this.mTvUSecInternal = new IntPtr(value); }
        }

        /// <summary>
        /// Timeval constructor.
        /// </summary>
        /// <param name="tvSec">seconds</param>
        /// <param name="tvUsec">milliseconds</param>
        public UnixTimeval(long tvSec, long tvUsec)
        {
            this.mTvSecInternal = new IntPtr(tvSec);
            this.mTvUSecInternal = new IntPtr(tvUsec);
        }

    }
}
