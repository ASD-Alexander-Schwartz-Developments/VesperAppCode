using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using static VesperApp.Models.ConfigurationJSON;

namespace VesperApp.Models
{
    public class ConfigScheduleJSONItem
    {
        private DateTime time;
        private WorkingConfiguration config_index;

        [JsonPropertyName("time"), JsonConverter(typeof(VesperDateTimeAlarmConverter))]
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


    public class VesperDateTimeAlarmConverter : JsonConverter<DateTime>
    {
        private readonly string Format;

        public override bool HandleNull => false;

        public VesperDateTimeAlarmConverter()
        {
            Format = "yyyy-MM-dd HH:mm:ss";
        }

        public VesperDateTimeAlarmConverter(string format)
        {
            Format = format;
        }
        public override void Write(Utf8JsonWriter writer, DateTime date, JsonSerializerOptions options)
        {
            writer.WriteStringValue(date.ToString(Format));
        }
        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            string? s = reader.GetString();

            if (s == null || reader.TokenType == JsonTokenType.Null || s?.Length == 0)
            {
                return DateTime.MinValue;
            }
            else
            {
                return DateTime.ParseExact(s!, Format, null);
            }
        }
    }
}
