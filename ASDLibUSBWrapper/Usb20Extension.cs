using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASDLibUSBWrapper
{

    /// <summary>
    ///  of the USB 2.0 Extension descriptor.
    /// </summary>
    [Flags]
    public enum Usb20ExtensionAttributes : byte
    {
        /// <summary>
        ///  Supports Link Power Management (LPM) 
        /// </summary>
        BmLpmSupport = 0x2,

    }


    /// <summary>
    ///  A structure representing the USB 2.0 Extension descriptor
    ///  This descriptor is documented in section 9.6.2.1 of the USB 3.0 specification.
    ///  All multiple-byte fields are represented in host-endian format.
    /// </summary>
    [StructLayoutAttribute(LayoutKind.Sequential, Pack = NativeImport.Pack)]
    public struct Usb20ExtensionDescriptor
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
        ///  LIBUSB_BT_USB_2_0_EXTENSION in this context.
        /// </summary>
        public byte DevCapabilityType;

        /// <summary>
        ///  Bitmap encoding of supported device level features.
        ///  A value of one in a bit location indicates a feature is
        ///  supported; a value of zero indicates it is not supported.
        ///  See
        /// </summary>
        public uint Attributes;

    }
}
