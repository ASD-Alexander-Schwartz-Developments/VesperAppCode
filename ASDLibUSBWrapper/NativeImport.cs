namespace ASDLibUSBWrapper
{
    internal static unsafe class NativeImport
    {
        /// <summary>
        /// Use the default struct alignment for this platform.
        /// </summary>
        internal const int Pack = 0;

        public const string LibUsbNativeLibrary = "libusb-1.0";

        static NativeImport()
        {
            //NativeLibraryResolver.EnsureRegistered();
            NativeLibrary.SetDllImportResolver(typeof(NativeImport).Assembly, DllImportResolver);
        }




        private static IntPtr DllImportResolver(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
        {
            if (libraryName != NativeImport.LibUsbNativeLibrary)
            {
                return IntPtr.Zero;
            }

            IntPtr lib;
            string nativeLibraryName;

            // Library names for the various platforms:
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                nativeLibraryName = "libusb-1.0.dll";

                if (RuntimeInformation.ProcessArchitecture == Architecture.X64)
                    nativeLibraryName = "LibUsb/Win/x64/" + nativeLibraryName;
                else
                    nativeLibraryName = "LibUsb/Win/x86/" + nativeLibraryName;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                nativeLibraryName = "libusb-1.0.so.0";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                nativeLibraryName = "LibUsb/macos/libusb-1.0.0.dylib";
            }
            else
            {
                return IntPtr.Zero;
            }


            // First, attempt to load the native library from the NuGet packages
            var nativeSearchDirectories = AppContext.GetData("NATIVE_DLL_SEARCH_DIRECTORIES") as string;
            var delimiter = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ";" : ":";

            if (nativeSearchDirectories != null)
            {
                foreach (var directory in nativeSearchDirectories.Split(delimiter))
                {
                    var path = Path.Combine(directory, nativeLibraryName);
                    if (NativeLibrary.TryLoad(path, out lib))
                    {
                        return lib;
                    }
                }
            }

            // Next, try to load any OS-provided version of the library
            if (NativeLibrary.TryLoad(nativeLibraryName, out lib))
            {
                return lib;
            }

            return IntPtr.Zero;
        }



        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_get_device_list")]
        public static extern IntPtr GetDeviceList(LibUsbContext ctx, IntPtr** list);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_free_device_list")]
        public static extern IntPtr FreeDeviceList(IntPtr* list, int unrefDevices);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_init")]
        public static extern LibUsbError Init(ref IntPtr ctx);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_exit")]
        public static extern void Exit(IntPtr ctx);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_set_debug")]
        public static extern void SetDebug(LibUsbContext ctx, int level);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_get_version")]
        public static extern Version* GetVersion();

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_has_capability")]
        public static extern int HasCapability(uint capability);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_error_name")]
        public static extern IntPtr ErrorName(int errcode);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_setlocale")]
        public static extern LibUsbError SetLocale(IntPtr locale);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_strerror")]
        public static extern IntPtr StrError(LibUsbError errcode);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_get_device_list")]
        public static extern IntPtr GetDeviceList(LibUsbContext ctx, ref IntPtr list);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_free_device_list")]
        public static extern void FreeDeviceList(ref IntPtr list, int unrefDevices);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_ref_device")]
        public static extern LibUsbDevice RefDevice(LibUsbDevice dev);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_unref_device")]
        public static extern void UnrefDevice(IntPtr dev);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_get_configuration")]
        public static extern LibUsbError GetConfiguration(LibUsbDeviceHandle dev, ref int config);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_get_device_descriptor")]
        public static extern LibUsbError GetDeviceDescriptor(LibUsbDevice dev, DeviceDescriptor* desc);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_get_active_config_descriptor")]
        public static extern LibUsbError GetActiveConfigDescriptor(LibUsbDevice dev, ConfigDescriptor** config);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_get_config_descriptor")]
        public static extern LibUsbError GetConfigDescriptor(LibUsbDevice dev, byte configIndex, ConfigDescriptor** config);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_get_config_descriptor_by_value")]
        public static extern LibUsbError GetConfigDescriptorByValue(LibUsbDevice dev, byte bconfigurationvalue, ConfigDescriptor** config);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_free_config_descriptor")]
        public static extern void FreeConfigDescriptor(ConfigDescriptor* config);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_get_ss_endpoint_companion_descriptor")]
        public static extern LibUsbError GetSsEndpointCompanionDescriptor(ref LibUsbContext ctx, EndpointDescriptor* endpoint, SsEndpointCompanionDescriptor** epComp);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_free_ss_endpoint_companion_descriptor")]
        public static extern void FreeSsEndpointCompanionDescriptor(SsEndpointCompanionDescriptor* epComp);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_get_bos_descriptor")]
        public static extern LibUsbError GetBosDescriptor(LibUsbDeviceHandle devHandle, BosDescriptor** bos);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_free_bos_descriptor")]
        public static extern void FreeBosDescriptor(BosDescriptor* bos);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_get_usb_2_0_extension_descriptor")]
        public static extern LibUsbError GetUsb20ExtensionDescriptor(ref LibUsbContext ctx, BosDevCapabilityDescriptor* devCap, Usb20ExtensionDescriptor** usb20Extension);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_free_usb_2_0_extension_descriptor")]
        public static extern void FreeUsb20ExtensionDescriptor(Usb20ExtensionDescriptor* usb20Extension);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_get_ss_usb_device_capability_descriptor")]
        public static extern LibUsbError GetSsUsbDeviceCapabilityDescriptor(ref LibUsbContext ctx, BosDevCapabilityDescriptor* devCap, SsUsbDeviceCapabilityDescriptor** ssUsbDeviceCap);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_free_ss_usb_device_capability_descriptor")]
        public static extern void FreeSsUsbDeviceCapabilityDescriptor(SsUsbDeviceCapabilityDescriptor* ssUsbDeviceCap);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_get_container_id_descriptor")]
        public static extern LibUsbError GetContainerIdDescriptor(ref LibUsbContext ctx, BosDevCapabilityDescriptor* devCap, ContainerIdDescriptor** containerId);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_free_container_id_descriptor")]
        public static extern void FreeContainerIdDescriptor(ContainerIdDescriptor* containerId);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_get_bus_number")]
        public static extern byte GetBusNumber(LibUsbDevice dev);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_get_port_number")]
        public static extern byte GetPortNumber(LibUsbDevice dev);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_get_port_numbers")]
        public static extern LibUsbError GetPortNumbers(LibUsbDevice dev, byte* portNumbers, int portNumbersLen);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_get_port_path")]
        public static extern LibUsbError GetPortPath(LibUsbContext ctx, LibUsbDevice dev, byte* path, byte pathLength);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_get_parent")]
        public static extern LibUsbDevice GetParent(LibUsbDevice dev);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_get_device_address")]
        public static extern byte GetDeviceAddress(LibUsbDevice dev);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_get_device_speed")]
        public static extern int GetDeviceSpeed(LibUsbDevice dev);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_get_max_packet_size")]
        public static extern int GetMaxPacketSize(LibUsbDevice dev, byte endpoint);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_get_max_iso_packet_size")]
        public static extern int GetMaxIsoPacketSize(LibUsbDevice dev, byte endpoint);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_open")]
        public static extern LibUsbError Open(LibUsbDevice dev, ref IntPtr devHandle);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_close")]
        public static extern void Close(IntPtr devHandle);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_get_device")]
        public static extern LibUsbDevice GetDevice(LibUsbDeviceHandle devHandle);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_set_configuration")]
        public static extern LibUsbError SetConfiguration(LibUsbDeviceHandle devHandle, int configuration);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_claim_interface")]
        public static extern LibUsbError ClaimInterface(LibUsbDeviceHandle devHandle, int interfaceNumber);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_release_interface")]
        public static extern LibUsbError ReleaseInterface(LibUsbDeviceHandle devHandle, int interfaceNumber);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_open_device_with_vid_pid")]
        public static extern LibUsbDeviceHandle OpenDeviceWithVidPid(LibUsbContext ctx, ushort vendorId, ushort productId);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_set_interface_alt_setting")]
        public static extern LibUsbError SetInterfaceAltSetting(LibUsbDeviceHandle devHandle, int interfaceNumber, int alternateSetting);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_clear_halt")]
        public static extern LibUsbError ClearHalt(LibUsbDeviceHandle devHandle, byte endpoint);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_reset_device")]
        public static extern LibUsbError ResetDevice(LibUsbDeviceHandle devHandle);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_alloc_streams")]
        public static extern LibUsbError AllocStreams(LibUsbDeviceHandle devHandle, uint numStreams, byte* endpoints, int numEndpoints);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_free_streams")]
        public static extern LibUsbError FreeStreams(LibUsbDeviceHandle devHandle, byte* endpoints, int numEndpoints);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_dev_mem_alloc")]
        public static extern byte* DevMemAlloc(LibUsbDeviceHandle devHandle, UIntPtr length);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_dev_mem_free")]
        public static extern LibUsbError DevMemFree(LibUsbDeviceHandle devHandle, byte* buffer, UIntPtr length);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_kernel_driver_active")]
        public static extern int KernelDriverActive(LibUsbDeviceHandle devHandle, int interfaceNumber);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_detach_kernel_driver")]
        public static extern LibUsbError DetachKernelDriver(LibUsbDeviceHandle devHandle, int interfaceNumber);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_attach_kernel_driver")]
        public static extern LibUsbError AttachKernelDriver(LibUsbDeviceHandle devHandle, int interfaceNumber);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_set_auto_detach_kernel_driver")]
        public static extern LibUsbError SetAutoDetachKernelDriver(LibUsbDeviceHandle devHandle, int enable);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_alloc_transfer")]
        public static extern Transfer* AllocTransfer(int isoPackets);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_submit_transfer")]
        public static extern LibUsbError SubmitTransfer(Transfer* transfer);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_cancel_transfer")]
        public static extern LibUsbError CancelTransfer(Transfer* transfer);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_free_transfer")]
        public static extern void FreeTransfer(Transfer* transfer);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_transfer_set_stream_id")]
        public static extern void TransferSetStreamId(Transfer* transfer, uint streamId);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_transfer_get_stream_id")]
        public static extern uint TransferGetStreamId(Transfer* transfer);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_control_transfer")]
        public static extern int ControlTransfer(LibUsbDeviceHandle devHandle, byte requestType, byte brequest, ushort wvalue, ushort windex, byte* data, ushort wlength, uint timeout);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_bulk_transfer")]
        public static extern LibUsbError BulkTransfer(LibUsbDeviceHandle devHandle, byte endpoint, byte* data, int length, ref int actualLength, uint timeout);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_interrupt_transfer")]
        public static extern LibUsbError InterruptTransfer(LibUsbDeviceHandle devHandle, byte endpoint, byte* data, int length, ref int actualLength, uint timeout);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_get_string_descriptor_ascii")]
        public static extern LibUsbError GetStringDescriptorAscii(LibUsbDeviceHandle devHandle, byte descIndex, byte* data, int length);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_try_lock_events")]
        public static extern int TryLockEvents(LibUsbContext ctx);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_lock_events")]
        public static extern void LockEvents(LibUsbContext ctx);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_unlock_events")]
        public static extern void UnlockEvents(LibUsbContext ctx);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_event_handling_ok")]
        public static extern int EventHandlingOk(LibUsbContext ctx);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_event_handler_active")]
        public static extern int EventHandlerActive(LibUsbContext ctx);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_interrupt_event_handler")]
        public static extern void InterruptEventHandler(LibUsbContext ctx);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_lock_event_waiters")]
        public static extern void LockEventWaiters(LibUsbContext ctx);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_unlock_event_waiters")]
        public static extern void UnlockEventWaiters(LibUsbContext ctx);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_wait_for_event")]
        public static extern int WaitForEvent(LibUsbContext ctx, ref UnixTimeval tv);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_handle_events_timeout")]
        public static extern LibUsbError HandleEventsTimeout(LibUsbContext ctx, ref UnixTimeval tv);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_handle_events_timeout_completed")]
        public static extern LibUsbError HandleEventsTimeoutCompleted(LibUsbContext ctx, ref UnixTimeval tv, ref int completed);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_handle_events")]
        public static extern LibUsbError HandleEvents(LibUsbContext ctx);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_handle_events_completed")]
        public static extern LibUsbError HandleEventsCompleted(LibUsbContext ctx, ref int completed);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_handle_events_locked")]
        public static extern LibUsbError HandleEventsLocked(LibUsbContext ctx, ref UnixTimeval tv);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_pollfds_handle_timeouts")]
        public static extern LibUsbError PollfdsHandleTimeouts(LibUsbContext ctx);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_get_next_timeout")]
        public static extern LibUsbError GetNextTimeout(LibUsbContext ctx, ref UnixTimeval tv);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_get_pollfds")]
        public static extern PollFileDescriptor** GetPollfds(LibUsbContext ctx);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_free_pollfds")]
        public static extern void FreePollfds(PollFileDescriptor** pollfds);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_set_pollfd_notifiers")]
        public static extern void SetPollfdNotifiers(LibUsbContext ctx, IntPtr addedDelegate, IntPtr removedDelegate, IntPtr userData);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_hotplug_register_callback")]
        public static extern LibUsbError HotplugRegisterCallback(LibUsbContext ctx, UsbHotplugEvent events, UsbHotplugFlag flags, int vendorId, int productId, int devClass, IntPtr Delegate, IntPtr userData, ref int callbackHandle);

        [DllImport(LibUsbNativeLibrary, EntryPoint = "libusb_hotplug_deregister_callback")]
        public static extern void HotplugDeregisterCallback(LibUsbContext ctx, int callbackHandle);
    }
}