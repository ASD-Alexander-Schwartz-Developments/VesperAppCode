using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VesperApp.Models
{
    public enum DeviceTypes
    {
        Nanotag,
        Vesper,
        Pipistrelle
    }

    public enum WorkingConfiguration : UInt32 
    { 
        Off = 0, 
        Config1 = 1, 
        Config2 = 2 
    }
    
    public enum ScheduleTypes : UInt16 
    { 
        Continues = 0, 
        Triggered, 
        Dated, 
        Daily, 
        Weekly, 
        Relative 
    }

}
