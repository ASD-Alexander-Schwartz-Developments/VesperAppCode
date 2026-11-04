using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VesperApp.Models
{

    public class ConfigADS1115Driver : ConfigurationDeviceDriver
    {
        // Sample Rate unit as the firmware reads it (2026-11-04).
        protected override RateKind SampleRateKind => RateKind.PeriodMs;
        protected override string SampleRateNote => "time between records, 10 ms or more; every enabled input is read each time (0 = off).";

        public ConfigADS1115Driver() : base("ADS1115", "DAQ24 board: 4-input 16-bit ADC (ADS1115), 0 - 4.096 V, every enabled input once per sample")
        {
            this.FileSize = 1 * 1024 * 1024;
        }

        private UInt32 channels;

        [Browsable(false)]
        [JsonPropertyName("channels"), JsonPropertyOrder(20)]   // after "name": the converter reads "name" first
        public UInt32 Channels
        {
            get { return this.channels; }
            set { this.channels = value; }
        }

        [DisplayName("Enable AIN0"),
        TypeConverter(typeof(bool)),
        CategoryAttribute("DAQ24 inputs"),
        DescriptionAttribute("Input AIN0 (TP1), 0 V to VDDA. None enabled = all four.")]
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

        [DisplayName("Enable AIN1"),
        TypeConverter(typeof(bool)),
        CategoryAttribute("DAQ24 inputs"),
        DescriptionAttribute("Input AIN1 (TP2), 0 V to VDDA. None enabled = all four.")]
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

        [DisplayName("Enable AIN2"),
        TypeConverter(typeof(bool)),
        CategoryAttribute("DAQ24 inputs"),
        DescriptionAttribute("Input AIN2 (TP3), 0 V to VDDA. None enabled = all four.")]
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


        [DisplayName("Enable AIN3"),
        TypeConverter(typeof(bool)),
        CategoryAttribute("DAQ24 inputs"),
        DescriptionAttribute("Input AIN3 (TP4), 0 V to VDDA. None enabled = all four.")]
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
