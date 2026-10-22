using System;
using System.ComponentModel;
using System.Text.Json.Serialization;
using VesperApp.Services;

namespace VesperApp.Models
{
    /// <summary>
    /// Vesper / Pipistrelle ultrasonic microphone (one SPH0641LU4H). The filter chain, rates, Solve window,
    /// migration and sound-triggered recording are shared with the KOL in <see cref="MicDriverBase"/>.
    /// </summary>
    public class ConfigSPH0641Driver : MicDriverBase
    {
        public const UInt32 BITMASK_MIKE_LED = 0x02;

        public ConfigSPH0641Driver() : base("SPH0641", "Vesper/Pipistrelle V4 Ultrasonic microphone recording") { }

        [JsonIgnore, Browsable(false)]
        public override MicChainSolver.Product Product => MicChainSolver.Product.VesperPipistrelle;

        [JsonIgnore, Browsable(false)]
        public override int MicCount => 1;

        [DisplayName("High Pass Filter"),
        TypeConverter(typeof(SPH0641Hpf)),
        Browsable(true),
        CategoryAttribute("Ultrasonic Mike specific Settings"),
        DescriptionAttribute("Removes rumble and the microphone’s DC offset. The cut-off is a fraction of the sample rate, e.g. 0.0025 × 200 kHz = 500 Hz. Press Solve to see the cut-off of every setting at your rates.")]
        [JsonIgnore]
        [MicSolver]
        public SPH0641Hpf HighPassFilter
        {
            get => SPH0641Hpf.CreateFromValue(this.hpf);
            set { this.hpf = value.Value; OnPropertyChanged(); }
        }

        [DisplayName("Programmable Gain"),
        TypeConverter(typeof(SPH0641Gain)),
        CategoryAttribute("Ultrasonic Mike specific Settings"),
        DescriptionAttribute("Loudness of the recording at Sample Rate [1] (or [2] if [1] is Off); the unit keeps the same loudness at the other rate. Press Solve to see where loud sounds clip and whether quiet ones are kept."),
        Browsable(true)]
        [JsonIgnore]
        [MicSolver]
        public SPH0641Gain Gain
        {
            get => SPH0641Gain.CreateFromValue((byte)this.gain);
            set { this.gain = (UInt16)value.Value; OnPropertyChanged(); }
        }

        [DisplayName("Mike activity LED indication"),
        TypeConverter(typeof(bool)),
        CategoryAttribute("Ultrasonic Mike specific Settings"),
        DescriptionAttribute("Blink the LED while the microphones record.")]
        [JsonIgnore]
        public bool EnableLEDIndication
        {
            get => (this.bitmask & BITMASK_MIKE_LED) == BITMASK_MIKE_LED;
            set
            {
                if (value) this.bitmask |= BITMASK_MIKE_LED;
                else this.bitmask &= ~(BITMASK_MIKE_LED);
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
