namespace ASDLibUSBWrapper
{
    public class UsbContext : IUsbContext
    {
        /// <summary>
        /// The native context.
        /// </summary>
        private readonly LibUsbContext context;

        /// <summary>
        /// Tracks whether this context has been disposed of, or not.
        /// </summary>
        private bool disposed = false;

        private Thread eventHandlingThread;
        private bool shouldHandleEvents = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="UsbContext"/> class.
        /// </summary>
        public UsbContext()
        {
            IntPtr contextHandle = IntPtr.Zero;
            NativeImport.Init(ref contextHandle).ThrowOnError();
            this.context = LibUsbContext.DangerousCreate(contextHandle);
            eventHandlingThread = new Thread(() => { });
        }

        ~UsbContext()
        {
            // Put cleanup code in Dispose(bool disposing).
            this.Dispose(false);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            // Put cleanup code in Dispose(bool disposing).
            this.Dispose(true);

            GC.SuppressFinalize(this);
        }

        /// <inheritdoc/>
        public void SetDebugLevel(LogLevel level)
        {
            NativeImport.SetDebug(this.context, (int)level);
        }

        /// <summary>
        /// Returns a list of USB devices currently attached to the system.
        /// </summary>
        /// <returns>
        /// A <see cref="UsbDeviceCollection"/> which contains the devices currently
        /// attached to the system.</returns>
        /// <remarks>
        /// <para>
        /// This is your entry point into finding a USB device to operate.
        /// </para>
        /// <para>
        /// You are expected to dispose all the devices once you are done with them. Disposing the <see cref="UsbDeviceCollection"/>
        /// will dispose all devices in that collection. You can <see cref="UsbDevice.Clone"/> a device to get a copy of the device
        /// which you can use after you've disposed the <see cref="UsbDeviceCollection"/>.
        /// </para>
        /// </remarks>
        public unsafe UsbDeviceCollection UsbDevices()
        {
            IntPtr* list;
            var deviceCount = NativeImport.GetDeviceList(this.context, &list);

            Collection<IUsbDevice> devices = new Collection<IUsbDevice>();

            for (int i = 0; i < deviceCount.ToInt32(); i++)
            {
                LibUsbDevice device = LibUsbDevice.DangerousCreate(list[i]);
                devices.Add(new UsbDevice(device));
            }

            NativeImport.FreeDeviceList(list, unrefDevices: 0 /* Do not unreference the devices */);

            return new UsbDeviceCollection(devices);
        }

        /// <inheritdoc/>
        public IUsbDevice FindSingleDevice(UsbDeviceFinder finder)
        {
            if (finder == null)
            {
                throw new ArgumentNullException(nameof(finder));
            }

            return this.FindSingleDevice(finder.Check);
        }

        /// <inheritdoc/>
        public IUsbDevice FindSingleDevice(Func<IUsbDevice, bool> predicate)
        {
            if (predicate == null)
            {
                throw new ArgumentNullException(nameof(predicate));
            }

            using (var list = this.UsbDevices())
            {
                foreach (var device in list)
                {
                    if (predicate(device))
                    {
                        return device.Clone();
                    }
                }
            }

            return null;
        }

        /// <inheritdoc/>
        public UsbDeviceCollection FindMultipleDevices(UsbDeviceFinder finder)
        {
            if (finder == null)
            {
                throw new ArgumentNullException(nameof(finder));
            }

            return this.FindMultipleDevices(finder.Check);
        }

        /// <inheritdoc/>
        public UsbDeviceCollection FindMultipleDevices(Func<IUsbDevice, bool> predicate)
        {
            if (predicate == null)
            {
                throw new ArgumentNullException(nameof(predicate));
            }

            var matchingDevices = new List<IUsbDevice>();

            using (var list = this.UsbDevices())
            {
                foreach (var device in list)
                {
                    if (predicate(device))
                    {
                        matchingDevices.Add(device.Clone());
                    }
                }
            }

            UsbDeviceCollection devices = new UsbDeviceCollection(matchingDevices);
            return devices;
        }

        public void StartHandlingEvents()
        {
            if (this.eventHandlingThread == null)
            {
                this.eventHandlingThread = new Thread(this.HandleEvents);
                this.shouldHandleEvents = true;
                this.eventHandlingThread.Start();
            }
        }

        public void StopHandlingEvents()
        {
            if (this.eventHandlingThread != null)
            {
                this.shouldHandleEvents = false;
                this.eventHandlingThread.Join();
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {
                    // Dispose managed state (managed objects).
                }

                // Free unmanaged resources (unmanaged objects) and override a finalizer below.
                // Set large fields to null.
                this.disposed = true;
            }
        }

        private void HandleEvents()
        {
            while (this.shouldHandleEvents)
            {
                int completed = this.shouldHandleEvents ? 0 : 1;
                NativeImport.HandleEventsCompleted(this.context, ref completed).ThrowOnError();
            }
        }
    }
}
