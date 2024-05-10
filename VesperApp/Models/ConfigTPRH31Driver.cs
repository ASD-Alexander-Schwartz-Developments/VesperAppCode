using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;



namespace VesperApp.Models
{
    public class ConfigTPRH31Driver : ConfigurationDeviceDriver
    {
        public ConfigTPRH31Driver() : base("TPRH31", "Temperature and relative humidity")
        {
            this.FileSize = 0;
        }
    }
}
