using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASDLibUSBWrapper
{
    public class UsbDeviceCollection : ReadOnlyCollection<IUsbDevice>, IDisposable
    {
        public UsbDeviceCollection(IList<IUsbDevice> list) : base(list)
        {

        }

        public void Dispose()
        {
            foreach(var device in this)
                device.Dispose();
        }
    }
}
