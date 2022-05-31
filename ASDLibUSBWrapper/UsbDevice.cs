namespace ASDLibUSBWrapper
{
    /// <summary>
    /// Represents a device which is managed by libusb. Use <see cref="UsbContext.List"/>
    /// to get a list of devices which are available for use.
    /// </summary>
    public class UsbDevice : IUsbDevice, IDisposable, ICloneable
    {
        private bool disposed;
        private readonly LibUsbDevice device = LibUsbDevice.Zero;
        private LibUsbDeviceHandle deviceHandle = LibUsbDeviceHandle.Zero;
        private UsbDeviceInfo? descriptor = null;
        private readonly List<int> mClaimedInterfaces = new List<int>();
        private readonly byte[] usbAltInterfaceSettings = new byte[UsbConstants.MaxDeviceCount];


        /// <summary>
        /// Initializes a new instance of the <see cref="UsbDevice"/> class.
        /// </summary>
        /// <param name="device">
        /// A device handle for this device. In most cases, you will want to use the
        /// <see cref="UsbContext.List()"/> methods to list all devices.
        /// </param>
        public UsbDevice(LibUsbDevice? device)
        {
            if (device == null || device == LibUsbDevice.Zero)
            {
                throw new ArgumentNullException(nameof(device));
            }

            if (device == LibUsbDevice.Zero || device.IsClosed || device.IsInvalid)
            {
                throw new ArgumentOutOfRangeException(nameof(device));
            }

            this.device = device;
            this.descriptor = new UsbDeviceInfo();
        }




        /// <summary>
        /// Claims the specified interface of the device.
        /// </summary>
        /// <param name="interfaceID">The interface to claim.</param>
        /// <returns>True on success.</returns>
        public bool ClaimInterface(int interfaceID)
        {
            this.EnsureOpen();

            if (this.mClaimedInterfaces.Contains(interfaceID))
            {
                return true;
            }

            NativeImport.ClaimInterface(this.deviceHandle, interfaceID).ThrowOnError();
            this.mClaimedInterfaces.Add(interfaceID);
            return true;
        }

        public bool GetAltInterface(out int alternateID)
        {
            int interfaceID = this.mClaimedInterfaces.Count == 0 ? 0 : this.mClaimedInterfaces[this.mClaimedInterfaces.Count - 1];
            return this.GetAltInterface(interfaceID, out alternateID);
        }

        /// <summary>
        /// Gets the alternate interface number for the specified interfaceID.
        /// </summary>
        /// <param name="interfaceID">The interface number of to get the alternate setting for.</param>
        /// <param name="alternateID">The currrently selected alternate interface number.</param>
        /// <returns>True on success.</returns>
        public bool GetAltInterface(int interfaceID, out int alternateID)
        {
            alternateID = this.usbAltInterfaceSettings[interfaceID & (UsbConstants.MaxDeviceCount - 1)];
            return true;
        }

        /// <summary>
        /// Gets the selected alternate interface of the specified interface.
        /// </summary>
        /// <param name="interfaceID">The interface settings number (index) to retrieve the selected alternate interface setting for.</param>
        /// <param name="selectedAltInterfaceID">The alternate interface setting selected for use with the specified interface.</param>
        public void GetAltInterfaceSetting(byte interfaceID, out byte selectedAltInterfaceID)
        {
            byte[] buf = new byte[1];
            int uTransferLength;

            UsbSetupPacket setupPkt = new UsbSetupPacket();
            setupPkt.RequestType = (byte)EndpointDirection.In | (byte)UsbRequestType.TypeStandard |
                                   (byte)UsbRequestRecipient.RecipInterface;
            setupPkt.Request = (byte)StandardRequest.GetInterface;
            setupPkt.Value = 0;
            setupPkt.Index = interfaceID;
            setupPkt.Length = 1;

            uTransferLength = this.ControlTransfer(setupPkt, buf, 0, buf.Length);
            if (uTransferLength == 1)
            {
                selectedAltInterfaceID = buf[0];
            }
            else
            {
                selectedAltInterfaceID = 0;
            }
        }

        /// <summary>
        /// Releases an interface that was previously claimed with <see cref="ClaimInterface"/>.
        /// </summary>
        /// <param name="interfaceID">The interface to release.</param>
        /// <returns>True on success.</returns>
        public bool ReleaseInterface(int interfaceID)
        {
            this.EnsureOpen();

            var ret = NativeImport.ReleaseInterface(this.deviceHandle, interfaceID);
            this.mClaimedInterfaces.Remove(interfaceID);
            ret.ThrowOnError();
            return true;
        }

        /// <summary>
        /// Sets an alternate interface for the most recent claimed interface.
        /// </summary>
        /// <param name="alternateID">The alternate interface to select for the most recent claimed interface See <see cref="ClaimInterface"/>.</param>
        /// <returns>True on success.</returns>
        public bool SetAltInterface(int interfaceID, int alternateID)
        {
            this.EnsureOpen();

            NativeImport.SetInterfaceAltSetting(this.deviceHandle, interfaceID, alternateID).ThrowOnError();
            this.usbAltInterfaceSettings[interfaceID & (UsbConstants.MaxDeviceCount - 1)] = (byte)alternateID;
            return true;
        }

        /// <summary>
        /// Sets an alternate interface for the most recent claimed interface.
        /// </summary>
        /// <param name="alternateID">The alternate interface to select for the most recent claimed interface See <see cref="ClaimInterface"/>.</param>
        /// <returns>True on success.</returns>
        public bool SetAltInterface(int alternateID)
        {
            if (this.mClaimedInterfaces.Count == 0)
            {
                throw new UsbException("You must claim an interface before setting an alternate interface.");
            }

            return this.SetAltInterface(this.mClaimedInterfaces[this.mClaimedInterfaces.Count - 1], alternateID);
        }


        /// <summary>
        /// Creates a clone of this device.
        /// </summary>
        /// <returns>
        /// A new <see cref="UsbDevice"/> which represents a clone of this device.
        /// </returns>
        /// 


        public LibUsbDeviceHandle DeviceHandle
        {
            get { return this.deviceHandle; }
        }

        /// <summary>
        /// Gets a value indicating whether the device has been opened. You can perform I/O on a
        /// device when it is open.
        /// </summary>
        public bool IsOpen
        {
            get
            {
                return ((this.deviceHandle != null) && (this.deviceHandle != LibUsbDeviceHandle.Zero));
            }
        }


        public int Configuration
        {
            get
            {
                this.EnsureNotDisposed();
                this.EnsureOpen();

                int config = 0;
                NativeImport.GetConfiguration(this.deviceHandle, ref config).ThrowOnError();
                return config;
            }
        }

        /// <inheritdoc/>
        public void SetConfiguration(int config)
        {
            this.EnsureNotDisposed();
            this.EnsureOpen();

            NativeImport.SetConfiguration(this.deviceHandle, config).ThrowOnError();
        }


        public IUsbDevice Clone()
        {
            return new UsbDevice(NativeImport.RefDevice(this.device));
        }

        /// <inheritdoc/>
        object ICloneable.Clone()
        {
            return this.Clone();
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (!this.disposed)
            {
                // Close the libusb_device_handle if required.
                this.Close();

                // Close the libusb_device handle.
                this.device.Dispose();

                this.disposed = true;
            }
        }



        /// <summary>
        /// Gets the device descriptor for this device.
        /// </summary>
        public unsafe UsbDeviceInfo Descriptor
        {
            get
            {
                this.EnsureNotDisposed();

                if ((this.descriptor == null || (this.descriptor.ProductId == 0 && this.descriptor.VendorId == 0)) && (this.device != null) )
                {
                    DeviceDescriptor descriptor;
                    NativeImport.GetDeviceDescriptor(this.device, &descriptor).ThrowOnError();
                    this.descriptor = UsbDeviceInfo.FromUsbDeviceDescriptor(this, descriptor);
                }

                return this.descriptor;
            }
        }

        /// <inheritdoc/>
        public UsbDeviceInfo Info => this.Descriptor;

        /// <inheritdoc/>
        public ushort VendorId => this.Descriptor.VendorId;

        /// <inheritdoc/>
        public ushort ProductId => this.Descriptor.ProductId;

        public ReadOnlyCollection<UsbConfigInfo> Configs
        {
            get
            {
                return this.Descriptor.Configurations;
            }
        }

        /// <summary>
        /// Gets the USB configuration descriptor for the currently active configuration.
        /// </summary>
        public unsafe UsbConfigInfo ActiveConfigDescriptor
        {
            get
            {
                this.EnsureNotDisposed();

                ConfigDescriptor* list = null;
                UsbConfigInfo value = null;

                try
                {
                    NativeImport.GetActiveConfigDescriptor(this.device, &list).ThrowOnError();
                    value = UsbConfigInfo.FromUsbConfigDescriptor(this, list[0]);
                    return value;
                }
                finally
                {
                    if (list != null)
                    {
                        NativeImport.FreeConfigDescriptor(list);
                    }
                }
            }
        }

        /// <summary>
        /// Get the number of the bus that a device is connected to.
        /// </summary>
        public byte BusNumber
        {
            get
            {
                return NativeImport.GetBusNumber(this.device);
            }
        }

        /// <summary>
        /// Gets the number of the port that a device is connected to.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Unless the OS does something funky, or you are hot-plugging USB extension cards, the port number returned by this
        /// call is usually guaranteed to be uniquely tied to a physical port, meaning that different devices
        /// plugged on the same physical port should return the same port number.
        /// </para>
        /// <para>
        /// But outside of this, there is no guarantee that the port number returned by this call will remain the same,
        /// or even match the order in which ports have been numbered by the HUB/HCD manufacturer.
        /// </para>
        /// </remarks>
        public byte PortNumber
        {
            get
            {
                return NativeImport.GetPortNumber(this.device);
            }
        }

        /// <summary>
        /// Gets the list of all port numbers from root for the specified device.
        /// </summary>
        public unsafe List<byte> PortNumbers
        {
            get
            {
                byte[] portNumbers = new byte[8];

                fixed (byte* ptr = portNumbers)
                {
                    NativeImport.GetPortNumbers(this.device, ptr, portNumbers.Length).ThrowOnError();
                }

                return new List<byte>(portNumbers);
            }
        }

        /// <summary>
        /// Get the the parent from the specified device.
        /// </summary>
        /// <returns>
        /// The device parent or <see langword="null"/> if not available
        /// </returns>
        public UsbDevice GetParent()
        {
            var parent = NativeImport.GetParent(this.device);

            if (parent == LibUsbDevice.Zero)
            {
                return null;
            }
            else
            {
                return new UsbDevice(parent);
            }
        }

        /// <summary>
        /// Gets the address of the device on the bus it is connected to.
        /// </summary>
        public byte Address
        {
            get
            {
                return NativeImport.GetDeviceAddress(this.device);
            }
        }

        /// <summary>
        /// Get the negotiated connection speed for a device.
        /// </summary>
        public int Speed
        {
            get
            {
                return NativeImport.GetDeviceSpeed(this.device);
            }
        }



        public bool IsKernelDriverActive(int InterfaceNumber)
        {
            return (NativeImport.KernelDriverActive(this.DeviceHandle, InterfaceNumber) != 0);
        }


        public void DetachKernelDriver(int InterfaceNumber)
        {
            NativeImport.DetachKernelDriver(this.DeviceHandle, InterfaceNumber).ThrowOnError();
        }


        public void SetAutoDetachKernelDriver(bool enable)
        {
            NativeImport.SetAutoDetachKernelDriver(this.DeviceHandle, (int)((enable == true) ? 1 : 0)).ThrowOnError();
        }

        /// <summary>
        /// Gets the <c>wMaxPacketSize</c> value for a particular endpoint in the active device configuration.
        /// </summary>
        /// <param name="endPoint">
        /// The address of the endpoint in question
        /// </param>
        /// <returns>
        /// The <c>wMaxPacketSize</c> value
        /// </returns>
        /// <remarks>
        /// This function was originally intended to be of assistance when setting up isochronous transfers,
        /// but a design mistake resulted in this function instead. It simply returns the <c>wMaxPacketSize</c>
        /// value without considering its contents. If you're dealing with isochronous transfers, you
        /// probably want libusb_get_max_iso_packet_size() instead.
        /// </remarks>
        public int GetMaxPacketSize(byte endPoint)
        {
            return NativeImport.GetMaxPacketSize(this.device, endPoint);
        }

        /// <summary>
        /// Calculate the maximum packet size which a specific endpoint is capable is sending or receiving
        /// in the duration of 1 microframe.
        /// </summary>
        /// <param name="endPoint">
        /// </param>
        /// <returns></returns>
        /// <remarks>
        /// <para>
        /// Only the active configuration is examined. The calculation is based on the <c>wMaxPacketSize</c> field in
        /// the endpoint descriptor as described in section 9.6.6 in the USB 2.0 specifications.
        /// </para>
        /// <para>
        /// If acting on an isochronous or interrupt endpoint, this function will multiply the value found in bits
        /// 0:10 by the number of transactions per microframe (determined by bits 11:12). Otherwise, this function
        /// just returns the numeric value found in bits 0:10.
        /// </para>
        /// <para>
        /// This function is useful for setting up isochronous transfers, for example you might pass the return value from
        /// this function to libusb_set_iso_packet_lengths() in order to set the length field of every isochronous
        /// packet in a transfer.
        /// </para>
        /// </remarks>
        public int GetMaxIsoPacketSize(byte endPoint)
        {
            return NativeImport.GetMaxIsoPacketSize(this.device, endPoint);
        }

        /// <summary>
        /// Get a USB configuration descriptor based on its index.
        /// </summary>
        /// <param name="configIndex">
        /// The index of the configuration you wish to retrieve
        /// </param>
        /// <returns>
        /// The requested descriptor.
        /// </returns>
        public UsbConfigInfo GetConfigDescriptor(byte configIndex)
        {
            if (this.TryGetConfigDescriptor(configIndex, out UsbConfigInfo descriptor))
            {
                return descriptor;
            }
            else
            {
                throw new UsbException(LibUsbError.NotFound);
            }
        }

        /// <summary>
        /// Attempts to get a USB configuration descriptor based on its index.
        /// </summary>
        /// <param name="configIndex">
        /// The index of the configuration you wish to retrieve
        /// </param>
        /// <param name="descriptor">
        /// The requested descriptor.
        /// </param>
        /// <returns>
        /// <see langword="true"/> if the descriptor could be loaded correctly; otherwise,
        /// <see langword="false">.
        /// </returns>
        public unsafe bool TryGetConfigDescriptor(byte configIndex, out UsbConfigInfo descriptor)
        {
            this.EnsureNotDisposed();

            ConfigDescriptor* list = null;
            UsbConfigInfo value = null;

            try
            {
                var ret = NativeImport.GetConfigDescriptor(this.device, configIndex, &list);

                if (ret == LibUsbError.NotFound)
                {
                    descriptor = null;
                    return false;
                }

                ret.ThrowOnError();
                //Console.WriteLine("UsbDevice.TryGetConfigDescriptor : " + list[0].ToString());
                value = UsbConfigInfo.FromUsbConfigDescriptor(this, list[0]);
                descriptor = value;
                return true;
            }
            finally
            {
                if (list != null)
                {
                    NativeImport.FreeConfigDescriptor(list);
                }
            }
        }



        /// <inheritdoc/>
        public override string ToString()
        {
            if (this.IsOpen)
            {
                return this.Descriptor.ToString();
            }
            else
            {
                return $"PID 0x{this.ProductId:X} - VID: 0x{this.VendorId:X}";
            }
        }

        /// <summary>
        /// Throws a <see cref="ObjectDisposedException"/> if this device has been disposed of.
        /// </summary>
        protected void EnsureNotDisposed()
        {
            if (this.disposed)
            {
                throw new ObjectDisposedException(nameof(UsbDevice));
            }
        }


        public unsafe string GetStringDescriptor(byte descriptorIndex, bool failSilently = false)
        {
            if (failSilently && !this.IsOpen)
            {
                return null;
            }

            this.EnsureNotDisposed();
            this.EnsureOpen();

            if (descriptorIndex == 0)
            {
                return null;
            }

            byte[] buffer = new byte[1024];

            fixed (byte* ptr = &buffer[0])
            {
                //Console.WriteLine("Got GetStringDescriptorAscii " + ((IsOpen == true) ? "Open" : "Closed") + " deviceHandle=" + this.deviceHandle.ToString());

                var length = (int)NativeImport.GetStringDescriptorAscii(this.deviceHandle, descriptorIndex, ptr, buffer.Length);

                if (length < 0)
                {
                    if (failSilently)
                    {
                        return null;
                    }
                    else
                    {
                        ((LibUsbError)length).ThrowOnError();
                    }
                }

                return Encoding.ASCII.GetString(buffer, 0, length);
            }
        }

        /// <inheritdoc/>
        public unsafe int ControlTransfer(UsbSetupPacket setupPacket)
        {
            return this.ControlTransfer(setupPacket, null, 0, 0);
        }

        /// <inheritdoc/>
        public unsafe int ControlTransfer(UsbSetupPacket setupPacket, byte[] buffer, int offset, int length)
        {
            this.EnsureNotDisposed();
            this.EnsureOpen();

            int result = 0;

            if (length > 0)
            {
                fixed (byte* data = &buffer[0])
                {
                    result = NativeImport.ControlTransfer(
                        this.deviceHandle,
                        setupPacket.RequestType,
                        setupPacket.Request,
                        (ushort)setupPacket.Value,
                        (ushort)setupPacket.Index,
                        data,
                        (ushort)length,
                        UsbConstants.DefaultTimeout);
                }
            }
            else
            {
                result = NativeImport.ControlTransfer(
                    this.deviceHandle,
                    setupPacket.RequestType,
                    setupPacket.Request,
                    (ushort)setupPacket.Value,
                    (ushort)setupPacket.Index,
                    null,
                    0,
                    UsbConstants.DefaultTimeout);
            }

            if (result >= 0)
            {
                return result;
            }
            else
            {
                throw new UsbException((LibUsbError)result);
            }
        }

        /// <inheritdoc/>
        public unsafe bool GetDescriptor(byte descriptorType, byte index, short langId, IntPtr buffer, int bufferLength, out int transferLength)
        {
            this.EnsureNotDisposed();
            this.EnsureOpen();

            int ret = NativeImport.ControlTransfer(
                this.deviceHandle,
                (byte)EndpointDirection.In,
                (byte)StandardRequest.GetDescriptor,
                (ushort)((descriptorType << 8) | index),
                0,
                (byte*)buffer.ToPointer(),
                (ushort)bufferLength,
                1000);

            if (ret < 0)
            {
                throw new UsbException((LibUsbError)ret);
            }

            transferLength = ret;
            return true;
        }

        /// <inheritdoc/>
        public bool GetDescriptor(byte descriptorType, byte index, short langId, object buffer, int bufferLength, out int transferLength)
        {
            using (PointerHandle p = new PointerHandle(buffer))
            {
                return this.GetDescriptor(descriptorType, index, langId, p.Handle, bufferLength, out transferLength);
            }
        }

        /// <inheritdoc/>
        public bool GetLangIDs(out short[] langIDs)
        {
            this.EnsureNotDisposed();
            this.EnsureOpen();

            LangStringDescriptor sd = new LangStringDescriptor(UsbDescriptor.Size + (16 * sizeof(short)));

            int ret;
            bool bSuccess = this.GetDescriptor((byte)DescriptorType.String, 0, 0, sd.Ptr, sd.MaxSize, out ret);
            bSuccess = sd.Get(out langIDs);
            sd.Free();
            return bSuccess;
        }

        /// <inheritdoc/>
        public bool GetString(out string stringData, short langId, byte stringIndex)
        {
            this.EnsureNotDisposed();
            this.EnsureOpen();

            stringData = null;
            int iTransferLength;
            LangStringDescriptor sd = new LangStringDescriptor(255);
            bool bSuccess = this.GetDescriptor((byte)DescriptorType.String, stringIndex, langId, sd.Ptr, sd.MaxSize, out iTransferLength);
            if (bSuccess && iTransferLength > UsbDescriptor.Size && sd.Length == iTransferLength)
            {
                bSuccess = sd.Get(out stringData);
            }

            return bSuccess;
        }

        /// <inheritdoc/>
        public void ResetDevice()
        {
            this.EnsureNotDisposed();
            this.EnsureOpen();

            NativeImport.ResetDevice(this.deviceHandle).ThrowOnError();
        }

        /// <summary>
        /// Opens a device, allowing you to perform I/O on this device.
        /// </summary>
        public void Open()
        {
            this.OpenNative().ThrowOnError();
        }

        /// <inheritdoc/>
        public bool TryOpen()
        {
            return this.OpenNative() == LibUsbError.Success;
        }

        /// <summary>
        /// Closes the device.
        /// </summary>
        public void Close()
        {
            this.EnsureNotDisposed();

            if (!this.IsOpen)
            {
                return;
            }

            if(this.deviceHandle != null)
                this.deviceHandle.Dispose();
            this.deviceHandle = null;
        }

        /// <summary>
        /// Throws a <see cref="UsbException"/> if the device is not open.
        /// </summary>
        protected void EnsureOpen()
        {
            if (!this.IsOpen)
            {
                throw new UsbException("The device has not been opened. You need to call Open() first.");
            }
        }


        public UsbEndpointReader OpenEndpointReader(ReadEndpointID readEndpointID)
        {
            return this.OpenEndpointReader(readEndpointID, UsbEndpointReader.DefReadBufferSize);
        }

        /// <summary>
        /// Opens a <see cref="EndpointType.Bulk"/> endpoint for reading
        /// </summary>
        /// <param name="readEndpointID">Endpoint number for read operations.</param>
        /// <param name="readBufferSize">Size of the read buffer allocated for the <see cref="UsbEndpointReader.DataReceived"/> event.</param>
        /// <returns>A <see cref="UsbEndpointReader"/> class ready for reading. If the specified endpoint is already been opened, the original <see cref="UsbEndpointReader"/> class is returned.</returns>
        public UsbEndpointReader OpenEndpointReader(ReadEndpointID readEndpointID, int readBufferSize)
        {
            return this.OpenEndpointReader(readEndpointID, readBufferSize, EndpointType.Bulk);
        }

        /// <summary>
        /// Opens an endpoint for reading
        /// </summary>
        /// <param name="readEndpointID">Endpoint number for read operations.</param>
        /// <param name="readBufferSize">Size of the read buffer allocated for the <see cref="UsbEndpointReader.DataReceived"/> event.</param>
        /// <param name="endpointType">The type of endpoint to open.</param>
        /// <returns>A <see cref="UsbEndpointReader"/> class ready for reading. If the specified endpoint is already been opened, the original <see cref="UsbEndpointReader"/> class is returned.</returns>
        public UsbEndpointReader OpenEndpointReader(ReadEndpointID readEndpointID, int readBufferSize, EndpointType endpointType)
        {
            byte altIntefaceID = this.mClaimedInterfaces.Count == 0 ? this.usbAltInterfaceSettings[0] : this.usbAltInterfaceSettings[this.mClaimedInterfaces[this.mClaimedInterfaces.Count - 1]];

            return new UsbEndpointReader(this, readBufferSize, altIntefaceID, readEndpointID, endpointType);
        }

        /// <summary>
        /// Opens a <see cref="EndpointType.Bulk"/> endpoint for writing
        /// </summary>
        /// <param name="writeEndpointID">Endpoint number for read operations.</param>
        /// <returns>A <see cref="UsbEndpointWriter"/> class ready for writing. If the specified endpoint is already been opened, the original <see cref="UsbEndpointWriter"/> class is returned.</returns>
        public UsbEndpointWriter OpenEndpointWriter(WriteEndpointID writeEndpointID)
        {
            return this.OpenEndpointWriter(writeEndpointID, EndpointType.Bulk);
        }

        /// <summary>
        /// Opens an endpoint for writing
        /// </summary>
        /// <param name="writeEndpointID">Endpoint number for read operations.</param>
        /// <param name="endpointType">The type of endpoint to open.</param>
        /// <returns>A <see cref="UsbEndpointWriter"/> class ready for writing. If the specified endpoint is already been opened, the original <see cref="UsbEndpointWriter"/> class is returned.</returns>
        public UsbEndpointWriter OpenEndpointWriter(WriteEndpointID writeEndpointID, EndpointType endpointType)
        {
            byte altIntefaceID = this.mClaimedInterfaces.Count == 0 ? this.usbAltInterfaceSettings[0] : this.usbAltInterfaceSettings[this.mClaimedInterfaces[this.mClaimedInterfaces.Count - 1]];

            return new UsbEndpointWriter(this, altIntefaceID, writeEndpointID, endpointType);
        }
        private LibUsbError OpenNative()
        {
            this.EnsureNotDisposed();

            if (this.IsOpen)
            {
                return LibUsbError.Success;
            }

            IntPtr deviceHandle = IntPtr.Zero;
            var ret = NativeImport.Open(this.device, ref deviceHandle);

            if (ret == LibUsbError.Success)
            {
                this.deviceHandle = LibUsbDeviceHandle.DangerousCreate(deviceHandle);
                this.descriptor = null;
            }

            return ret;
        }
    }
}
