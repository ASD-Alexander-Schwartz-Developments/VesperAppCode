using System.ComponentModel;
using System.Text.Json;

namespace VesperApp.Models
{
    /// <summary>
    /// A config.json device entry whose "name" this VesperApp version does not know (a driver
    /// from newer firmware / a newer VesperApp). Kept verbatim: the configuration still opens and
    /// the entry is written back unchanged on save. It used to make the whole config.json fail to
    /// open (the converter returned null without consuming the object).
    /// </summary>
    public sealed class ConfigUnknownDriver : ConfigurationDeviceDriver
    {
        public ConfigUnknownDriver(string name, JsonElement raw)
            : base(name, "Sensor driver not known to this VesperApp version - kept unchanged")
        {
            Raw = raw;
        }

        /// <summary>The entry exactly as read (a detached clone of the JSON object).</summary>
        [Browsable(false)]
        public JsonElement Raw { get; }

        public override string ToString() => Name;
    }
}
