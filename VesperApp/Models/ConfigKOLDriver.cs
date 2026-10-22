using System;
using System.Collections.Generic;
using System.Text.Json;
using System.ComponentModel;
using System.Text.Json.Serialization;
using VesperApp.Services;

namespace VesperApp.Models
{
    /// <summary>
    /// KOL microphones. The filter chain is chosen from the sample rate (<see cref="MicChainSolver"/>,
    /// firmware mic_chain.c): the old "Use CIC4" and "Digital filter /4" settings are gone — they were
    /// confusing and the filter checkbox worked the wrong way round (256 = /4 on, 257 = off). Configs are
    /// always written with "filter" bit 15; an old config is converted on load to the same output level.
    /// </summary>
    public class ConfigKOLDriver : ConfigurationDeviceDriver, IJsonOnDeserialized, IMicChainSolvable
    {
        public const UInt32 BITMASK_MIKE_LED = 0x02;
        public const UInt32 SAMPLING_RATE_MASTERCLOCK = 4800000;

        public const UInt32 MAX_SAMPLING_RATE = 100000;
        public const UInt32 MIN_SAMPLING_RATE = 4800;
        public const UInt32 DEF_SAMPLING_RATE = 48000;

        public const Int16 MAX_GAIN_DB = 72;
        public const Int16 MIN_GAIN_DB = -48;


        private UInt16 thresholdup;
        private UInt16 thresholddown;
        private bool cic4;
        private UInt16 gain;
        private UInt16 hpf;
        private UInt16 num_mikes;

        public ConfigKOLDriver() : base("KOL", "KOL - Scientific Audio Configuration")
        {
            this.FileSize = 8 * 1024 * 1024;
        }


        public override void Load(ConfigurationDeviceDriver ldrv)
        {
            if (ldrv is not null)
            {
                ConfigKOLDriver driver = (ldrv as ConfigKOLDriver)!;

                base.Load(ldrv);
                this.ThresholdUp = driver.ThresholdUp;
                this.ThresholdDown = driver.ThresholdDown;
                this.UseCic4Filter = driver.UseCic4Filter;
                this.Gain = driver.Gain;
                this.MikesOn = driver.MikesOn;
                this.HPF = driver.HPF;
                this.digitalFilter = driver.digitalFilter;
                this.MigrationNote = driver.MigrationNote;
            }
        }

        /// <summary>Set when an old config was converted on load; shown by the solver.</summary>
        [JsonIgnore, Browsable(false)]
        public string? MigrationNote { get; private set; }

        private uint RefRate => MicChainSolver.RefRate(SampleRate[1], SampleRate[2], 0);

        /// <summary>An old config ("filter" without bit 15) is rebuilt with the automatic chain at the same
        /// output level at the reference rate, where the hardware allows it (MicChainSolver.MigrateLegacy).</summary>
        public void OnDeserialized()
        {
            var m = MicChainSolver.MigrateLegacy(SampleRate[1], SampleRate[2], digitalFilter, cic4, gain);
            gain = m.Gain;
            SampleRate[1] = m.Rate1;
            SampleRate[2] = m.Rate2;
            MigrationNote = m.Note;
            digitalFilter |= MicChainSolver.AutoBit;
            cic4 = false;
        }

        public MicChainSolver.SolverReport MicChainReport() =>
            MicChainSolver.BuildReport(SampleRate[1], SampleRate[2], gain, MicChainSolver.Product.Kol, MigrationNote, hpf);

        public void ApplyRecommendedGain()
        {
            uint refFs = RefRate;
            if (refFs == 0) return;
            Gain = KOLGain.CreateFromValue((byte)MicChainSolver.GainValue(MicChainSolver.Recommended(refFs)));
        }

        public void ApplyRecommendedHpf()
        {
            uint refFs = RefRate;
            if (refFs == 0) return;
            HighPassFilter = KOLHpf.CreateFromValue((ushort)MicChainSolver.RecommendedHpf(refFs));
        }


        [DisplayName("Number of Active Microphones"),
        CategoryAttribute("Ultrasonic Mike specific Settings"),
        DescriptionAttribute("How many of the KOL's four microphones record: 1, 2 or 4.")]
        [JsonPropertyName("mikeon"), JsonPropertyOrder(20)]
        [Browsable(true)]
        public UInt32 MikesOn
        {
            get { return (UInt32)this.num_mikes; }
            set
            {
                if (value > 4)
                    this.num_mikes = 4;
                else
                    this.num_mikes = (UInt16)value;

                OnPropertyChanged();
            }
        }


        [DisplayName("Threshold Up"),
        CategoryAttribute("Ultrasonic Mike specific Settings"),
        DescriptionAttribute("Reserved: level-triggered recording is not implemented in the firmware yet and this value is not used. Leave at 0.")]
        [JsonPropertyName("thresup"), JsonPropertyOrder(21)]
        [Browsable(true)]
        public UInt32 ThresholdUp
        {
            get { return (UInt32)this.thresholdup; }
            set
            {
                if (value > 2047)
                    throw new ArgumentException("Maximum threshold level is 2047");
                else
                    this.thresholdup = (UInt16)value;
                OnPropertyChanged();
            }
        }

        [DisplayName("Threshold Down"),
        CategoryAttribute("Ultrasonic Mike specific Settings"),
        DescriptionAttribute("Reserved: any value other than 0 only switches on the microphone filter’s sound-activity detector with default settings, it is not a level trigger. Leave at 0.")]
        [JsonPropertyName("thresdn"), JsonPropertyOrder(22)]
        [Browsable(true)]
        public UInt32 ThresholdDown
        {
            get { return (UInt32)this.thresholddown; }
            set
            {
                if (value > 2047)
                    throw new ArgumentException("Maximum threshold level is 2047");
                else
                    this.thresholddown = (UInt16)value;
                OnPropertyChanged();
            }
        }

        

        private UInt32 CheckSamplingRate(UInt32 ovalue, UInt32 nvalue)
        {
            UInt32 rval = nvalue;
           /* if(nvalue == 0)
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
                rval = DEF_SAMPLING_RATE;
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
        [JsonPropertyName("cic4"), JsonPropertyOrder(23)]
        [Browsable(false)]   // chosen from the sample rate (MicChainSolver); kept for the JSON field
        public bool UseCic4Filter
        {
            get => cic4;
            set
            {
                cic4 = value;
                OnPropertyChanged();
            }
        }


        private UInt16 digitalFilter;


        [DisplayName("Digital Filter"),
        CategoryAttribute("Ultrasonic Mike specific Settings"),
        Browsable(false)]
        [JsonPropertyName("filter"), JsonPropertyOrder(24)]
        public UInt16 DFilter
        {
            /* always written in auto mode, with the reshape bits older firmware needs (MicChainSolver.FilterValue) */
            get { return MicChainSolver.FilterValue(RefRate); }
            set { this.digitalFilter = value; }
        }

        [DisplayName("Enable Digital Filter"),
        TypeConverter(typeof(bool)),
        CategoryAttribute("Ultrasonic Mike specific Settings"),
        DescriptionAttribute("Enables Digital LPF with /4 decimation ratio"),
        Browsable(false)]   // replaced by the automatic chain; this checkbox worked the wrong way round
        [JsonIgnore]
        public bool EnableDigitalFilter
        {
            get
            {
                if (this.digitalFilter == 257)
                    return true;
                else
                    return false;
            }
            set
            {
                if (value == true)
                    this.digitalFilter = 257;
                else
                    this.digitalFilter = 256;

                OnPropertyChanged();
            }
        }

        [DisplayName("HPF"),
        CategoryAttribute("Ultrasonic Mike specific Settings"),
        Browsable(false)]
        [JsonPropertyName("hpf"), JsonPropertyOrder(25)]
        public UInt16 HPF
        {
            get { return this.hpf; }
            set { this.hpf = value; }
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
            get
            {
                return KOLHpf.CreateFromValue(this.HPF);
            }
            set
            {
                this.HPF = value.Value;
                OnPropertyChanged();
            }
        }

        [DisplayName("Gain"),
        CategoryAttribute("Ultrasonic Mike specific Settings"),
        Browsable(false)]
        [JsonPropertyName("gain"), JsonPropertyOrder(26)]
        public UInt16 GAIN
        {
            get { return this.gain; }
            set { this.gain = value; }
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
            get
            {
                return KOLGain.CreateFromValue((byte)this.GAIN);
            }
            set
            {
                this.gain = (UInt16)value.Value;
                OnPropertyChanged();
            }
        }



        [CategoryAttribute("Standard configuration"),
        DescriptionAttribute("Microphone sample rate while the schedule runs Config 1 (Off = microphones not used). The unit picks the best filter for it; press Solve for details."),
        DisplayName("Sample Rate [1]")]
        [JsonIgnore]
        [Browsable(true)]
        [MicSolver]
        public KOLSampleRate Rate1
        {
            get => KOLSampleRate.CreateFromValue(SampleRate[1]);
            set { SampleRate[1] = value.Value; OnPropertyChanged(); }
        }

        [CategoryAttribute("Standard configuration"),
        DescriptionAttribute("Microphone sample rate while the schedule runs Config 2 (Off = microphones not used). The unit picks the best filter for it; press Solve for details."),
        DisplayName("Sample Rate [2]")]
        [JsonIgnore]
        [Browsable(true)]
        [MicSolver]
        public KOLSampleRate Rate2
        {
            get => KOLSampleRate.CreateFromValue(SampleRate[2]);
            set { SampleRate[2] = value.Value; OnPropertyChanged(); }
        }

        [CategoryAttribute("Standard configuration"),
        DescriptionAttribute("Sample Rate of the sensor operating in Config1 schedule"),
        DisplayName("Sample Rate [1]")]
        [JsonIgnore]
        [Browsable(false)]   // edited as Rate1 (predefined rates)
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
        [Browsable(false)]   // edited as Rate2 (predefined rates)
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
        DescriptionAttribute("Blink the LED while the microphones record.")]
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
