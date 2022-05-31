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
        public const UInt32 BITMASK_LEPTON3 = 0x01;

        public ConfigLeptonDriver() : base("LEPTON", "Lepton thermal camera")
        {
            this.MemoryBufferSize = 9840;
            this.FileSize = 0;
        }

        private UInt32 width;
        private UInt32 height;

        [Browsable(false)]
        [JsonPropertyName("cwidth")]
        public UInt32 Width
        {
            get { return this.width; }
            set { this.width = value; }
        }


        [Browsable(false)]
        [JsonPropertyName("cheight")]
        public UInt32 Height
        {
            get { return this.height; }
            set { this.height = value; }
        }



        [DisplayName("Lepton Type"),
        TypeConverter(typeof(LeptonTypeConverter)),
        CategoryAttribute("Lepton settings")]
        [JsonIgnore]
        public string LeptonType
        {
            get
            {
                if ((this.bitmask & BITMASK_LEPTON3) == BITMASK_LEPTON3)
                    return LeptonTypeConverter.LEPTON_V3;
                else
                    return LeptonTypeConverter.LEPTON_V1;
            }
            set
            {
                if (value == LeptonTypeConverter.LEPTON_V3)
                {
                    this.bitmask |= BITMASK_LEPTON3;
                }
                else
                {
                    this.bitmask &= ~BITMASK_LEPTON3;
                }
            }
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


    public class LeptonTypeConverter : StringConverter
    {
        public const string LEPTON_V1 = "Lepton 1st generation (80x60)";
        public const string LEPTON_V3 = "Lepton 3";

        public override bool GetStandardValuesSupported(ITypeDescriptorContext? context)
        {
            return true;
        }


        public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext? context)
        {
            return new StandardValuesCollection(new string[] {
                LEPTON_V1,
                LEPTON_V3});
        }
    }


}
