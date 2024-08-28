using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.ComponentModel;
using System.Globalization;
using System.Collections;
using VesperApp.Services;

namespace VesperApp.Models
{
    public class ConfigurationJSON
    {
        private const string valid_name = "";
        private const string valid_minimum_supported_hw = "4.0";


        private string name;
        private string minimum_supported_hw;
        private bool is_magnet_off_enabled;
        //private string schedule_type;                   // 0 = continues, 1 = daily, 2 = weekly, 3 = absolute
        private UInt32 battery_capacity;
        private PowerOnTime wake_up_time;                  // time to turn on when no magnet mode
        private ScheduleTypes schedule_type;
        private UInt32? clock_drift;
        private List<ConfigScheduleJSONItem> schedule;
        private List<ConfigurationDeviceDriver> drivers;

        public ConfigurationJSON()
        {
            this.name = valid_name;
            this.minimum_supported_hw = valid_minimum_supported_hw;
            this.MinimumSupportedHardware = valid_minimum_supported_hw;

            this.IsMagnetOffEnabled = true;
            this.BatteryCapacity = 60;
            this.ScheduleType = ScheduleTypes.Continues;

            this.wake_up_time = new PowerOnTime();
            this.clock_drift = null;
            this.schedule = new List<ConfigScheduleJSONItem>();
            this.drivers = new List<ConfigurationDeviceDriver>();
            this.Schedule.Clear();
            this.DeviceDrivers.Clear();
        }


        public void Load(ConfigurationJSON newconf)
        {
            this.Name = newconf.name;
            this.BatteryCapacity = newconf.battery_capacity;
            this.IsMagnetOffEnabled = newconf.is_magnet_off_enabled;
            this.MinimumSupportedHardware = newconf.minimum_supported_hw;
            this.wake_up_time = newconf.wake_up_time;
            this.ScheduleType = newconf.schedule_type;
            this.clock_drift = newconf.clock_drift;
            this.Schedule.Clear();
            this.Schedule.AddRange(newconf.Schedule);
            this.DeviceDrivers.Clear();
            this.DeviceDrivers.AddRange(newconf.DeviceDrivers);
        }


        [JsonPropertyName("name"), JsonPropertyOrder(0)]
        [CategoryAttribute("General configuration"),
        DefaultValueAttribute(typeof(string), "vesper"),
        DisplayName("Device Name"),
        DescriptionAttribute("Name of the device model that should use this configuration")]
        public string Name
        {
            get { return this.name; }
            set { this.name = value; }
        }

        [JsonPropertyName("cdrift"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull), JsonPropertyOrder(2)]
        [CategoryAttribute("Clock Drift compensation"),
        DefaultValueAttribute(typeof(UInt32?), null),
        DisplayName("Clock Drift"),
        DescriptionAttribute("Clock drift constant")]
        public UInt32? CDrift
        {
            get { return this.clock_drift; }
            set { this.clock_drift = value; }
        }

        [JsonPropertyName("scheduleType"), JsonPropertyOrder(3)]
        [CategoryAttribute("General configuration"),
        DisplayName("Schedule Type"),
        DescriptionAttribute("Sampling schedule type - Daily/Weekly/Continues/Custom. Details in Schedule.")]
        public ScheduleTypes ScheduleType
        {
            get 
            { 
                return this.schedule_type; 
            }
            set
            {
                this.schedule_type = value;                         // should trigger emptying of all schedule!!!
            }
        }

        [JsonPropertyName("magnetOff"), JsonPropertyOrder(4)]
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

        [JsonPropertyName("minhw"), JsonPropertyOrder(1)]
        [CategoryAttribute("General configuration"),
        DefaultValueAttribute(typeof(string), "4.0"),
        DisplayName("Minimal Version"),
        DescriptionAttribute("Allows restricting the configuration to any firmware version above (including) the specified")]
        public string MinimumSupportedHardware
        {
            get { return this.minimum_supported_hw; }
            set { this.minimum_supported_hw = value; }
        }

        [JsonPropertyName("battery"), JsonPropertyOrder(5)]
        [CategoryAttribute("General configuration"),
        DefaultValueAttribute(typeof(UInt32), "60"),
        DisplayName("Battery Capacity"),
        DescriptionAttribute("Nominal capacity of the battery in use with the Vesper (note, this is only informational - will not affect operation)")]
        public UInt32 BatteryCapacity
        {
            get { return this.battery_capacity; }
            set { this.battery_capacity = value; }
        }

        [JsonPropertyOrder(6)]
        [JsonPropertyName("poweron")]
        [CategoryAttribute("General configuration"),
        DisplayName("Device Power on time"),
        DescriptionAttribute("Name of the device model that should use this configuration")]
        public PowerOnTime PowerOn
        {
            get { return this.wake_up_time; }
            set { this.wake_up_time = value; }
        }


        [CategoryAttribute("General configuration"),
        DisplayName("Schedule"),
        DescriptionAttribute("Plan sampling schedule details"), JsonPropertyOrder(7)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [JsonPropertyName("schedule")]
        public List<ConfigScheduleJSONItem> Schedule
        {
            get { return this.schedule; }
            set { this.schedule = value; }
        }

        [CategoryAttribute("General configuration"),
        DisplayName("Sensors"),
        DescriptionAttribute("Control and configure which sensors will be active"), JsonPropertyOrder(8)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [JsonPropertyName("devices")]
        public List<ConfigurationDeviceDriver> DeviceDrivers
        {
            get { return this.drivers; }
            set { this.drivers = value; }
        }


        public class PowerOnTime
        {
            [JsonIgnore]
            public bool IsRelative { get; set; }

            [JsonIgnore]
            public string PowerOn
            {
                get
                {
                    return pwon;
                }
                set
                {
                    try
                    {
                        if (IsRelative == true)
                        {
                            relativeOffset = TimeSpan.ParseExact(value, @"dd\ hh\:mm\:ss", CultureInfo.InvariantCulture, TimeSpanStyles.None);
                        }
                        else
                        {
                            absoluteOffset = DateTime.ParseExact(value, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.NoCurrentDateDefault);
                        }

                        pwon = value;
                    }
                    catch
                    {

                    }
                }
            }


            public UInt32 ToNanotagDateTime()
            {
                UInt32 ret = 0xFFFFFFFF;

                if (IsRelative && relativeOffset != null)
                {
                    int year = Nanotag.REFERENCE_YEAR;
                    int month = 0;

                    UInt32 yr = (UInt32)((UInt32)year << 26);
                    UInt32 mt = (UInt32)((UInt32)month << 22);
                    UInt32 dy = (UInt32)((UInt32)relativeOffset.Value.Days << 17);
                    UInt32 hr = (UInt32)((UInt32)relativeOffset.Value.Hours << 12);
                    UInt32 mn = (UInt32)((UInt32)relativeOffset.Value.Minutes << 6);
                    UInt32 sc = (UInt32)((UInt32)relativeOffset.Value.Seconds);

                    ret = yr | mt | dy | hr | mn | sc;
                }
                else if (!IsRelative && absoluteOffset != null)
                {
                    int year = (absoluteOffset.Value.Year > Nanotag.REFERENCE_YEAR) ? 
                        absoluteOffset.Value.Year - Nanotag.REFERENCE_YEAR : Nanotag.REFERENCE_YEAR;

                    UInt32 yr = (UInt32)((UInt32)year << 26);
                    UInt32 mt = (UInt32)((UInt32)absoluteOffset.Value.Month << 22);
                    UInt32 dy = (UInt32)((UInt32)absoluteOffset.Value.Day << 17);
                    UInt32 hr = (UInt32)((UInt32)absoluteOffset.Value.Hour << 12);
                    UInt32 mn = (UInt32)((UInt32)absoluteOffset.Value.Minute << 6);
                    UInt32 sc = (UInt32)((UInt32)absoluteOffset.Value.Second);

                    ret = yr | mt | dy | hr | mn | sc;
                }

                return ret;

            }

            private string pwon;
            private TimeSpan? relativeOffset;
            private DateTime? absoluteOffset;

            public PowerOnTime()
            {
                this.absoluteOffset = null;
                this.relativeOffset = null;
                IsRelative = true;
                pwon = string.Empty;
            }
            public PowerOnTime(string s)
            {
                this.absoluteOffset = null;
                this.relativeOffset = null;
                IsRelative = true;
                pwon = string.Empty;

                if (s != null && s.Length > 0)
                {
                    ArrayList arrayList = Utils.scan(s, "%u-%u-%u %u:%u:%u");

                    if(arrayList != null && arrayList.Count == 6)
                    {
                        if (arrayList[0] != null && arrayList[1] != null && arrayList[2] != null &&
                            arrayList[3] != null && arrayList[4] != null && arrayList[5] != null)
                        {
                            uint yr = (uint)arrayList[0]!;
                            uint mt = (uint)arrayList[1]!;
                            uint dy = (uint)arrayList[2]!;
                            uint hr = (uint)arrayList[3]!;
                            uint mn = (uint)arrayList[4]!;
                            uint sc = (uint)arrayList[5]!;

                            if (yr == (uint)2000)
                            {
                                IsRelative = true;

                                PowerOn = dy.ToString("00") +
                                            " " + hr.ToString("00") +
                                            ":" + mn.ToString("00") +
                                            ":" + sc.ToString("00");
                            }
                            else
                            {
                                IsRelative = false;
                                PowerOn = s;
                            }
                        }
                    }
                }
            }

            public override string ToString()
            {
                if(IsRelative && relativeOffset != null)
                {
                    return "2000-00-" + relativeOffset.Value.Days.ToString("00") + 
                        " " + relativeOffset.Value.Hours.ToString("00") + 
                        ":" + relativeOffset.Value.Minutes.ToString("00") +
                        ":" + relativeOffset.Value.Seconds.ToString("00");
                }
                else if(!IsRelative && absoluteOffset != null)
                {
                    return absoluteOffset.Value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                }
                else
                {
                    return string.Empty;
                }
            }
        }

        // A converter for a specific Enum.
        public class ScheduleTypesEnumConverter : JsonConverter<ScheduleTypes>
        {
            // CanConvert does not need to be implemented here since we only convert MyBoolEnum.

            public override ScheduleTypes Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                ushort ? enumValue = reader.GetUInt16();
                if (enumValue == 0)
                {
                    return ScheduleTypes.Continues;
                }
                else if (enumValue == 1)
                {
                    return ScheduleTypes.Daily;
                }
                else if (enumValue == 2)
                {
                    return ScheduleTypes.Dated;
                }
                else if (enumValue == 3)
                {
                    return ScheduleTypes.Relative;
                }

                throw new JsonException();
            }

            public override void Write(Utf8JsonWriter writer, ScheduleTypes value, JsonSerializerOptions options)
            {
                if (value is ScheduleTypes.Continues)
                {
                    writer.WriteNumberValue(0);
                }
                else if (value is ScheduleTypes.Daily)
                {
                    writer.WriteNumberValue(1);
                }
                else if (value is ScheduleTypes.Dated)
                {
                    writer.WriteNumberValue(2);
                }
                else if (value is ScheduleTypes.Relative)
                {
                    writer.WriteNumberValue(3);
                }
                else
                {
                    writer.WriteNumberValue(0);
                }
            }
        }


        public class VesperDateTimeConverter : JsonConverter<DateTime?>
        {
            private readonly string Format;

            public override bool HandleNull => true;

            public VesperDateTimeConverter()
            {
                Format = "yyyy-MM-dd HH:mm:ss";
            }

            public VesperDateTimeConverter(string format)
            {
                Format = format;
            }
            public override void Write(Utf8JsonWriter writer, DateTime? date, JsonSerializerOptions options)
            {
                if (date == null)
                {
                    writer.WriteStringValue(String.Empty);
                }
                else
                {
                    writer.WriteStringValue(date?.ToString(Format));
                }
            }
            public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                string? s = reader.GetString();

                if (s == null || reader.TokenType == JsonTokenType.Null || s?.Length == 0)
                {
                    return null;
                }
                else
                {
                    return DateTime.ParseExact(s!, Format, null);
                }
            }
        }

        public class VesperPowerOnConverter : JsonConverter<PowerOnTime>
        {
            public VesperPowerOnConverter()
            {
            }

            public override void Write(Utf8JsonWriter writer, PowerOnTime date, JsonSerializerOptions options)
            {
                writer.WriteStringValue(date.ToString());
            }
            public override PowerOnTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                string? s = reader.GetString();

                if (s == null || reader.TokenType == JsonTokenType.Null || s?.Length == 0)
                {
                    return new PowerOnTime();
                }
                else
                {
                    return new PowerOnTime(s!);
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
