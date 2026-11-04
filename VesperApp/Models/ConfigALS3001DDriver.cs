using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.ComponentModel;

namespace VesperApp.Models
{

    public class ConfigALS3001DDriver : ConfigurationDeviceDriver
    {
        // Sample Rate unit as the firmware reads it (2026-11-04).
        protected override RateKind SampleRateKind => RateKind.PeriodMs;
        protected override string SampleRateNote => "time between readings (0 = off).";

        public ConfigALS3001DDriver() : base("ALS3001D", "Huaman Eye calibrated response ambient light sensor")
        {
            this.FileSize = 512 * 1024;
        }
    }
}
