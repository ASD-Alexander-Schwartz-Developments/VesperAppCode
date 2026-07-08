using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace VesperApp.Services
{
    /// <summary>
    /// Minimal dependency-free Win32 HID transport. Needed because a Microchip USB-HID
    /// bootloader is owned by Windows' hidusb.sys and cannot be opened through libusb (the
    /// app's normal USB layer). Enumerates a HID device by VID/PID and exchanges fixed-size
    /// reports with overlapped I/O + timeouts. Windows-only.
    /// </summary>
    [SupportedOSPlatform("windows")]
    internal sealed class Win32HidDevice : IHidReportTransport
    {
        private readonly SafeFileHandle _handle;
        public int InputReportLength { get; }
        public int OutputReportLength { get; }

        private Win32HidDevice(SafeFileHandle handle, int inLen, int outLen)
        {
            _handle = handle;
            InputReportLength = inLen;
            OutputReportLength = outLen;
        }

        // ── IHidReportTransport (payload-based; report-id byte handled here) ──
        public int ReportSize => OutputReportLength - 1;

        public void WriteReport(byte[] payload)
        {
            byte[] report = new byte[OutputReportLength]; // [0] = report id 0
            Array.Copy(payload, 0, report, 1, Math.Min(payload.Length, ReportSize));
            RawWrite(report);
        }

        public bool ReadReport(byte[] payload, int timeoutMs)
        {
            byte[] report = new byte[InputReportLength];
            if (!RawRead(report, timeoutMs)) return false;
            Array.Copy(report, 1, payload, 0, Math.Min(payload.Length, InputReportLength - 1));
            return true;
        }

        /// <summary>Opens the first present HID device matching <paramref name="vid"/>/<paramref name="pid"/>.</summary>
        public static Win32HidDevice? Open(int vid, int pid)
        {
            HidD_GetHidGuid(out Guid guid);
            IntPtr set = SetupDiGetClassDevs(ref guid, null, IntPtr.Zero, DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);
            if (set == INVALID_HANDLE_VALUE) return null;

            try
            {
                var ifData = new SP_DEVICE_INTERFACE_DATA();
                ifData.cbSize = Marshal.SizeOf<SP_DEVICE_INTERFACE_DATA>();

                for (uint i = 0; SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref guid, i, ref ifData); i++)
                {
                    string? path = GetDevicePath(set, ref ifData);
                    if (path == null) continue;

                    SafeFileHandle h = CreateFile(path, GENERIC_READ | GENERIC_WRITE,
                        FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, FILE_FLAG_OVERLAPPED, IntPtr.Zero);
                    if (h.IsInvalid) { h.Dispose(); continue; }

                    var attrs = new HIDD_ATTRIBUTES();
                    attrs.Size = Marshal.SizeOf<HIDD_ATTRIBUTES>();
                    if (HidD_GetAttributes(h, ref attrs) && attrs.VendorID == (ushort)vid && attrs.ProductID == (ushort)pid)
                    {
                        int inLen = 65, outLen = 65;
                        if (HidD_GetPreparsedData(h, out IntPtr pp))
                        {
                            if (HidP_GetCaps(pp, out HIDP_CAPS caps) == HIDP_STATUS_SUCCESS)
                            {
                                if (caps.InputReportByteLength > 0) inLen = caps.InputReportByteLength;
                                if (caps.OutputReportByteLength > 0) outLen = caps.OutputReportByteLength;
                            }
                            HidD_FreePreparsedData(pp);
                        }
                        return new Win32HidDevice(h, inLen, outLen);
                    }
                    h.Dispose();
                }
            }
            finally { SetupDiDestroyDeviceInfoList(set); }

            return null;
        }

        private static string? GetDevicePath(IntPtr set, ref SP_DEVICE_INTERFACE_DATA ifData)
        {
            SetupDiGetDeviceInterfaceDetail(set, ref ifData, IntPtr.Zero, 0, out int required, IntPtr.Zero);
            if (required <= 0) return null;

            IntPtr detail = Marshal.AllocHGlobal(required);
            try
            {
                // SP_DEVICE_INTERFACE_DETAIL_DATA.cbSize: 8 on 64-bit, 6 on 32-bit.
                Marshal.WriteInt32(detail, IntPtr.Size == 8 ? 8 : 6);
                if (!SetupDiGetDeviceInterfaceDetail(set, ref ifData, detail, required, out _, IntPtr.Zero))
                    return null;
                return Marshal.PtrToStringUni(detail + 4); // DevicePath follows the 4-byte cbSize
            }
            finally { Marshal.FreeHGlobal(detail); }
        }

        private void RawWrite(byte[] report)
        {
            using var ev = new ManualResetEvent(false);
            var ov = new NativeOverlapped { EventHandle = ev.SafeWaitHandle.DangerousGetHandle() };

            if (!WriteFile(_handle, report, report.Length, out _, ref ov))
            {
                if (Marshal.GetLastWin32Error() != ERROR_IO_PENDING)
                    throw new IOException("HID WriteFile failed (" + Marshal.GetLastWin32Error() + ").");
                if (!ev.WaitOne(2000)) { CancelIo(_handle); throw new IOException("HID write timed out."); }
                GetOverlappedResult(_handle, ref ov, out _, false);
            }
        }

        private bool RawRead(byte[] buffer, int timeoutMs)
        {
            using var ev = new ManualResetEvent(false);
            var ov = new NativeOverlapped { EventHandle = ev.SafeWaitHandle.DangerousGetHandle() };

            if (ReadFile(_handle, buffer, buffer.Length, out _, ref ov))
                return true;
            if (Marshal.GetLastWin32Error() != ERROR_IO_PENDING)
                return false;
            if (!ev.WaitOne(timeoutMs)) { CancelIo(_handle); return false; }
            return GetOverlappedResult(_handle, ref ov, out int read, false) && read > 0;
        }

        public void Dispose() => _handle.Dispose();

        // ───────────────────────── interop ─────────────────────────

        private const uint DIGCF_PRESENT = 0x02, DIGCF_DEVICEINTERFACE = 0x10;
        private const uint GENERIC_READ = 0x80000000, GENERIC_WRITE = 0x40000000;
        private const uint FILE_SHARE_READ = 0x1, FILE_SHARE_WRITE = 0x2;
        private const uint OPEN_EXISTING = 3, FILE_FLAG_OVERLAPPED = 0x40000000;
        private const int ERROR_IO_PENDING = 997;
        private const int HIDP_STATUS_SUCCESS = unchecked((int)0x00110000);
        private static readonly IntPtr INVALID_HANDLE_VALUE = new(-1);

        [StructLayout(LayoutKind.Sequential)]
        private struct SP_DEVICE_INTERFACE_DATA { public int cbSize; public Guid InterfaceClassGuid; public int Flags; public IntPtr Reserved; }

        [StructLayout(LayoutKind.Sequential)]
        private struct HIDD_ATTRIBUTES { public int Size; public ushort VendorID; public ushort ProductID; public ushort VersionNumber; }

        [StructLayout(LayoutKind.Sequential)]
        private struct HIDP_CAPS
        {
            public ushort Usage, UsagePage, InputReportByteLength, OutputReportByteLength, FeatureReportByteLength;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)] public ushort[] Reserved;
            public ushort NumberLinkCollectionNodes;
            public ushort NumberInputButtonCaps, NumberInputValueCaps, NumberInputDataIndices;
            public ushort NumberOutputButtonCaps, NumberOutputValueCaps, NumberOutputDataIndices;
            public ushort NumberFeatureButtonCaps, NumberFeatureValueCaps, NumberFeatureDataIndices;
        }

        [DllImport("hid.dll")] private static extern void HidD_GetHidGuid(out Guid hidGuid);
        [DllImport("hid.dll")] private static extern bool HidD_GetAttributes(SafeFileHandle device, ref HIDD_ATTRIBUTES attributes);
        [DllImport("hid.dll")] private static extern bool HidD_GetPreparsedData(SafeFileHandle device, out IntPtr preparsed);
        [DllImport("hid.dll")] private static extern bool HidD_FreePreparsedData(IntPtr preparsed);
        [DllImport("hid.dll")] private static extern int HidP_GetCaps(IntPtr preparsed, out HIDP_CAPS caps);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr SetupDiGetClassDevs(ref Guid classGuid, string? enumerator, IntPtr hwndParent, uint flags);
        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiEnumDeviceInterfaces(IntPtr set, IntPtr devInfoData, ref Guid interfaceClassGuid, uint memberIndex, ref SP_DEVICE_INTERFACE_DATA ifData);
        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr set, ref SP_DEVICE_INTERFACE_DATA ifData, IntPtr detail, int detailSize, out int requiredSize, IntPtr devInfoData);
        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateFile(string fileName, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadFile(SafeFileHandle handle, byte[] buffer, int count, out int read, ref NativeOverlapped overlapped);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteFile(SafeFileHandle handle, byte[] buffer, int count, out int written, ref NativeOverlapped overlapped);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetOverlappedResult(SafeFileHandle handle, ref NativeOverlapped overlapped, out int transferred, bool wait);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CancelIo(SafeFileHandle handle);
    }
}
