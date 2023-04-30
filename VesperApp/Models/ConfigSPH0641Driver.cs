using System;
using System.Collections.Generic;
using System.Text.Json;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace VesperApp.Models
{
    public enum HPF_VALUES { MDF_HPS_OFF = 0, MDF_HPF_CUTOFF_0_000625FPCM = 1, MDF_HPF_CUTOFF_0_00125FPCM = 2, MDF_HPF_CUTOFF_0_0025FPCM = 3, MDF_HPF_CUTOFF_0_0095FPCM = 4}

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
        private bool digital_filter;
        private UInt16 gain;
        private UInt16 hpf;

        public ConfigSPH0641Driver() : base("SPH0641", "Vesper/Pipistrelle V4 Ultrasonic microphone recording")
        {
            this.MemoryBufferSize = 63 * 1024;
            this.FileSize = MemoryBufferSize*512;
        }


        [DisplayName("Threshold Up"),
        CategoryAttribute("Ultrasonic Mike specific Settings"),
        DescriptionAttribute("Enables recording of audio data upon audio threshold level trigger.")]
        [JsonPropertyName("thresup")]
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
        [JsonPropertyName("thresdn")]
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
        [JsonPropertyName("cic4")]
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






    }




}
