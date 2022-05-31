using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASDLibUSBWrapper
{
    /// <summary>
    ///  A structure representing the Container ID descriptor.
    ///  This descriptor is documented in section 9.6.2.3 of the USB 3.0 specification.
    ///  All multiple-byte fields, except UUIDs, are represented in host-endian format.
    /// </summary>
    [StructLayoutAttribute(LayoutKind.Sequential, Pack = NativeImport.Pack)]
    public unsafe struct ContainerIdDescriptor
    {
        /// <summary>
        ///  Size of this descriptor (in bytes)
        /// </summary>
        public byte Length;

        /// <summary>
        ///  Descriptor type. Will have value
        ///  LIBUSB_DT_DEVICE_CAPABILITY in this context.
        /// </summary>
        public byte DescriptorType;

        /// <summary>
        ///  Capability type. Will have value
        ///  LIBUSB_BT_CONTAINER_ID in this context.
        /// </summary>
        public byte DevCapabilityType;

        /// <summary>
        ///  Reserved field
        /// </summary>
        public byte Reserved;

        /// <summary>
        ///  128 bit UUID
        /// </summary>
        public fixed char ContainerID[16];

    }
}
