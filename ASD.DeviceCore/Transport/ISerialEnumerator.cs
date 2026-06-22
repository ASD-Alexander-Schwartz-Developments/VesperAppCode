using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using ASD.DeviceCore.Transport.Platform;

namespace ASD.DeviceCore.Transport
{
    /// <summary>
    /// Lists the system's serial ports with their USB identity (VID/PID/serial) without
    /// opening them. Platform-specific underneath (Windows SetupAPI, Linux sysfs, macOS
    /// IORegistry/device nodes), one interface on top.
    /// </summary>
    public interface ISerialEnumerator
    {
        IReadOnlyList<SerialPortInfo> List();
    }

    /// <summary>Picks the right <see cref="ISerialEnumerator"/> for the current OS.</summary>
    public static class SerialEnumerator
    {
        private static readonly ISerialEnumerator _default = Create();

        public static ISerialEnumerator Default => _default;

        public static IReadOnlyList<SerialPortInfo> List() => _default.List();

        private static ISerialEnumerator Create()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return new WindowsSerialEnumerator();
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return new LinuxSerialEnumerator();
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return new MacSerialEnumerator();
            return new FallbackSerialEnumerator();
        }
    }
}
