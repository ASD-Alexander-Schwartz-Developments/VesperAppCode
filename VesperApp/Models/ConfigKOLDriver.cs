using System;
using System.ComponentModel;
using System.Text.Json.Serialization;
using VesperApp.Services;

namespace VesperApp.Models
{
    /// <summary>
    /// KOL microphones (4-microphone array). The filter chain, rates, Solve window, migration and
    /// sound-triggered recording are shared with Vesper / Pipistrelle in <see cref="MicDriverBase"/>.
    /// </summary>
    public class ConfigKOLDriver : MicDriverBase
    {
        public const UInt32 BITMASK_MIKE_LED = 0x02;

        /// <summary>Microphones a new KOL driver records: all four.</summary>
        public const UInt16 DefaultMicCount = 4;

        /// <summary>
        /// The counts the KOL variants have (1, 2 or 4 microphones). The firmware copies "mikeon" straight into
        /// MIKE_NUMBER (ultrasonic.c) and loops over that many microphones: 0 starts no microphone at all (the unit
        /// "records" with no audio files), above 4 runs past its four-entry microphone tables.
        /// </summary>
        public static readonly UInt16[] ValidMicCounts = { 1, 2, 4 };

        /// <summary>A valid microphone count for <paramref name="value"/>: 1, 2 or 4 as given; 0 (not set) = 4;
        /// 3 or above 4 = 4 (never fewer microphones than asked for).</summary>
        public static UInt16 CoerceMicCount(UInt32 value) => value switch
        {
            1 => 1,
            2 => 2,
            _ => DefaultMicCount,
        };

        private UInt16 num_mikes = DefaultMicCount;

        public ConfigKOLDriver() : base("KOL", "KOL - Scientific Audio Configuration") { }

        [JsonIgnore, Browsable(false)]
        public override MicChainSolver.Product Product => MicChainSolver.Product.Kol;

        [JsonIgnore, Browsable(false)]
        public override int MicCount => num_mikes;

        public override void Load(ConfigurationDeviceDriver ldrv)
        {
            if (ldrv is not null)
            {
                base.Load(ldrv);
                this.MikesOn = (ldrv as ConfigKOLDriver)!.MikesOn;
            }
        }

        [DisplayName("Number of Active Microphones"),
        CategoryAttribute("Ultrasonic Mike specific Settings"),
        DescriptionAttribute("How many of the KOL's four microphones record: 1, 2 or 4 (default 4). Other values are set to 4.")]
        [JsonPropertyName("mikeon"), JsonPropertyOrder(20)]
        [Browsable(true)]
        public UInt32 MikesOn
        {
            get { return (UInt32)this.num_mikes; }
            set
            {
                /* never 0: the firmware would start no microphone (see ValidMicCounts) */
                this.num_mikes = CoerceMicCount(value);

                OnPropertyChanged();
            }
        }

        [DisplayName("High Pass Filter"),
        TypeConverter(typeof(KOLHpf)),
        Browsable(true),
        CategoryAttribute("Ultrasonic Mike specific Settings"),
        DescriptionAttribute("Removes rumble and the microphone’s DC offset. The cut-off is a fraction of the sample rate, e.g. 0.0025 × 200 kHz = 500 Hz. Press Solve to see the cut-off of every setting at your rates.")]
        [JsonIgnore]
        [MicSolver]
        public KOLHpf HighPassFilter
        {
            get => KOLHpf.CreateFromValue(this.hpf);
            set { this.hpf = value.Value; OnPropertyChanged(); }
        }

        [DisplayName("Programmable Gain"),
        TypeConverter(typeof(KOLGain)),
        CategoryAttribute("Ultrasonic Mike specific Settings"),
        DescriptionAttribute("Loudness of the recording at Sample Rate [1] (or [2] if [1] is Off); the unit keeps the same loudness at the other rate and in Live Audio. Press Solve to see where loud sounds clip and whether quiet ones are kept."),
        Browsable(true)]
        [JsonIgnore]
        [MicSolver]
        public KOLGain Gain
        {
            get => KOLGain.CreateFromValue((byte)this.gain);
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


    public class KOLHpf
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
        private static readonly KOLHpf[] listOfOptions = 
        { 
            new KOLHpf(HPF_OFF),
            new KOLHpf(HPF_000625),
            new KOLHpf(HPF_00125),
            new KOLHpf(HPF_00250),
            new KOLHpf(HPF_00950) 
        };

        public static KOLHpf[] ListOfOptions
        {
            get => listOfOptions;
        }
        public static KOLHpf CreateFromValue(ushort v)
        {
            return new KOLHpf(v);
        }


        public KOLHpf(ushort value)
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



    public class KOLGain
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

        public static KOLGain[] ListOfOptions
        {
            get
            {
                KOLGain[] list = new KOLGain[listOfOptions.Length];


                for(int i = 0; i < listOfOptions.Length; i++)
                {
                    list[i] = KOLGain.CreateFromValue((byte)i) as KOLGain;
                }

                return list;
            }
        }
        public static KOLGain CreateFromValue(byte v)
        {
            return new KOLGain(v);
        }


        public KOLGain(byte value)
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
