using System;
using System.Collections.Generic;
using System.Text.Json;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace VesperApp.Models
{
    public class ConfigEXG48Driver : ConfigurationDeviceDriver
    {
        public const UInt32 BITMASK_LED = 0x02;

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

        public ConfigEXG48Driver() : base("EXG48", "ECG/EMG/EEG-4/6/8 Common Driver")
        {
            this.FileSize = 0;
        }

        public override void Load(ConfigurationDeviceDriver ldrv)
        {
            if (ldrv is not null)
            {
                ConfigEXG48Driver driver = (ldrv as ConfigEXG48Driver)!;

                base.Load(ldrv);
                this.EegConf1 = driver.EegConf1;

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
                return EXGMuxOptions.CreateFromValue((byte)((this.eegconf2 >> 5) & 0x07));
            }
            set
            {
                this.eegconf2 &= 0xFFFFFF1F;
                this.eegconf2 |= (uint)((value.Value & 0x07) << 5);
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
                return ((this.eegconf2 & 0x01) == 0x01);
            }
            set
            {
                this.eegconf2 &= 0xFFFFFFFE;
                if(value) this.eegconf2 |= (uint)(0x01);
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
                return ((this.eegconf2 & 0x02) == 0x02);
            }
            set
            {
                this.eegconf2 &= 0xFFFFFFFD;
                if (value) this.eegconf2 |= (uint)(0x02);
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
                return ((this.eegconf2 & 0x04) == 0x04);
            }
            set
            {
                this.eegconf2 &= 0xFFFFFFFB;
                if (value) this.eegconf2 |= (uint)(0x04);
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
                return ((this.eegconf2 & 0x08) == 0x08);
            }
            set
            {
                this.eegconf2 &= 0xFFFFFFF7;
                if (value) this.eegconf2 |= (uint)(0x08);
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
                return ((this.eegconf2 & 0x10) == 0x10);
            }
            set
            {
                this.eegconf2 &= 0xFFFFFFEF;
                if (value) this.eegconf2 |= (uint)(0x10);
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
                return EXGMuxOptions.CreateFromValue((byte)((this.eegconf2 >> 13) & 0x07));
            }
            set
            {
                this.eegconf2 &= 0xFFFF1FFF;
                this.eegconf2 |= (uint)((value.Value & 0x07) << 13);
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
                return (((this.eegconf2 >> 8) & 0x01) == 0x01);
            }
            set
            {
                this.eegconf2 &= 0xFFFFFEFF;
                if (value) this.eegconf2 |= (uint)(0x01 << 8);
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
                return (((this.eegconf2 >> 8) & 0x02) == 0x02);
            }
            set
            {
                this.eegconf2 &= 0xFFFFFDFF;
                if (value) this.eegconf2 |= (uint)(0x02 << 8);
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
                return (((this.eegconf2 >> 8) & 0x04) == 0x04);
            }
            set
            {
                this.eegconf2 &= 0xFFFFFBFF;
                if (value) this.eegconf2 |= (uint)(0x04 << 8);
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
                return (((this.eegconf2 >> 8) & 0x08) == 0x08);
            }
            set
            {
                this.eegconf2 &= 0xFFFFF7FF;
                if (value) this.eegconf2 |= (uint)(0x08 << 8);
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
                return (((this.eegconf2 >> 8) & 0x10) == 0x10);
            }
            set
            {
                this.eegconf2 &= 0xFFFFEFFF;
                if (value) this.eegconf2 |= (uint)(0x10 << 8);
                OnPropertyChanged();
            }
        }



        [DisplayName("Mux 3"),
        CategoryAttribute("EXG Channel 3"),
        DescriptionAttribute("Define Channel3 Mux connection")]
        [JsonIgnore]
        [Browsable(true)]
        public EXGMuxOptions Mux3
        {
            get
            {
                return EXGMuxOptions.CreateFromValue((byte)((this.eegconf2 >> 21) & 0x07));
            }
            set
            {
                this.eegconf2 &= 0xFF1FFFFF;
                this.eegconf2 |= (uint)((value.Value & 0x07) << 21);
                OnPropertyChanged();
            }
        }

        [DisplayName("Power Down 3"),
        CategoryAttribute("EXG Channel 3"),
        DescriptionAttribute("Power Down Channel 3")]
        [JsonIgnore]
        [Browsable(true)]
        public bool PD3
        {
            get
            {
                return (((this.eegconf2 >> 16) & 0x01) == 0x01);
            }
            set
            {
                this.eegconf2 &= 0xFFFEFFFF;
                if (value) this.eegconf2 |= (uint)(0x01 << 16);
                OnPropertyChanged();
            }
        }


        [DisplayName("RLD Pos 3"),
        CategoryAttribute("EXG Channel 3"),
        DescriptionAttribute("RLD Connect Positive Channel 3 to RLD Feedback")]
        [JsonIgnore]
        [Browsable(true)]
        public bool RLD3Pos
        {
            get
            {
                return (((this.eegconf2 >> 16) & 0x02) == 0x02);
            }
            set
            {
                this.eegconf2 &= 0xFFFDFFFF;
                if (value) this.eegconf2 |= (uint)(0x02 << 16);
                OnPropertyChanged();
            }
        }

        [DisplayName("RLD Neg 3"),
        CategoryAttribute("EXG Channel 3"),
        DescriptionAttribute("RLD Connect Negative Channel 3 to RLD Feedback")]
        [JsonIgnore]
        [Browsable(true)]
        public bool RLD3Neg
        {
            get
            {
                return (((this.eegconf2 >> 16) & 0x04) == 0x04);
            }
            set
            {
                this.eegconf2 &= 0xFFFBFFFF;
                if (value) this.eegconf2 |= (uint)(0x04 << 16);
                OnPropertyChanged();
            }
        }

        [DisplayName("LeadOff Pos 3"),
        CategoryAttribute("EXG Channel 3"),
        DescriptionAttribute("Enable Lead Off Channel 3 positive")]
        [JsonIgnore]
        [Browsable(true)]
        public bool LOff3Pos
        {
            get
            {
                return (((this.eegconf2 >> 16) & 0x08) == 0x08);
            }
            set
            {
                this.eegconf2 &= 0xFFF7FFFF;
                if (value) this.eegconf2 |= (uint)(0x08 << 16);
                OnPropertyChanged();
            }
        }

        [DisplayName("LeadOff Neg 3"),
        CategoryAttribute("EXG Channel 3"),
        DescriptionAttribute("Enable Lead Off Channel 3 positive")]
        [JsonIgnore]
        [Browsable(true)]
        public bool LOff3Neg
        {
            get
            {
                return (((this.eegconf2 >> 16) & 0x10) == 0x10);
            }
            set
            {
                this.eegconf2 &= 0xFFEFFFFF;
                if (value) this.eegconf2 |= (uint)(0x10 << 16);
                OnPropertyChanged();
            }
        }



        [DisplayName("Mux 4"),
        CategoryAttribute("EXG Channel 4"),
        DescriptionAttribute("Define Channel4 Mux connection")]
        [JsonIgnore]
        [Browsable(true)]
        public EXGMuxOptions Mux4
        {
            get
            {
                return EXGMuxOptions.CreateFromValue((byte)((this.eegconf2 >> 29) & 0x07));
            }
            set
            {
                this.eegconf2 &= 0x1FFFFFFF;
                this.eegconf2 |= (uint)((value.Value & 0x07) << 29);
                OnPropertyChanged();
            }
        }

        [DisplayName("Power Down 4"),
        CategoryAttribute("EXG Channel 4"),
        DescriptionAttribute("Power Down Channel 4")]
        [JsonIgnore]
        [Browsable(true)]
        public bool PD4
        {
            get
            {
                return (((this.eegconf2 >> 24) & 0x01) == 0x01);
            }
            set
            {
                this.eegconf2 &= 0xFEFFFFFF;
                if (value) this.eegconf2 |= (uint)(0x01 << 24);
                OnPropertyChanged();
            }
        }

        [DisplayName("RLD Pos 4"),
        CategoryAttribute("EXG Channel 4"),
        DescriptionAttribute("RLD Connect Positive Channel 4 to RLD Feedback")]
        [JsonIgnore]
        [Browsable(true)]
        public bool RLD4Pos
        {
            get
            {
                return (((this.eegconf2 >> 24) & 0x02) == 0x02);
            }
            set
            {
                this.eegconf2 &= 0xFDFFFFFF;
                if (value) this.eegconf2 |= (uint)(0x02 << 24);
                OnPropertyChanged();
            }
        }

        [DisplayName("RLD Neg 4"),
        CategoryAttribute("EXG Channel 4"),
        DescriptionAttribute("RLD Connect Negative Channel 4 to RLD Feedback")]
        [JsonIgnore]
        [Browsable(true)]
        public bool RLD4Neg
        {
            get
            {
                return (((this.eegconf2 >> 24) & 0x04) == 0x04);
            }
            set
            {
                this.eegconf2 &= 0xFBFFFFFF;
                if (value) this.eegconf2 |= (uint)(0x04 << 24);
                OnPropertyChanged();
            }
        }

        [DisplayName("LeadOff Pos 4"),
        CategoryAttribute("EXG Channel 4"),
        DescriptionAttribute("Enable Lead Off Channel 4 positive")]
        [JsonIgnore]
        [Browsable(true)]
        public bool LOff4Pos
        {
            get
            {
                return (((this.eegconf2 >> 24) & 0x08) == 0x08);
            }
            set
            {
                this.eegconf2 &= 0xF7FFFFFF;
                if (value) this.eegconf2 |= (uint)(0x08 << 24);
                OnPropertyChanged();
            }
        }

        [DisplayName("LeadOff Neg 4"),
        CategoryAttribute("EXG Channel 4"),
        DescriptionAttribute("Enable Lead Off Channel 4 positive")]
        [JsonIgnore]
        [Browsable(true)]
        public bool LOff4Neg
        {
            get
            {
                return (((this.eegconf2 >> 24) & 0x10) == 0x10);
            }
            set
            {
                this.eegconf2 &= 0xEFFFFFFF;
                if (value) this.eegconf2 |= (uint)(0x10 << 24);
                OnPropertyChanged();
            }
        }


        [DisplayName("Mux 5"),
        CategoryAttribute("EXG Channel 5"),
        DescriptionAttribute("Define Channel5 Mux connection")]
        [JsonIgnore]
        [Browsable(true)]
        public EXGMuxOptions Mux5
        {
            get
            {
                return EXGMuxOptions.CreateFromValue((byte)((this.eegconf3 >> 5) & 0x07));
            }
            set
            {
                this.eegconf3 &= 0xFFFFFF1F;
                this.eegconf3 |= (uint)((value.Value & 0x07) << 5);
                OnPropertyChanged();
            }
        }

        [DisplayName("Power Down 5"),
        CategoryAttribute("EXG Channel 5"),
        DescriptionAttribute("Power Down Channel 5")]
        [JsonIgnore]
        [Browsable(true)]
        public bool PD5
        {
            get
            {
                return ((this.eegconf3 & 0x01) == 0x01);
            }
            set
            {
                this.eegconf3 &= 0xFFFFFFFE;
                if (value) this.eegconf3 |= (uint)(0x01);
                OnPropertyChanged();
            }
        }

        [DisplayName("RLD Pos 5"),
        CategoryAttribute("EXG Channel 5"),
        DescriptionAttribute("RLD Connect Positive Channel 5 to RLD Feedback")]
        [JsonIgnore]
        [Browsable(true)]
        public bool RLD5Pos
        {
            get
            {
                return (((this.eegconf3) & 0x02) == 0x02);
            }
            set
            {
                this.eegconf3 &= 0xFFFFFFFD;
                if (value) this.eegconf3 |= (uint)(0x02);
                OnPropertyChanged();
            }
        }

        [DisplayName("RLD Neg 5"),
        CategoryAttribute("EXG Channel 5"),
        DescriptionAttribute("RLD Connect Negative Channel 5 to RLD Feedback")]
        [JsonIgnore]
        [Browsable(true)]
        public bool RLD5Neg
        {
            get
            {
                return (((this.eegconf3) & 0x04) == 0x04);
            }
            set
            {
                this.eegconf3 &= 0xFFFFFFFB;
                if (value) this.eegconf3 |= (uint)(0x04);
                OnPropertyChanged();
            }
        }

        [DisplayName("LeadOff Pos 5"),
        CategoryAttribute("EXG Channel 5"),
        DescriptionAttribute("Enable Lead Off Channel 5 positive")]
        [JsonIgnore]
        [Browsable(true)]
        public bool LOff5Pos
        {
            get
            {
                return ((this.eegconf3 & 0x08) == 0x08);
            }
            set
            {
                this.eegconf3 &= 0xFFFFFFF7;
                if (value) this.eegconf3 |= (uint)(0x08);
                OnPropertyChanged();
            }
        }

        [DisplayName("LeadOff Neg 5"),
        CategoryAttribute("EXG Channel 5"),
        DescriptionAttribute("Enable Lead Off Channel 5 positive")]
        [JsonIgnore]
        [Browsable(true)]
        public bool LOff5Neg
        {
            get
            {
                return ((this.eegconf3 & 0x10) == 0x10);
            }
            set
            {
                this.eegconf3 &= 0xFFFFFFEF;
                if (value) this.eegconf3 |= (uint)(0x10);
                OnPropertyChanged();
            }
        }



        [DisplayName("Mux 6"),
        CategoryAttribute("EXG Channel 6"),
        DescriptionAttribute("Define Channel6 Mux connection")]
        [JsonIgnore]
        [Browsable(true)]
        public EXGMuxOptions Mux6
        {
            get
            {
                return EXGMuxOptions.CreateFromValue((byte)((this.eegconf3 >> 13) & 0x07));
            }
            set
            {
                this.eegconf3 &= 0xFFFF1FFF;
                this.eegconf3 |= (uint)((value.Value & 0x07) << 13);
                OnPropertyChanged();
            }
        }

        [DisplayName("Power Down 6"),
        CategoryAttribute("EXG Channel 6"),
        DescriptionAttribute("Power Down Channel 6")]
        [JsonIgnore]
        [Browsable(true)]
        public bool PD6
        {
            get
            {
                return (((this.eegconf3 >> 8) & 0x01) == 0x01);
            }
            set
            {
                this.eegconf3 &= 0xFFFFFEFF;
                if (value) this.eegconf3 |= (uint)(0x01 << 8);
                OnPropertyChanged();
            }
        }

        [DisplayName("RLD Pos 6"),
        CategoryAttribute("EXG Channel 6"),
        DescriptionAttribute("RLD Connect Positive Channel 6 to RLD Feedback")]
        [JsonIgnore]
        [Browsable(true)]
        public bool RLD6Pos
        {
            get
            {
                return (((this.eegconf3 >> 8) & 0x02) == 0x02);
            }
            set
            {
                this.eegconf3 &= 0xFFFFFDFF;
                if (value) this.eegconf3 |= (uint)(0x02 << 8);
                OnPropertyChanged();
            }
        }

        [DisplayName("RLD Neg 6"),
        CategoryAttribute("EXG Channel 6"),
        DescriptionAttribute("RLD Connect Negative Channel 6 to RLD Feedback")]
        [JsonIgnore]
        [Browsable(true)]
        public bool RLD6Neg
        {
            get
            {
                return (((this.eegconf3 >> 8) & 0x04) == 0x04);
            }
            set
            {
                this.eegconf3 &= 0xFFFFFBFF;
                if (value) this.eegconf3 |= (uint)(0x04 << 8);
                OnPropertyChanged();
            }
        }

        [DisplayName("LeadOff Pos 6"),
        CategoryAttribute("EXG Channel 6"),
        DescriptionAttribute("Enable Lead Off Channel 6 positive")]
        [JsonIgnore]
        [Browsable(true)]
        public bool LOff6Pos
        {
            get
            {
                return (((this.eegconf3 >> 8) & 0x08) == 0x08);
            }
            set
            {
                this.eegconf3 &= 0xFFFFF7FF;
                if (value) this.eegconf3 |= (uint)(0x08 << 8);
                OnPropertyChanged();
            }
        }

        [DisplayName("LeadOff Neg 6"),
        CategoryAttribute("EXG Channel 6"),
        DescriptionAttribute("Enable Lead Off Channel 6 positive")]
        [JsonIgnore]
        [Browsable(true)]
        public bool LOff6Neg
        {
            get
            {
                return (((this.eegconf3 >> 8) & 0x10) == 0x10);
            }
            set
            {
                this.eegconf3 &= 0xFFFFEFFF;
                if (value) this.eegconf3 |= (uint)(0x10 << 8);
                OnPropertyChanged();
            }
        }


        [DisplayName("Mux 7"),
        CategoryAttribute("EXG Channel 7"),
        DescriptionAttribute("Define Channel7 Mux connection")]
        [JsonIgnore]
        [Browsable(true)]
        public EXGMuxOptions Mux7
        {
            get
            {
                return EXGMuxOptions.CreateFromValue((byte)((this.eegconf3 >> 21) & 0x07));
            }
            set
            {
                this.eegconf3 &= 0xFF1FFFFF;
                this.eegconf3 |= (uint)((value.Value & 0x07) << 21);
                OnPropertyChanged();
            }
        }

        [DisplayName("Power Down 7"),
        CategoryAttribute("EXG Channel 7"),
        DescriptionAttribute("Power Down Channel 7")]
        [JsonIgnore]
        [Browsable(true)]
        public bool PD7
        {
            get
            {
                return (((this.eegconf3 >> 16) & 0x01) == 0x01);
            }
            set
            {
                this.eegconf3 &= 0xFFFEFFFF;
                if (value) this.eegconf3 |= (uint)(0x01 << 16);
                OnPropertyChanged();
            }
        }

        [DisplayName("RLD Pos 7"),
        CategoryAttribute("EXG Channel 7"),
        DescriptionAttribute("RLD Connect Positive Channel 7 to RLD Feedback")]
        [JsonIgnore]
        [Browsable(true)]
        public bool RLD7Pos
        {
            get
            {
                return (((this.eegconf3 >> 16) & 0x02) == 0x02);
            }
            set
            {
                this.eegconf3 &= 0xFFFDFFFF;
                if (value) this.eegconf3 |= (uint)(0x02 << 16);
                OnPropertyChanged();
            }
        }

        [DisplayName("RLD Neg 7"),
        CategoryAttribute("EXG Channel 7"),
        DescriptionAttribute("RLD Connect Negative Channel 7 to RLD Feedback")]
        [JsonIgnore]
        [Browsable(true)]
        public bool RLD7Neg
        {
            get
            {
                return (((this.eegconf3 >> 16) & 0x04) == 0x04);
            }
            set
            {
                this.eegconf3 &= 0xFFFBFFFF;
                if (value) this.eegconf3 |= (uint)(0x04 << 16);
                OnPropertyChanged();
            }
        }


        [DisplayName("LeadOff Pos 7"),
        CategoryAttribute("EXG Channel 7"),
        DescriptionAttribute("Enable Lead Off Channel 7 positive")]
        [JsonIgnore]
        [Browsable(true)]
        public bool LOff7Pos
        {
            get
            {
                return (((this.eegconf3 >> 16) & 0x08) == 0x08);
            }
            set
            {
                this.eegconf3 &= 0xFFF7FFFF;
                if (value) this.eegconf3 |= (uint)(0x08 << 16);
                OnPropertyChanged();
            }
        }

        [DisplayName("LeadOff Neg 7"),
        CategoryAttribute("EXG Channel 7"),
        DescriptionAttribute("Enable Lead Off Channel 7 positive")]
        [JsonIgnore]
        [Browsable(true)]
        public bool LOff7Neg
        {
            get
            {
                return (((this.eegconf3 >> 16) & 0x10) == 0x10);
            }
            set
            {
                this.eegconf3 &= 0xFFEFFFFF;
                if (value) this.eegconf3 |= (uint)(0x10 << 16);
                OnPropertyChanged();
            }
        }


        [DisplayName("Mux 8"),
        CategoryAttribute("EXG Channel 8"),
        DescriptionAttribute("Define Channel8 Mux connection")]
        [JsonIgnore]
        [Browsable(true)]
        public EXGMuxOptions Mux8
        {
            get
            {
                return EXGMuxOptions.CreateFromValue((byte)((this.eegconf3 >> 29) & 0x07));
            }
            set
            {
                this.eegconf3 &= 0x1FFFFFFF;
                this.eegconf3 |= (uint)((value.Value & 0x07) << 29);
                OnPropertyChanged();
            }
        }

        [DisplayName("Power Down 8"),
        CategoryAttribute("EXG Channel 8"),
        DescriptionAttribute("Power Down Channel 8")]
        [JsonIgnore]
        [Browsable(true)]
        public bool PD8
        {
            get
            {
                return (((this.eegconf3 >> 24) & 0x01) == 0x01);
            }
            set
            {
                this.eegconf3 &= 0xFEFFFFFF;
                if (value) this.eegconf3 |= (uint)(0x01 << 24);
                OnPropertyChanged();
            }
        }

        [DisplayName("RLD Pos 8"),
        CategoryAttribute("EXG Channel 8"),
        DescriptionAttribute("RLD Connect Positive Channel 8 to RLD Feedback")]
        [JsonIgnore]
        [Browsable(true)]
        public bool RLD8Pos
        {
            get
            {
                return (((this.eegconf3 >> 24) & 0x02) == 0x02);
            }
            set
            {
                this.eegconf3 &= 0xFDFFFFFF;
                if (value) this.eegconf3 |= (uint)(0x02 << 24);
                OnPropertyChanged();
            }
        }

        [DisplayName("RLD Neg 8"),
        CategoryAttribute("EXG Channel 8"),
        DescriptionAttribute("RLD Connect Negative Channel 8 to RLD Feedback")]
        [JsonIgnore]
        [Browsable(true)]
        public bool RLD8Neg
        {
            get
            {
                return (((this.eegconf3 >> 24) & 0x04) == 0x04);
            }
            set
            {
                this.eegconf3 &= 0xFBFFFFFF;
                if (value) this.eegconf3 |= (uint)(0x04 << 24);
                OnPropertyChanged();
            }
        }

        [DisplayName("LeadOff Pos 8"),
        CategoryAttribute("EXG Channel 8"),
        DescriptionAttribute("Enable Lead Off Channel 8 positive")]
        [JsonIgnore]
        [Browsable(true)]
        public bool LOff8Pos
        {
            get
            {
                return (((this.eegconf3 >> 24) & 0x08) == 0x08);
            }
            set
            {
                this.eegconf3 &= 0xF7FFFFFF;
                if (value) this.eegconf3 |= (uint)(0x08 << 24);
                OnPropertyChanged();
            }
        }

        [DisplayName("LeadOff Neg 8"),
        CategoryAttribute("EXG Channel 8"),
        DescriptionAttribute("Enable Lead Off Channel 8 positive")]
        [JsonIgnore]
        [Browsable(true)]
        public bool LOff8Neg
        {
            get
            {
                return (((this.eegconf3 >> 24) & 0x10) == 0x10);
            }
            set
            {
                this.eegconf3 &= 0xEFFFFFFF;
                if (value) this.eegconf3 |= (uint)(0x10 << 24);
                OnPropertyChanged();
            }
        }


        [DisplayName("Enable High Performance Mode"),
        TypeConverter(typeof(bool)),
        CategoryAttribute("EXG Specific Settings"),
        DescriptionAttribute("Enable high power high performance mode or disable for low power mode"),
        Browsable(true)]
        [JsonIgnore]
        public bool EnableHR
        {
            get
            {
                return ((this.eegconf1 & 0x00000001) == 0x00000001);
            }
            set
            {
                this.eegconf1 &= ~(uint)(0x00000001);
                if (value == true) this.eegconf1 |= (uint)(1);
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
                return ((this.eegconf1 & (1 << 4)) == (1 << 4));
            }
            set
            {
                this.eegconf1 &= ~(uint)(1 << 4);
                if(value == true) this.eegconf1 |= (uint)(1 << 4);
                OnPropertyChanged();
            }
        }

        [DisplayName("Test Frequency"),
        TypeConverter(typeof(EXGTestFrequencyOptions)),
        CategoryAttribute("EXG Specific Settings"),
        DescriptionAttribute("Select internal test signal frequency"),
        Browsable(true)]
        [JsonIgnore]

        public EXGTestFrequencyOptions TestFrequency
        {
            get
            {
                return EXGTestFrequencyOptions.CreateFromValue((byte)((this.eegconf1 >> 6) & 0x03));
            }
            set
            {
                this.eegconf1 &= ~(uint)(0x03 << 6);
                this.eegconf1 |= (uint)((value.Value & 0x03) << 6);
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
                return EXGCompThOptions.CreateFromValue((byte)((this.eegconf1 >> 10) & 0x07));
            }
            set
            {
                this.eegconf1 &= ~(uint)(0x07 << 10);
                this.eegconf1 |= ((uint)((value.Value & 0x07) << 10));
                OnPropertyChanged();
            }
        }


        [DisplayName("Gain"),
        TypeConverter(typeof(bool)),
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
                return ((this.eegconf1 & (1 << 8)) == (1 << 8));
            }
            set
            {
                this.eegconf1 &= ~(uint)(1 << 8);
                if (value == true) this.eegconf1 |= (uint)(1 << 8);
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
                return ((this.eegconf1 & (1 << 9)) == (1 << 9));
            }
            set
            {
                this.eegconf1 &= ~(uint)(1 << 9);
                if (value == true) this.eegconf1 |= (uint)(1 << 9);
                OnPropertyChanged();
            }
        }

        [DisplayName("En RLD Measure"),
        TypeConverter(typeof(bool)),
        CategoryAttribute("EXG Specific Settings"),
        DescriptionAttribute("Enable RLD Electrode Measurement over selected chennel"),
        Browsable(true)]
        [JsonIgnore]
        public bool EnableRLDMeasure
        {
            get
            {
                return ((this.eegconf1 & (1 << 19)) == (1 << 19));
            }
            set
            {
                this.eegconf1 &= ~(uint)(1 << 19);
                if (value == true) this.eegconf1 |= (uint)(1 << 19);
                OnPropertyChanged();
            }
        }



        [DisplayName("WCT to RLD"),
        TypeConverter(typeof(bool)),
        CategoryAttribute("EXG WCT Settings"),
        DescriptionAttribute("Connect WCT output to RLD"),
        Browsable(true)]
        [JsonIgnore]
        public bool WCT2RLD
        {
            get
            {
                return ((this.eegconf4 & (1)) == (1));
            }
            set
            {
                this.eegconf1 &= ~(uint)(1);
                OnPropertyChanged();
            }
        }

        [DisplayName("WCT Chop"),
        TypeConverter(typeof(bool)),
        CategoryAttribute("EXG WCT Settings"),
        DescriptionAttribute("If Enabled WCT uses Fmod/16 fixed chopping frequency"),
        Browsable(true)]
        [JsonIgnore]
        public bool WCTChop
        {
            get
            {
                return ((this.eegconf4 & (1<<1)) == (1<<1));
            }
            set
            {
                this.eegconf1 &= ~(uint)(1<<1);
                OnPropertyChanged();
            }
        }

        [DisplayName("avF Ch6"),
        TypeConverter(typeof(bool)),
        CategoryAttribute("EXG WCT Settings"),
        DescriptionAttribute("Enable (WCTA + WCTB)/2 to the negative input of channel 6"),
        Browsable(true)]
        [JsonIgnore]
        public bool AVFCh6
        {
            get
            {
                return ((this.eegconf4 & (1 << 2)) == (1 << 2));
            }
            set
            {
                this.eegconf1 &= ~(uint)(1 << 2);
                OnPropertyChanged();
            }
        }

        [DisplayName("avL Ch5"),
        TypeConverter(typeof(bool)),
        CategoryAttribute("EXG WCT Settings"),
        DescriptionAttribute("Enable (WCTA + WCTC)/2 to the negative input of channel 5"),
        Browsable(true)]
        [JsonIgnore]
        public bool AVLCh5
        {
            get
            {
                return ((this.eegconf4 & (1 << 3)) == (1 << 3));
            }
            set
            {
                this.eegconf1 &= ~(uint)(1 << 3);
                OnPropertyChanged();
            }
        }

        [DisplayName("avR Ch7"),
        TypeConverter(typeof(bool)),
        CategoryAttribute("EXG WCT Settings"),
        DescriptionAttribute("Enable (WCTB + WCTC)/2 to the negative input of channel 7"),
        Browsable(true)]
        [JsonIgnore]
        public bool AVRCh7
        {
            get
            {
                return ((this.eegconf4 & (1 << 4)) == (1 << 4));
            }
            set
            {
                this.eegconf1 &= ~(uint)(1 << 4);
                OnPropertyChanged();
            }
        }

        [DisplayName("avR Ch4"),
        TypeConverter(typeof(bool)),
        CategoryAttribute("EXG WCT Settings"),
        DescriptionAttribute("Enable (WCTB + WCTC)/2 to the negative input of channel 4"),
        Browsable(true)]
        [JsonIgnore]
        public bool AVRCh4
        {
            get
            {
                return ((this.eegconf4 & (1 << 5)) == (1 << 5));
            }
            set
            {
                this.eegconf1 &= ~(uint)(1 << 5);
                OnPropertyChanged();
            }
        }

        [DisplayName("En WCTA"),
        TypeConverter(typeof(bool)),
        CategoryAttribute("EXG WCTA Settings"),
        DescriptionAttribute("Enable WCTA Amplifier"),
        Browsable(true)]
        [JsonIgnore]
        public bool EnWCTA
        {
            get
            {
                return ((this.eegconf4 & (1 << 8)) == (1 << 8));
            }
            set
            {
                this.eegconf1 &= ~(uint)(1 << 8);
                OnPropertyChanged();
            }
        }

        [DisplayName("En WCTB"),
        TypeConverter(typeof(bool)),
        CategoryAttribute("EXG WCTB Settings"),
        DescriptionAttribute("Enable WCTB Amplifier"),
        Browsable(true)]
        [JsonIgnore]
        public bool EnWCTB
        {
            get
            {
                return ((this.eegconf4 & (1 << 16)) == (1 << 16));
            }
            set
            {
                this.eegconf1 &= ~(uint)(1 << 16);
                OnPropertyChanged();
            }
        }

        [DisplayName("En WCTC"),
        TypeConverter(typeof(bool)),
        CategoryAttribute("EXG WCTC Settings"),
        DescriptionAttribute("Enable WCTC Amplifier"),
        Browsable(true)]
        [JsonIgnore]
        public bool EnWCTC
        {
            get
            {
                return ((this.eegconf4 & (1 << 24)) == (1 << 24));
            }
            set
            {
                this.eegconf1 &= ~(uint)(1 << 24);
                OnPropertyChanged();
            }
        }


        [DisplayName("WCTA Mux"),
        TypeConverter(typeof(bool)),
        CategoryAttribute("EXG WCTA Settings"),
        DescriptionAttribute("Select WCTA Mux connection"),
        Browsable(true)]
        [JsonIgnore]

        public EXGWCTOptions WCTA
        {
            get
            {
                return EXGWCTOptions.CreateFromValue((byte)(this.eegconf4 & (7 >> 9)));
            }
            set
            {
                this.eegconf4 &= ~(uint)(7 << 9);
                this.eegconf4 |= (uint)((value.Value & 0x07) << 9);
                OnPropertyChanged();
            }
        }


        [DisplayName("WCTB Mux"),
        TypeConverter(typeof(bool)),
        CategoryAttribute("EXG WCTB Settings"),
        DescriptionAttribute("Select WCTB Mux connection"),
        Browsable(true)]
        [JsonIgnore]

        public EXGWCTOptions WCTB
        {
            get
            {
                return EXGWCTOptions.CreateFromValue((byte)(this.eegconf4 & (7 >> 17)));
            }
            set
            {
                this.eegconf4 &= ~(uint)(0x07 << 17);
                this.eegconf4 |= (uint)((value.Value & 0x07) << 17);
                OnPropertyChanged();
            }
        }

        [DisplayName("WCTC Mux"),
        TypeConverter(typeof(bool)),
        CategoryAttribute("EXG WCTC Settings"),
        DescriptionAttribute("Select WCTC Mux connection"),
        Browsable(true)]
        [JsonIgnore]

        public EXGWCTOptions WCTC
        {
            get
            {
                return EXGWCTOptions.CreateFromValue((byte)(this.eegconf4 & (0x07 >> 25)));
            }
            set
            {
                this.eegconf4 &= ~(uint)(0x07 << 25);
                this.eegconf4 |= (uint)((value.Value & 0x07) << 25);
                OnPropertyChanged();
            }
        }


        [CategoryAttribute("Standard configuration"),
        DescriptionAttribute("Sample Rate of the sensor operating in Config1 schedule"),
        DisplayName("Sample Rate [1]")]
        [JsonIgnore]
        [Browsable(true)]

        public EXGSampleRateOptions SampleRate_1
        {
            get
            {
                return EXGSampleRateOptions.CreateFromValue((byte)(SampleRate[1]));
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

        public EXGSampleRateOptions SampleRate_2
        {
            get
            {
                return EXGSampleRateOptions.CreateFromValue((byte)(SampleRate[2]));
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
        }



        [CategoryAttribute("Standard configuration"),
        DescriptionAttribute("Sample Rate of the sensor operating in Config2 schedule"),
        DisplayName("Sample Rate [2]")]
        [JsonIgnore]
        [Browsable(true)]
        public override UInt32 SampleRate2
        {
            get => SampleRate[2];
        }


        [DisplayName("Mike activity LED indication"),
        TypeConverter(typeof(bool)),
        CategoryAttribute("Ultrasonic Mike specific Settings"),
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




    public class EXGWCTOptions
    {
        public const byte CH1POS = 0;
        public const byte CH1NEG = 1;
        public const byte CH2POS = 2;
        public const byte CH2NEG = 3;
        public const byte CH3POS = 4;
        public const byte CH3NEG = 5;
        public const byte CH4POS = 6;
        public const byte CH4NEG = 7;

        public const string CH1POS_S = "Ch1 + connected to WCT amp";
        public const string CH1NEG_S = "Ch1 - connected to WCT amp";
        public const string CH2POS_S = "Ch2 + connected to WCT amp";
        public const string CH2NEG_S = "Ch2 - connected to WCT amp";
        public const string CH3POS_S = "Ch3 + connected to WCT amp";
        public const string CH3NEG_S = "Ch3 - connected to WCT amp";
        public const string CH4POS_S = "Ch4 + connected to WCT amp";
        public const string CH4NEG_S = "Ch4 - connected to WCT amp";

        private static readonly string[] listOfStrings =
        {
            CH1POS_S, CH1NEG_S,CH2POS_S,CH2NEG_S,CH3POS_S,CH3NEG_S,CH4POS_S,CH4NEG_S
        };

        private static readonly EXGWCTOptions[] listOfOptions =
        {
            new EXGWCTOptions(CH1POS),
            new EXGWCTOptions(CH1NEG),
            new EXGWCTOptions(CH2POS),
            new EXGWCTOptions(CH2NEG),
            new EXGWCTOptions(CH3POS),
            new EXGWCTOptions(CH3NEG),
            new EXGWCTOptions(CH4POS),
            new EXGWCTOptions(CH4NEG),
        };

        public static EXGWCTOptions[] ListOfOptions
        {
            get => listOfOptions;
        }
        public static EXGWCTOptions CreateFromValue(byte v)
        {
            return new EXGWCTOptions(v);
        }


        public EXGWCTOptions(byte value)
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
            return listOfStrings[this.value & 0x07];
        }

    }




    public class EXGSampleRateOptions
    {
        public const byte SR_8K_HR = 2;
        public const byte SR_4K_HR = 3;
        public const byte SR_2K_HR = 4;
        public const byte SR_1K_HR = 5;
        public const byte SR_500_HR = 6;

        public const byte SR_8K_LP = 1;
        public const byte SR_4K_LP = 2;
        public const byte SR_2K_LP = 3;
        public const byte SR_1K_LP = 4;
        public const byte SR_500_LP = 5;
        public const byte SR_250_LP = 6;

        public const int SR_8K_I = 0;
        public const int SR_4K_I = 1;
        public const int SR_2K_I = 2;
        public const int SR_1K_I = 3;
        public const int SR_500_I = 4;

        public const string SR_8K_S = "8 kSPS";
        public const string SR_4K_S = "4 kSPS";
        public const string SR_2K_S = "2 kSPS";
        public const string SR_1K_S = "1 kSPS";
        public const string SR_500_S = "500 SPS";
        
        private static readonly EXGSampleRateOptions[] listOfOptions = 
        { 
            new EXGSampleRateOptions(SR_8K_I),
            new EXGSampleRateOptions(SR_4K_I),
            new EXGSampleRateOptions(SR_2K_I),
            new EXGSampleRateOptions(SR_1K_I),
            new EXGSampleRateOptions(SR_500_I) 
        };

        private static readonly string[] listOfStrings =
        {
            SR_8K_S,
            SR_4K_S,
            SR_2K_S,
            SR_1K_S,
            SR_500_S
        };

        public static EXGSampleRateOptions[] ListOfOptions
        {
            get => listOfOptions;
        }
        public static EXGSampleRateOptions CreateFromValue(byte v)
        {
            return new EXGSampleRateOptions(v);
        }


        public byte GetTrueValue(bool ishr)
        {
            byte ret = SR_250_LP;

            if(ishr)
            {
                switch(this.value)
                {
                    case SR_8K_I: ret = SR_8K_HR; break;
                    case SR_4K_I: ret = SR_4K_HR; break;
                    case SR_2K_I: ret = SR_2K_HR; break;
                    case SR_1K_I: ret = SR_1K_HR; break;
                    case SR_500_HR: ret = SR_500_HR; break;
                    default: ret = SR_500_HR; break;
                }
            }
            else
            {
                switch (this.value)
                {
                    case SR_8K_I: ret = SR_8K_LP; break;
                    case SR_4K_I: ret = SR_4K_LP; break;
                    case SR_2K_I: ret = SR_2K_LP; break;
                    case SR_1K_I: ret = SR_1K_LP; break;
                    case SR_500_HR: ret = SR_500_LP; break;
                    default: ret = SR_500_LP; break;
                }
            }

            return ret;
        }


        public EXGSampleRateOptions(byte value)
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

            if (index > SR_500_I) index = SR_500_I;
            
            return listOfStrings[index];
        }

    }



    public class EXGMuxOptions
    {
        /*
         * 	unsigned ch1_pd			:	1;					// 	'0' - normal operation, '1' - power down
			unsigned rld1p			:	1;					// connect ch1 pos to rld feedback loop
			unsigned rld1n			:	1;					// connect ch1 neg to rld feedback loop
			unsigned loff1p			:	1;					// enable lead off ch1 pos
			unsigned loff1n			:	1;					// enable lead off ch1 neg
			unsigned mux1			: 	3;					// mux: 000-Normal, 001-Short, 010-RLD, 011-VDD, 100-Temp, 101-Test, 110-RLD_POS, 111-RLD_NEG
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
            "RLD Negative"
        };

        public static EXGMuxOptions[] ListOfOptions
        {
            get
            {
                EXGMuxOptions[] list = new EXGMuxOptions[listOfOptions.Length];


                for(int i = 0; i < listOfOptions.Length; i++)
                {
                    list[i] = EXGMuxOptions.CreateFromValue((byte)i) as EXGMuxOptions;
                }

                return list;
            }
        }
        public static EXGMuxOptions CreateFromValue(byte v)
        {
            return new EXGMuxOptions(v);
        }


        public EXGMuxOptions(byte value)
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
            int index = this.value & 0x07;

            return listOfOptions[index];
        }
    }


    public class EXGCompThOptions
    {
        private static readonly string[] listOfOptions =
        {
            "(+) 95% / (-) 5%",
            "(+) 92.5% / (-) 7.5%",
            "(+) 90% / (-) 10%",
            "(+) 87.5% / (-) 12.5%",
            "(+) 85% / (-) 15%",
            "(+) 80% / (-) 20%",
            "(+) 75% / (-) 25%",
            "(+) 70% / (-) 30%",
        };

        public static EXGCompThOptions[] ListOfOptions
        {
            get
            {
                EXGCompThOptions[] list = new EXGCompThOptions[listOfOptions.Length];


                for (int i = 0; i < listOfOptions.Length; i++)
                {
                    list[i] = EXGCompThOptions.CreateFromValue((byte)i) as EXGCompThOptions;
                }

                return list;
            }
        }
        public static EXGCompThOptions CreateFromValue(byte v)
        {
            return new EXGCompThOptions(v);
        }


        public EXGCompThOptions(byte value)
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
            int index = this.value & 0x07;

            return listOfOptions[index];
        }
    }


    public class EXGTestFrequencyOptions
    {
        private static readonly string[] listOfOptions =
        {
            "Pulsed at ~1Hz",
            "Pulsed at ~2Hz",
            "N/A",
            "DC",
        };

        public static EXGTestFrequencyOptions[] ListOfOptions
        {
            get
            {
                EXGTestFrequencyOptions[] list = new EXGTestFrequencyOptions[listOfOptions.Length];


                for (int i = 0; i < listOfOptions.Length; i++)
                {
                    list[i] = EXGTestFrequencyOptions.CreateFromValue((byte)i) as EXGTestFrequencyOptions;
                }

                return list;
            }
        }
        public static EXGTestFrequencyOptions CreateFromValue(byte v)
        {
            return new EXGTestFrequencyOptions(v);
        }


        public EXGTestFrequencyOptions(byte value)
        {
            this.value = value;
        }

        private byte value;

        public byte Value
        {
            get => value;
            set
            {
                if (value != 0x02)
                {
                    this.value = value;
                }
                else
                {
                    this.value = 0x03;
                }
            }
        }

        public override string ToString()
        {
            int index = this.value & 0x03;

            return listOfOptions[index];
        }
    }

    public class EXGGainOptions
    {
        private static readonly string[] listOfOptions =
        {
            "x6",
            "x1",
            "x2",
            "x3",
            "x4",
            "x8",
            "x12",
        };

        private static readonly double[] listOfMults =
        {
            6.0,
            1.0,
            2.0,
            3.0,
            4.0,
            8.0,
            12.0,
        };

        public static EXGGainOptions[] ListOfOptions
        {
            get
            {
                EXGGainOptions[] list = new EXGGainOptions[listOfOptions.Length];


                for (int i = 0; i < listOfOptions.Length; i++)
                {
                    list[i] = EXGGainOptions.CreateFromValue((byte)i) as EXGGainOptions;
                }

                return list;
            }
        }
        public static EXGGainOptions CreateFromValue(byte v)
        {
            return new EXGGainOptions(v);
        }


        public EXGGainOptions(byte value)
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

        static public double ToMultiplier(byte v)
        {
            int index = v & 0x07;

            return listOfMults[index];
        }

        public override string ToString()
        {
            int index = this.value & 0x07;

            return listOfOptions[index];
        }
    }





}
