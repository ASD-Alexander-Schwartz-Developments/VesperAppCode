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
        private const UInt32 IMU10_BITMASK_LED = 0x20;


        private bool isStartup = true;
        private ArrayList atts = new ArrayList();

        public ConfigIMU10Driver() : base("IMU10", "Innertial Motion Unit sensor with 10 degrees of freedom")
        {
            this.imu_range = 0;

            this.FileSize = 512 * 1024;
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
            get => ((Bitmask & IMU10_BITMASK_LED) == IMU10_BITMASK_LED);
            set
            {
                if (value == true)
                    Bitmask |= IMU10_BITMASK_LED;
                else
                    Bitmask &= ~((UInt32)IMU10_BITMASK_LED);

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
        public IMU10AccRanges AccRange
        {
            get
            {
                return IMU10AccRanges.CreateFromValue((UInt16)((this.imu_range >> 16) & 0xFFFF));
            }
            set
            {
                this.imu_range &= 0x0000FFFF;

                switch (value.Value)
                {
                    case IMU10AccRanges.IMU10_G2N:
                        this.imu_range |= (IMU10AccRanges.IMU10_G2N << 16);
                        break;
                    case IMU10AccRanges.IMU10_G4N:
                        this.imu_range |= (IMU10AccRanges.IMU10_G4N << 16);
                        break;
                    case IMU10AccRanges.IMU10_G8N:
                        this.imu_range |= (IMU10AccRanges.IMU10_G8N << 16);
                        break;
                    case IMU10AccRanges.IMU10_G16N:
                        this.imu_range |= (IMU10AccRanges.IMU10_G16N << 16);
                        break;
                    default:
                        this.imu_range |= (IMU10AccRanges.IMU10_G2N << 16);
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
        public IMU10GyroRanges GyroRange
        {
            get
            {
                return IMU10GyroRanges.CreateFromValue((UInt16)((this.imu_range) & 0xFFFF));
            }
            set
            {
                this.imu_range &= 0xFFFF0000;

                switch (value.Value)
                {
                    case IMU10GyroRanges.IMU10_D125N:
                        this.imu_range |= (IMU10GyroRanges.IMU10_D125N);
                        break;
                    case IMU10GyroRanges.IMU10_D250N:
                        this.imu_range |= (IMU10GyroRanges.IMU10_D250N);
                        break;
                    case IMU10GyroRanges.IMU10_D500N:
                        this.imu_range |= (IMU10GyroRanges.IMU10_D500N);
                        break;
                    case IMU10GyroRanges.IMU10_D1000N:
                        this.imu_range |= (IMU10GyroRanges.IMU10_D1000N);
                        break;
                    case IMU10GyroRanges.IMU10_D2000N:
                        this.imu_range |= (IMU10GyroRanges.IMU10_D2000N);
                        break;
                    default:
                        this.imu_range |= (0 << 16);
                        break;
                }
                OnPropertyChanged();
            }
        }
    }




    public class IMU10AccRanges
    {
        public const ushort IMU10_G2N = 0;
        public const ushort IMU10_G4N = 1;
        public const ushort IMU10_G8N = 2;
        public const ushort IMU10_G16N = 3;

        public const string IMU10_G2 = "±2g";
        public const string IMU10_G4 = "±4g";
        public const string IMU10_G8 = "±8g";
        public const string IMU10_G16 = "±16g";

        private static readonly IMU10AccRanges[] listOfConstants = 
        { 
            new IMU10AccRanges(IMU10_G2N),
            new IMU10AccRanges(IMU10_G4N),
            new IMU10AccRanges(IMU10_G8N),
            new IMU10AccRanges(IMU10_G16N) 
        };

        public static IMU10AccRanges[] ListOfLength
        {
            get => listOfConstants;
        }

        public static IMU10AccRanges CreateFromValue(ushort v)
        {
            return new IMU10AccRanges(v);
        }


        public IMU10AccRanges(ushort value)
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
            string r = IMU10_G2;

            if (value == IMU10_G4N)
            {
                r = IMU10_G4;
            }
            else if (value == IMU10_G8N)
            {
                r = IMU10_G8;
            }
            else if (value == IMU10_G16N)
            {
                r = IMU10_G16;
            }
            else
            {
                value = IMU10_G2N;
            }

            return r;
        }
    }


    public class IMU10GyroRanges
    {
        public const ushort IMU10_D125N = 0;
        public const ushort IMU10_D250N = 1;
        public const ushort IMU10_D500N = 2;
        public const ushort IMU10_D1000N = 3;
        public const ushort IMU10_D2000N = 4;

        public const string IMU10_D125 = "±125dps";
        public const string IMU10_D250 = "±250dps";
        public const string IMU10_D500 = "±500dps";
        public const string IMU10_D1000 = "±1000dps";
        public const string IMU10_D2000 = "±2000dps";

        private static readonly IMU10GyroRanges[] listOfConstants = 
        { 
            new IMU10GyroRanges(IMU10_D125N),
            new IMU10GyroRanges(IMU10_D250N),
            new IMU10GyroRanges(IMU10_D500N),
            new IMU10GyroRanges(IMU10_D1000N),
            new IMU10GyroRanges(IMU10_D2000N) 
        };

        public static IMU10GyroRanges[] ListOfLength
        {
            get => listOfConstants;
        }

        public static IMU10GyroRanges CreateFromValue(ushort v)
        {
            return new IMU10GyroRanges(v);
        }


        public IMU10GyroRanges(ushort value)
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
            string r = IMU10_D125;

            if (value == IMU10_D250N)
            {
                r = IMU10_D250;
            }
            else if (value == IMU10_D500N)
            {
                r = IMU10_D500;
            }
            else if (value == IMU10_D1000N)
            {
                r = IMU10_D1000;
            }
            else if (value == IMU10_D2000N)
            {
                r = IMU10_D2000;
            }
            else
            {
                value = IMU10_D125N;
            }

            return r;
        }

    }
}
