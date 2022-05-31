using System.Diagnostics;

namespace ASDLibUSBWrapper
{
    public abstract class UsbBaseInfo
    {
        protected byte[] RawDescriptors { get; set; }
            = Array.Empty<byte>();

        /// <summary>
        /// Gets the device-specific custom descriptor lists.
        /// </summary>
        public virtual ReadOnlyCollection<byte> CustomDescriptors
        {
            get { return new ReadOnlyCollection<byte>(new List<byte>(this.RawDescriptors)); }
        }

    }


    /// <summary> Contains USB device descriptor information.
    /// </summary>
    public class UsbDeviceInfo
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="UsbDeviceInfo"/> class.
        /// </summary>
        public UsbDeviceInfo()
        {
            Manufacturer = "";
            Product = "";
            SerialNumber = "";
            this.VendorId = 0;
            this.ProductId = 0;
        }

        private readonly Collection<UsbConfigInfo> configurations = new Collection<UsbConfigInfo>();

        public static UsbDeviceInfo FromUsbDeviceDescriptor(IUsbDevice device, DeviceDescriptor descriptor)
        {
            Debug.Assert(descriptor.DescriptorType == (int)DescriptorType.Device, "A config descriptor was expected");

            var value = new UsbDeviceInfo();
            value.Device = descriptor.Device;
            value.DeviceClass = descriptor.DeviceClass;
            value.DeviceProtocol = descriptor.DeviceProtocol;
            value.DeviceSubClass = descriptor.DeviceSubClass;
            value.ProductId = descriptor.IdProduct;
            value.VendorId = descriptor.IdVendor;
            value.Manufacturer = device.GetStringDescriptor(descriptor.Manufacturer, failSilently: true);

            value.MaxPacketSize0 = descriptor.MaxPacketSize0;

            for (byte i = 0; i < descriptor.NumConfigurations; i++)
            {
                if (device.TryGetConfigDescriptor(i, out var configDescriptor))
                {
                    //Console.WriteLine("Adding Configuration descriptor: " + configDescriptor.ConfigurationValue.ToString());
                    value.configurations.Add(configDescriptor);
                }
            }

            value.Product = device.GetStringDescriptor(descriptor.Product, failSilently: true);
            value.SerialNumber = device.GetStringDescriptor(descriptor.SerialNumber, failSilently: true);
            value.Usb = descriptor.USB;
            return value;
        }

        public virtual ushort Device { get; protected set; }

        public virtual byte DeviceClass { get; protected set; }

        public virtual byte DeviceProtocol { get; protected set; }

        public virtual byte DeviceSubClass { get; protected set; }

        public virtual ushort ProductId { get; protected set; }

        public virtual ushort VendorId { get; protected set; }

        public virtual string Manufacturer { get; protected set; }

        public virtual byte MaxPacketSize0 { get; protected set; }

        public virtual byte NumConfigurations { get; protected set; }

        public virtual string Product { get; protected set; }

        public virtual string SerialNumber { get; protected set; }

        public virtual ushort Usb { get; protected set; }

        public virtual ReadOnlyCollection<UsbConfigInfo> Configurations
        {
            get { return new ReadOnlyCollection<UsbConfigInfo>(this.configurations); }
        }

        public override string ToString()
        {
            if (!string.IsNullOrEmpty(this.SerialNumber))
            {
                return $"{this.Manufacturer} {this.Product} ({this.SerialNumber})";
            }
            else
            {
                return $"{this.Manufacturer} {this.Product}";
            }
        }
    }



    /// <summary> Describes a USB device interface.
    /// </summary>
    public class UsbInterfaceInfo : UsbBaseInfo
    {
        private List<UsbEndpointInfo> endpoints = new List<UsbEndpointInfo>();

        public static unsafe Collection<UsbInterfaceInfo> FromUsbInterface(UsbDevice device, UsbInterface @interface)
        {
            var interfaces = (InterfaceDescriptor*)@interface.Altsetting;
            Collection<UsbInterfaceInfo> value = new Collection<UsbInterfaceInfo>();

            for (int i = 0; i < @interface.NumAltsetting; i++)
            {
                value.Add(FromUsbInterfaceDescriptor(device, interfaces[i]));
            }

            return value;
        }

        public static unsafe UsbInterfaceInfo FromUsbInterfaceDescriptor(UsbDevice device, InterfaceDescriptor descriptor)
        {
            Debug.Assert(descriptor.DescriptorType == (int)DescriptorType.Interface, "A config descriptor was expected");

            UsbInterfaceInfo value = new UsbInterfaceInfo();
            value.AlternateSetting = descriptor.AlternateSetting;

            var endpoints = (EndpointDescriptor*)descriptor.Endpoint;

            for (int i = 0; i < descriptor.NumEndpoints; i++)
            {
                if (endpoints[i].DescriptorType != 0)
                {
                    value.endpoints.Add(UsbEndpointInfo.FromUsbEndpointDescriptor(endpoints[i]));
                }
            }

            value.RawDescriptors = new byte[descriptor.ExtraLength];
            if (descriptor.ExtraLength > 0)
            {
                Span<byte> extra = new Span<byte>(descriptor.Extra, descriptor.ExtraLength);
                extra.CopyTo(value.RawDescriptors);
            }

            value.Interface = device.GetStringDescriptor(descriptor.Interface, failSilently: true);
            value.Class = (ClassCode)descriptor.InterfaceClass;
            value.Number = descriptor.InterfaceNumber;
            value.Protocol = descriptor.InterfaceProtocol;
            value.SubClass = descriptor.InterfaceSubClass;

            return value;
        }

        public virtual byte AlternateSetting { get; private set; }

        public virtual ClassCode Class { get; private set; }

        public virtual int Number { get; private set; }

        public virtual byte Protocol { get; private set; }

        public virtual string Interface { get; private set; } = "";

        public virtual byte SubClass { get; private set; }

        /// <summary>
        /// Gets the collection of endpoint descriptors associated with this interface.
        /// </summary>
        public virtual ReadOnlyCollection<UsbEndpointInfo> Endpoints
        {
            get { return this.endpoints.AsReadOnly(); }
        }

        public override string ToString()
        {
            return this.Interface;
        }
    }



    /// <summary> 
    /// Contains Endpoint information for the current UsbConfigInfo
    /// </summary>
    public class UsbEndpointInfo : UsbBaseInfo
    {
        public static unsafe UsbEndpointInfo FromUsbEndpointDescriptor(EndpointDescriptor descriptor)
        {
            Debug.Assert(descriptor.DescriptorType == (int)DescriptorType.Endpoint, "An endpoint descriptor was expected");

            var value = new UsbEndpointInfo();
            value.Attributes = descriptor.Attributes;
            value.EndpointAddress = descriptor.EndpointAddress;

            value.RawDescriptors = new byte[descriptor.ExtraLength];
            if (descriptor.ExtraLength > 0)
            {
                Span<byte> extra = new Span<byte>(descriptor.Extra, descriptor.ExtraLength);
                extra.CopyTo(value.RawDescriptors);
            }

            value.Interval = descriptor.Interval;
            value.MaxPacketSize = descriptor.MaxPacketSize;
            value.Refresh = descriptor.Refresh;
            value.SyncAddress = descriptor.SynchAddress;

            return value;
        }

        public virtual byte Attributes { get; private set; }

        public virtual byte EndpointAddress { get; private set; }

        public virtual byte Interval { get; private set; }

        public virtual ushort MaxPacketSize { get; private set; }

        public virtual byte Refresh { get; private set; }

        public virtual byte SyncAddress { get; private set; }

        public override string ToString()
        {
            return $"{this.EndpointAddress}";
        }
    }



    /// <summary> 
    /// Contains all Configuration information for the current.
    /// </summary>
    public class UsbConfigInfo : UsbBaseInfo
    {
        private readonly List<UsbInterfaceInfo> interfaces = new List<UsbInterfaceInfo>();

        internal static unsafe UsbConfigInfo FromUsbConfigDescriptor(UsbDevice device, ConfigDescriptor descriptor)
        {
            Debug.Assert(descriptor.DescriptorType == (int)DescriptorType.Config, "A config descriptor was expected");

            UsbConfigInfo value = new UsbConfigInfo();
            value.Attributes = descriptor.Attributes;
            value.Configuration = device.GetStringDescriptor(descriptor.Configuration, failSilently: true);
            //Console.WriteLine("UsbConfigInfo FromUsbConfigDescriptor : " +( (value.Configuration == null) ? "NULL" : value.Configuration));
            value.ConfigurationValue = descriptor.ConfigurationValue;

            value.RawDescriptors = new byte[descriptor.ExtraLength];
            if (descriptor.ExtraLength > 0)
            {
                Span<byte> extra = new Span<byte>(descriptor.Extra, descriptor.ExtraLength);
                extra.CopyTo(value.RawDescriptors);
            }

            var interfaces = (UsbInterface*)descriptor.Interface;
            for (int i = 0; i < descriptor.NumInterfaces; i++)
            {
                var values = UsbInterfaceInfo.FromUsbInterface(device, interfaces[i]);
                value.interfaces.AddRange(values);
            }

            value.MaxPower = descriptor.MaxPower;

            return value;
        }

        public UsbConfigInfo() : base()
        {
            Configuration = "";
        }

        public virtual string Configuration { get; protected set; } = "";

        public virtual byte Attributes { get; protected set; }

        public virtual int ConfigurationValue { get; protected set; }

        public virtual byte MaxPower { get; protected set; }

        /// <summary>
        /// Gets the collection of USB device interfaces associated with this <see cref="UsbConfigInfo"/> instance.
        /// </summary>
        public virtual ReadOnlyCollection<UsbInterfaceInfo> Interfaces
        {
            get { return this.interfaces.AsReadOnly(); }
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return this.Configuration;
        }
    }

}
