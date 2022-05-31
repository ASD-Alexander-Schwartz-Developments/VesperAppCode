using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASDLibUSBWrapper
{
    /// <summary>
    ///  A structure representing the superspeed endpoint companion
    ///  descriptor. This descriptor is documented in section 9.6.7 of
    ///  the USB 3.0 specification. All multiple-byte fields are represented in
    ///  host-endian format.
    /// </summary>
    [StructLayoutAttribute(LayoutKind.Sequential, Pack = NativeImport.Pack)]
    public struct SsEndpointCompanionDescriptor
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
        ///  The maximum number of packets the endpoint can send or
        ///  receive as part of a burst.
        /// </summary>
        public byte MaxBurst;

        /// <summary>
        ///  In bulk EP:	bits 4:0 represents the	maximum	number of
        ///  streams the	EP supports. In	isochronous EP:	bits 1:0
        ///  represents the Mult	- a zero based value that determines
        ///  the	maximum	number of packets within a service interval
        /// </summary>
        public byte Attributes;

        /// <summary>
        ///  The	total number of bytes this EP will transfer every
        ///  service interval. valid only for periodic EPs.
        /// </summary>
        public ushort BytesPerInterval;

    }
}
