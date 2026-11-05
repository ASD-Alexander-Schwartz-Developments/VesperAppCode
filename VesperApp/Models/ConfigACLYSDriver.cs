using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;


namespace VesperApp.Models
{


    public class ConfigACLYSDriver : ConfigurationDeviceDriver
    {
        // Sample Rate unit as the firmware reads it (2026-11-04).
        protected override RateKind SampleRateKind => RateKind.PeriodMs;
        protected override string SampleRateNote => "time between snapshot starts (0 = off); above 5000 ms the receiver sleeps between snapshots.";
        protected override bool FileSizeIgnored => true;

        public const UInt32 BITMASK_ACC_GPSTRIGGER = 0x1000;

        public ConfigACLYSDriver() : base("ACLYS", "Snap GPS receiver") // GPS
        {
            SnapSize = AclysSnapLength.CreateFromValue(AclysSnapLength.SNAP256ms);
            this.FileSize = 0;
        }

        //private AclysSnapLength snap_size;


        // Window length / window rate mean nothing for the GPS (VesperU5 never reads them; Nanotag from 090bb2f
        // neither - D-32): hidden, kept 0 in the file.
        [Browsable(false), JsonIgnore]
        public override UInt32 WindowLength1 { get => base.WindowLength1; set => base.WindowLength1 = value; }
        [Browsable(false), JsonIgnore]
        public override UInt32 WindowLength2 { get => base.WindowLength2; set => base.WindowLength2 = value; }
        [Browsable(false), JsonIgnore]
        public override UInt32 WindowRate1 { get => base.WindowRate1; set => base.WindowRate1 = value; }
        [Browsable(false), JsonIgnore]
        public override UInt32 WindowRate2 { get => base.WindowRate2; set => base.WindowRate2 = value; }

        [JsonPropertyName("name"), JsonPropertyOrder(0)]
        [Browsable(false)]
        public override string Name
        {
            get { return base.Name; }
            //set { this.name = value; }
        }

        [DisplayName("Motion-triggered snapshots"),
        TypeConverter(typeof(bool)),
        CategoryAttribute("NANOACC Specific Settings"),
        DescriptionAttribute("Nanotag: one snapshot per motion event, at the next sample period (so at most one per period). Firmware 1.39 and older: snapshots per trigger came from Window Length, and 0 meant no end after the first motion."),
        Browsable(true)]
        [JsonIgnore]
        public bool EnableGPSTrigger
        {
            get
            {
                if ((this.Bitmask & BITMASK_ACC_GPSTRIGGER) == BITMASK_ACC_GPSTRIGGER)
                    return true;
                else
                    return false;
            }
            set
            {
                if (value == true)
                    this.Bitmask |= BITMASK_ACC_GPSTRIGGER;
                else
                    this.Bitmask &= ~(BITMASK_ACC_GPSTRIGGER);
            }
        }

        [CategoryAttribute("ACLYS specific Settings"),
        DisplayName("Snapshot Length"),
        DescriptionAttribute("Length of one GNSS snapshot: longer fixes better but costs more power.")]
        [JsonIgnore]
        [Browsable(true)]
        public AclysSnapLength SnapSize
        {
            get 
            { 
                return AclysSnapLength.CreateFromValue(this.snap_size); 
            }
            set 
            { 
                this.snap_size = value.Value; 
                base.RawData1 = this.snap_size;
            }
        }

        [JsonPropertyName("snapSize"), JsonPropertyOrder(20)]
        [Browsable(false)]

        public uint JsonSnapSize
        {
            get => this.snap_size;
            set
            {
                this.snap_size = value;
                base.RawData1 = this.snap_size;
            }
        }

        private uint snap_size;

        public override void Load(ConfigurationDeviceDriver ldrv)
        {
            if (ldrv is not null)
            {
                base.Load(ldrv);
                this.SnapSize = (ldrv as ConfigACLYSDriver)!.SnapSize;
            }
        }
    }



    public class AclysSnapLength
    {
        public const uint SNAP64ms = 64;
        public const uint SNAP128ms = 128;
        public const uint SNAP256ms = 256;
        public const uint SNAP512ms = 512;

        private const string LS64 = "64 [ms]";
        private const string LS128 = "128 [ms]";
        private const string LS256 = "256 [ms]";
        private const string LS512 = "512[ms]";

        private static readonly AclysSnapLength[] listOfConstants = 
        {
            CreateFromValue(SNAP64ms),
            CreateFromValue(SNAP128ms),
            CreateFromValue(SNAP256ms),
            CreateFromValue(SNAP512ms),
        };

        public static AclysSnapLength[] ListOfLength
        {
            get => listOfConstants;
        }

        public static AclysSnapLength CreateFromValue(uint v)
        {
            return new AclysSnapLength(v);
        }


        public AclysSnapLength(uint value)
        {
            this.value = value;
        }


        private uint value;

        public uint Value
        {
            get => value;
            set => this.value = value;
        }

        public override string ToString()
        {
            string r = LS64;

            if(value == SNAP128ms) 
            {
                r = LS128;
            } 
            else if(value == SNAP256ms)
            {
                r = LS256;
            }
            else if(value == SNAP512ms)
            {
                r = LS512;
            }
            else
            {
                value = SNAP64ms;
            }

            return r;
        }

    }

}
