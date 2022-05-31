using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.ComponentModel;

namespace VesperApp.Models
{

    public class ConfigALS3001DDriver : ConfigurationDeviceDriver
    {
        public const UInt32 BITMASK_ALS3001D_LED = 0x02;

        public ConfigALS3001DDriver() : base("ALS3001D", "Huaman Eye calibrated response ambient light sensor")
        {

            this.MemoryBufferSize = 512;
            this.FileSize = 512 * 1024;

        }

        [DisplayName("Light sensor activity LED indication"),
        TypeConverter(typeof(bool)),
        CategoryAttribute("Light sensor specific Settings")]
        [JsonIgnore]
        public bool EnableLEDIndication
        {
            get
            {
                if ((this.bitmask & BITMASK_ALS3001D_LED) == BITMASK_ALS3001D_LED)
                    return true;
                else
                    return false;
            }
            set
            {
                if (value == true)
                    this.bitmask |= BITMASK_ALS3001D_LED;
                else
                    this.bitmask &= ~(BITMASK_ALS3001D_LED);
            }
        }

    }
}
