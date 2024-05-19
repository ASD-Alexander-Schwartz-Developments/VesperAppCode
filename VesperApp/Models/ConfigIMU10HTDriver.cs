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
    
    public class ConfigIMU10HTDriver : ConfigurationDeviceDriver
    {
        public const UInt32 BITMASK_ACC_ON = 0x01;
        public const UInt32 BITMASK_GYRO_ON = 0x02;
        public const UInt32 BITMASK_MAG_ON = 0x04;
        public const UInt32 BITMASK_BAR_ON = 0x08;
        private const UInt32 IMU10HT_BITMASK_LED = 0x20;


        private bool isStartup = true;
        private ArrayList atts = new ArrayList();

        public ConfigIMU10HTDriver() : base("IMU10HT", "Low Noise Innertial Motion Unit sensor with 10 degrees of freedom")
        {
            this.imu_range = 0;

            this.FileSize = 512 * 1024;
        }

        public override void Load(ConfigurationDeviceDriver ldrv)
        {
            if (ldrv is not null)
            {
                ConfigIMU10HTDriver driver = (ldrv as ConfigIMU10HTDriver)!;

                base.Load(ldrv);
                this.ImuRange = driver.ImuRange;
            }
        }



        private UInt32 imu_range;


        [DisplayName("ACC Dynamic Range"),
        CategoryAttribute("IMU10 specific Settings"),
        Browsable(false)]
        [JsonPropertyName("imuRange"), JsonPropertyOrder(20)]
        public UInt32 ImuRange
        {
            get { return this.imu_range; }
            set { this.imu_range = value; }
        }

        [Browsable(true)]
        [JsonIgnore]
        [CategoryAttribute("Standard configuration"),
        DescriptionAttribute("Should LED indicate activity is this driver"),
        DisplayName("LED Activity")]
        public override bool IsLEDActive
        {
            get => ((Bitmask & IMU10HT_BITMASK_LED) == IMU10HT_BITMASK_LED);
            set
            {
                if (value == true)
                    Bitmask |= IMU10HT_BITMASK_LED;
                else
                    Bitmask &= ~((UInt32)IMU10HT_BITMASK_LED);

                OnPropertyChanged();
            }
        }


        [DisplayName("Enable BAR"),
        Browsable(true),
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

                OnPropertyChanged();
            }
        }

        [DisplayName("Enable ACC"),
        Browsable(true),
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

                OnPropertyChanged();
            }
        }

        [DisplayName("Enable GYRO"),
        Browsable(true),
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

                OnPropertyChanged();
            }
        }

        [DisplayName("Enable Magnetometer"),
        Browsable(true),
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

                OnPropertyChanged();
            }
        }

        [DisplayName("ACC Dynamic Range"),
        Browsable(true),
        CategoryAttribute("IMU10 specific Settings"),
        DescriptionAttribute("Choose dynamic range for accelerometer sensor (in G)")]
        [JsonIgnore]
        public IMU10HTAccRanges AccRange
        {
            get
            {
                return IMU10HTAccRanges.CreateFromValue((UInt16)((this.imu_range >> 16) & 0xFFFF));
            }
            set
            {
                this.imu_range &= 0x0000FFFF;

                switch (value.Value)
                {
                    case IMU10HTAccRanges.IMU10HT_G2N:
                        this.imu_range |= (IMU10HTAccRanges.IMU10HT_G2N << 16);
                        break;
                    case IMU10HTAccRanges.IMU10HT_G4N:
                        this.imu_range |= (IMU10HTAccRanges.IMU10HT_G4N << 16);
                        break;
                    case IMU10HTAccRanges.IMU10HT_G8N:
                        this.imu_range |= (IMU10HTAccRanges.IMU10HT_G8N << 16);
                        break;
                    case IMU10HTAccRanges.IMU10HT_G16N:
                        this.imu_range |= (IMU10HTAccRanges.IMU10HT_G16N << 16);
                        break;
                    default:
                        this.imu_range |= (IMU10HTAccRanges.IMU10HT_G2N << 16);
                        break;
                }
                OnPropertyChanged();
            }
        }

        [DisplayName("GYRO Dynamic Range"),
        Browsable(true),
        CategoryAttribute("IMU10 specific Settings"),
        DescriptionAttribute("Choose dynamic range for gyro sensor (in degrees / second)")]
        [JsonIgnore]
        public IMU10HTGyroRanges GyroRange
        {
            get
            {
                return IMU10HTGyroRanges.CreateFromValue((UInt16)((this.imu_range) & 0xFFFF));
            }
            set
            {
                this.imu_range &= 0xFFFF0000;

                switch (value.Value)
                {
                    case IMU10HTGyroRanges.IMU10HT_D250N:
                        this.imu_range |= (IMU10HTGyroRanges.IMU10HT_D250N);
                        break;
                    case IMU10HTGyroRanges.IMU10HT_D500N:
                        this.imu_range |= (IMU10HTGyroRanges.IMU10HT_D500N);
                        break;
                    case IMU10HTGyroRanges.IMU10HT_D1000N:
                        this.imu_range |= (IMU10HTGyroRanges.IMU10HT_D1000N);
                        break;
                    case IMU10HTGyroRanges.IMU10HT_D2000N:
                        this.imu_range |= (IMU10HTGyroRanges.IMU10HT_D2000N);
                        break;
                    default:
                        this.imu_range |= (0 << 16);
                        break;
                }
                OnPropertyChanged();
            }
        }
    }




    public class IMU10HTAccRanges
    {
        public const ushort IMU10HT_G2N = 0;
        public const ushort IMU10HT_G4N = 1;
        public const ushort IMU10HT_G8N = 2;
        public const ushort IMU10HT_G16N = 3;

        public const string IMU10HT_G2 = "±2g";
        public const string IMU10HT_G4 = "±4g";
        public const string IMU10HT_G8 = "±8g";
        public const string IMU10HT_G16 = "±16g";

        private static readonly IMU10HTAccRanges[] listOfConstants = 
        { 
            new IMU10HTAccRanges(IMU10HT_G2N),
            new IMU10HTAccRanges(IMU10HT_G4N),
            new IMU10HTAccRanges(IMU10HT_G8N),
            new IMU10HTAccRanges(IMU10HT_G16N) 
        };

        public static IMU10HTAccRanges[] ListOfLength
        {
            get => listOfConstants;
        }

        public static IMU10HTAccRanges CreateFromValue(ushort v)
        {
            return new IMU10HTAccRanges(v);
        }


        public IMU10HTAccRanges(ushort value)
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
            string r = IMU10HT_G2;

            if (value == IMU10HT_G4N)
            {
                r = IMU10HT_G4;
            }
            else if (value == IMU10HT_G8N)
            {
                r = IMU10HT_G8;
            }
            else if (value == IMU10HT_G16N)
            {
                r = IMU10HT_G16;
            }
            else
            {
                value = IMU10HT_G2N;
            }

            return r;
        }
    }


    public class IMU10HTGyroRanges
    {
        public const ushort IMU10HT_D250N = 0;
        public const ushort IMU10HT_D500N = 1;
        public const ushort IMU10HT_D1000N = 2;
        public const ushort IMU10HT_D2000N = 3;

        public const string IMU10HT_D250 = "±250dps";
        public const string IMU10HT_D500 = "±500dps";
        public const string IMU10HT_D1000 = "±1000dps";
        public const string IMU10HT_D2000 = "±2000dps";

        private static readonly IMU10HTGyroRanges[] listOfConstants = 
        { 
            new IMU10HTGyroRanges(IMU10HT_D250N),
            new IMU10HTGyroRanges(IMU10HT_D500N),
            new IMU10HTGyroRanges(IMU10HT_D1000N),
            new IMU10HTGyroRanges(IMU10HT_D2000N) 
        };

        public static IMU10HTGyroRanges[] ListOfLength
        {
            get => listOfConstants;
        }

        public static IMU10HTGyroRanges CreateFromValue(ushort v)
        {
            return new IMU10HTGyroRanges(v);
        }


        public IMU10HTGyroRanges(ushort value)
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
            string r = IMU10HT_D250;

            if (value == IMU10HT_D250N)
            {
                r = IMU10HT_D250;
            }
            else if (value == IMU10HT_D500N)
            {
                r = IMU10HT_D500;
            }
            else if (value == IMU10HT_D1000N)
            {
                r = IMU10HT_D1000;
            }
            else if (value == IMU10HT_D2000N)
            {
                r = IMU10HT_D2000;
            }
            else
            {
                value = IMU10HT_D250N;
            }

            return r;
        }
    }
}
