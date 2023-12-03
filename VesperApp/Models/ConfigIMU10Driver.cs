using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace VesperApp.Models
{
    
    public class ConfigIMU10Driver : ConfigurationDeviceDriver
    {
        public const UInt32 BITMASK_ACC_ON = 0x01;
        public const UInt32 BITMASK_GYRO_ON = 0x02;
        public const UInt32 BITMASK_MAG_ON = 0x04;
        public const UInt32 BITMASK_BAR_ON = 0x08;
        public const UInt32 BITMASK_LP_ON = 0x10;

        private bool isStartup = true;
        private ArrayList atts = new ArrayList();

        public ConfigIMU10Driver() : base("IMU10", "Innertial Motion Unit sensor with 10 degrees of freedom")
        {
            this.imu_range = 0;

            this.MemoryBufferSize = 4 * 1024 + 256;
            this.FileSize = 512 * 1024;

            imu_sample_rate = new UInt32[3];

        }

        private UInt32 imu_range;


        [TypeConverter(typeof(IMU10AccRanges)),
        DisplayName("ACC Dynamic Range"),
        CategoryAttribute("IMU10 specific Settings"),
        Browsable(false)]
        [JsonPropertyName("imuRange")]
        public UInt32 ImuRange
        {
            get { return this.imu_range; }
            set { this.imu_range = value; }
        }

        private UInt32[] imu_sample_rate;

        public override UInt32[] SampleRate
        {
            get
            {
                this.imu_sample_rate[1] = imu_sample_rate1;
                this.imu_sample_rate[2] = imu_sample_rate2;
                return this.imu_sample_rate;
            }
            set
            {
                this.imu_sample_rate = value;
                imu_sample_rate1 = imu_sample_rate[1];
                imu_sample_rate2 = imu_sample_rate[2];
            }
        }

        private UInt32 imu_sample_rate1;
        private UInt32 imu_sample_rate2;


        [TypeConverter(typeof(IMU10SamplingRates)),
        DisplayName("Sample Rate - Configuration 1"),
        CategoryAttribute("IMU10 specific Settings")]
        [JsonIgnore]
        public string ImuSampleRate
        {
            get
            {
                if (this.imu_sample_rate1 == 0)
                    this.imu_sample_rate1 = 1;
                return GetIMU10SampleRate(this.imu_sample_rate1);
            }
            set
            {

                if (imu_lp_mode)
                {
                    this.imu_sample_rate1 = 11;
                    return;
                }

                switch (value)
                {
                    case IMU10SamplingRates.IMU10_R12:
                        this.imu_sample_rate1 = 1;
                        break;
                    case IMU10SamplingRates.IMU10_R26:
                        this.imu_sample_rate1 = 2;
                        break;
                    case IMU10SamplingRates.IMU10_R52:
                        this.imu_sample_rate1 = 3;
                        break;
                    case IMU10SamplingRates.IMU10_R104:
                        this.imu_sample_rate1 = 4;
                        break;
                    case IMU10SamplingRates.IMU10_R208:
                        this.imu_sample_rate1 = 5;
                        break;
                    case IMU10SamplingRates.IMU10_R416:
                        this.imu_sample_rate1 = 6;
                        break;
                    case IMU10SamplingRates.IMU10_R833:
                        this.imu_sample_rate1 = 7;
                        break;
                    case IMU10SamplingRates.IMU10_R1666:
                        this.imu_sample_rate1 = 8;
                        break;
                    case IMU10SamplingRates.IMU10_R3332:
                        this.imu_sample_rate1 = 9;
                        break;
                    case IMU10SamplingRates.IMU10_R6664:
                        this.imu_sample_rate1 = 10;
                        break;
                    default:
                        this.imu_sample_rate1 = 1;
                        ;
                        break;
                }
            }
        }

        [TypeConverter(typeof(IMU10SamplingRates)),
        DisplayName("Sample Rate - Configuration 2"),
        CategoryAttribute("IMU10 specific Settings")]
        [JsonIgnore]
        public string ImuSampleRate2
        {
            get
            {
                if (this.imu_sample_rate2 == 0)
                    this.imu_sample_rate2 = 1;
                return GetIMU10SampleRate(this.imu_sample_rate2);
            }
            set
            {

                //this.imu_sample_rate2 &= 0x0000FFFF;

                if (imu_lp_mode)
                {
                    this.imu_sample_rate2 = 11;
                    return;
                }

                switch (value)
                {
                    case IMU10SamplingRates.IMU10_R12:
                        this.imu_sample_rate2 = 1;
                        break;
                    case IMU10SamplingRates.IMU10_R26:
                        this.imu_sample_rate2 = 2;
                        break;
                    case IMU10SamplingRates.IMU10_R52:
                        this.imu_sample_rate2 = 3;
                        break;
                    case IMU10SamplingRates.IMU10_R104:
                        this.imu_sample_rate2 = 4;
                        break;
                    case IMU10SamplingRates.IMU10_R208:
                        this.imu_sample_rate2 = 5;
                        break;
                    case IMU10SamplingRates.IMU10_R416:
                        this.imu_sample_rate2 = 6;
                        break;
                    case IMU10SamplingRates.IMU10_R833:
                        this.imu_sample_rate2 = 7;
                        break;
                    case IMU10SamplingRates.IMU10_R1666:
                        this.imu_sample_rate2 = 8;
                        break;
                    case IMU10SamplingRates.IMU10_R3332:
                        this.imu_sample_rate2 = 9;
                        break;
                    case IMU10SamplingRates.IMU10_R6664:
                        this.imu_sample_rate2 = 10;
                        break;
                    default:
                        this.imu_sample_rate2 = 1;
                        ;
                        break;
                }
            }
        }

        private string GetIMU10SampleRate(uint sr)
        {
            string tmp = "";

            if (!imu_lp_mode && sr == 11)
            {
                imu_sample_rate1 = 1;
                imu_sample_rate2 = 1;
                sr = 1;
            }

            switch (sr)
            {
                case 1:
                    tmp = IMU10SamplingRates.IMU10_R12;
                    break;
                case 2:
                    tmp = IMU10SamplingRates.IMU10_R26;
                    break;
                case 3:
                    tmp = IMU10SamplingRates.IMU10_R52;
                    break;
                case 4:
                    tmp = IMU10SamplingRates.IMU10_R104;
                    break;
                case 5:
                    tmp = IMU10SamplingRates.IMU10_R208;
                    break;
                case 6:
                    tmp = IMU10SamplingRates.IMU10_R416;
                    break;
                case 7:
                    tmp = IMU10SamplingRates.IMU10_R833;
                    break;
                case 8:
                    tmp = IMU10SamplingRates.IMU10_R1666;
                    break;
                case 9:
                    tmp = IMU10SamplingRates.IMU10_R3332;
                    break;
                case 10:
                    tmp = IMU10SamplingRates.IMU10_R6664;
                    break;
                case 11:
                    tmp = "1Hz";
                    break;
                default:
                    tmp = IMU10SamplingRates.IMU10_R12;
                    break;
            }

            return tmp;
        }

        //[DisplayName("Sample Rate - Configuration 1"),
        //CategoryAttribute("IMU10 specific Settings")]
        //[JsonIgnore]
        //[PropertyOrder(1)]
        //[PropertyAttributesProvider("UnhiddenAttributeProvider")]
        //[ReadOnly(true)]
        //public string ImuLPSampleRate1
        //{
        //    get
        //    {
        //        imu_sample_rate1 = 10;
        //        imu_sample_rate2 = 10;
        //        return "1Hz";
        //    }
        //}

        //[DisplayName("Sample Rate - Configuration 2"),
        //CategoryAttribute("IMU10 specific Settings")]
        //[JsonIgnore]
        //[PropertyOrder(1)]
        //[PropertyAttributesProvider("UnhiddenAttributeProvider")]
        //[ReadOnly(true)]
        //public string ImuLPSampleRate2
        //{
        //    get
        //    {
        //        imu_sample_rate1 = 10;
        //        imu_sample_rate2 = 10;
        //        return "1Hz";
        //    }
        //}




        private bool imu_lp_mode;

#if false
        [TypeConverter(typeof(IMU10SamplingModes)),
        DisplayName("Sampling Modes"),
        CategoryAttribute("IMU10 specific Settings")]
        [JsonIgnore]
        public string ImuLowPower
        {
            get
            {
                //Console.WriteLine("LP get");

                if (imu_lp_mode || ((this.bitmask & BITMASK_LP_ON) == BITMASK_LP_ON))
                {
                    imu_lp_mode = true;
                    MagON = false;
                    GyroON = false;
                    imu_sample_rate1 = 11;
                    imu_sample_rate2 = 11;

                    if (isStartup)
                    {
                        while (atts.Count > 0)
                        {
                            //ReadOnlyAttributeProvider((MyConfigGridTypeConverter.PropertyAttributes)atts[0]);

                            atts.RemoveAt(0);
                        }
                        //string tmp;
                        //tmp = ImuLPSampleRate1;
                        //tmp = ImuLPSampleRate2;

                        isStartup = false;
                    }

                    return IMU10SamplingModes.IMU10_LP;
                }
                return IMU10SamplingModes.IMU10_NP;
            }
            set
            {
                //Console.WriteLine("LP set");

                switch (value)
                {
                    case IMU10SamplingModes.IMU10_NP:
                        {
                            imu_lp_mode = false;
                            this.bitmask &= ~(BITMASK_LP_ON);
                        }
                        break;
                    case IMU10SamplingModes.IMU10_LP:
                        {
                            imu_lp_mode = true;
                            this.bitmask |= BITMASK_LP_ON;
                        }
                        break;
                }
            }
        }
#endif

        [DisplayName("Enable BAR"),
        CategoryAttribute("IMU10 specific Settings"),
        DescriptionAttribute("Enable Barometer sensor inside IMU10 module")]
        [JsonIgnore]
        public bool BarON
        {
            get
            {
                if ((this.bitmask & BITMASK_BAR_ON) == BITMASK_BAR_ON)
                    return true;
                else
                    return false;
            }

            set
            {
                if (value == true)
                    this.bitmask |= BITMASK_BAR_ON;
                else
                    this.bitmask &= ~(BITMASK_BAR_ON);
            }
        }

        [DisplayName("Enable ACC"),
        CategoryAttribute("IMU10 specific Settings"),
        DescriptionAttribute("Enable Accelerometer sensor inside IMU10 module")]
        [JsonIgnore]
        public bool AccON
        {
            get
            {
                if ((this.bitmask & BITMASK_ACC_ON) == BITMASK_ACC_ON)
                    return true;
                else
                    return false;
            }

            set
            {
                if (value == true)
                    this.bitmask |= BITMASK_ACC_ON;
                else
                    this.bitmask &= ~(BITMASK_ACC_ON);
            }
        }

        [DisplayName("Enable GYRO"),
        CategoryAttribute("IMU10 specific Settings"),
        DescriptionAttribute("Enable Gyro sensor inside IMU10 module")]
        [JsonIgnore]
        public bool GyroON
        {
            get
            {
                if ((this.bitmask & BITMASK_GYRO_ON) == BITMASK_GYRO_ON)
                    return true;
                else
                    return false;
            }

            set
            {
                if (value == true)
                    this.bitmask |= BITMASK_GYRO_ON;
                else
                    this.bitmask &= ~(BITMASK_GYRO_ON);
            }
        }

        [DisplayName("Enable Magnetometer"),
        CategoryAttribute("IMU10 specific Settings"),
        DescriptionAttribute("Enable Magnetometer sensor inside IMU10 module")]
        [JsonIgnore]
        public bool MagON
        {
            get
            {
                if ((this.bitmask & BITMASK_MAG_ON) == BITMASK_MAG_ON)
                    return true;
                else
                    return false;
            }

            set
            {
                if (value == true)
                    this.bitmask |= BITMASK_MAG_ON;
                else
                    this.bitmask &= ~(BITMASK_MAG_ON);
            }
        }

        [TypeConverter(typeof(IMU10AccRanges)),
        DisplayName("ACC Dynamic Range"),
        CategoryAttribute("IMU10 specific Settings"),
        DescriptionAttribute("Choose dynamic range for accelerometer sensor (in G)")]
        [JsonIgnore]
        public string AccRange
        {
            get
            {
                string tmp = "";
                switch (((UInt16)((this.imu_range >> 16) & 0xFFFF)))
                {
                    case 0:
                        tmp = IMU10AccRanges.IMU10_G2;
                        break;
                    case 1:
                        tmp = IMU10AccRanges.IMU10_G4;
                        break;
                    case 2:
                        tmp = IMU10AccRanges.IMU10_G8;
                        break;
                    case 3:
                        tmp = IMU10AccRanges.IMU10_G16;
                        break;
                    default:
                        tmp = IMU10AccRanges.IMU10_G2;
                        break;
                }

                return tmp;
            }
            set
            {
                this.imu_range &= 0x0000FFFF;

                switch (value)
                {
                    case IMU10AccRanges.IMU10_G2:
                        this.imu_range |= (0 << 16);
                        break;
                    case IMU10AccRanges.IMU10_G4:
                        this.imu_range |= (1 << 16);
                        break;
                    case IMU10AccRanges.IMU10_G8:
                        this.imu_range |= (2 << 16);
                        break;
                    case IMU10AccRanges.IMU10_G16:
                        this.imu_range |= (3 << 16);
                        break;
                    default:
                        this.imu_range |= (0 << 16);
                        break;
                }
            }
        }

        [TypeConverter(typeof(IMU10GyroRanges)),
        DisplayName("GYRO Dynamic Range"),
        CategoryAttribute("IMU10 specific Settings"),
        DescriptionAttribute("Choose dynamic range for gyro sensor (in degrees / second)")]
        [JsonIgnore]
        public string GyroRange
        {
            get
            {
                string tmp = "";
                switch (((UInt16)((this.imu_range) & 0xFFFF)))
                {
                    case 0:
                        tmp = IMU10GyroRanges.IMU10_D125;
                        break;
                    case 1:
                        tmp = IMU10GyroRanges.IMU10_D250;
                        break;
                    case 2:
                        tmp = IMU10GyroRanges.IMU10_D500;
                        break;
                    case 3:
                        tmp = IMU10GyroRanges.IMU10_D1000;
                        break;
                    case 4:
                        tmp = IMU10GyroRanges.IMU10_D2000;
                        break;
                    default:
                        tmp = IMU10GyroRanges.IMU10_D125;
                        break;
                }

                return tmp;
            }
            set
            {
                this.imu_range &= 0xFFFF0000;

                switch (value)
                {
                    case IMU10GyroRanges.IMU10_D125:
                        this.imu_range |= (0);
                        break;
                    case IMU10GyroRanges.IMU10_D250:
                        this.imu_range |= (1);
                        break;
                    case IMU10GyroRanges.IMU10_D500:
                        this.imu_range |= (2);
                        break;
                    case IMU10GyroRanges.IMU10_D1000:
                        this.imu_range |= (3);
                        break;
                    case IMU10GyroRanges.IMU10_D2000:
                        this.imu_range |= (4);
                        break;
                    default:
                        this.imu_range |= (0 << 16);
                        break;
                }
            }
        }

    }




    public class IMU10AccRanges : StringConverter
    {
        public const string IMU10_G2 = "±2g";
        public const string IMU10_G4 = "±4g";
        public const string IMU10_G8 = "±8g";
        public const string IMU10_G16 = "±16g";

        public override bool GetStandardValuesSupported(
                           ITypeDescriptorContext ? context)
        {
            return true;
        }


        public override StandardValuesCollection
                     GetStandardValues(ITypeDescriptorContext ? context)
        {
            return new StandardValuesCollection(new string[] {
                IMU10_G2,
                IMU10_G4,
                IMU10_G8,
                IMU10_G16});
        }
    }

    public class IMU10SamplingRates : StringConverter
    {
        public const string IMU10_R12 = "12.5Hz";
        public const string IMU10_R26 = "26Hz";
        public const string IMU10_R52 = "52Hz";
        public const string IMU10_R104 = "104Hz";
        public const string IMU10_R208 = "208Hz";
        public const string IMU10_R416 = "416Hz";
        public const string IMU10_R833 = "833Hz";
        public const string IMU10_R1666 = "1666Hz";
        public const string IMU10_R3332 = "3332Hz";
        public const string IMU10_R6664 = "6664Hz";


        public override bool GetStandardValuesSupported(
                           ITypeDescriptorContext ? context)
        {
            return true;
        }


        public override StandardValuesCollection
                     GetStandardValues(ITypeDescriptorContext ? context)
        {
            return new StandardValuesCollection(new string[] {
                IMU10_R12,
                IMU10_R26,
                IMU10_R52,
                IMU10_R104,
                IMU10_R208,
                IMU10_R416,
                IMU10_R833,
                IMU10_R1666,
                IMU10_R3332,
                IMU10_R6664
            });
        }
    }

    public class IMU10SamplingModes : StringConverter
    {
        public const string IMU10_LP = "Low power mode";
        public const string IMU10_NP = "Normal power mode";

        public override bool GetStandardValuesSupported(
                           ITypeDescriptorContext ? context)
        {
            return true;
        }


        public override StandardValuesCollection
                     GetStandardValues(ITypeDescriptorContext ? context)
        {
            return new StandardValuesCollection(new string[] {
                IMU10_LP,
                IMU10_NP
            });
        }
    }

    public class IMU10GyroRanges : StringConverter
    {
        public const string IMU10_D125 = "±125dps";
        public const string IMU10_D250 = "±250dps";
        public const string IMU10_D500 = "±500dps";
        public const string IMU10_D1000 = "±1000dps";
        public const string IMU10_D2000 = "±2000dps";

        public override bool GetStandardValuesSupported(
                           ITypeDescriptorContext ? context)
        {
            return true;
        }


        public override StandardValuesCollection
                     GetStandardValues(ITypeDescriptorContext ? context)
        {
            return new StandardValuesCollection(new string[] {
                IMU10_D125,
                IMU10_D250,
                IMU10_D500,
                IMU10_D1000,
                IMU10_D2000});
        }
    }

}
