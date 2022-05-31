using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.ComponentModel;

namespace VesperApp.Models
{
    public class ConfigurationJSON
    {
        private const string valid_name = "";
        private const string valid_minimum_supported_hw = "";


        private string name;
        private string minimum_supported_hw;
        private bool is_magnet_off_enabled;
        //private string schedule_type;                   // 0 = continues, 1 = daily, 2 = weekly, 3 = absolute
        private UInt32 battery_capacity;
        private DateTime wake_up_time;                  // time to turn on when no magnet mode
        private ScheduleTypes schedule_type;

        private List<ConfigScheduleJSONItem> schedule;
        private List<ConfigurationDeviceDriver> drivers;

        public ConfigurationJSON()
        {
            this.name = valid_name;
            this.minimum_supported_hw = valid_minimum_supported_hw;

            this.is_magnet_off_enabled = true;
            this.battery_capacity = 40;
            this.schedule_type = ScheduleTypes.Continues;

            this.wake_up_time = DateTime.Now;

            this.schedule = new List<ConfigScheduleJSONItem>();
            this.drivers = new List<ConfigurationDeviceDriver>();
        }

        [JsonPropertyName("name")]
        [CategoryAttribute("General configuration"),
        DefaultValueAttribute(typeof(string), "vesper"),
        DisplayName("Device Name"),
        DescriptionAttribute("Name of the device model that should use this configuration")]
        public string Name
        {
            get { return this.name; }
            set { this.name = value; }
        }


        [JsonPropertyName("scheduleType")]
        [CategoryAttribute("General configuration"),
        DisplayName("Schedule Type"),
        DescriptionAttribute("Sampling schedule type - Daily/Weekly/Continues/Custom. Details in Schedule.")]
        public ScheduleTypes ScheduleType
        {
            get { return this.schedule_type; }
            set
            {
                this.schedule_type = value;                         // should trigger emptying of all schedule!!!
            }
        }

        [JsonPropertyName("magnetOff")]
        [CategoryAttribute("General configuration"),
        DefaultValueAttribute(typeof(bool), "true"),
        DisplayName("Allow Magnet Turn-Off"),
        DescriptionAttribute("Should the device turn off sampling (transition to sleep) on magnet detection." +
            " If set to false, device will follow only scheduler, no manual deactiviation is possible")]
        public bool IsMagnetOffEnabled
        {
            get { return this.is_magnet_off_enabled; }
            set { this.is_magnet_off_enabled = value; }
        }

        [JsonPropertyName("minhw")]
        [CategoryAttribute("General configuration"),
        DefaultValueAttribute(typeof(string), "3.0"),
        DisplayName("Minimal Version"),
        DescriptionAttribute("Allows restricting the configuration to any firmware version above (including) the specified")]
        public string MinimumSupportedHardware
        {
            get { return this.minimum_supported_hw; }
            set { this.minimum_supported_hw = value; }
        }

        [JsonPropertyName("battery")]
        [CategoryAttribute("General configuration"),
        DefaultValueAttribute(typeof(UInt32), "60"),
        DisplayName("Battery Capacity"),
        DescriptionAttribute("Nominal capacity of the battery in use with the Vesper (note, this is only informational - will not affect operation)")]
        public UInt32 BatteryCapacity
        {
            get { return this.battery_capacity; }
            set { this.battery_capacity = value; }
        }


        [CategoryAttribute("General configuration"),
        DisplayName("Schedule"),
        DescriptionAttribute("Plan sampling schedule details")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [JsonPropertyName("schedule")]
        public List<ConfigScheduleJSONItem> Schedule
        {
            get { return this.schedule; }
            set { this.schedule = value; }
        }

        [CategoryAttribute("General configuration"),
        DisplayName("Sensors"),
        DescriptionAttribute("Control and configure which sensors will be active")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [JsonPropertyName("devices")]
        public List<ConfigurationDeviceDriver> DeviceDrivers
        {
            get { return this.drivers; }
            set { this.drivers = value; }
        }




        // A converter for a specific Enum.
        public class ScheduleTypesEnumConverter : JsonConverter<ScheduleTypes>
        {
            // CanConvert does not need to be implemented here since we only convert MyBoolEnum.

            public override ScheduleTypes Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                string ? enumValue = reader.GetString();
                if (enumValue == "continues")
                {
                    return ScheduleTypes.Continues;
                }
                else if (enumValue == "trigger")
                {
                    return ScheduleTypes.Triggered;
                }
                else if (enumValue == "daily")
                {
                    return ScheduleTypes.Daily;
                }
                else if (enumValue == "dated")
                {
                    return ScheduleTypes.Dated;
                }
                else if (enumValue == "weekly")
                {
                    return ScheduleTypes.Weekly;
                }
                else if (enumValue == "relative")
                {
                    return ScheduleTypes.Relative;
                }

                throw new JsonException();
            }

            public override void Write(Utf8JsonWriter writer, ScheduleTypes value, JsonSerializerOptions options)
            {
                if (value is ScheduleTypes.Continues)
                {
                    writer.WriteStringValue("continues");
                }
                else if (value is ScheduleTypes.Triggered)
                {
                    writer.WriteStringValue("trigger");
                }
                else if (value is ScheduleTypes.Daily)
                {
                    writer.WriteStringValue("daily");
                }
                else if (value is ScheduleTypes.Dated)
                {
                    writer.WriteStringValue("dated");
                }
                else if (value is ScheduleTypes.Weekly)
                {
                    writer.WriteStringValue("weekly");
                }
                else if (value is ScheduleTypes.Relative)
                {
                    writer.WriteStringValue("relative");
                }
                else
                {
                    writer.WriteStringValue("?");
                }
            }
        }


        /*

        [Fact]
        public static void CustomEnumConverter()
        {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ScheduleTypesEnumConverter());

            {
                ScheduleTypes value = JsonSerializer.Deserialize<ScheduleTypes>(@"""TRUE""", options);
                Assert.Equal(ScheduleTypes.SCH_CONTINUES, value);
                Assert.Equal(@"""TRUE""", JsonSerializer.Serialize(value, options));
            }

            {
                MyBoolEnum value = JsonSerializer.Deserialize<MyBoolEnum>(@"""FALSE""", options);
                Assert.Equal(MyBoolEnum.False, value);
                Assert.Equal(@"""FALSE""", JsonSerializer.Serialize(value, options));
            }

            {
                MyBoolEnum value = JsonSerializer.Deserialize<MyBoolEnum>(@"""?""", options);
                Assert.Equal(MyBoolEnum.Unknown, value);
                Assert.Equal(@"""?""", JsonSerializer.Serialize(value, options));
            }
        }

        [Fact]
        public static void NullableCustomEnumConverter()
        {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new MyBoolEnumConverter());

            {
                MyBoolEnum? value = JsonSerializer.Deserialize<MyBoolEnum?>(@"null", options);
                Assert.Null(value);
            }

            {
                MyBoolEnum? value = JsonSerializer.Deserialize<MyBoolEnum?>(@"""TRUE""", options);
                Assert.Equal(MyBoolEnum.True, value);
                Assert.Equal(@"""TRUE""", JsonSerializer.Serialize(value, options));
            }

            {
                MyBoolEnum? value = JsonSerializer.Deserialize<MyBoolEnum?>(@"""FALSE""", options);
                Assert.Equal(MyBoolEnum.False, value);
                Assert.Equal(@"""FALSE""", JsonSerializer.Serialize(value, options));
            }

            {
                MyBoolEnum? value = JsonSerializer.Deserialize<MyBoolEnum?>(@"""?""", options);
                Assert.Equal(MyBoolEnum.Unknown, value);
                Assert.Equal(@"""?""", JsonSerializer.Serialize(value, options));
            }
        }

        */



    }

}
