using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;



namespace VesperApp.Models
{
    public class ConfigTPRH31Driver : ConfigurationDeviceDriver
    {
        // Sample Rate unit as the firmware reads it (2026-11-04).
        protected override RateKind SampleRateKind => RateKind.PeriodMs;
        protected override string SampleRateNote => "time between readings (0 = off).";

        public ConfigTPRH31Driver() : base("TPRH31", "Temperature and relative humidity")
        {
            this.FileSize = 0;
        }
    }
}
