using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VesperApp.Models
{

    public class ConfigADS1115Driver : ConfigurationDeviceDriver
    {
        public ConfigADS1115Driver() : base("ADS1115", "Low datarate analog to digital converter")
        {
            this.MemoryBufferSize = 2 * 1024;
            this.FileSize = 1 * 1024 * 1024;
        }

        private UInt32 channels;

        [Browsable(false)]
        [JsonPropertyName("channels")]
        public UInt32 Channels
        {
            get { return this.channels; }
            set { this.channels = value; }
        }

        [DisplayName("Enable Ch.1"),
        TypeConverter(typeof(bool)),
        CategoryAttribute("Ext. ADC specific Settings"),
        DescriptionAttribute("Enables ADS115 ADC board channel 1. Note sampling frequency divided between all enabled channels")]
        [JsonIgnore]
        public bool EnableCh1
        {
            get
            {
                if ((this.channels & 0x01) == 0x01)
                    return true;
                else
                    return false;
            }
            set
            {
                if (value == true)
                    this.channels |= 0x01;
                else
                    this.channels &= (0xFE);
            }
        }

        [DisplayName("Enable Ch.2"),
        TypeConverter(typeof(bool)),
        CategoryAttribute("Ext. ADC specific Settings"),
        DescriptionAttribute("Enables ADS115 ADC board channel 2. Note sampling frequency divided between all enabled channels")]
        [JsonIgnore]
        public bool EnableCh2
        {
            get
            {
                if ((this.channels & 0x02) == 0x02)
                    return true;
                else
                    return false;
            }
            set
            {
                if (value == true)
                    this.channels |= 0x02;
                else
                    this.channels &= (0xFD);
            }
        }

        [DisplayName("Enable Ch.3"),
        TypeConverter(typeof(bool)),
        CategoryAttribute("Ext. ADC specific Settings"),
        DescriptionAttribute("Enables ADS115 ADC board channel 3. Note sampling frequency divided between all enabled channels")]
        [JsonIgnore]
        public bool EnableCh3
        {
            get
            {
                if ((this.channels & 0x04) == 0x04)
                    return true;
                else
                    return false;
            }
            set
            {
                if (value == true)
                    this.channels |= 0x04;
                else
                    this.channels &= (0xFB);
            }
        }


        [DisplayName("Enable Ch.4"),
        TypeConverter(typeof(bool)),
        CategoryAttribute("Ext. ADC specific Settings"),
        DescriptionAttribute("Enables ADS115 ADC board channel 4. Note sampling frequency divided between all enabled channels")]
        [JsonIgnore]
        public bool EnableCh4
        {
            get
            {
                if ((this.channels & 0x08) == 0x08)
                    return true;
                else
                    return false;
            }
            set
            {
                if (value == true)
                    this.channels |= 0x08;
                else
                    this.channels &= (0xF7);
            }
        }
    }
}
