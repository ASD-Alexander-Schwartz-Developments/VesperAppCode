using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace ASD.DeviceCore.Transport.Platform
{
    /// <summary>
    /// macOS serial enumeration. Lists the USB-CDC callout device nodes
    /// (<c>/dev/cu.usbmodem*</c>, <c>/dev/cu.usbserial*</c>) and best-effort enriches
    /// them with VID/PID/serial by parsing <c>ioreg</c> (IORegistry). If <c>ioreg</c>
    /// is unavailable, the nodes are still returned (VID/PID null) — the
    /// <c>usbmodem</c>/<c>usbserial</c> naming already restricts discovery to USB serial
    /// devices, which is far better than probing every port.
    /// </summary>
    internal sealed class MacSerialEnumerator : ISerialEnumerator
    {
        public IReadOnlyList<SerialPortInfo> List()
        {
            var nodes = new List<string>();
            try
            {
                foreach (string f in Directory.EnumerateFiles("/dev"))
                {
                    string name = Path.GetFileName(f);
                    if (name.StartsWith("cu.usbmodem", StringComparison.Ordinal) ||
                        name.StartsWith("cu.usbserial", StringComparison.Ordinal))
                        nodes.Add(f);
                }
            }
            catch { /* /dev not readable */ }

            IReadOnlyDictionary<string, (int vid, int pid, string? serial, string? product)> ioreg = TryReadIoReg();

            var result = new List<SerialPortInfo>(nodes.Count);
            foreach (string node in nodes)
            {
                // The IODialinDevice path ends in the same suffix as the /dev node.
                string suffix = Path.GetFileName(node).Replace("cu.", "", StringComparison.Ordinal);
                (int vid, int pid, string? serial, string? product) info = default;
                bool have = false;
                foreach (var kv in ioreg)
                    if (kv.Key.Contains(suffix, StringComparison.Ordinal)) { info = kv.Value; have = true; break; }

                result.Add(have
                    ? new SerialPortInfo(node, info.vid, info.pid, info.serial, info.product)
                    : new SerialPortInfo(node));
            }
            return result;
        }

        // Map IODialinDevice -> (vid, pid, serial, product) by parsing `ioreg -r -c IOSerialBSDClient -l`.
        private static IReadOnlyDictionary<string, (int, int, string?, string?)> TryReadIoReg()
        {
            var map = new Dictionary<string, (int, int, string?, string?)>();
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo("ioreg",
                    "-r -c IOUSBHostDevice -l -w 0")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                using var p = System.Diagnostics.Process.Start(psi);
                if (p is null) return map;
                string text = p.StandardOutput.ReadToEnd();
                p.WaitForExit(3000);

                // Very light parse: pair up VID/PID/serial within a device block and key by serial.
                foreach (Match m in Regex.Matches(text,
                    "\"idVendor\"\\s*=\\s*(\\d+).*?\"idProduct\"\\s*=\\s*(\\d+).*?\"USB Serial Number\"\\s*=\\s*\"([^\"]*)\"",
                    RegexOptions.Singleline))
                {
                    int vid = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                    int pid = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
                    string serial = m.Groups[3].Value;
                    if (!string.IsNullOrEmpty(serial))
                        map[serial] = (vid, pid, serial, null);
                }
            }
            catch { /* ioreg missing or denied — fall back to node listing */ }
            return map;
        }
    }

    /// <summary>Last-resort enumerator for unknown platforms: port names only.</summary>
    internal sealed class FallbackSerialEnumerator : ISerialEnumerator
    {
        public IReadOnlyList<SerialPortInfo> List()
        {
            var list = new List<SerialPortInfo>();
            foreach (string p in System.IO.Ports.SerialPort.GetPortNames())
                list.Add(new SerialPortInfo(p));
            return list;
        }
    }
}
