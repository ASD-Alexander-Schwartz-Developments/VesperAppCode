using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASDLibUSBWrapper
{
    /// <summary>
    ///  A structure representing the standard USB device descriptor. This
    ///  descriptor is documented in section 9.6.1 of the USB 3.0 specification.
    ///  All multiple-byte fields are represented in host-endian format.
    /// </summary>
    [StructLayoutAttribute(LayoutKind.Sequential, Pack = NativeImport.Pack)]
    public struct DeviceDescriptor
    {
        /// <summary>
        ///  Size of this descriptor (in bytes)
        /// </summary>
        public byte Length;

        /// <summary>
        ///  Descriptor type. Will have value
        ///  context.
        /// </summary>
        public byte DescriptorType;

        /// <summary>
        ///  USB specification release number in binary-coded decimal. A value of
        ///  0x0200 indicates USB 2.0, 0x0110 indicates USB 1.1, etc.
        /// </summary>
        public ushort USB;

        /// <summary>
        ///  USB-IF class code for the device. See
        /// </summary>
        public byte DeviceClass;

        /// <summary>
        ///  USB-IF subclass code for the device, qualified by the bDeviceClass
        ///  value
        /// </summary>
        public byte DeviceSubClass;

        /// <summary>
        ///  USB-IF protocol code for the device, qualified by the bDeviceClass and
        ///  bDeviceSubClass values
        /// </summary>
        public byte DeviceProtocol;

        /// <summary>
        ///  Maximum packet size for endpoint 0
        /// </summary>
        public byte MaxPacketSize0;

        /// <summary>
        ///  USB-IF vendor ID
        /// </summary>
        public ushort IdVendor;

        /// <summary>
        ///  USB-IF product ID
        /// </summary>
        public ushort IdProduct;

        /// <summary>
        ///  LibUsbDevice release number in binary-coded decimal
        /// </summary>
        public ushort Device;

        /// <summary>
        ///  Index of string descriptor describing manufacturer
        /// </summary>
        public byte Manufacturer;

        /// <summary>
        ///  Index of string descriptor describing product
        /// </summary>
        public byte Product;

        /// <summary>
        ///  Index of string descriptor containing device serial number
        /// </summary>
        public byte SerialNumber;

        /// <summary>
        ///  Number of possible configurations
        /// </summary>
        public byte NumConfigurations;

    }
}
