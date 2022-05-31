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
            this.snap_size = 256;
            this.MemoryBufferSize = 0;
            this.FileSize = 0;
        }

        private UInt32 snap_size;


        [JsonPropertyName("name"), JsonPropertyOrder(0)]
        [Browsable(false)]
        public override string Name
        {
            get { return base.Name; }
            //set { this.name = value; }
        }



        [TypeConverter(typeof(AclysSNAPLengthConverter)),
        CategoryAttribute("ACLYS specific Settings"),
        DisplayName("Snap Size"),
        DescriptionAttribute("Size (in ms) of a single GPS snap. The longer the snap - the better the positioning in expense of power consumption")]
        [JsonPropertyName("snapSize")]
        [Browsable(true)]
        public UInt32 SnapSize
        {
            get { return this.snap_size; }
            set { this.snap_size = value; }
        }

        
//        public string TEST { get; set; }
    }


    public class AclysSNAPLengthConverter : UInt32Converter
    {
        public override bool GetStandardValuesSupported(
                           ITypeDescriptorContext? context)
        {
            return true;
        }


        public override StandardValuesCollection
                     GetStandardValues(ITypeDescriptorContext? context)
        {
            return new StandardValuesCollection(new UInt32[] {
                64,
                128,
                256,
                512});
        }
    }

}
