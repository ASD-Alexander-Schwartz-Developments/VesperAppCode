using System;
using System.Collections.Generic;
using System.Text.Json;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace VesperApp.Models
{
    public class ConfigSPH0641Driver : ConfigurationDeviceDriver
    {
        public const UInt32 BITMASK_MIKE_LED = 0x02;
        public const UInt32 SAMPLING_RATE_MASTERCLOCK = 4800000;

        public const UInt32 MAX_SAMPLING_RATE = 400000;
        public const UInt32 MIN_SAMPLING_RATE = 2344;

        public const Int16 MAX_GAIN_DB = 72;
        public const Int16 MIN_GAIN_DB = -48;


        private UInt32 thresholdup;
        private UInt32 thresholddown;
        private bool cic4;
        private UInt16 gain;
        private UInt16 hpf;

        public ConfigSPH0641Driver() : base("SPH0641", "Vesper/Pipistrelle V4 Ultrasonic microphone recording")
        {
            this.FileSize = 8 * 1024 * 1024;
        }


        [DisplayName("Threshold Up"),
        CategoryAttribute("Ultrasonic Mike specific Settings"),
        DescriptionAttribute("Enables recording of audio data upon audio threshold level trigger.")]
        [JsonPropertyName("thresup"), JsonPropertyOrder(20)]
        [Browsable(true)]
        public UInt32 ThresholdUp
        {
            get { return this.thresholdup; }
            set
            {
                if (value > 2047)
                    throw new ArgumentException("Maximum threshold level is 2047");
                else
                    this.thresholdup = value;

            }
        }

        [DisplayName("Threshold Down"),
        CategoryAttribute("Ultrasonic Mike specific Settings"),
        DescriptionAttribute("Enables recording of audio data upon audio threshold level trigger.")]
        [JsonPropertyName("thresdn"), JsonPropertyOrder(21)]
        [Browsable(true)]
        public UInt32 ThresholdDown
        {
            get { return this.thresholddown; }
            set
            {
                if (value > 2047)
                    throw new ArgumentException("Maximum threshold level is 2047");
                else
                    this.thresholddown = value;

            }
        }

        

        private UInt32 CheckSamplingRate(UInt32 ovalue, UInt32 nvalue)
        {
            UInt32 rval = nvalue;
            /*if(nvalue == 0)
            {
                rval = 0;
            }
            else if(nvalue <= MAX_SAMPLING_RATE && nvalue > MIN_SAMPLING_RATE)
            {
                UInt32 decimation_factor = SAMPLING_RATE_MASTERCLOCK / nvalue;

                rval = SAMPLING_RATE_MASTERCLOCK / decimation_factor;
            }
            else
            {
                rval = MIN_SAMPLING_RATE;
            }
            */
            return rval;
        }


        private Int16 CalcGainFromDB(Int16 ovalue, Int16 nvalue)
        {
            Int16 rval = ovalue;
            /*
            if (nvalue <= MAX_GAIN_DB && nvalue > MIN_GAIN_DB)
            {
                UInt16 decimation_factor = (UInt16)(SAMPLING_RATE_MASTERCLOCK / (Int16)nvalue);

                rval = (UInt16)(SAMPLING_RATE_MASTERCLOCK / (UInt16)decimation_factor);
            }
            */
            return rval;
        }




        [CategoryAttribute("Ultrasonic Mike specific Settings"),
        DescriptionAttribute("Use CIC4 filter by default if enabled. Otherwise, default is CIC5."),
        DisplayName("Use CIC4 Digital Filter")]
        [JsonPropertyName("cic4"), JsonPropertyOrder(22)]
        [Browsable(true)]
        public bool UseCic4Filter
        {
            get => cic4;
            set
            {
                cic4 = value;
                OnPropertyChanged();
            }
        }


        private UInt16 digitalFilter = 0;


        [DisplayName("Digital Filter"),
        CategoryAttribute("Ultrasonic Mike specific Settings"),
        Browsable(false)]
        [JsonPropertyName("filter"), JsonPropertyOrder(23)]
        public UInt16 DFilter
        {
            get { return this.digitalFilter; }
            set { this.digitalFilter = value; }
        }

        [DisplayName("Enable Digital Filter"),
        TypeConverter(typeof(bool)),
        CategoryAttribute("Ultrasonic Mike specific Settings"),
        DescriptionAttribute("Enables Digital LPF with Fc=0.111*Fs"),
        Browsable(true)]
        [JsonIgnore]
        public bool EnableDigitalFilter
        {
            get
            {
                if ((this.digitalFilter & 0x0100) == 0x0100)
                    return true;
                else
                    return false;
            }
            set
            {
                if (value == true)
                    this.digitalFilter = 0x0100;
                else
                    this.digitalFilter = 0;

                OnPropertyChanged();
            }
        }

        [DisplayName("Enable Digital Filter Decimation"),
        TypeConverter(typeof(bool)),
        CategoryAttribute("Ultrasonic Mike specific Settings"),
        DescriptionAttribute("Enables Digital LPF Decimation factor of /4"),
        Browsable(true)]
        [JsonIgnore]
        public bool EnableDigitalFilterDecimation
        {
            get
            {
                if ((this.digitalFilter & 0x101) == 0x101)
                    return true;
                else
                    return false;
            }
            set
            {
                if (value == true)
                    this.digitalFilter |= 0x1;
                else
                    this.digitalFilter &= 0x10E;

                OnPropertyChanged();
            }
        }

        [DisplayName("HPF"),
        CategoryAttribute("Ultrasonic Mike specific Settings"),
        Browsable(false)]
        [JsonPropertyName("hpf"), JsonPropertyOrder(24)]
        public UInt16 HPF
        {
            get { return this.hpf; }
            set { this.hpf = value; }
        }

        [DisplayName("High Pass Filter"),
        TypeConverter(typeof(SPH0641Hpf)),
        Browsable(true),
        CategoryAttribute("Ultrasonic Mike specific Settings"),
        DescriptionAttribute("Programmable High pass Filter cutoff -3dB frequency")]
        [JsonIgnore]
        public SPH0641Hpf HighPassFilter
        {
            get
            {
                return SPH0641Hpf.CreateFromValue(this.HPF);
            }
            set
            {
                this.HPF = value.Value;
                OnPropertyChanged();
            }
        }

        [DisplayName("GAIN"),
        CategoryAttribute("Ultrasonic Mike specific Settings"),
        Browsable(false)]
        [JsonPropertyName("gain"), JsonPropertyOrder(25)]
        public UInt16 GAIN
        {
            get { return this.gain; }
            set { this.gain = value; }
        }

        [DisplayName("Programmable Gain"),
        TypeConverter(typeof(SPH0641Gain)),
        CategoryAttribute("Ultrasonic Mike specific Settings"),
        DescriptionAttribute("Programmable Gain"),
        Browsable(true)]
        [JsonIgnore]
        public SPH0641Gain Gain
        {
            get
            {
                return SPH0641Gain.CreateFromValue((byte)this.GAIN);
            }
            set
            {
                this.gain = (UInt16)value.Value;
                OnPropertyChanged();
            }
        }



        [CategoryAttribute("Standard configuration"),
        DescriptionAttribute("Sample Rate of the sensor operating in Config1 schedule"),
        DisplayName("Sample Rate [1]")]
        [JsonIgnore]
        [Browsable(true)]
        public override UInt32 SampleRate1
        {
            get => SampleRate[1];
            set
            {
                SampleRate[1] = CheckSamplingRate(SampleRate[1], value);
                OnPropertyChanged();
            }
        }

        [CategoryAttribute("Standard configuration"),
        DescriptionAttribute("Sample Rate of the sensor operating in Config2 schedule"),
        DisplayName("Sample Rate [2]")]
        [JsonIgnore]
        [Browsable(true)]
        public override UInt32 SampleRate2
        {
            get => SampleRate[2];
            set
            {
                SampleRate[2] = CheckSamplingRate(SampleRate[2], value);
                OnPropertyChanged();
            }
        }


        [DisplayName("Mike activity LED indication"),
        TypeConverter(typeof(bool)),
        CategoryAttribute("Ultrasonic Mike specific Settings"),
        DescriptionAttribute("Enables LED to blink on audio recording activity")]
        [JsonIgnore]
        public bool EnableLEDIndication
        {
            get
            {
                if ((this.bitmask & BITMASK_MIKE_LED) == BITMASK_MIKE_LED)
                    return true;
                else
                    return false;
            }
            set
            {
                if (value == true)
                    this.bitmask |= BITMASK_MIKE_LED;
                else
                    this.bitmask &= ~(BITMASK_MIKE_LED);
            }
        }


        public override void Load(ConfigurationDeviceDriver ldrv)
        {
            if (ldrv is not null)
            {
                base.Load(ldrv);
                this.Gain = (ldrv as ConfigSPH0641Driver)!.Gain;
                this.HighPassFilter = (ldrv as ConfigSPH0641Driver)!.HighPassFilter;
                this.ThresholdDown = (ldrv as ConfigSPH0641Driver)!.ThresholdDown;
                this.ThresholdUp = (ldrv as ConfigSPH0641Driver)!.ThresholdUp;
                this.UseCic4Filter = (ldrv as ConfigSPH0641Driver)!.UseCic4Filter;
                this.EnableDigitalFilter = (ldrv as ConfigSPH0641Driver)!.EnableDigitalFilter;
                this.EnableDigitalFilterDecimation = (ldrv as ConfigSPH0641Driver)!.EnableDigitalFilterDecimation;
            }
        }

    }


    public class SPH0641Hpf
    {
        /*
         * • 0.000625 x FS
           • 0.00125 x FS
           • 0.00250 x FS
           • 0.00950 x FS
         * */

        public const ushort HPF_OFF = 0;
        public const ushort HPF_000625 = 1;
        public const ushort HPF_00125 = 2;
        public const ushort HPF_00250 = 3;
        public const ushort HPF_00950 = 4;

        public const string HPF_OFF_S = "HPF OFF";
        public const string HPF_000625_S = "F(-3db) = 0.000625 x FS";
        public const string HPF_00125_S = "F(-3db) = 0.00125 x FS";
        public const string HPF_00250_S = "F(-3db) = 0.00250 x FS";
        public const string HPF_00950_S = "F(-3db) = 0.00950 x FS";
        private static readonly SPH0641Hpf[] listOfOptions = 
        { 
            new SPH0641Hpf(HPF_OFF),
            new SPH0641Hpf(HPF_000625),
            new SPH0641Hpf(HPF_00125),
            new SPH0641Hpf(HPF_00250),
            new SPH0641Hpf(HPF_00950) 
        };

        public static SPH0641Hpf[] ListOfOptions
        {
            get => listOfOptions;
        }
        public static SPH0641Hpf CreateFromValue(ushort v)
        {
            return new SPH0641Hpf(v);
        }


        public SPH0641Hpf(ushort value)
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
            string r = HPF_OFF_S;

            if (value == HPF_000625)
            {
                r = HPF_000625_S;
            }
            else if (value == HPF_00125)
            {
                r = HPF_00125_S;
            }
            else if (value == HPF_00250)
            {
                r = HPF_00250_S;
            }
            else if (value == HPF_00950)
            {
                r = HPF_00950_S;
            }
            else
            {
                value = HPF_OFF;
            }

            return r;
        }

    }



    public class SPH0641Gain
    {
        private static readonly string[] listOfOptions =
        {
                /*0x20*/
                "-48.2dB", "-44.6dB", "-42.1dB", "-38.6dB", "-36.1dB", "-32.6dB",
                "-30.1dB", "-26.6dB", "-24.1dB", "-20.6dB", "-18.1dB", "-14.5dB", "-12.0dB", "-8.5dB",
                "-6.0dB", "-2.5dB",
                /*0*/
                "0.0dB", "+3.5dB", "+6.0dB", "+9.5dB", "+12.0dB", "+15.6dB", "+18.1dB", "+21.6dB",
                "+24.1dB", "+27.6dB", "+30.1dB", "+33.6dB", "+36.1dB", "+39.6dB", "+42.1dB", "+45.7dB",
                "+48.2dB", "+51.7dB", "+54.2dB", "+57.7dB", "+60.2dB", "+63.7dB", "+66.2dB", "+69.7dB",
                "+72.2dB", "N/A"
        };

        public static SPH0641Gain[] ListOfOptions
        {
            get
            {
                SPH0641Gain[] list = new SPH0641Gain[listOfOptions.Length];


                for(int i = 0; i < listOfOptions.Length; i++)
                {
                    list[i] = SPH0641Gain.CreateFromValue((byte)i) as SPH0641Gain;
                }

                return list;
            }
        }
        public static SPH0641Gain CreateFromValue(byte v)
        {
            return new SPH0641Gain(v);
        }


        public SPH0641Gain(byte value)
        {
            this.value = value;
        }


        private byte value;

        public byte Value
        {
            get => value;
            set
            {
                if (value >= 0 && value < 0x10)
                {
                    this.value = (byte)(value + 0x20);
                }
                else if (value < 41)
                {
                    this.value = (byte)(value - 0x10);
                }
                else
                {
                    this.value = (byte)(0x2F);
                }

                this.value = value;
            }
        }

        public override string ToString()
        {

            if (this.value >= 0 && this.value < 0x19)
                return listOfOptions[this.value + 0x10];
            else if (this.value >= 0x20 && this.value < 0x30)
                return listOfOptions[this.value - 0x20];
            else
                return "N/A";
        }
    }



}
