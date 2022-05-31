using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASDLibUSBWrapper
{
    /// <summary>
    ///  A structure representing the standard USB endpoint descriptor. This
    ///  descriptor is documented in section 9.6.6 of the USB 3.0 specification.
    ///  All multiple-byte fields are represented in host-endian format.
    /// </summary>
    [StructLayoutAttribute(LayoutKind.Sequential, Pack = NativeImport.Pack)]
    public unsafe struct EndpointDescriptor
    {
        /// <summary>
        ///  Size of this descriptor (in bytes)
        /// </summary>
        public byte Length;

        /// <summary>
        ///  Descriptor type. Will have value
        ///  this context.
        /// </summary>
        public byte DescriptorType;

        /// <summary>
        ///  The address of the endpoint described by this descriptor. Bits 0:3 are
        ///  the endpoint number. Bits 4:6 are reserved. Bit 7 indicates direction,
        ///  see
        /// </summary>
        public byte EndpointAddress;

        /// <summary>
        ///  Attributes which apply to the endpoint when it is configured using
        ///  the bConfigurationValue. Bits 0:1 determine the transfer type and
        ///  correspond to
        ///  isochronous endpoints and correspond to
        ///  Bits 4:5 are also only used for isochronous endpoints and correspond to
        /// </summary>
        public byte Attributes;

        /// <summary>
        ///  Maximum packet size this endpoint is capable of sending/receiving.
        /// </summary>
        public ushort MaxPacketSize;

        /// <summary>
        ///  Interval for polling endpoint for data transfers.
        /// </summary>
        public byte Interval;

        /// <summary>
        ///  For audio devices only: the rate at which synchronization feedback
        ///  is provided.
        /// </summary>
        public byte Refresh;

        /// <summary>
        ///  For audio devices only: the address if the synch endpoint
        /// </summary>
        public byte SynchAddress;

        /// <summary>
        ///  Extra descriptors. If libusb encounters unknown endpoint descriptors,
        ///  it will store them here, should you wish to parse them.
        /// </summary>
        public byte* Extra;

        /// <summary>
        ///  Length of the extra descriptors, in bytes.
        /// </summary>
        public int ExtraLength;

    }
}
