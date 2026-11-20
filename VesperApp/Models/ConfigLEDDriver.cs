using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;


namespace VesperApp.Models
{
    public class ConfigLEDDriver : ConfigurationDeviceDriver
    {
        // Sample Rate unit as the firmware reads it (2026-11-04).
        protected override RateKind SampleRateKind => RateKind.PeriodMs;
        protected override string SampleRateNote => "one LED flash every period while recording (0 = off).";
        public override (string? Name, string? Description)? EditorLabel(string property) => property switch
        {
            nameof(WindowLength1) => ("Flash Length [1] (ms)", "Config 1: length of each flash (30 ms if 0)."),
            nameof(WindowLength2) => ("Flash Length [2] (ms)", "Config 2: length of each flash (30 ms if 0)."),
            nameof(WindowRate1) => (null, "Config 1: flash period when Sample Period is 0 (with Flash Length set)."),
            nameof(WindowRate2) => (null, "Config 2: flash period when Sample Period is 0 (with Flash Length set)."),
            _ => base.EditorLabel(property),
        };

        public ConfigLEDDriver() : base("LED", "LED Heartbeat")
        {
            this.FileSize = 0;
        }

        public override string ToString()
        {
            return "LED";
        }
    }
}
