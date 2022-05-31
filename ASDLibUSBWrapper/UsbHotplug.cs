using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASDLibUSBWrapper
{
    /// <summary>
    ///  Since version 1.0.16, 
    ///  Flags for hotplug events 
    /// </summary>
    [Flags]
    public enum UsbHotplugFlag : byte
    {
        /// <summary>
        ///  Default value when not using any flags. 
        /// </summary>
        NoFlags = 0,

        /// <summary>
        ///  Arm the callback and fire it for all matching currently attached devices. 
        /// </summary>
        Enumerate = 0x1,

    }


    /// <summary>
    ///  Since version 1.0.16, 
    ///  Hotplug events 
    /// </summary>
    [Flags]
    public enum UsbHotplugEvent : byte
    {
        /// <summary>
        ///  A device has been plugged in and is ready to use 
        /// </summary>
        DeviceArrived = 0x1,

        DeviceLeft = 0x2,

    }
}
