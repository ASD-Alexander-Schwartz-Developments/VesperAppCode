using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASDLibUSBWrapper
{
    /// <summary>
    ///  File descriptor for polling
    /// </summary>
    [StructLayoutAttribute(LayoutKind.Sequential, Pack = NativeImport.Pack)]
    public struct PollFileDescriptor
    {
        /// <summary>
        ///  Numeric file descriptor
        /// </summary>
        public int Fd;

        /// <summary>
        ///  Event flags to poll for from
        ///  <poll
        ///  .h>. POLLIN indicates that you
        ///  should monitor this file descriptor for becoming ready to read from,
        ///  and POLLOUT indicates that you should monitor this file descriptor for
        ///  nonblocking write readiness.
        /// </summary>
        public short Events;

    }

    public unsafe delegate void PollFileDescriptorAddedDelegate(int fd, short events, IntPtr userData);
    public unsafe delegate void PollFileDescriptorRemovedDelegate(int fd, IntPtr userData);
}
