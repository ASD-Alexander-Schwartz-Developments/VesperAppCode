using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Text.Json.Serialization;
using static VesperApp.Models.ConfigurationJSON;

namespace VesperApp.Models
{
    /// <summary>
    /// One config.json schedule entry: <c>{ "time": "...", "config": n }</c> (asd-interfaces interfaces/vesper-schedule.md).
    ///
    /// Daily / dated entries are dates ("yyyy-MM-dd HH:mm:ss"; daily ones in year 2000). A relative entry is an
    /// offset from the start of the schedule: days 0..31 + time of day, written <c>"2000-00-DD hh:mm:ss"</c>
    /// (as the relative power-on). VesperU5 / KOL (1.22 / 1.8, newlib strptime, no range check) and Nanotag
    /// (1.39, binary config: year 0, day = offset) zero the year and month and add the rest to the arming
    /// time, so only the day field and the time count, and day 0 = the day the schedule starts.
    /// </summary>
    public class ConfigScheduleJSONItem
    {
        /// <summary>Largest day offset of a relative entry: the 5-bit day field of the firmware date.</summary>
        public const int MaxRelativeDays = 31;

        private const string DateFormat = "yyyy-MM-dd HH:mm:ss";
        private static readonly Regex TimePattern = new Regex(@"^(\d{4})-(\d{2})-(\d{2}) (\d{2}):(\d{2}):(\d{2})$", RegexOptions.CultureInvariant);

        private DateTime time;
        private int day;                    // the day field as written: day of month, or the relative day offset (0..31)
        private WorkingConfiguration config_index;

        /// <summary>A relative entry, built from a day offset and a time.</summary>
        public static ConfigScheduleJSONItem Relative(int days, TimeSpan timeOfDay, WorkingConfiguration config)
            => new ConfigScheduleJSONItem { RelativeOffset = new TimeSpan(days, 0, 0, 0) + timeOfDay, Configuration = config };

        /// <summary>The entry as a date (daily / dated). For a relative entry: 2000-01-01 at its time of day.</summary>
        [JsonIgnore]
        public DateTime Alarm
        {
            get { return this.time; }
            set { this.time = value; this.day = value.Day; }
        }

        /// <summary>Written as a relative entry ("2000-00-DD hh:mm:ss"). Set for entries of a relative schedule.</summary>
        [JsonIgnore]
        public bool IsRelative { get; set; }

        /// <summary>
        /// Days + time after the start of the schedule. For an entry read as a date (older VesperApp builds
        /// wrote relative entries from a date picker) it is what the firmware takes from it: day of month + time.
        /// Setting it makes the entry relative; days 0..<see cref="MaxRelativeDays"/>.
        /// </summary>
        [JsonIgnore]
        public TimeSpan RelativeOffset
        {
            get { return new TimeSpan(this.day, 0, 0, 0) + this.time.TimeOfDay; }
            set
            {
                if (value < TimeSpan.Zero || value.Days > MaxRelativeDays)
                    throw new ArgumentOutOfRangeException(nameof(RelativeOffset), $"A relative schedule entry is 0 to {MaxRelativeDays} days after the start.");
                this.time = new DateTime(2000, 1, 1).Add(value - new TimeSpan(value.Days, 0, 0, 0));
                this.day = value.Days;
                this.IsRelative = true;
            }
        }

        /// <summary>The config.json "time" text.</summary>
        [JsonPropertyName("time")]
        [Browsable(false)]
        public string Time
        {
            get
            {
                if (IsRelative)
                    return "2000-00-" + this.day.ToString("00", CultureInfo.InvariantCulture) + " " + this.time.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
                return this.time.ToString(DateFormat, CultureInfo.InvariantCulture);
            }
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    Alarm = DateTime.MinValue;
                    return;
                }
                if (DateTime.TryParseExact(value, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dt))
                {
                    Alarm = dt;
                    return;
                }
                // Not a calendar date: a relative entry ("2000-00-DD hh:mm:ss", day 0..31)
                Match m = TimePattern.Match(value);
                if (m.Success)
                {
                    int d = int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
                    int h = int.Parse(m.Groups[4].Value, CultureInfo.InvariantCulture);
                    int mi = int.Parse(m.Groups[5].Value, CultureInfo.InvariantCulture);
                    int sc = int.Parse(m.Groups[6].Value, CultureInfo.InvariantCulture);
                    if (d <= MaxRelativeDays && h < 24 && mi < 60 && sc < 60)
                    {
                        RelativeOffset = new TimeSpan(d, h, mi, sc);
                        return;
                    }
                }
                throw new FormatException($"Schedule time \"{value}\" is not \"yyyy-MM-dd HH:mm:ss\".");
            }
        }

        /// <summary>List text: "Day N  hh:mm:ss" for a relative entry, else the date and time.</summary>
        [JsonIgnore]
        public string DisplayTime => IsRelative
            ? $"Day {this.day}  {this.time.ToString("HH:mm:ss", CultureInfo.InvariantCulture)}"
            : this.time.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture);

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
