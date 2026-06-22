using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Text.RegularExpressions;

namespace ASD.DeviceCore.Transport.Platform
{
    /// <summary>
    /// Windows serial enumeration via SetupAPI (the Ports device class). Reads each COM
    /// port's friendly name, hardware id (VID/PID) and instance id (serial) from the
    /// device registry without opening the port. No WMI / System.Management dependency.
    /// </summary>
    [SupportedOSPlatform("windows")]
    internal sealed class WindowsSerialEnumerator : ISerialEnumerator
    {
        private static readonly Guid GuidDevClassPorts =
            new("4d36e978-e325-11ce-bfc1-08002be10318");

        private const uint DIGCF_PRESENT = 0x02;
        private const uint SPDRP_DEVICEDESC = 0x00;
        private const uint SPDRP_HARDWAREID = 0x01;
        private const uint SPDRP_MFG = 0x0B;
        private const uint SPDRP_FRIENDLYNAME = 0x0C;

        public IReadOnlyList<SerialPortInfo> List()
        {
            var result = new List<SerialPortInfo>();
            Guid classGuid = GuidDevClassPorts;
            IntPtr h = SetupDiGetClassDevs(ref classGuid, IntPtr.Zero, IntPtr.Zero, DIGCF_PRESENT);
            if (h == IntPtr.Zero || h == new IntPtr(-1))
                return result;

            try
            {
                var data = new SP_DEVINFO_DATA { cbSize = (uint)Marshal.SizeOf<SP_DEVINFO_DATA>() };
                for (uint i = 0; SetupDiEnumDeviceInfo(h, i, ref data); i++)
                {
                    string? friendly = GetProp(h, ref data, SPDRP_FRIENDLYNAME);
                    string? portName = ExtractComName(friendly);
                    if (portName is null)
                        continue; // not a COM port

                    string? hardwareId = GetProp(h, ref data, SPDRP_HARDWAREID);
                    (int? vid, int? pid) = ParseVidPid(hardwareId);
                    string? serial = ExtractSerial(GetInstanceId(h, ref data));
                    string? mfg = GetProp(h, ref data, SPDRP_MFG);
                    string? desc = GetProp(h, ref data, SPDRP_DEVICEDESC) ?? friendly;

                    result.Add(new SerialPortInfo(portName, vid, pid, serial, desc, mfg));
                    data.cbSize = (uint)Marshal.SizeOf<SP_DEVINFO_DATA>();
                }
            }
            finally
            {
                SetupDiDestroyDeviceInfoList(h);
            }
            return result;
        }

        private static string? ExtractComName(string? friendly)
        {
            if (friendly is null) return null;
            Match m = Regex.Match(friendly, @"\((COM\d+)\)");
            return m.Success ? m.Groups[1].Value : null;
        }

        private static (int?, int?) ParseVidPid(string? hardwareId)
        {
            if (hardwareId is null) return (null, null);
            Match m = Regex.Match(hardwareId, @"VID_([0-9A-Fa-f]{4}).*?PID_([0-9A-Fa-f]{4})");
            if (!m.Success) return (null, null);
            return (int.Parse(m.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                    int.Parse(m.Groups[2].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture));
        }

        private static string? ExtractSerial(string? instanceId)
        {
            // e.g. USB\VID_10C4&PID_EA60\0001  -> last segment, when it isn't a composite (&) id.
            if (string.IsNullOrEmpty(instanceId)) return null;
            int slash = instanceId.LastIndexOf('\\');
            if (slash < 0 || slash + 1 >= instanceId.Length) return null;
            string last = instanceId[(slash + 1)..];
            return last.Contains('&') ? null : last;
        }

        private static string? GetProp(IntPtr h, ref SP_DEVINFO_DATA data, uint prop)
        {
            var buffer = new byte[1024];
            if (SetupDiGetDeviceRegistryProperty(h, ref data, prop, out _, buffer, (uint)buffer.Length, out uint size)
                && size > 0)
            {
                int len = Math.Min((int)size, buffer.Length);
                string s = Encoding.Unicode.GetString(buffer, 0, len).TrimEnd('\0');
                // REG_MULTI_SZ (hardware ids) — take the first entry.
                int nul = s.IndexOf('\0');
                if (nul >= 0) s = s[..nul];
                return string.IsNullOrWhiteSpace(s) ? null : s;
            }
            return null;
        }

        private static string? GetInstanceId(IntPtr h, ref SP_DEVINFO_DATA data)
        {
            var sb = new StringBuilder(512);
            return SetupDiGetDeviceInstanceId(h, ref data, sb, (uint)sb.Capacity, out _)
                ? sb.ToString() : null;
        }

        // ---- P/Invoke ----

        [StructLayout(LayoutKind.Sequential)]
        private struct SP_DEVINFO_DATA
        {
            public uint cbSize;
            public Guid ClassGuid;
            public uint DevInst;
            public IntPtr Reserved;
        }

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr SetupDiGetClassDevs(
            ref Guid ClassGuid, IntPtr Enumerator, IntPtr hwndParent, uint Flags);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiEnumDeviceInfo(
            IntPtr DeviceInfoSet, uint MemberIndex, ref SP_DEVINFO_DATA DeviceInfoData);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetupDiGetDeviceRegistryProperty(
            IntPtr DeviceInfoSet, ref SP_DEVINFO_DATA DeviceInfoData, uint Property,
            out uint PropertyRegDataType, byte[] PropertyBuffer, uint PropertyBufferSize, out uint RequiredSize);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetupDiGetDeviceInstanceId(
            IntPtr DeviceInfoSet, ref SP_DEVINFO_DATA DeviceInfoData,
            StringBuilder DeviceInstanceId, uint DeviceInstanceIdSize, out uint RequiredSize);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiDestroyDeviceInfoList(IntPtr DeviceInfoSet);
    }
}
