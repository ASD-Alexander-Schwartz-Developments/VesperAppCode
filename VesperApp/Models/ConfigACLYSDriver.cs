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
                return AclysSnapLength.CreateFromValue(base.RawData1); 
            }
            set 
            { 
                base.RawData1 = value.Value; 
            }
        }

        [JsonPropertyName("snapSize")]
        [Browsable(false)]
        public uint JsonSnapSize
        {
            get => base.RawData1;
            set => base.RawData1 = (uint)value;
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

        private static readonly string[] listOfConstants = {LS64, LS128, LS256, LS512 };

        public static string[] ListOfLength
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
