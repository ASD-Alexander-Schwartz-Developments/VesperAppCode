using System;
using System.Collections.Generic;
using ASDLibUSBWrapper;
using FirmwareUpdater.Dfu;

namespace LibUsbDfu
{
    /// <summary>
    /// Concrete USB DFU device transport bound to ASDLibUSBWrapper (libusb). Implements
    /// the abstract <see cref="FirmwareUpdater.Dfu.Device"/> host using the project's
    /// unified USB layer. Reconciled with the migrated wrapper API (UsbDeviceInfo /
    /// UsbConfigInfo / UsbInterfaceInfo descriptor model + raw UsbSetupPacket transfers).
    /// </summary>
    public class Device : FirmwareUpdater.Dfu.Device, IDisposable
    {
        // bmRequestType bytes: class request, interface recipient.
        private const byte ClassInterfaceOut = 0x21; // host→device
        private const byte ClassInterfaceIn = 0xA1;  // device→host

        private byte configIndex;
        private byte interfaceIndex;
        private UsbDevice device;
        private Identification info;
        private FunctionalDescriptor dfuDesc;

        public override FunctionalDescriptor DfuDescriptor { get { return dfuDesc; } }
        public override Identification Info { get { return info; } }
        private UsbConfigInfo ConfigInfo { get { return device.Configs[configIndex]; } }
        private UsbInterfaceInfo InterfaceInfo { get { return ConfigInfo.Interfaces[interfaceIndex]; } }
        private byte InterfaceID { get { return (byte)InterfaceInfo.Number; } }

        public override string ToString()
        {
            return device.Info.ToString();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                (device as IDisposable)?.Dispose();
            }
        }

        /// <summary>
        /// Gets one (or none) DFU device with the specified IDs. If no exact match is
        /// found, a second pass matches the Vendor ID only (device already in DFU mode).
        /// </summary>
        public static Device OpenFirst(UsbContext context, int vid, int pid)
        {
            var devs = OpenAll(context.FindMultipleDevices(d => d.VendorId == vid && d.ProductId == pid));

            // it's possible that the device is already in DFU mode, in which case only the VID has to match
            if (devs.Count == 0)
            {
                devs = OpenAll(context.FindMultipleDevices(d => d.VendorId == vid));
            }

            if (devs.Count == 0)
            {
                throw new ArgumentException(String.Format("No DFU device was found with {0:X}:{1:X}", vid, pid));
            }
            // if more than one are connected, use the first and release the rest
            else if (devs.Count > 1)
            {
                for (int i = 1; i < devs.Count; i++)
                {
                    devs[i].Close();
                }
            }
            return devs[0];
        }

        /// <summary>
        /// Finds and opens all DFU devices in the supplied collection.
        /// </summary>
        public static List<Device> OpenAll(UsbDeviceCollection deviceList)
        {
            List<Device> devs = new List<Device>();
            foreach (IUsbDevice item in deviceList)
            {
                if (Device.TryOpen((UsbDevice)item, out Device dev))
                {
                    devs.Add(dev);
                }
            }
            return devs;
        }

        /// <summary>
        /// Attempts to open a USB device as a USB DFU device.
        /// </summary>
        public static bool TryOpen(UsbDevice dev, out Device dfuDevice)
        {
            dfuDevice = null;

            byte cfIndex = 0;
            byte ifIndex;

            dev.Open();

            var confInfo = dev.Configs[cfIndex];

            // Select the configuration before claiming an interface.
            IUsbDevice usbDevice = dev as IUsbDevice;
            usbDevice?.SetConfiguration(confInfo.ConfigurationValue);

            // find the DFU interface (alt setting)
            for (ifIndex = 0; ifIndex < confInfo.Interfaces.Count; ifIndex++)
            {
                var iface = confInfo.Interfaces[ifIndex];

                if (!IsDfuInterface(iface))
                {
                    continue;
                }

                usbDevice?.ClaimInterface(iface.Number);
                break;
            }

            try
            {
                if (ifIndex == confInfo.Interfaces.Count)
                {
                    throw new ArgumentException("The device doesn't have a valid DFU interface");
                }
                dfuDevice = new Device(dev, cfIndex, ifIndex);
                return true;
            }
            catch (Exception)
            {
                (dev as IDisposable)?.Dispose();
                return false;
            }
        }

        private Device(UsbDevice dev, byte conf, byte interf)
        {
            this.configIndex = conf;
            this.interfaceIndex = interf;
            this.device = dev;

            LoadDfuDescriptor();

            this.info = new Identification(device.Info.VendorId, device.Info.ProductId,
                device.Info.Device, dfuDesc.bcdDFUVersion);
        }

        private static bool IsDfuInterface(UsbInterfaceInfo iinfo)
        {
            return ((byte)iinfo.Class == InterfaceClass) &&
                (iinfo.SubClass == InterfaceSubClass) &&
                ((iinfo.Protocol == InterfaceProtocol_Runtime) ||
                 (iinfo.Protocol == InterfaceProtocol_DFU));
        }

        // The DFU functional descriptor (type 0x21) lives in an interface's "extra"
        // descriptors, exposed flat by the wrapper. libusb attaches trailing extras to
        // the LAST parsed alt-setting, while we match/claim alt-setting 0 — so search
        // every interface entry of the configuration, not just the one we claimed
        // (observed on the STM32U585 ROM bootloader: 3 alt settings, descriptor on the
        // last one only).
        private void LoadDfuDescriptor()
        {
            foreach (var iface in ConfigInfo.Interfaces)
            {
                var raw = iface.CustomDescriptors;
                if (raw == null || raw.Count == 0) continue;

                byte[] extra = new byte[raw.Count];
                for (int i = 0; i < raw.Count; i++) extra[i] = raw[i];

                for (int i = 0; i + 1 < extra.Length;)
                {
                    byte len = extra[i];
                    if (len == 0) break;
                    byte type = extra[i + 1];
                    if (type == FunctionalDescriptor.Type &&
                        len >= FunctionalDescriptor.Size &&
                        i + FunctionalDescriptor.Size <= extra.Length)
                    {
                        try
                        {
                            dfuDesc = new FunctionalDescriptor(extra, i);
                            return;
                        }
                        catch (Exception)
                        {
                        }
                    }
                    i += len;
                }
            }

            throw new ApplicationException(String.Format("Failed to find the DFU Functional Descriptor on target {0}", this));
        }

        protected override byte NumberOfAlternateSettings
        {
            get { return (byte)ConfigInfo.Interfaces.Count; }
        }

        protected override byte AlternateSetting
        {
            get
            {
                device.GetAltInterfaceSetting(InterfaceID, out byte alt);
                return alt;
            }
            set
            {
                if (AlternateSetting == value)
                    return;

                device.SetAltInterface(value);
            }
        }

        // The wrapper already resolves interface string descriptors, so we use the
        // alt-setting index as the "string id" and return the resolved string below.
        protected override byte iAlternateSetting(byte altSetting)
        {
            return altSetting;
        }

        protected override string GetString(byte iString)
        {
            var ifaces = ConfigInfo.Interfaces;
            if (iString < ifaces.Count)
                return ifaces[iString].Interface ?? string.Empty;

            // Fall back to a real string-descriptor fetch (e.g. for a DFU status string).
            return device.GetString(out string s, 0, iString) ? (s ?? string.Empty) : string.Empty;
        }

        protected override void ControlTransfer(Request request, ushort value = 0)
        {
            var s = new UsbSetupPacket(ClassInterfaceOut, (byte)request, value, InterfaceID, 0);
            device.ControlTransfer(s);
        }

        protected override void ControlTransfer(Request request, ushort value, byte[] outdata)
        {
            var s = new UsbSetupPacket(ClassInterfaceOut, (byte)request, value, InterfaceID, outdata.Length);
            int n = device.ControlTransfer(s, outdata, 0, outdata.Length);
            if (n != outdata.Length)
                throw new ApplicationException(String.Format("DFU control transfer ({0}) sent {1}/{2} bytes to {3}", request, n, outdata.Length, this));
        }

        protected override void ControlTransfer(Request request, ushort value, ref byte[] indata)
        {
            var s = new UsbSetupPacket(ClassInterfaceIn, (byte)request, value, InterfaceID, indata.Length);
            device.ControlTransfer(s, indata, 0, indata.Length);
        }

        public override void Close()
        {
            device.Close();
        }

        public override bool IsOpen()
        {
            return device.IsOpen;
        }

        protected override void BusReset()
        {
            // ResetDevice disposes the underlying handle; the device re-enumerates.
            try { device.ResetDevice(); }
            catch (Exception) { }
        }
    }
}
