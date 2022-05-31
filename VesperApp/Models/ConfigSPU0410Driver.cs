using System;
using System.Collections.Generic;
using System.Text.Json;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace VesperApp.Models
{
    public class ConfigSPU0410Driver : ConfigurationDeviceDriver
    {
        public const UInt32 BITMASK_MIKE_RESOLUTION = 0x01;
        public const UInt32 BITMASK_MIKE_LED = 0x02;

        public ConfigSPU0410Driver() : base("SPU0410", "Ultrasonic microphone recording")
        {
            this.MemoryBufferSize = 60 * 1024;
            this.FileSize = 4 * 1024 * 1024;
        }

        private UInt32 threshold_level;

        [DisplayName("Threshold"),
        CategoryAttribute("Ultrasonic Mike specific Settings"),
        DescriptionAttribute("Enables recording of audio data upon audio threshold level trigger. Threshold scale is normalized 0-4095. Note that when non-zero, Window Length defined time to record on threshold detection and Window Rate - time to wait after recording for Window Length time.")]
        [JsonPropertyName("threshold")]
        public UInt32 Threshold
        {
            get { return this.threshold_level; }
            set
            {
                if (value > 2047)
                    throw new ArgumentException("Maximum threshold level is 2047");
                else
                    this.threshold_level = value;

            }
        }

        [DisplayName("Mike sampler resolution"),
        TypeConverter(typeof(MikeResolutionConverter)),
        CategoryAttribute("Ultrasonic Mike specific Settings"),
        DescriptionAttribute("All data sampled at 12bit however saving data to the disk can be downsampled to 8bit or kept 12bit (saved as 16bit right justified samples)")]
        [JsonIgnore]
        public string Resolution12bit
        {
            get
            {
                if ((this.bitmask & BITMASK_MIKE_RESOLUTION) == BITMASK_MIKE_RESOLUTION)
                    return MikeResolutionConverter.MIKE_RES_12BIT;
                else
                    return MikeResolutionConverter.MIKE_RES_8BIT;
            }
            set
            {
                if (value == MikeResolutionConverter.MIKE_RES_12BIT)
                    this.bitmask |= BITMASK_MIKE_RESOLUTION;
                else
                    this.bitmask &= ~(BITMASK_MIKE_RESOLUTION);
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




    public class MikeResolutionConverter : StringConverter
    {
        public const string MIKE_RES_12BIT = "12bit";
        public const string MIKE_RES_8BIT = "8bit";

        public override bool GetStandardValuesSupported(
                           ITypeDescriptorContext context)
        {
            return true;
        }


        public override StandardValuesCollection
                     GetStandardValues(ITypeDescriptorContext context)
        {
            return new StandardValuesCollection(new string[] {
                MIKE_RES_12BIT,
                MIKE_RES_8BIT});
        }
    }


}
