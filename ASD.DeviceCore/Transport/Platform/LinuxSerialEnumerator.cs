using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace ASD.DeviceCore.Transport.Platform
{
    /// <summary>
    /// Linux serial enumeration via sysfs. For each tty under <c>/sys/class/tty</c> that
    /// has a backing USB device, walks up the device tree to the USB node and reads
    /// <c>idVendor</c>/<c>idProduct</c>/<c>serial</c>/<c>manufacturer</c>/<c>product</c>.
    /// No port is opened. Returns <c>/dev/&lt;tty&gt;</c> as the port name.
    /// </summary>
    internal sealed class LinuxSerialEnumerator : ISerialEnumerator
    {
        private const string SysTty = "/sys/class/tty";

        public IReadOnlyList<SerialPortInfo> List()
        {
            var result = new List<SerialPortInfo>();
            if (!Directory.Exists(SysTty))
                return result;

            foreach (string ttyDir in Directory.EnumerateDirectories(SysTty))
            {
                string tty = Path.GetFileName(ttyDir);
                string deviceLink = Path.Combine(ttyDir, "device");
                if (!Directory.Exists(deviceLink) && !File.Exists(deviceLink))
                    continue; // virtual ports (tty, console, ...) have no backing device

                // Resolve the real device path and walk up looking for a USB node.
                string? usbDir = FindUsbParent(deviceLink);
                // Only surface USB-backed serial nodes (ttyACM*/ttyUSB*) or anything we
                // could resolve to a USB device — that's what ASD devices present as.
                bool isUsbTty = tty.StartsWith("ttyACM", StringComparison.Ordinal)
                                || tty.StartsWith("ttyUSB", StringComparison.Ordinal);
                if (usbDir is null && !isUsbTty)
                    continue;

                int? vid = ReadHex(usbDir, "idVendor");
                int? pid = ReadHex(usbDir, "idProduct");
                string? serial = ReadText(usbDir, "serial");
                string? manufacturer = ReadText(usbDir, "manufacturer");
                string? product = ReadText(usbDir, "product");

                result.Add(new SerialPortInfo(
                    PortName: "/dev/" + tty,
                    Vid: vid, Pid: pid, SerialNumber: serial,
                    Description: product, Manufacturer: manufacturer));
            }
            return result;
        }

        private static string? FindUsbParent(string deviceLink)
        {
            try
            {
                // device is a symlink into /sys/devices/...; the USB device dir is the
                // first ancestor that carries an idVendor file.
                string path = Path.GetFullPath(
                    Directory.ResolveLinkTarget(deviceLink, returnFinalTarget: true)?.FullName ?? deviceLink);
                for (int i = 0; i < 8 && !string.IsNullOrEmpty(path); i++)
                {
                    if (File.Exists(Path.Combine(path, "idVendor")))
                        return path;
                    path = Path.GetDirectoryName(path) ?? "";
                }
            }
            catch { /* unreadable sysfs entry */ }
            return null;
        }

        private static int? ReadHex(string? dir, string file)
        {
            string? s = ReadText(dir, file);
            return s != null && int.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int v)
                ? v : null;
        }

        private static string? ReadText(string? dir, string file)
        {
            if (dir is null) return null;
            try
            {
                string p = Path.Combine(dir, file);
                return File.Exists(p) ? File.ReadAllText(p).Trim() : null;
            }
            catch { return null; }
        }
    }
}
