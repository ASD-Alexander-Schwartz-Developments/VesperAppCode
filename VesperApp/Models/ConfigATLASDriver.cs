using System.Text.Json;
using System.Text.Json.Serialization;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace VesperApp.Models
{
    
    public class ConfigATLASDriver : ConfigurationDeviceDriver
    {

        public ConfigATLASDriver() : base("ATLAS", "ATLAS system trasceiver")
        {

            this.MemoryBufferSize = 0;
            this.FileSize = 0;

        }


        [Browsable(false)]
        public override UInt32 FileSize
        {
            get { return this.file_size; }
            set { this.file_size = value; }
        }

        private UInt32 pingRate;

        [DisplayName("ATLAS Receiver/Transmitters ping rate (in ms)"),
        CategoryAttribute("ATLAS Receiver/Transmitters specific Settings")]
        [JsonPropertyName("pRate")]
        public UInt32 PingRate
        {
            get
            {
                return pingRate;
            }
            set
            {
                this.pingRate = value;
            }
        }

    }
}
