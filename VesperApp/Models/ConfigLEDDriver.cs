using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;


namespace VesperApp.Models
{
    public class ConfigLEDDriver : ConfigurationDeviceDriver
    {
        public ConfigLEDDriver() : base("LED", "LED heartbeat while recording: one flash every Sample Rate ms (Window Length = flash length in ms, 30 ms if 0). Sample Rate 0 with Window Rate and Length set: on for Window Length every Window Rate. VesperU5 1.22-pre.22+")
        {
            this.FileSize = 0;
        }

        public override string ToString()
        {
            return "LED";
        }
    }
}
