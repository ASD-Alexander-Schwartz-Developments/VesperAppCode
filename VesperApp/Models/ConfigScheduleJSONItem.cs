using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VesperApp.Models
{
    public class ConfigScheduleJSONItem
    {
        private DateTime time;
        private WorkingConfiguration config_index;

        [JsonPropertyName("time")]
        public DateTime Alarm
        {
            get { return this.time; }
            set { this.time = value; }
        }

        [JsonPropertyName("config")]
        [Browsable(false)]
        public WorkingConfiguration Configuration
        {
            get { return this.config_index; }
            set { this.config_index = value; }
        }

        [JsonIgnore]
        public string ConfigName
        {
            get
            {
                return this.config_index.ToString();
            }
            set
            {
                WorkingConfiguration c = WorkingConfiguration.Off;

                if (value == "1") c = WorkingConfiguration.Config1;
                else if (value == "2") c = WorkingConfiguration.Config2;

                this.config_index = c;
            }
        }

    }
}
