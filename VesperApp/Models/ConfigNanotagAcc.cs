using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VesperApp.Models
{
    internal class ConfigNanotagAcc : ConfigurationDeviceDriver
    {
        public const UInt32 BITMASK_NANOTAGACC_GPSTRIGGER = 0x80000000;
        public ConfigNanotagAcc() : base("NANOACC", "Nanotag on board Accelerometer")
        {
            this.FileSize = 0;
        }

        [DisplayName("Enable GPS Trigger"),
        TypeConverter(typeof(bool)),
        CategoryAttribute("Special Settings"),
        DescriptionAttribute("Enables Acc to be used as GPS trigger on motion detection")]
        [JsonIgnore]
        public bool EnableGPSTrigger
        {
            get
            {
                if ((this.Bitmask & BITMASK_NANOTAGACC_GPSTRIGGER) == BITMASK_NANOTAGACC_GPSTRIGGER)
                    return true;
                else
                    return false;
            }
            set
            {
                if (value == true)
                    this.Bitmask |= BITMASK_NANOTAGACC_GPSTRIGGER;
                else
                    this.Bitmask &= ~(BITMASK_NANOTAGACC_GPSTRIGGER);
            }
        }


        public override string ToString()
        {
            return "NANOACC";
        }

    }
}
