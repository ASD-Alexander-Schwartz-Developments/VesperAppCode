using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VesperApp.Models
{
    public enum DeviceTypes
    {
        Nanotag = 0,
        Vesper = 1,
        Pipistrelle = 2,
        Kol = 3
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
        Daily,
        Dated, 
        Relative 
    }

}
