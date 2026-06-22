using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ASDLibUSBWrapper;

namespace VesperApp.Models
{
    public enum DockType { DOCK_TYPE_DOCKV2 };

    public class DockDevice : IEquatable<DockDevice>
    {
        private UsbContext? _usbContext;
        private DockType _dockType;
        private UsbDevice? _dockUsbDevice;
        private DockDeviceInfo _info;
        private FTD2XX_NET.FTDI? _fTDI;
        private FTD2XX_NET.FTDI.FT_DEVICE_INFO_NODE? _ftdiNODE;

        public DockDeviceInfo Info => _info;
        public UsbDevice? UsbDock => _dockUsbDevice;
        public DockType DockType => _dockType;

        public FTD2XX_NET.FTDI? FTDIDevice => _fTDI;

        /*
        public DockDevice(UsbContext c, UsbDevice d)
        {
            _ftdiNODE = null;
            _fTDI = null;
            this._usbContext = c;
            this._dockUsbDevice = d;
            this._info = new DockDeviceInfo();
        }*/

        public DockDevice(UsbContext c, UsbDevice d, DockDeviceInfo info)
        {
            _ftdiNODE = null;
            _fTDI = null;
            this._usbContext = c;
            this._dockUsbDevice = d;
            this._info = info;
        }

        public DockDevice(FTD2XX_NET.FTDI ftdi, FTD2XX_NET.FTDI.FT_DEVICE_INFO_NODE nodeinfo, DockDeviceInfo info)
        {
            _ftdiNODE = nodeinfo;
            _fTDI = ftdi;
            this._usbContext = null;
            this._dockUsbDevice = null;
            this._info = info;
        }

        public DockDevice(DockDeviceInfo info)
        {
            _ftdiNODE = null;
            _fTDI = null;
            _usbContext = null;
            _dockUsbDevice = null;
            _info = info;
        }

        public bool IsOpen { get => (_fTDI != null) ? _fTDI.IsOpen : (_dockUsbDevice != null) ? _dockUsbDevice.IsOpen : false; }



        public override bool Equals(object? obj) => this.Equals(obj as DockDevice);

        public bool Equals(DockDevice? d)
        {
            if (d is null)
            {
                return false;
            }

            // Optimization for a common success case.
            if (Object.ReferenceEquals(this, d))
            {
                return true;
            }

            // If run-time types are not exactly the same, return false.
            if (this.GetType() != d.GetType())
            {
                return false;
            }

            // Return true if the fields match.
            // Note that the base class is not invoked because it is
            // System.Object, which defines Equals as reference equality.
            return (Info.Id == d.Info.Id);
        }

        public override int GetHashCode() => ((Info.Id == null) ? "" : Info.Id).GetHashCode();

        public static bool operator == (DockDevice? lhs, DockDevice? rhs)
        {
            if (lhs is null)
            {
                if (rhs is null)
                {
                    return true;
                }

                // Only the left side is null.
                return false;
            }
            // Equals handles case of null on right side.
            return lhs.Equals(rhs);
        }

        public static bool operator != (DockDevice? lhs, DockDevice? rhs) => !(lhs == rhs);
    }
}
