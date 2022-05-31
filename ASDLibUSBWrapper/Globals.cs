global using System;
global using System.Reflection;
global using System.Runtime.InteropServices;
global using System.Runtime.Serialization;
global using System.Security.Permissions;
global using System.Collections.Generic;
global using System.Collections.ObjectModel;
global using System.Linq;
global using System.Text;
global using System.Threading.Tasks;
global using ASDLibUSBWrapper.Descriptors;

namespace ASDLibUSBWrapper
{
    /// <summary> Various USB constants.
    /// </summary>
    public static class UsbConstants
    {
        /// <summary>
        /// Default timeout for all USB IO operations.
        /// </summary>
        public const int DefaultTimeout = 1000;

        /// <summary>
        /// Maximum number of USB devices connected to the driver at once.
        /// </summary>
        public const int MaxDeviceCount = 256;

        /// <summary>
        /// Endpoint direction mask.
        /// </summary>
        public const byte EndpointDirectionMask = 0x80;

        /// <summary>
        /// Endpoint number mask.
        /// </summary>
        public const byte EndpointNumberMask = 0xf;
    }

    [Flags]
    public enum LibUsbError : int
    {
        /// <summary>
        ///  Success (no error) 
        /// </summary>
        Success = 0,

        /// <summary>
        ///  Input/output error 
        /// </summary>
        Io = -1,

        /// <summary>
        ///  Invalid parameter 
        /// </summary>
        InvalidParam = -2,

        /// <summary>
        ///  Access denied (insufficient permissions) 
        /// </summary>
        Access = -3,

        /// <summary>
        ///  No such device (it may have been disconnected) 
        /// </summary>
        NoDevice = -4,

        /// <summary>
        ///  Entity not found 
        /// </summary>
        NotFound = -5,

        /// <summary>
        ///  Resource busy 
        /// </summary>
        Busy = -6,

        /// <summary>
        ///  Operation timed out 
        /// </summary>
        Timeout = -7,

        /// <summary>
        ///  Overflow 
        /// </summary>
        Overflow = -8,

        /// <summary>
        ///  Pipe error 
        /// </summary>
        Pipe = -9,

        /// <summary>
        ///  System call interrupted (perhaps due to signal) 
        /// </summary>
        Interrupted = -10,

        /// <summary>
        ///  Insufficient memory 
        /// </summary>
        NoMem = -11,

        /// <summary>
        ///  Operation not supported or unimplemented on this platform 
        /// </summary>
        NotSupported = -12,

        /// <summary>
        ///  Other error 
        /// </summary>
        Other = -99,

    }

    /// <summary>
    ///  Device and/or Interface Class codes 
    /// </summary>
    [Flags]
    public enum ClassCode : byte
    {
        /// <summary>
        ///  In the context of a 
        /// </summary>
        PerInterface = 0,

        /// <summary>
        ///  Audio class 
        /// </summary>
        Audio = 0x1,

        /// <summary>
        ///  Communications class 
        /// </summary>
        Comm = 0x2,

        /// <summary>
        ///  Human Interface Device class 
        /// </summary>
        Hid = 0x3,

        /// <summary>
        ///  Physical 
        /// </summary>
        Physical = 0x5,

        /// <summary>
        ///  Printer class 
        /// </summary>
        Printer = 0x7,

        /// <summary>
        ///  Image class 
        /// </summary>
        Ptp = 0x6,

        /// <summary>
        ///  Image class 
        /// </summary>
        Image = 0x6,

        /// <summary>
        ///  Mass storage class 
        /// </summary>
        MassStorage = 0x8,

        /// <summary>
        ///  Hub class 
        /// </summary>
        Hub = 0x9,

        /// <summary>
        ///  Data class 
        /// </summary>
        Data = 0xA,

        /// <summary>
        ///  Smart Card 
        /// </summary>
        SmartCard = 0xB,

        /// <summary>
        ///  Content Security 
        /// </summary>
        ContentSecurity = 0xD,

        /// <summary>
        ///  Video 
        /// </summary>
        Video = 0xE,

        /// <summary>
        ///  Personal Healthcare 
        /// </summary>
        PersonalHealthcare = 0xF,

        /// <summary>
        ///  Diagnostic Device 
        /// </summary>
        DiagnosticDevice = 0xDC,

        /// <summary>
        ///  Wireless class 
        /// </summary>
        Wireless = 0xE0,

        /// <summary>
        ///  Application class 
        /// </summary>
        Application = 0xFE,

        /// <summary>
        ///  Class is vendor-specific 
        /// </summary>
        VendorSpec = 0xFF,

    }

    /// <summary>
    ///  Structure providing the version of the libusb runtime
    /// </summary>
    [StructLayoutAttribute(LayoutKind.Sequential, Pack = NativeImport.Pack)]
    public unsafe struct Version
    {
        /// <summary>
        ///  Library major version.
        /// </summary>
        public ushort Major;

        /// <summary>
        ///  Library minor version.
        /// </summary>
        public ushort Minor;

        /// <summary>
        ///  Library micro version.
        /// </summary>
        public ushort Micro;

        /// <summary>
        ///  Library nano version.
        /// </summary>
        public ushort Nano;

        /// <summary>
        ///  Library release candidate suffix string, e.g. "-rc4".
        /// </summary>
        public IntPtr Rc;

        /// <summary>
        ///  For ABI compatibility only.
        /// </summary>
        public IntPtr Describe;

    }


    /// <summary>
    ///  Speed codes. Indicates the speed at which the device is operating.
    /// </summary>
    [Flags]
    public enum Speed : byte
    {
        /// <summary>
        ///  The OS doesn't report or know the device speed. 
        /// </summary>
        Unknown = 0,

        /// <summary>
        ///  The device is operating at low speed (1.5MBit/s). 
        /// </summary>
        Low = 0x1,

        /// <summary>
        ///  The device is operating at full speed (12MBit/s). 
        /// </summary>
        Full = 0x2,

        /// <summary>
        ///  The device is operating at high speed (480MBit/s). 
        /// </summary>
        High = 0x3,

        /// <summary>
        ///  The device is operating at super speed (5000MBit/s). 
        /// </summary>
        Super = 0x4,

    }



    /// <summary>
    ///  Descriptor types as defined by the USB specification. 
    /// </summary>
    [Flags]
    public enum DescriptorType : byte
    {
        /// <summary>
        ///  LibUsbDevice descriptor. See libusb_device_descriptor. 
        /// </summary>
        Device = 0x1,

        /// <summary>
        ///  Configuration descriptor. See libusb_config_descriptor. 
        /// </summary>
        Config = 0x2,

        /// <summary>
        ///  String descriptor 
        /// </summary>
        String = 0x3,

        /// <summary>
        ///  Interface descriptor. See libusb_interface_descriptor. 
        /// </summary>
        Interface = 0x4,

        /// <summary>
        ///  Endpoint descriptor. See libusb_endpoint_descriptor. 
        /// </summary>
        Endpoint = 0x5,

        /// <summary>
        ///  BOS descriptor 
        /// </summary>
        Bos = 0xF,

        /// <summary>
        ///  LibUsbDevice Capability descriptor 
        /// </summary>
        DeviceCapability = 0x10,

        /// <summary>
        ///  HID descriptor 
        /// </summary>
        Hid = 0x21,

        /// <summary>
        ///  HID report descriptor 
        /// </summary>
        Report = 0x22,

        /// <summary>
        ///  Physical descriptor 
        /// </summary>
        Physical = 0x23,

        /// <summary>
        ///  Hub descriptor 
        /// </summary>
        Hub = 0x29,

        /// <summary>
        ///  SuperSpeed Hub descriptor 
        /// </summary>
        SuperspeedHub = 0x2A,

        /// <summary>
        ///  SuperSpeed Endpoint Companion descriptor 
        /// </summary>
        SsEndpointCompanion = 0x30,

    }

    ///<summary>Recipient of the request.</summary>
    /// <seealso cref="UsbCtrlFlags"/>
    [Flags]
    public enum UsbRequestRecipient : byte
    {
        /// <summary>
        /// Device is recipient.
        /// </summary>
        RecipDevice = 0x00,

        /// <summary>
        /// Endpoint is recipient.
        /// </summary>
        RecipEndpoint = 0x02,

        /// <summary>
        /// Interface is recipient.
        /// </summary>
        RecipInterface = 0x01,

        /// <summary>
        /// Other is recipient.
        /// </summary>
        RecipOther = 0x03,
    }
    /// <summary> Availabled endpoint numbers/ids for writing.
    /// </summary>
    public enum WriteEndpointID : byte
    {
        /// <summary>
        /// Endpoint 1
        /// </summary>
        Ep01 = 0x01,

        /// <summary>
        /// Endpoint 2
        /// </summary>
        Ep02 = 0x02,

        /// <summary>
        /// Endpoint 3
        /// </summary>
        Ep03 = 0x03,

        /// <summary>
        /// Endpoint 4
        /// </summary>
        Ep04 = 0x04,

        /// <summary>
        /// Endpoint 5
        /// </summary>
        Ep05 = 0x05,

        /// <summary>
        /// Endpoint 6
        /// </summary>
        Ep06 = 0x06,

        /// <summary>
        /// Endpoint 7
        /// </summary>
        Ep07 = 0x07,

        /// <summary>
        /// Endpoint 8
        /// </summary>
        Ep08 = 0x08,

        /// <summary>
        /// Endpoint 9
        /// </summary>
        Ep09 = 0x09,

        /// <summary>
        /// Endpoint 10
        /// </summary>
        Ep10 = 0x0A,

        /// <summary>
        /// Endpoint 11
        /// </summary>
        Ep11 = 0x0B,

        /// <summary>
        /// Endpoint 12
        /// </summary>
        Ep12 = 0x0C,

        /// <summary>
        /// Endpoint 13
        /// </summary>
        Ep13 = 0x0D,

        /// <summary>
        /// Endpoint 14
        /// </summary>
        Ep14 = 0x0E,

        /// <summary>
        /// Endpoint 15
        /// </summary>
        Ep15 = 0x0F,
    }
    /// <summary>
    /// Availabled endpoint numbers/ids for reading.
    /// </summary>
    public enum ReadEndpointID : byte
    {
        /// <summary>
        /// Endpoint 1
        /// </summary>
        Ep01 = 0x81,

        /// <summary>
        /// Endpoint 2
        /// </summary>
        Ep02 = 0x82,

        /// <summary>
        /// Endpoint 3
        /// </summary>
        Ep03 = 0x83,

        /// <summary>
        /// Endpoint 4
        /// </summary>
        Ep04 = 0x84,

        /// <summary>
        /// Endpoint 5
        /// </summary>
        Ep05 = 0x85,

        /// <summary>
        /// Endpoint 6
        /// </summary>
        Ep06 = 0x86,

        /// <summary>
        /// Endpoint 7
        /// </summary>
        Ep07 = 0x87,

        /// <summary>
        /// Endpoint 8
        /// </summary>
        Ep08 = 0x88,

        /// <summary>
        /// Endpoint 9
        /// </summary>
        Ep09 = 0x89,

        /// <summary>
        /// Endpoint 10
        /// </summary>
        Ep10 = 0x8A,

        /// <summary>
        /// Endpoint 11
        /// </summary>
        Ep11 = 0x8B,

        /// <summary>
        /// Endpoint 12
        /// </summary>
        Ep12 = 0x8C,

        /// <summary>
        /// Endpoint 13
        /// </summary>
        Ep13 = 0x8D,

        /// <summary>
        /// Endpoint 14
        /// </summary>
        Ep14 = 0x8E,

        /// <summary>
        /// Endpoint 15
        /// </summary>
        Ep15 = 0x8F,
    }

    [Flags]
    public enum EndpointDirection : byte
    {
        /// <summary>
        ///  In: device-to-host 
        /// </summary>
        In = 0x80,

        /// <summary>
        ///  Out: host-to-device 
        /// </summary>
        Out = 0,

    }

    [Flags]
    public enum LogLevel : byte
    {
        None = 0,

        Error = 0x1,

        Warning = 0x2,

        Info = 0x3,

        Debug = 0x4,

    }

    /// <summary> All possible USB endpoint types.
    /// </summary>
    [Flags]
    public enum EndpointType : byte
    {
        /// <summary>
        /// Control endpoint type.
        /// </summary>
        Control,

        /// <summary>
        /// Isochronous endpoint type.
        /// </summary>
        Isochronous,

        /// <summary>
        /// Bulk endpoint type.
        /// </summary>
        Bulk,

        /// <summary>
        /// Interrupt endpoint type.
        /// </summary>
        Interrupt
    }

    /// <summary>
    /// Standard USB requests.
    /// </summary>
    /// <seealso cref="UsbCtrlFlags"/>
    [Flags]
    public enum UsbRequestType : byte
    {
        /// <summary>
        /// Class specific request.
        /// </summary>
        TypeClass = 0x01 << 5,

        /// <summary>
        /// RESERVED.
        /// </summary>
        TypeReserved = 0x03 << 5,

        /// <summary>
        /// Standard request.
        /// </summary>
        TypeStandard = 0x00 << 5,

        /// <summary>
        /// Vendor specific request.
        /// </summary>
        TypeVendor = 0x02 << 5,
    }


    /// <summary>
    ///  Standard requests, as defined in table 9-5 of the USB 3.0 specifications 
    /// </summary>
    [Flags]
    public enum StandardRequest : byte
    {
        /// <summary>
        ///  Request status of the specific recipient 
        /// </summary>
        GetStatus = 0,

        /// <summary>
        ///  Clear or disable a specific feature 
        /// </summary>
        ClearFeature = 0x1,

        /// <summary>
        ///  Set or enable a specific feature 
        /// </summary>
        SetFeature = 0x3,

        /// <summary>
        ///  Set device address for all future accesses 
        /// </summary>
        SetAddress = 0x5,

        /// <summary>
        ///  Get the specified descriptor 
        /// </summary>
        GetDescriptor = 0x6,

        /// <summary>
        ///  Used to update existing descriptors or add new descriptors 
        /// </summary>
        SetDescriptor = 0x7,

        /// <summary>
        ///  Get the current device configuration value 
        /// </summary>
        GetConfiguration = 0x8,

        /// <summary>
        ///  Set device configuration 
        /// </summary>
        SetConfiguration = 0x9,

        /// <summary>
        ///  Return the selected alternate setting for the specified interface 
        /// </summary>
        GetInterface = 0xA,

        /// <summary>
        ///  Select an alternate interface for the specified interface 
        /// </summary>
        SetInterface = 0xB,

        /// <summary>
        ///  Set then report an endpoint's synchronization frame 
        /// </summary>
        SynchFrame = 0xC,

        /// <summary>
        ///  Sets both the U1 and U2 Exit Latency 
        /// </summary>
        SetSel = 0x30,

        SetIsochDelay = 0x31,

    }

}