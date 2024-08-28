using System;
using System.Collections.Generic;
using System.Text.Json;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace VesperApp.Models
{
    public class ConfigEXG2Driver : ConfigurationDeviceDriver
    {
        public new const UInt32 BITMASK_LED = 0x02;

        public const byte config1_default = 0x06;
        public const byte config2_default = 0x40;
        public const byte config3_default = 0x40;
        public const byte config4_default = 0x00;
        public const byte chset_default = 0x00;
        public const byte rldsense_default = 0x00;
        public const byte loffsense_default = 0x00;

        private UInt32 eegconf1;
        private UInt32 eegconf2;
        private UInt32 eegconf3;
        private UInt32 eegconf4;

        public ConfigEXG2Driver() : base("EXG2", "ECG/EMG/EEG 2 channels Driver")
        {
            this.FileSize = 0;
        }

        public override void Load(ConfigurationDeviceDriver ldrv)
        {
            if (ldrv is not null)
            {
                ConfigEXG2Driver driver = (ldrv as ConfigEXG2Driver)!;

                base.Load(ldrv);
                this.EegConf1 = driver.EegConf1;
                this.EegConf2 = driver.EegConf2;
                this.EegConf3 = driver.EegConf3;
                this.EegConf4 = driver.EegConf4;
            }
        }



        [JsonPropertyName("eegconf1"), JsonPropertyOrder(20)]
        [Browsable(false)]
        public UInt32 EegConf1
        {
            get => this.eegconf1;
            set => this.eegconf1 = value;
        }


        [JsonPropertyName("eegconf2"), JsonPropertyOrder(21)]
        [Browsable(false)]
        public UInt32 EegConf2 
        {
            get => this.eegconf2;
            set => this.eegconf2 = value;
        } 

        [JsonPropertyName("eegconf3"), JsonPropertyOrder(22)]
        [Browsable(false)]
        public UInt32 EegConf3
        {
            get => this.eegconf3;
            set => this.eegconf3 = value;
        }

        [JsonPropertyName("eegconf4"), JsonPropertyOrder(23)]
        [Browsable(false)]
        public UInt32 EegConf4
        {
            get => this.eegconf4;
            set => this.eegconf4 = value;
        }



        [DisplayName("Mux 1"),
        CategoryAttribute("EXG Channel 1"),
        DescriptionAttribute("Define Channel1 Mux connection")]
        [JsonIgnore]
        [Browsable(true)]
        public EXGMuxOptions Mux1
        {
            get 
            {
                return EXGMuxOptions.CreateFromValue((byte)((this.eegconf1 >> 16) & 0x0F));
            }
            set
            {
                this.eegconf1 &= 0xFFF0FFFF;
                this.eegconf1 |= (uint)((value.Value & 0x0F) << 16);
                OnPropertyChanged();
            }
        }

        [DisplayName("Power Down 1"),
        CategoryAttribute("EXG Channel 1"),
        DescriptionAttribute("Power Down Channel 1")]     
        [JsonIgnore]
        [Browsable(true)]
        public bool PD1
        {
            get
            {
                return ((this.eegconf1 & (1 << 8)) == (1 << 8));
            }
            set
            {
                this.eegconf1 &= 0xFFFFFEFF;
                if(value) this.eegconf1 |= (uint)((1 << 8));
                OnPropertyChanged();
            }
        }

        [DisplayName("RLD Pos 1"),
        CategoryAttribute("EXG Channel 1"),
        DescriptionAttribute("RLD Connect Positive Channel 1 to RLD Feedback")]
        [JsonIgnore]
        [Browsable(true)]
        public bool RLD1Pos
        {
            get
            {
                return ((this.eegconf2 & 0x80) == 0x80);
            }
            set
            {
                this.eegconf2 &= 0xFFFFFF7F;
                if (value) this.eegconf2 |= (uint)(0x80);
                OnPropertyChanged();
            }
        }

        [DisplayName("RLD Neg 1"),
        CategoryAttribute("EXG Channel 1"),
        DescriptionAttribute("RLD Connect Negative Channel 1 to RLD Feedback")]
        [JsonIgnore]
        [Browsable(true)]
        public bool RLD1Neg
        {
            get
            {
                return ((this.eegconf2 & 0x40) == 0x40);
            }
            set
            {
                this.eegconf2 &= 0xFFFFFFBF;
                if (value) this.eegconf2 |= (uint)(0x40);
                OnPropertyChanged();
            }
        }

        [DisplayName("LeadOff Pos 1"),
        CategoryAttribute("EXG Channel 1"),
        DescriptionAttribute("Enable Lead Off Channel 1 positive")]
        [JsonIgnore]
        [Browsable(true)]
        public bool LOff1Pos
        {
            get
            {
                return ((this.eegconf1 & 0x80000000) == 0x80000000);
            }
            set
            {
                this.eegconf1 &= 0x7FFFFFFF;
                if (value) this.eegconf1 |= (uint)(0x80000000);
                OnPropertyChanged();
            }
        }

        [DisplayName("LeadOff Neg 1"),
        CategoryAttribute("EXG Channel 1"),
        DescriptionAttribute("Enable Lead Off Channel 1 positive")]
        [JsonIgnore]
        [Browsable(true)]
        public bool LOff1Neg
        {
            get
            {
                return ((this.eegconf1 & 0x40000000) == 0x40000000);
            }
            set
            {
                this.eegconf1 &= 0xBFFFFFFF;
                if (value) this.eegconf1 |= (uint)(0x40000000);
                OnPropertyChanged();
            }
        }



        [DisplayName("Mux 2"),
        CategoryAttribute("EXG Channel 2"),
        DescriptionAttribute("Define Channel2 Mux connection")]
        [JsonIgnore]
        [Browsable(true)]
        public EXGMuxOptions Mux2
        {
            get
            {
                return EXGMuxOptions.CreateFromValue((byte)((this.eegconf1 >> 20) & 0x0F));
            }
            set
            {
                this.eegconf1 &= 0xFF0FFFFF;
                this.eegconf1 |= (uint)((value.Value & 0x0F) << 20);
                OnPropertyChanged();
            }
        }

        [DisplayName("Power Down 2"),
        CategoryAttribute("EXG Channel 2"),
        DescriptionAttribute("Power Down Channel 2")]
        [JsonIgnore]
        [Browsable(true)]
        public bool PD2
        {
            get
            {
                return ((this.eegconf1 & (1 << 9)) == (1 << 9));
            }
            set
            {
                this.eegconf1 &= 0xFFFFFDFF;
                if (value) this.eegconf1 |= (uint)((1 << 9));
                OnPropertyChanged();
            }
        }

        [DisplayName("RLD Pos 2"),
        CategoryAttribute("EXG Channel 2"),
        DescriptionAttribute("RLD Connect Positive Channel 2 to RLD Feedback")]
        [JsonIgnore]
        [Browsable(true)]
        public bool RLD2Pos
        {
            get
            {
                return (((this.eegconf2) & 0x20) == 0x20);
            }
            set
            {
                this.eegconf2 &= 0xFFFFFFDF;
                if (value) this.eegconf2 |= (uint)(0x20);
                OnPropertyChanged();
            }
        }

        [DisplayName("RLD Neg 2"),
        CategoryAttribute("EXG Channel 2"),
        DescriptionAttribute("RLD Connect Negative Channel 2 to RLD Feedback")]
        [JsonIgnore]
        [Browsable(true)]
        public bool RLD2Neg
        {
            get
            {
                return (((this.eegconf2) & 0x10) == 0x10);
            }
            set
            {
                this.eegconf2 &= 0xFFFFFFEF;
                if (value) this.eegconf2 |= (uint)(0x10);
                OnPropertyChanged();
            }
        }

        [DisplayName("LeadOff Pos 2"),
        CategoryAttribute("EXG Channel 2"),
        DescriptionAttribute("Enable Lead Off Channel 2 positive")]
        [JsonIgnore]
        [Browsable(true)]
        public bool LOff2Pos
        {
            get
            {
                return (((this.eegconf1) & 0x20000000) == 0x20000000);
            }
            set
            {
                this.eegconf1 &= 0xDFFFFFFF;
                if (value) this.eegconf1 |= (uint)(0x20000000);
                OnPropertyChanged();
            }
        }

        [DisplayName("LeadOff Neg 2"),
        CategoryAttribute("EXG Channel 2"),
        DescriptionAttribute("Enable Lead Off Channel 2 positive")]
        [JsonIgnore]
        [Browsable(true)]
        public bool LOff2Neg
        {
            get
            {
                return (((this.eegconf1) & 0x10000000) == 0x10000000);
            }
            set
            {
                this.eegconf1 &= 0xEFFFFFFF;
                if (value) this.eegconf1 |= (uint)(0x10000000);
                OnPropertyChanged();
            }
        }


        [DisplayName("Enable int test"),
        TypeConverter(typeof(bool)),
        CategoryAttribute("EXG Specific Settings"),
        DescriptionAttribute("Enable internal pattern generator"),
        Browsable(true)]
        [JsonIgnore]
        public bool EnableIntTest
        {
            get
            {
                return ((this.eegconf1 & (1 << 7)) == (1 << 7));
            }
            set
            {
                this.eegconf1 &= ~(uint)(1 << 7);
                if(value == true) this.eegconf1 |= (uint)(1 << 7);
                OnPropertyChanged();
            }
        }

        [DisplayName("Test Frequency"),
        TypeConverter(typeof(EXGTestFrequencyOptions)),
        CategoryAttribute("EXG Specific Settings"),
        DescriptionAttribute("Select internal test signal frequency"),
        Browsable(true)]
        [JsonIgnore]

        public EXG2TestFrequencyOptions TestFrequency
        {
            get
            {
                return EXG2TestFrequencyOptions.CreateFromValue((byte)((this.eegconf1 >> 6) & 0x01));
            }
            set
            {
                this.eegconf1 &= ~(uint)(0x01 << 6);
                this.eegconf1 |= (uint)((value.Value & 0x01) << 6);
                OnPropertyChanged();
            }
        }

        [DisplayName("LeadOff Threshold"),
        TypeConverter(typeof(EXGCompThOptions)),
        CategoryAttribute("EXG Specific Settings"),
        DescriptionAttribute("Select Lead Off detect comparator threshold (positive / negative electrodes)"),
        Browsable(true)]
        [JsonIgnore]

        public EXGCompThOptions LOffCompTh
        {
            get
            {
                return EXGCompThOptions.CreateFromValue((byte)((this.eegconf1 >> 16) & 0x07));
            }
            set
            {
                this.eegconf1 &= ~(uint)(0x07 << 16);
                this.eegconf1 |= ((uint)((value.Value & 0x07) << 16));
                OnPropertyChanged();
            }
        }


        [DisplayName("Gain"),
        TypeConverter(typeof(EXGGainOptions)),
        CategoryAttribute("EXG Specific Settings"),
        DescriptionAttribute("Select gain for all active channels"),
        Browsable(true)]
        [JsonIgnore]

        public EXGGainOptions GlobalGain
        {
            get
            {
                return EXGGainOptions.CreateFromValue((byte)(this.eegconf1 & 0x07));
            }
            set
            {
                this.eegconf1 &= ~(uint)(0x07);
                this.eegconf1 |= (uint)(value.Value & 0x07);
                OnPropertyChanged();
            }
        }


        [DisplayName("En LeadOff"),
        TypeConverter(typeof(bool)),
        CategoryAttribute("EXG Specific Settings"),
        DescriptionAttribute("Enable Lead Off detection comparator"),
        Browsable(true)]
        [JsonIgnore]
        public bool EnableLOffComp
        {
            get
            {
                return ((this.eegconf1 & (1 << 10)) == (1 << 10));
            }
            set
            {
                this.eegconf1 &= ~(uint)(1 << 10);
                if (value == true) this.eegconf1 |= (uint)(1 << 10);
                OnPropertyChanged();
            }
        }

        [DisplayName("En RLD Lead sense"),
        TypeConverter(typeof(bool)),
        CategoryAttribute("EXG Specific Settings"),
        DescriptionAttribute("Enable Lead Off detection for RLD Electrode"),
        Browsable(true)]
        [JsonIgnore]
        public bool EnableLOffRLD
        {
            get
            {
                return ((this.eegconf2 & (1 << 3)) == (1 << 3));
            }
            set
            {
                this.eegconf2 &= ~(uint)(1 << 3);
                if (value == true) this.eegconf2 |= (uint)(1 << 3);
                OnPropertyChanged();
            }
        }



        [CategoryAttribute("Standard configuration"),
        DescriptionAttribute("Sample Rate of the sensor operating in Config1 schedule"),
        DisplayName("Sample Rate [1]")]
        [JsonIgnore]
        [Browsable(true)]

        public EXG2SampleRateOptions SampleRate_1
        {
            get
            {
                return EXG2SampleRateOptions.CreateFromValue((byte)(SampleRate[1]));
            }
            set
            {
                this.SampleRate[1] = (uint)value.Value;
            }
        }

        [CategoryAttribute("Standard configuration"),
        DescriptionAttribute("Sample Rate of the sensor operating in Config2 schedule"),
        DisplayName("Sample Rate [2]")]
        [JsonIgnore]
        [Browsable(true)]

        public EXG2SampleRateOptions SampleRate_2
        {
            get
            {
                return EXG2SampleRateOptions.CreateFromValue((byte)(SampleRate[2]));
            }
            set
            {
                this.SampleRate[2] = (uint)value.Value;
            }
        }


        [CategoryAttribute("Standard configuration"),
        DescriptionAttribute("Sample Rate of the sensor operating in Config1 schedule"),
        DisplayName("Sample Rate [1]")]
        [JsonIgnore]
        [Browsable(false)]
        public override UInt32 SampleRate1
        {
            get => SampleRate[1];
            set => SampleRate[1] = (uint) value;
        }



        [CategoryAttribute("Standard configuration"),
        DescriptionAttribute("Sample Rate of the sensor operating in Config2 schedule"),
        DisplayName("Sample Rate [2]")]
        [JsonIgnore]
        [Browsable(false)]
        public override UInt32 SampleRate2
        {
            get => SampleRate[2];
            set => SampleRate[2] = (uint)value;
        }


        [DisplayName("EXG sensor activity LED indication"),
        TypeConverter(typeof(bool)),
        CategoryAttribute("EXG specific Settings"),
        DescriptionAttribute("Enables LED to blink on EXG recording activity")]
        [JsonIgnore]
        public bool EnableLEDIndication
        {
            get
            {
                if ((this.bitmask & BITMASK_LED) == BITMASK_LED)
                    return true;
                else
                    return false;
            }
            set
            {
                if (value == true)
                    this.bitmask |= BITMASK_LED;
                else
                    this.bitmask &= ~(BITMASK_LED);
            }
        }
    }



    public class EXG2SampleRateOptions
    {
        public const byte SR2_DNU_B = 8;
        public const byte SR2_8K_B = 7;
        public const byte SR2_4K_B = 6;
        public const byte SR2_2K_B = 5;
        public const byte SR2_1K_B = 4;
        public const byte SR2_500_B = 3;
        public const byte SR2_250_B = 2;
        public const byte SR2_125_B = 1;
        public const byte SR2_NONE_B = 0;

        public const string SR2_8K_S = "8 kSPS";
        public const string SR2_4K_S = "4 kSPS";
        public const string SR2_2K_S = "2 kSPS";
        public const string SR2_1K_S = "1 kSPS";
        public const string SR2_500_S = "500 SPS";
        public const string SR2_250_S = "250 SPS";
        public const string SR2_125_S = "125 SPS";
        public const string SR2_NONE_S = "0 SPS";

        private static readonly EXG2SampleRateOptions[] listOfOptions = 
        {
            new EXG2SampleRateOptions(SR2_NONE_B),
            new EXG2SampleRateOptions(SR2_125_B),
            new EXG2SampleRateOptions(SR2_250_B),
            new EXG2SampleRateOptions(SR2_500_B),
            new EXG2SampleRateOptions(SR2_1K_B),
            new EXG2SampleRateOptions(SR2_2K_B),
            new EXG2SampleRateOptions(SR2_4K_B),
            new EXG2SampleRateOptions(SR2_8K_B)
        };

        private static readonly string[] listOfStrings =
        {
            SR2_NONE_S,
            SR2_125_S,
            SR2_250_S,
            SR2_500_S,
            SR2_1K_S,
            SR2_2K_S,
            SR2_4K_S,
            SR2_8K_S
        };

        public static EXG2SampleRateOptions[] ListOfOptions
        {
            get => listOfOptions;
        }
        public static EXG2SampleRateOptions CreateFromValue(byte v)
        {
            return new EXG2SampleRateOptions(v);
        }


        public byte GetTrueValue()
        {
            byte ret = SR2_500_B;

            switch (this.value)
            {
                case SR2_8K_B: ret = SR2_8K_B; break;
                case SR2_4K_B: ret = SR2_4K_B; break;
                case SR2_2K_B: ret = SR2_2K_B; break;
                case SR2_1K_B: ret = SR2_1K_B; break;
                case SR2_500_B: ret = SR2_500_B; break;
                case SR2_250_B: ret = SR2_250_B; break;
                case SR2_125_B: ret = SR2_125_B; break;
                case SR2_NONE_B: ret = SR2_NONE_B; break;
                default: ret = SR2_500_B; break;
            }

            return ret;
        }


        public EXG2SampleRateOptions(byte value)
        {
            this.value = value;
        }

        private byte value;

        public byte Value
        {
            get => value;
            set => this.value = value;
        }

        public override string ToString()
        {
            int index = this.value & 0x07;

            if (index > SR2_8K_B) index = SR2_8K_B;
            
            return listOfStrings[index];
        }

    }



    public class EXG2MuxOptions
    {
        /*
         * 	unsigned ch1_pd			:	1;					// 	'0' - normal operation, '1' - power down
			unsigned gain           :   3;                  // gain
            unsigned mux1			: 	4;					// mux: 000-Normal, 001-Short, 010-RLD, 011-VDD, 100-Temp, 101-Test, 110-RLD_POS, 111-RLD_NEG
         * */
        private static readonly string[] listOfOptions =
        {
            "Normal Electrode", 
            "Inputs Shorted",
            "RLD Measurement",
            "Supply Measurement",
            "Temperature Measurement",
            "Test",
            "RLD Positive",
            "RLD Negative",
            "RLD Pos&Neg"
        };

        public static EXG2MuxOptions[] ListOfOptions
        {
            get
            {
                EXG2MuxOptions[] list = new EXG2MuxOptions[listOfOptions.Length];


                for(int i = 0; i < listOfOptions.Length; i++)
                {
                    list[i] = EXG2MuxOptions.CreateFromValue((byte)i) as EXG2MuxOptions;
                }

                return list;
            }
        }
        public static EXG2MuxOptions CreateFromValue(byte v)
        {
            return new EXG2MuxOptions(v);
        }


        public EXG2MuxOptions(byte value)
        {
            this.value = value;
        }

        private byte value;

        public byte Value
        {
            get => value;
            set
            {
                this.value = value;
            }
        }

        public override string ToString()
        {
            int index = this.value & 0x0F;

            if(index > listOfOptions.Length-1)
            {
                index = listOfOptions.Length-1;
            }

            return listOfOptions[index];
        }
    }


    public class EXG2TestFrequencyOptions
    {
        private static readonly string[] listOfOptions =
        {
            "DC",
            "Pulsed at ~1Hz"
        };

        public static EXG2TestFrequencyOptions[] ListOfOptions
        {
            get
            {
                EXG2TestFrequencyOptions[] list = new EXG2TestFrequencyOptions[listOfOptions.Length];


                for (int i = 0; i < listOfOptions.Length; i++)
                {
                    list[i] = EXG2TestFrequencyOptions.CreateFromValue((byte)i) as EXG2TestFrequencyOptions;
                }

                return list;
            }
        }
        public static EXG2TestFrequencyOptions CreateFromValue(byte v)
        {
            return new EXG2TestFrequencyOptions(v);
        }


        public EXG2TestFrequencyOptions(byte value)
        {
            this.value = value;
        }

        private byte value;

        public byte Value
        {
            get => value;
            set
            {
                this.value = (byte)(value & 0x01);
            }
        }

        public override string ToString()
        {
            int index = this.value & 0x01;

            return listOfOptions[index];
        }
    }
}
