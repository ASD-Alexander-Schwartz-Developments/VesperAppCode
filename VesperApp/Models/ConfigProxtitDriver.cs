using System.Text.Json;
using System.Text.Json.Serialization;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace VesperApp.Models
{
    
    public class ConfigProxtitDriver : ConfigurationDeviceDriver
    {
        public ConfigProxtitDriver() : base("Proxtit", "Proxtit wireless trasceiver")
        {
            this.FileSize = 0;
        }


        [Browsable(false), JsonPropertyOrder(30)]
        public override UInt32 FileSize
        {
            get { return this.file_size; }
            set { this.file_size = value; }
        }


        private UInt32 advertiseRate;
        private UInt32 advertiseLength;

        [DisplayName("Ping Rate (in ms)"),
        CategoryAttribute("Proxtit specific Settings")]
        [JsonPropertyName("pingrate"), JsonPropertyOrder(20)]
        public UInt32 PingRate
        {
            get
            {
                return advertiseRate;
            }
            set
            {
                this.advertiseRate = value;
            }
        }

        [DisplayName("Ping Length (in ms)"),
        CategoryAttribute("Proxtit specific Settings")]
        [JsonPropertyName("pinglen"), JsonPropertyOrder(21)]
        public UInt32 PingLength
        {
            get
            {
                return advertiseLength;
            }
            set
            {
                this.advertiseLength = value;
            }
        }

    }
}
