using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASDLibUSBWrapper
{
    /// <summary>
    ///  A structure representing the standard USB configuration descriptor. This
    ///  descriptor is documented in section 9.6.3 of the USB 3.0 specification.
    ///  All multiple-byte fields are represented in host-endian format.
    /// </summary>
    [StructLayoutAttribute(LayoutKind.Sequential, Pack = NativeImport.Pack)]
    public unsafe struct ConfigDescriptor
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
        ///  Total length of data returned for this configuration
        /// </summary>
        public ushort TotalLength;

        /// <summary>
        ///  Number of interfaces supported by this configuration
        /// </summary>
        public byte NumInterfaces;

        /// <summary>
        ///  Identifier value for this configuration
        /// </summary>
        public byte ConfigurationValue;

        /// <summary>
        ///  Index of string descriptor describing this configuration
        /// </summary>
        public byte Configuration;

        /// <summary>
        ///  Configuration characteristics
        /// </summary>
        public byte Attributes;

        /// <summary>
        ///  Maximum power consumption of the USB device from this bus in this
        ///  configuration when the device is fully operation. Expressed in units
        ///  of 2 mA when the device is operating in high-speed mode and in units
        ///  of 8 mA when the device is operating in super-speed mode.
        /// </summary>
        public byte MaxPower;

        /// <summary>
        ///  Array of interfaces supported by this configuration. The length of
        ///  this array is determined by the bNumInterfaces field.
        /// </summary>
        public UsbInterface* Interface;

        /// <summary>
        ///  Extra descriptors. If libusb encounters unknown configuration
        ///  descriptors, it will store them here, should you wish to parse them.
        /// </summary>
        public byte* Extra;

        /// <summary>
        ///  Length of the extra descriptors, in bytes.
        /// </summary>
        public int ExtraLength;

    }
}
