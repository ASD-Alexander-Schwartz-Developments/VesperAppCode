using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;


namespace VesperApp.Models
{

    public class ConfigLeptonDriver : ConfigurationDeviceDriver
    {
        // Sample Rate unit as the firmware reads it (2026-11-04).
        protected override RateKind SampleRateKind => RateKind.PeriodMs;
        protected override string SampleRateNote => "time between frames (0 = off); from 10000 ms the camera is off between frames.";
        protected override bool FileSizeIgnored => true;

        public const UInt32 BITMASK_LEPTON_LED = 0x02;

        public ConfigLeptonDriver() : base("LEPTON", "Lepton thermal camera")
        {
            this.FileSize = 0;
        }



        [DisplayName("LED Activity"),
        TypeConverter(typeof(bool)),
        CategoryAttribute("Lepton specific Settings"),
        DescriptionAttribute("Flash the LED on every camera frame.")]
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
