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
            SnapSize = AclysSnapLength.SNAP256ms;
            this.MemoryBufferSize = 0;
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
        [JsonPropertyName("snapSize")]
        [Browsable(true)]
        public AclysSnapLength SnapSize
        {
            get { return (AclysSnapLength)base.RawData1; }
            set { base.RawData1 = (UInt32)value; }
        }

        
//        public string TEST { get; set; }
    }



    public enum AclysSnapLength : UInt32
    {
        SNAP64ms = 64,
        SNAP128ms = 128,
        SNAP256ms = 256,
        SNAP512ms = 512,
    }

}
