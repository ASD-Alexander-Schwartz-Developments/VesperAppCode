using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;


namespace VesperApp.Models
{
    public class ConfigLEDDriver : ConfigurationDeviceDriver
    {
        public ConfigLEDDriver() : base("LED", "Basic LED control")
        {
            this.FileSize = 0;
        }

        public override string ToString()
        {
            return "LED";
        }
    }
}
