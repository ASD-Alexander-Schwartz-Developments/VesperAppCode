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

        [Browsable(false)]
        public override UInt32 FileSize
        {
            get { return this.file_size; }
            set { this.file_size = value; }
        }


        public override string ToString()
        {
            return "VT03-TPRH31";
        }
    }
}
