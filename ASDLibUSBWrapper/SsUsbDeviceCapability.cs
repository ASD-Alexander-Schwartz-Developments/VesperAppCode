using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASDLibUSBWrapper
{
    /// <summary>
    ///  A structure representing the SuperSpeed USB LibUsbDevice Capability descriptor
    ///  This descriptor is documented in section 9.6.2.2 of the USB 3.0 specification.
    ///  All multiple-byte fields are represented in host-endian format.
    /// </summary>
    [StructLayoutAttribute(LayoutKind.Sequential, Pack = NativeImport.Pack)]
    public struct SsUsbDeviceCapabilityDescriptor
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
        ///  LIBUSB_BT_SS_USB_DEVICE_CAPABILITY in this context.
        /// </summary>
        public byte DevCapabilityType;

        /// <summary>
        ///  Bitmap encoding of supported device level features.
        ///  A value of one in a bit location indicates a feature is
        ///  supported; a value of zero indicates it is not supported.
        ///  See
        /// </summary>
        public byte Attributes;

        /// <summary>
        ///  Bitmap encoding of the speed supported by this device when
        ///  operating in SuperSpeed mode. See
        /// </summary>
        public ushort SpeedSupported;

        /// <summary>
        ///  The lowest speed at which all the functionality supported
        ///  by the device is available to the user. For example if the
        ///  device supports all its functionality when connected at
        ///  full speed and above then it sets this value to 1.
        /// </summary>
        public byte FunctionalitySupport;

        /// <summary>
        ///  U1 LibUsbDevice Exit Latency.
        /// </summary>
        public byte U1DevExitLat;

        /// <summary>
        ///  U2 LibUsbDevice Exit Latency.
        /// </summary>
        public ushort U2DevExitLat;

    }


    /// <summary>
    ///  field of the SuperSpeed USB LibUsbDevice Capability descriptor.
    /// </summary>
    [Flags]
    public enum SsUsbDeviceCapabilityAttributes : byte
    {
        /// <summary>
        ///  Supports Latency Tolerance Messages (LTM) 
        /// </summary>
        BmLtmSupport = 0x2,

    }
}
