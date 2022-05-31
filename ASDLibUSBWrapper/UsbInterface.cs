using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASDLibUSBWrapper
{

    /// <summary>
    ///  A structure representing the standard USB interface descriptor. This
    ///  descriptor is documented in section 9.6.5 of the USB 3.0 specification.
    ///  All multiple-byte fields are represented in host-endian format.
    /// </summary>
    [StructLayoutAttribute(LayoutKind.Sequential, Pack = NativeImport.Pack)]
    public unsafe struct InterfaceDescriptor
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
        ///  Number of this interface
        /// </summary>
        public byte InterfaceNumber;

        /// <summary>
        ///  Value used to select this alternate setting for this interface
        /// </summary>
        public byte AlternateSetting;

        /// <summary>
        ///  Number of endpoints used by this interface (excluding the control
        ///  endpoint).
        /// </summary>
        public byte NumEndpoints;

        /// <summary>
        ///  USB-IF class code for this interface. See
        /// </summary>
        public byte InterfaceClass;

        /// <summary>
        ///  USB-IF subclass code for this interface, qualified by the
        ///  bInterfaceClass value
        /// </summary>
        public byte InterfaceSubClass;

        /// <summary>
        ///  USB-IF protocol code for this interface, qualified by the
        ///  bInterfaceClass and bInterfaceSubClass values
        /// </summary>
        public byte InterfaceProtocol;

        /// <summary>
        ///  Index of string descriptor describing this interface
        /// </summary>
        public byte Interface;

        /// <summary>
        ///  Array of endpoint descriptors. This length of this array is determined
        ///  by the bNumEndpoints field.
        /// </summary>
        public EndpointDescriptor* Endpoint;

        /// <summary>
        ///  Extra descriptors. If libusb encounters unknown interface descriptors,
        ///  it will store them here, should you wish to parse them.
        /// </summary>
        public byte* Extra;

        /// <summary>
        ///  Length of the extra descriptors, in bytes.
        /// </summary>
        public int ExtraLength;

    }

    /// <summary>
    ///  A collection of alternate settings for a particular USB interface.
    /// </summary>
    [StructLayoutAttribute(LayoutKind.Sequential, Pack = NativeImport.Pack)]
    public unsafe struct UsbInterface
    {
        /// <summary>
        ///  Array of interface descriptors. The length of this array is determined
        ///  by the num_altsetting field.
        /// </summary>
        public InterfaceDescriptor* Altsetting;

        /// <summary>
        ///  The number of alternate settings that belong to this interface
        /// </summary>
        public int NumAltsetting;

    }



}
