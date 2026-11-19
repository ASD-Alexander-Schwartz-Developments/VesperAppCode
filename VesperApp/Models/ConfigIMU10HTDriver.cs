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

        public ConfigIMU10HTDriver() : base("IMU10HT", "Low Noise Inertial Motion Unit sensor with 10 degrees of freedom")
        {
            this.imu_range = 0;

            this.FileSize = MinFileSize;
        }

        /// <summary>params.c: IMU_BUFFER_SIZE (8 KiB) x 256. It used to default to 512 KiB, which the firmware raised.</summary>
        [Browsable(false), System.Text.Json.Serialization.JsonIgnore]
        public override UInt32 MinFileSize => 2 * 1024 * 1024;

        /// <summary>Highest IMU10HT sample rate the firmware encodes (KOL / VesperU5 rate table), Hz.</summary>
        public const UInt32 MaxSampleRateHz = 1000;

        /// <summary>0 = off; otherwise 1..<see cref="MaxSampleRateHz"/> Hz (higher values are capped).</summary>
        public static UInt32 ClampRate(UInt32 hz) => Math.Min(hz, MaxSampleRateHz);

        [CategoryAttribute("Standard configuration"),
        DescriptionAttribute("Config 1: 1 to 1000 Hz (0 = off)."),
        DisplayName("Sample Rate [1] (Hz)")]
        [JsonIgnore]
        [Browsable(true)]
        public override UInt32 SampleRate1
        {
            get => SampleRate[1];
            set { SampleRate[1] = ClampRate(value); OnPropertyChanged(); }
        }

        [CategoryAttribute("Standard configuration"),
        DescriptionAttribute("Config 2: 1 to 1000 Hz (0 = off)."),
        DisplayName("Sample Rate [2] (Hz)")]
        [JsonIgnore]
        [Browsable(true)]
        public override UInt32 SampleRate2
        {
            get => SampleRate[2];
            set { SampleRate[2] = ClampRate(value); OnPropertyChanged(); }
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
        DescriptionAttribute("Flash the LED on every IMU save."),
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


        [DisplayName("Enable barometer"),
        Browsable(true),
        CategoryAttribute("IMU10 specific Settings"),
        DescriptionAttribute("Barometer in the IMU10 module.")]
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

        [DisplayName("Enable accelerometer"),
        Browsable(true),
        CategoryAttribute("IMU10 specific Settings"),
        DescriptionAttribute("Accelerometer in the IMU10 module.")]
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

        [DisplayName("Enable gyroscope"),
        Browsable(true),
        CategoryAttribute("IMU10 specific Settings"),
        DescriptionAttribute("Gyroscope in the IMU10 module.")]
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

        [DisplayName("Enable magnetometer"),
        Browsable(true),
        CategoryAttribute("IMU10 specific Settings"),
        DescriptionAttribute("Magnetometer in the IMU10 module.")]
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

        [DisplayName("Accelerometer range"),
        Browsable(true),
        CategoryAttribute("IMU10 specific Settings"),
        DescriptionAttribute("Full scale, in g.")]
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

        /// <summary>imuRange bit 11 = the firmware writes the ICM-20680 DLPF_CFG from bits 8..10 (VesperU5 after 1.22,
        /// imu10ht_cfg.h; open decision D-16). Bit 11 clear = DLPF_CFG 0, the reset value (wide bandwidth, as 1.22).
        /// The bits are in the existing imuRange field: 1.22 ignores them and records unfiltered (a new config.json
        /// key would make 1.22 refuse the whole file).</summary>
        public const UInt32 RANGE_DLPF_SET = 1u << 11;
        public const int RANGE_DLPF_POS = 8;

        [DisplayName("Low-pass filter"),
        Browsable(true),
        CategoryAttribute("IMU10 specific Settings"),
        DescriptionAttribute("Gyroscope low-pass filter (ICM-20680 DLPF). Off = as before (wide bandwidth). Needs firmware after 1.22; older firmware ignores it.")]
        [JsonIgnore]
        public IMU10HTLowPass LowPassFilter
        {
            get => IMU10HTLowPass.CreateFromValue((this.imu_range & RANGE_DLPF_SET) == 0
                ? (ushort)0 : (ushort)((this.imu_range >> RANGE_DLPF_POS) & 0x7));
            set
            {
                this.imu_range &= ~(RANGE_DLPF_SET | (0x7u << RANGE_DLPF_POS));
                ushort v = value?.Value ?? 0;
                if (v >= 1 && v <= 6)
                    this.imu_range |= RANGE_DLPF_SET | ((UInt32)v << RANGE_DLPF_POS);
                OnPropertyChanged();
            }
        }

        [DisplayName("Gyroscope range"),
        Browsable(true),
        CategoryAttribute("IMU10 specific Settings"),
        DescriptionAttribute("Full scale, in degrees per second.")]
        [JsonIgnore]
        public IMU10HTGyroRanges GyroRange
        {
            get
            {
                return IMU10HTGyroRanges.CreateFromValue((UInt16)((this.imu_range) & 0x7));
            }
            set
            {
                this.imu_range &= ~0x7u;           // gyro range bits only: bits 8..11 hold the low-pass filter

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


    /// <summary>IMU10HT low-pass filter: 0 = off, 1..6 = ICM-20680 DLPF_CFG (gyro bandwidth).</summary>
    public class IMU10HTLowPass
    {
        private static readonly string[] Labels = { "Off", "176 Hz", "92 Hz", "41 Hz", "20 Hz", "10 Hz", "5 Hz" };

        private static readonly IMU10HTLowPass[] listOfConstants =
        {
            new IMU10HTLowPass(0), new IMU10HTLowPass(1), new IMU10HTLowPass(2), new IMU10HTLowPass(3),
            new IMU10HTLowPass(4), new IMU10HTLowPass(5), new IMU10HTLowPass(6)
        };

        public static IMU10HTLowPass[] ListOfOptions => listOfConstants;

        public static IMU10HTLowPass CreateFromValue(ushort v) => new IMU10HTLowPass(v <= 6 ? v : (ushort)0);

        public IMU10HTLowPass(ushort value) { this.value = value; }

        private ushort value;

        public ushort Value
        {
            get => value;
            set => this.value = value;
        }

        public override string ToString() => value < Labels.Length ? Labels[value] : Labels[0];
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
