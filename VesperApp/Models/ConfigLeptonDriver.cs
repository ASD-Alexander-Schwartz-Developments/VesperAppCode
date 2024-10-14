using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;


namespace VesperApp.Models
{

    public class ConfigLeptonDriver : ConfigurationDeviceDriver
    {
        public const UInt32 BITMASK_LEPTON_LED = 0x02;

        public ConfigLeptonDriver() : base("LEPTON", "Lepton thermal camera")
        {
            this.FileSize = 0;
        }



        [DisplayName("Lepton camera snapshot activity LED indication"),
        TypeConverter(typeof(bool)),
        CategoryAttribute("Lepton specific Settings"),
        DescriptionAttribute("Enables LED to blink on camera activity")]
        [JsonIgnore]
        public bool EnableLEDIndication
        {
            get
            {
                if ((this.bitmask & BITMASK_LEPTON_LED) == BITMASK_LEPTON_LED)
                    return true;
                else
                    return false;
            }
            set
            {
                if (value == true)
                    this.bitmask |= BITMASK_LEPTON_LED;
                else
                    this.bitmask &= ~(BITMASK_LEPTON_LED);
            }
        }

        public override string ToString()
        {
            return "Lepton";
        }
    }
}
