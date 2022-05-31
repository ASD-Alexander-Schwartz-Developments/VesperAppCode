using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASDLibUSBWrapper
{

    /// <summary>
    ///  USB capability types
    /// </summary>
    [Flags]
    public enum BosType : byte
    {
        /// <summary>
        ///  Wireless USB device capability 
        /// </summary>
        WirelessUsbDeviceCapability = 0x1,

        /// <summary>
        ///  USB 2.0 extensions 
        /// </summary>
        Usb20Extension = 0x2,

        /// <summary>
        ///  SuperSpeed USB device capability 
        /// </summary>
        SsUsbDeviceCapability = 0x3,

        /// <summary>
        ///  Container ID type 
        /// </summary>
        ContainerId = 0x4,

    }



    /// <summary>
    ///  A structure representing the Binary LibUsbDevice Object Store (BOS) descriptor.
    ///  This descriptor is documented in section 9.6.2 of the USB 3.0 specification.
    ///  All multiple-byte fields are represented in host-endian format.
    /// </summary>
    [StructLayoutAttribute(LayoutKind.Sequential, Pack = NativeImport.Pack)]
    public struct BosDescriptor
    {
        /// <summary>
        ///  Size of this descriptor (in bytes)
        /// </summary>
        public byte Length;

        /// <summary>
        ///  Descriptor type. Will have value
        ///  in this context.
        /// </summary>
        public byte DescriptorType;

        /// <summary>
        ///  Length of this descriptor and all of its sub descriptors
        /// </summary>
        public ushort TotalLength;

        /// <summary>
        ///  The number of separate device capability descriptors in
        ///  the BOS
        /// </summary>
        public byte NumDeviceCaps;

        /// <summary>
        ///  bNumDeviceCap LibUsbDevice Capability Descriptors
        /// </summary>
        public IntPtr DevCapability;

    }


    /// <summary>
    ///  A generic representation of a BOS LibUsbDevice Capability descriptor. It is
    ///  advised to check bDevCapabilityType and call the matching
    ///  libusb_get_*_descriptor function to get a structure fully matching the type.
    /// </summary>
    [StructLayoutAttribute(LayoutKind.Sequential, Pack = NativeImport.Pack)]
    public struct BosDevCapabilityDescriptor
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
        ///  LibUsbDevice Capability type
        /// </summary>
        public byte DevCapabilityType;

        /// <summary>
        ///  LibUsbDevice Capability data (bLength - 3 bytes)
        /// </summary>
        public IntPtr DevCapabilityData;

    }




}
