using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VesperApp.Models
{
    public class ConfigNanotagAcc : ConfigurationDeviceDriver
    {
        public const UInt32 BITMASK_NANOTAGACC_GPSTRIGGER = 0x2000;
        public ConfigNanotagAcc() : base("NANOACC", "Nanotag on board Accelerometer")
        {
            this.FileSize = 0;
        }

        [DisplayName("Enable GPS Trigger"),
        TypeConverter(typeof(bool)),
        CategoryAttribute("NANOACC Specific Settings"),
        DescriptionAttribute("Enables Acc to be used as GPS trigger on motion detection"),
        Browsable(true)]
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


        [DisplayName("Dynamic Range"),
        TypeConverter(typeof(NanoAccRanges)),
        CategoryAttribute("NANOACC Specific Settings"),
        DescriptionAttribute("Accelerometer Dynamic Range"),
        Browsable(true)]
        [JsonIgnore]
        public NanoAccRanges Range
        {
            get
            {
                return NanoAccRanges.CreateFromValue(this.dynamic_range);
            }
            set
            {
                this.dynamic_range = value.Value;
                this.RawData1 &= 0xFFFF0000;
                this.RawData1 |= this.dynamic_range;
            }
        }


        private ushort dynamic_range;
        
        [JsonPropertyName("range"), JsonPropertyOrder(20)]
        [Browsable(false)]

        public ushort JsonDynamicRange
        {
            get => this.dynamic_range;
            set
            {
                this.dynamic_range = value;
                this.RawData1 &= 0xFFFF0000;
                this.RawData1 |= this.dynamic_range;
            }
        }



        [DisplayName("Mode"),
        TypeConverter(typeof(NanoAccOpMode)),
        CategoryAttribute("NANOACC Specific Settings"),
        DescriptionAttribute("Accelerometer Operating Mode"),
        Browsable(true)]
        [JsonIgnore]
        public NanoAccOpMode Mode
        {
            get
            {
                return NanoAccOpMode.CreateFromValue(this.operating_mode);
            }
            set
            {
                this.operating_mode = value.Value;
                this.RawData2 &= 0xFFFF0000;
                this.RawData2 |= this.operating_mode;
            }
        }


        private ushort operating_mode;

        [JsonPropertyName("mode"), JsonPropertyOrder(21)]
        [Browsable(false)]

        public ushort JsonOperatingMode
        {
            get => this.operating_mode;
            set
            {
                this.operating_mode = value;
                this.RawData2 &= 0xFFFF0000;
                this.RawData2 |= this.operating_mode;
            }
        }


        [DisplayName("Trigger Threshold"),
        TypeConverter(typeof(uint)),
        CategoryAttribute("NANOACC Specific Settings"),
        DescriptionAttribute("Accelerometer Trigger Threshold in mg"),
        Browsable(true)]
        [JsonPropertyName("threshold"), JsonPropertyOrder(22)]
        public uint Threshold
        {
            get
            {
                return this.threshold;
            }
            set
            {
                this.threshold = value;
                this.RawData3 = this.threshold;
            }
        }
        private uint threshold;


        [DisplayName("Trigger Deadtime"),
        TypeConverter(typeof(uint)),
        CategoryAttribute("NANOACC Specific Settings"),
        DescriptionAttribute("Accelerometer Trigger deadtime in seconds"),
        Browsable(true)]
        [JsonPropertyName("deadtime"), JsonPropertyOrder(23)]
        public UInt32 Deadtime
        {
            get
            {
                return this.deadtime;
            }
            set
            {
                this.deadtime = value;
                this.RawData4 = this.deadtime;
            }
        }
        private uint deadtime;


        /// Dynamic Range - 16bit
        /// 0=2g, 1=4g, 2=8g, 3=16g
        /// reserved 16bit

        /// Mode (0=Data Collection / 1=ODBA) - 16bit
        /// 

        /// reswerved2 - 16bit 

        /// acc threshold - 32bit (200 works!!!)
        /// 

        /// Dead Time between triggers - 32bit 
    }


    public class NanoAccRanges
    {
        public const ushort NACC_G2N = 0;
        public const ushort NACC_G4N = 1;
        public const ushort NACC_G8N = 2;
        public const ushort NACC_G16N = 3;

        public const string NACC_G2 = "±2g";
        public const string NACC_G4 = "±4g";
        public const string NACC_G8 = "±8g";
        public const string NACC_G16 = "±16g";

        private static readonly NanoAccRanges[] listOfConstants =
        {
            new NanoAccRanges(NACC_G2N),
            new NanoAccRanges(NACC_G4N),
            new NanoAccRanges(NACC_G8N),
            new NanoAccRanges(NACC_G16N)
        };

        public static NanoAccRanges[] ListOfOptions
        {
            get => listOfConstants;
        }

        public static NanoAccRanges CreateFromValue(ushort v)
        {
            return new NanoAccRanges(v);
        }


        public NanoAccRanges(ushort value)
        {
            this.value = value;
        }


        private ushort value;

        public ushort Value
        {
            get => value;
            set => this.value = value;
        }

        public override string ToString()
        {
            string r = NACC_G2;

            if (value == NACC_G4N)
            {
                r = NACC_G4;
            }
            else if (value == NACC_G8N)
            {
                r = NACC_G8;
            }
            else if (value == NACC_G16N)
            {
                r = NACC_G16;
            }
            else
            {
                value = NACC_G2N;
            }

            return r;
        }
    }


    public class NanoAccOpMode
    {
        public const ushort NACC_RAW_DATA = 0;
        public const ushort NACC_ODBA = 1;

        public const string NACC_RAW_DATA_S = "Raw Data sample";
        public const string NACC_ODBA_S = "Add ODBA/VEDBA to GPS";

        private static readonly NanoAccOpMode[] listOfConstants =
        {
            new NanoAccOpMode(NACC_RAW_DATA),
            new NanoAccOpMode(NACC_ODBA)
        };

        public static NanoAccOpMode[] ListOfOptions
        {
            get => listOfConstants;
        }

        public static NanoAccOpMode CreateFromValue(ushort v)
        {
            return new NanoAccOpMode(v);
        }


        public NanoAccOpMode(ushort value)
        {
            this.value = value;
        }


        private ushort value;

        public ushort Value
        {
            get => value;
            set => this.value = value;
        }

        public override string ToString()
        {
            string r = NACC_RAW_DATA_S;

            if (value == NACC_ODBA)
            {
                r = NACC_ODBA_S;
            }
            else
            {
                value = NACC_RAW_DATA;
            }

            return r;
        }
    }
}
