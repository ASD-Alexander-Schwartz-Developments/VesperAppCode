using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;


namespace VesperApp.Models
{


    public class ConfigACLYSDriver : ConfigurationDeviceDriver
    {
        public ConfigACLYSDriver() : base("ACLYS", "Snap GPS receiver") // GPS
        {
            SnapSize = AclysSnapLength.CreateFromValue(AclysSnapLength.SNAP256ms);
            this.FileSize = 0;
        }

        //private AclysSnapLength snap_size;


        [JsonPropertyName("name"), JsonPropertyOrder(0)]
        [Browsable(false)]
        public override string Name
        {
            get { return base.Name; }
            //set { this.name = value; }
        }




        [CategoryAttribute("ACLYS specific Settings"),
        DisplayName("Snap Size"),
        DescriptionAttribute("Size (in ms) of a single GPS snap. The longer the snap - the better the positioning in expense of power consumption")]
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
            }
        }

        [JsonPropertyName("snapSize"), JsonPropertyOrder(20)]
        [Browsable(false)]

        public uint JsonSnapSize
        {
            get => this.snap_size;
            set => this.snap_size = value;
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
