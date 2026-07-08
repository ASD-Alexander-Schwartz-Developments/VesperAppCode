namespace ASD.DeviceCore.Transport
{
    /// <summary>
    /// A serial port discovered on the system, together with the USB identity behind it
    /// (when it is a USB CDC/ACM device) — obtained <b>without opening the port</b>. This
    /// is what lets discovery filter to ASD devices by VID/PID instead of probing every
    /// COM port. <see cref="Vid"/>/<see cref="Pid"/>/<see cref="SerialNumber"/> are null
    /// for non-USB ports or where the platform can't supply them.
    /// </summary>
    public sealed record SerialPortInfo(
        string PortName,
        int? Vid = null,
        int? Pid = null,
        string? SerialNumber = null,
        string? Description = null,
        string? Manufacturer = null)
    {
        /// <summary>True when this port matches the given USB vendor/product id.</summary>
        public bool Matches(int vid, int pid) => Vid == vid && Pid == pid;

        public override string ToString()
        {
            string usb = Vid is int v && Pid is int p ? $" [{v:X4}:{p:X4}]" : "";
            string sn = string.IsNullOrEmpty(SerialNumber) ? "" : $" sn={SerialNumber}";
            return $"{PortName}{usb}{sn}{(Description is null ? "" : $" ({Description})")}";
        }
    }
}
