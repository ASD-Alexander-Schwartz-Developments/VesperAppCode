using System;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace VesperApp.Models
{
    /// <summary>Log level of the LOGGER pseudo-sensor (logmask bits 4-5).</summary>
    public enum LoggerLogLevel : uint
    {
        Off = 0,
        Errors = 1,
        Warnings = 2,
        Debug = 3,
    }

    /// <summary>
    /// LOGGER pseudo-sensor (KOL and VesperU5 firmware, logger.c): health samples - battery,
    /// MCU temperature, RTC correction, sampling sensors - and log codes, in /log/&lt;n&gt;H.BIN.
    /// Keys MUST match the firmware (params.c json_devices_attrs); contract:
    /// asd-interfaces interfaces/logger-file.md. The sample rate fields are the sample
    /// PERIOD in ms (0 = no samples in that config; log codes are written all session).
    /// </summary>
    public class ConfigLoggerDriver : ConfigurationDeviceDriver
    {
        public const UInt32 LOGMASK_BATTERY = 0x01;
        public const UInt32 LOGMASK_TEMPERATURE = 0x02;
        public const UInt32 LOGMASK_RTC = 0x04;
        public const UInt32 LOGMASK_SENSORS = 0x08;
        public const int LOGMASK_LEVEL_SHIFT = 4;
        public const UInt32 LOGMASK_LEVEL = 0x30;

        private UInt32 logmask = LOGMASK_BATTERY | LOGMASK_TEMPERATURE | LOGMASK_RTC | LOGMASK_SENSORS
                                 | ((UInt32)LoggerLogLevel.Warnings << LOGMASK_LEVEL_SHIFT);

        public ConfigLoggerDriver() : base("LOGGER", "Health logger: battery, temperature, RTC correction, sensors, log codes")
        {
            this.FileSize = 1024 * 1024;
            this.SampleRate[1] = 60000;
            this.SampleRate[2] = 60000;
        }

        public override string ToString() => "LOGGER";

        public override void Load(ConfigurationDeviceDriver ldrv)
        {
            if (ldrv is not null)
            {
                base.Load(ldrv);
                this.LogMask = (ldrv as ConfigLoggerDriver)!.LogMask;
            }
        }

        [JsonPropertyName("logmask"), JsonPropertyOrder(20)]
        [Browsable(false)]
        public UInt32 LogMask
        {
            get => this.logmask;
            set { this.logmask = value; OnPropertyChanged(); }
        }

        private bool GetBit(UInt32 bit) => (this.logmask & bit) == bit;

        private void SetBit(UInt32 bit, bool on)
        {
            if (on) this.logmask |= bit; else this.logmask &= ~bit;
            OnPropertyChanged();
        }

        [CategoryAttribute("Standard configuration"),
        DescriptionAttribute("Time between two health samples in Config 1, in ms (minimum 1000; 0 = no samples - log codes are still written)."),
        DisplayName("Sample Period [1] (ms)")]
        [JsonIgnore]
        [Browsable(true)]
        public override UInt32 SampleRate1
        {
            get => SampleRate[1];
            set { SampleRate[1] = (value != 0 && value < 1000) ? 1000 : value; OnPropertyChanged(); }
        }

        [CategoryAttribute("Standard configuration"),
        DescriptionAttribute("Time between two health samples in Config 2, in ms (minimum 1000; 0 = no samples - log codes are still written)."),
        DisplayName("Sample Period [2] (ms)")]
        [JsonIgnore]
        [Browsable(true)]
        public override UInt32 SampleRate2
        {
            get => SampleRate[2];
            set { SampleRate[2] = (value != 0 && value < 1000) ? 1000 : value; OnPropertyChanged(); }
        }

        [DisplayName("Battery"),
        CategoryAttribute("Logger specific Settings"),
        DescriptionAttribute("Battery reading in mV in every sample (as the unit reports it in the console).")]
        [JsonIgnore]
        public bool LogBattery { get => GetBit(LOGMASK_BATTERY); set => SetBit(LOGMASK_BATTERY, value); }

        [DisplayName("MCU temperature"),
        CategoryAttribute("Logger specific Settings"),
        DescriptionAttribute("Processor temperature in every sample, 0.01 °C resolution (about ±5 °C absolute).")]
        [JsonIgnore]
        public bool LogTemperature { get => GetBit(LOGMASK_TEMPERATURE); set => SetBit(LOGMASK_TEMPERATURE, value); }

        [DisplayName("RTC correction"),
        CategoryAttribute("Logger specific Settings"),
        DescriptionAttribute("Clock correction the unit applies (crystal calibration and temperature compensation), in ppb.")]
        [JsonIgnore]
        public bool LogRtcCorrection { get => GetBit(LOGMASK_RTC); set => SetBit(LOGMASK_RTC, value); }

        [DisplayName("Sampling sensors"),
        CategoryAttribute("Logger specific Settings"),
        DescriptionAttribute("Which sensors are configured to sample at the time of each sample.")]
        [JsonIgnore]
        public bool LogSensors { get => GetBit(LOGMASK_SENSORS); set => SetBit(LOGMASK_SENSORS, value); }

        [DisplayName("Log level"),
        CategoryAttribute("Logger specific Settings"),
        DescriptionAttribute("Event codes written to the file: Off, Errors, Warnings (and errors) or Debug (everything, including every recording window).")]
        [JsonIgnore]
        public LoggerLogLevel LogLevel
        {
            get => (LoggerLogLevel)((this.logmask & LOGMASK_LEVEL) >> LOGMASK_LEVEL_SHIFT);
            set
            {
                this.logmask = (this.logmask & ~LOGMASK_LEVEL) | (((UInt32)value << LOGMASK_LEVEL_SHIFT) & LOGMASK_LEVEL);
                OnPropertyChanged();
            }
        }

        [Browsable(false), JsonIgnore]
        public override UInt32 WindowLength1 { get => base.WindowLength1; set => base.WindowLength1 = value; }

        [Browsable(false), JsonIgnore]
        public override UInt32 WindowLength2 { get => base.WindowLength2; set => base.WindowLength2 = value; }

        [Browsable(false), JsonIgnore]
        public override UInt32 WindowRate1 { get => base.WindowRate1; set => base.WindowRate1 = value; }

        [Browsable(false), JsonIgnore]
        public override UInt32 WindowRate2 { get => base.WindowRate2; set => base.WindowRate2 = value; }

        [Browsable(false), JsonIgnore]
        public override bool IsLEDActive { get => base.IsLEDActive; set => base.IsLEDActive = value; }

        [Browsable(false), JsonIgnore]
        public override bool EnAccTrig { get => base.EnAccTrig; set => base.EnAccTrig = value; }
    }
}
