using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.ComponentModel;

namespace VesperApp.Models
{

    public class ConfigALS3001DDriver : ConfigurationDeviceDriver
    {
        public ConfigALS3001DDriver() : base("ALS3001D", "Huaman Eye calibrated response ambient light sensor")
        {
            this.MemoryBufferSize = 512;
            this.FileSize = 512 * 1024;
        }
    }
}
