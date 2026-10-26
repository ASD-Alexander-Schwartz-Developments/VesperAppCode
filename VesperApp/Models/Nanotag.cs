using ASDLibUSBWrapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Runtime.InteropServices;
using System.Diagnostics;
using VesperApp.Models;
using static VesperApp.Models.ConfigurationJSON;

namespace VesperApp.Models
{
    public class Nanotag
    {
        public const byte VND_CMD_GET_INFO = 0x00;
        public const byte VND_CMD_GET_DISKINFO = 0x01;
        public const byte VND_CMD_GET_LOGNPAGES = 0x02;
        public const byte VND_CMD_GET_LOGCHUNK = 0x03;
        public const byte VND_CMD_GET_DATANPAGES = 0x04;
        public const byte VND_CMD_GET_DATACHUNK = 0x05;
        public const byte VND_CMD_SET_DATETIME = 0x06;
        public const byte VND_CMD_SET_SLEEP = 0x07;
        public const byte VND_CMD_SET_BOOT = 0x0F;

        public const byte VND_CMD_GET_CFGCHUNK_GEN = 0x0A;
        public const byte VND_CMD_GET_CFGCHUNK_SCH = 0x0B;
        public const byte VND_CMD_GET_CFGCHUNK_DEV = 0x0C;
        public const byte VND_CMD_SET_CFGCHUNK_GEN = 0x1A;
        public const byte VND_CMD_SET_CFGCHUNK_SCH = 0x1B;
        public const byte VND_CMD_SET_CFGCHUNK_DEV = 0x1C;

        public const byte VND_CMD_FORMAT_DISK = 0x3F;


        public static readonly int DEVICE_NAME_LENGTH = 16;
		public static readonly int MAX_LOADED_DEVICES = 4;
		public static readonly int MAX_SCHEDULE_SIZE = 32;
		// The table holds 32 slots, but the firmware refuses to arm when all 32 are used
		// (app.c: count >= MAX_SCHEDULE_SIZE), so at most 31 entries can be written.
		public static readonly int MAX_SCHEDULE_ENTRIES = MAX_SCHEDULE_SIZE - 1;
		public static readonly int REFERENCE_YEAR = 2020;
		public static readonly int CONFIG_CHUNK_SIZE = 256;

		public static readonly int VendorId = 0x04d8;
		public static readonly int ProductId = 0xfe57;


        public static readonly List<ConfigurationDeviceDriver> SupportedDeviceDrivers = new List<ConfigurationDeviceDriver>
		{
			{ new ConfigACLYSDriver() },
			{ new ConfigLEDDriver() },
			{ new ConfigTPRH31Driver() },
			{ new ConfigNanotagAcc() },
			{ new ConfigALS3001DDriver() },
            { new ConfigProxtitDriver() }

        };

		public static ushort ConvertNanotagScheduleType(ScheduleTypes scheduleType)
		{
			if(scheduleType == ScheduleTypes.Continues) 
			{
				return 0;
			}

            if (scheduleType == ScheduleTypes.Dated)
            {
                return 2;
            }

            if (scheduleType == ScheduleTypes.Daily)
            {
                return 3;
            }

            if (scheduleType == ScheduleTypes.Relative)
            {
                return 5;
            }

			return 0;
        }

        public static byte [] ConfigBinaryConverter(string json)
        {
			var options = new JsonSerializerOptions();
			options.WriteIndented = false;

            options.Converters.Add(new ScheduleTypesEnumConverter());
            options.Converters.Add(new ConfigurationDeviceDriver.ConfigurationDeviceDriverConverter());
            options.Converters.Add(new VesperDateTimeConverter());
            options.Converters.Add(new VesperPowerOnConverter());
            options.Converters.Add(new VesperDateTimeAlarmConverter());

            ConfigurationJSON? config = null;
			bool ok = false;

			byte[] buffer = new byte[0];

			try
			{
				config = JsonSerializer.Deserialize<ConfigurationJSON>(json, options)!;
				ok = true;
			}
			catch (Exception ex)
			{
				//Debug.WriteLine(ex);
			}
			finally
			{ }


			UInt32 delayedStart = 0;
			if (ok == true && config != null)
			{
				try
				{
					delayedStart = config.PowerOn.ToNanotagDateTime();
				}
				catch (ArgumentOutOfRangeException)
				{
					ok = false;                     // a delay the tag cannot hold: no config (upload fails)
				}
			}

			if (ok == true && config != null && config.Schedule != null && config.Schedule.Count > MAX_SCHEDULE_ENTRIES)
				ok = false;                         // the tag would refuse to arm: no config (upload fails)

			if (ok == true && config != null)
			{
				int structsize = 3 * CONFIG_CHUNK_SIZE;
				buffer = new byte[structsize];
				IntPtr ptr;

				NanotagConfiguration sConfiguration;
				sConfiguration = new NanotagConfiguration();
				sConfiguration.devices_list = new NanotagDeviceConfigParams[MAX_LOADED_DEVICES];
				sConfiguration.schedule_entries = new NanotagScheduleEntry[MAX_SCHEDULE_SIZE];

				if (config != null)
				{
					int i;

					sConfiguration.general.isMagnetOffEnabled = (byte)(config.IsMagnetOffEnabled ? 1 : 0);
					ushort.TryParse(config.MinimumSupportedHardware, out sConfiguration.general.minimum_hw_ver);
					sConfiguration.general.battery_capacity = (ushort)config.BatteryCapacity;
					sConfiguration.general.config_rev = 0;
					sConfiguration.general.reference_year = 2020;
					sConfiguration.general.Schedule_type = ConvertNanotagScheduleType(config.ScheduleType);
					// Delayed start ("poweron") at offset 18, firmware 1.39 (older firmware ignores it). The
					// byte at 10 stays 0: the packed time used to be written there and spilled into 11..13.
					sConfiguration.general.powerOnUnused = 0;
					sConfiguration.general.poweron = delayedStart;
					// RTC temperature compensation (firmware rtc_cal.c); 0 = firmware default.
					sConfiguration.general.rtc_tcomp = (byte)(config.TempComp == true ? 1 : 0);
					sConfiguration.general.rtc_tck = (ushort)Math.Min(config.TempCompCurvature ?? 0u, ushort.MaxValue);
					sConfiguration.general.rtc_tct0 = (short)Math.Min(config.TempCompTurnover ?? 0u, (uint)short.MaxValue);
					sConfiguration.general.rtc_tcper = (ushort)Math.Min(config.TempCompPeriod ?? 0u, ushort.MaxValue);

                    if (sConfiguration.devices_list != null)
					{
						for (i = 0; i < MAX_LOADED_DEVICES; i++)
						{
							if (config.DeviceDrivers.Count > i)
							{
								sConfiguration.devices_list[i].name = new char[DEVICE_NAME_LENGTH];
								for (int c = 0; c < DEVICE_NAME_LENGTH; c++)
									sConfiguration.devices_list[i].name[c] = (config.DeviceDrivers[i].Name.Length > c) ? config.DeviceDrivers[i].Name[c] : (char)0;

								sConfiguration.devices_list[i].windowLen1 = config.DeviceDrivers[i].WindowLength[1];
								sConfiguration.devices_list[i].windowLen2 = config.DeviceDrivers[i].WindowLength[2];
								sConfiguration.devices_list[i].windowRate1 = config.DeviceDrivers[i].WindowRate[1];
								sConfiguration.devices_list[i].windowRate2 = config.DeviceDrivers[i].WindowRate[2];
								sConfiguration.devices_list[i].sampleRate1 = config.DeviceDrivers[i].SampleRate[1];
								sConfiguration.devices_list[i].sampleRate2 = config.DeviceDrivers[i].SampleRate[2];
								sConfiguration.devices_list[i].controlBitmask = config.DeviceDrivers[i].Bitmask;
								///sConfiguration.devices_list[i].memorySize = config.DeviceDrivers[i].MemoryBufferSize;	/// deprecated
								sConfiguration.devices_list[i].RawData1 = config.DeviceDrivers[i].RawData1;
								sConfiguration.devices_list[i].RawData2 = config.DeviceDrivers[i].RawData2;
								sConfiguration.devices_list[i].RawData3 = config.DeviceDrivers[i].RawData3;
								sConfiguration.devices_list[i].RawData4 = config.DeviceDrivers[i].RawData4;
								// NANOTAG does not have file size property
							}
							else
							{
								sConfiguration.devices_list[i].name = new char[DEVICE_NAME_LENGTH];
								for (int c = 0; c < DEVICE_NAME_LENGTH; c++)
									sConfiguration.devices_list[i].name[c] = (char)0;
							}
						}
						if (sConfiguration.schedule_entries != null)
						{
							for (i = 0; i < MAX_SCHEDULE_SIZE; i++)
							{
								if (config.Schedule.Count > i)
								{
									sConfiguration.schedule_entries[i].time = ScheduleEntryTimestamp(config.Schedule[i], config.ScheduleType);
									sConfiguration.schedule_entries[i].config = ((uint)config.Schedule[i].Configuration);
								}
								else
								{
									sConfiguration.schedule_entries[i].time = 0xFFFFFFFF;
									sConfiguration.schedule_entries[i].config = 0xFF;
								}
							}
						}
					}
				}

				ptr = Marshal.AllocHGlobal(structsize);
				Marshal.StructureToPtr(sConfiguration, ptr, true);
				Marshal.Copy(ptr, buffer, 0, structsize);
				Marshal.FreeHGlobal(ptr);
			}
			else
            {

            }

			return buffer;
        }


		/// <summary>
		/// A schedule entry in the tag's RTC layout. Relative schedule: year 0 (= relative), month 0, day = day
		/// offset 0..31 + time (firmware 1.39 schedule.c Sched_ShiftRelative adds it to the start time, so day 0 =
		/// the start day). Other types: <see cref="ToTimestamp"/>.
		/// </summary>
		public static UInt32 ScheduleEntryTimestamp(ConfigScheduleJSONItem item, ScheduleTypes scheduleType)
		{
			if (scheduleType != ScheduleTypes.Relative)
				return ToTimestamp(item.Alarm);

			TimeSpan off = item.RelativeOffset;
			int days = Math.Min(off.Days, ConfigScheduleJSONItem.MaxRelativeDays);
			return ((UInt32)days << 17) | ((UInt32)off.Hours << 12) | ((UInt32)off.Minutes << 6) | (UInt32)off.Seconds;
		}

		public static UInt32 ToTimestamp(DateTime dt)
        {

			UInt32 ret = 0;

			// Years since 2020 in 6 bits; 2020 and earlier (daily entries are written with year 2000)
			// are 0 - it used to be 2020, which wrapped to 36 (2056) in the 6-bit field.
			int year = (dt.Year > REFERENCE_YEAR) ? Math.Min(dt.Year - REFERENCE_YEAR, 63) : 0;

			UInt32 yr  = (UInt32)((UInt32)year << 26);
			UInt32 mt = (UInt32)((UInt32)dt.Month << 22);
			UInt32 dy = (UInt32)((UInt32)dt.Day << 17);
			UInt32 hr = (UInt32)((UInt32)dt.Hour << 12);
			UInt32 mn = (UInt32)((UInt32)dt.Minute << 6);
			UInt32 sc = (UInt32)((UInt32)dt.Second);

			ret = yr | mt | dy | hr | mn | sc;

			return ret;
		}

		public static DateTime FromTimestamp(UInt32 ts, UInt16 ms)
		{
			int seconds = (int)(ts & ((1 << 6) - 1));
			int minutes = (int)((ts >> 6) & ((1 << 6) - 1));
			int hours = (int)((ts >> 12) & ((1 << 5) - 1));
			int days = (int)((ts >> 17) & ((1 << 5) - 1));
			int months = (int)((ts >> 22) & ((1 << 4) - 1));
			int years = (int)((ts >> 26) & ((1 << 6) - 1));
			years += REFERENCE_YEAR;

			if (seconds < 0) seconds = 0;
			else if (seconds > 59) seconds = 59;

			if (minutes < 0) minutes = 0;
			else if (minutes > 59) minutes = 59;

			if (hours < 0) hours = 0;
			else if (hours > 23) hours = 23;

			if (days < 0) days = 0;
			else if (days > 31) days = 31;

			if (months < 0) months = 0;
			else if (months > 12) months = 12;

			if (years < 0) years = REFERENCE_YEAR;
			else if (years > REFERENCE_YEAR + 1000) years = REFERENCE_YEAR;

			DateTime dt;

			try
			{
				dt = new DateTime(years, months, days, hours, minutes, seconds, (int)(((double)ms / 1024) * 1000));
			}
			catch (Exception ex)
			{
				dt = DateTime.Now;
			}

			return dt;
		}

		public static FlashGeometry ConvertFlashGeometry(byte[] buffer, int startindex)
		{
			FlashGeometry flashGeometry = new FlashGeometry();
			IntPtr ptr;

			ptr = Marshal.AllocHGlobal(36);
			Marshal.StructureToPtr(flashGeometry, ptr, true);
			Marshal.Copy(buffer, startindex, ptr, 36);
			Marshal.FreeHGlobal(ptr);

			return flashGeometry;
		}

	}




	[System.Runtime.InteropServices.StructLayout(LayoutKind.Explicit)]
	internal struct NanotagScheduleEntry
	{
		[System.Runtime.InteropServices.FieldOffset(0)]
		public UInt32 time;
		[System.Runtime.InteropServices.FieldOffset(4)]
		public UInt32 config;
	}


	[System.Runtime.InteropServices.StructLayout(LayoutKind.Explicit)]
	internal struct NanotagDeviceConfigParams
	{
		[System.Runtime.InteropServices.FieldOffset(0)]
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
		public char[] name;

		[System.Runtime.InteropServices.FieldOffset(16)]
		public UInt32 sampleRate1;                        // config1, config2
		[System.Runtime.InteropServices.FieldOffset(20)]
		public UInt32 sampleRate2;                        // config1, config2

		[System.Runtime.InteropServices.FieldOffset(24)]
		public UInt32 windowLen1;
		[System.Runtime.InteropServices.FieldOffset(28)]
		public UInt32 windowLen2;
		[System.Runtime.InteropServices.FieldOffset(32)]
		public UInt32 windowRate1;
		[System.Runtime.InteropServices.FieldOffset(36)]
		public UInt32 windowRate2;
		[System.Runtime.InteropServices.FieldOffset(40)]
		public UInt32 memorySize;
		[System.Runtime.InteropServices.FieldOffset(44)]
		public UInt32 controlBitmask;                    // for all devices - bit1 reserved for LED

		[System.Runtime.InteropServices.FieldOffset(48)]
		public UInt32 RawData1;
		[System.Runtime.InteropServices.FieldOffset(52)]
		public UInt32 RawData2;
		[System.Runtime.InteropServices.FieldOffset(56)]
		public UInt32 RawData3;
		[System.Runtime.InteropServices.FieldOffset(60)]
		public UInt32 RawData4;

		[System.Runtime.InteropServices.FieldOffset(48)]
		public UInt32 GPSSnapshotSize;
		[System.Runtime.InteropServices.FieldOffset(52)]
		public UInt32 GPSData2;
		[System.Runtime.InteropServices.FieldOffset(56)]
		public UInt32 GPSData3;
		[System.Runtime.InteropServices.FieldOffset(60)]
		public UInt32 GPSData4;

		[System.Runtime.InteropServices.FieldOffset(48)]
		public UInt32 ACCDynamicRange;
		[System.Runtime.InteropServices.FieldOffset(52)]
		public UInt32 ACCData2;
		[System.Runtime.InteropServices.FieldOffset(56)]
		public UInt32 ACCData3;
		[System.Runtime.InteropServices.FieldOffset(60)]
		public UInt32 ACCData4;

		[System.Runtime.InteropServices.FieldOffset(48)]
		public UInt32 ADSChannels;
		[System.Runtime.InteropServices.FieldOffset(52)]
		public UInt32 ADSData2;
		[System.Runtime.InteropServices.FieldOffset(56)]
		public UInt32 ADSData3;
		[System.Runtime.InteropServices.FieldOffset(60)]
		public UInt32 ADSData4;

		[System.Runtime.InteropServices.FieldOffset(48)]
		public UInt32 ALSResolution;
		[System.Runtime.InteropServices.FieldOffset(52)]
		public UInt32 ALSContinues;
		[System.Runtime.InteropServices.FieldOffset(56)]
		public UInt32 ALSData3;
		[System.Runtime.InteropServices.FieldOffset(60)]
		public UInt32 ALSData4;
	}


	[System.Runtime.InteropServices.StructLayout(LayoutKind.Sequential, Pack = 1)]
	// Byte-for-byte mirror of the firmware's device_general_params_t (NANOTAG firmware src/params.h,
	// 1.39): 64 bytes used + 3 x 64 spare = 256 bytes (one EEPROM row). Offsets are pinned
	// by VesperApp.Tests NanotagConfigLayoutTests — change both sides together.
	internal struct NanotagGeneralConfiguration
	{
		public UInt16 minimum_hw_ver;                                       // 0
		public byte isMagnetOffEnabled;                                     // 2
		public byte config_rev;                                             // 3
		public UInt16 battery_capacity;                                     // 4
		public UInt16 reference_year;                                       // 6
		public UInt16 Schedule_type;                                        // 8   0=continues, 1=first_trigger,  2=dated, 3 = daily, 4 = weekly, 5 = relative
		public byte powerOnUnused;                                          // 10  u8 powerOn in the firmware: unused, always 0
		public byte rtc_tcomp;                                              // 11  RTC temperature compensation, 1 = on (config.json "tcomp")
		public UInt16 rtc_tck;                                              // 12  curvature ppb/degC^2, 0 = default 34 ("tck")
		public Int16 rtc_tct0;                                              // 14  turnover in 0.1 degC, 0 = default 250 ("tct0")
		public UInt16 rtc_tcper;                                            // 16  re-apply period s, 0 = default 600, min 60 ("tcper")
		public UInt32 poweron;                                              // 18  delayed start ("poweron", firmware 1.39): packed RTC time, 0 = none,
		                                                                    //     year 0 = relative (days + h:m:s after arming), else absolute
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 42)]
		public byte[]res;                                                   // 22..63
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
		public byte[]reserved2;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
		public byte[]reserved3;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
		public byte[]reserved4;
	}


	[System.Runtime.InteropServices.StructLayout(LayoutKind.Explicit)]
	internal struct NanotagConfiguration
	{
		[System.Runtime.InteropServices.FieldOffset(0)]
		public NanotagGeneralConfiguration general;			// toal of 256 bytes

		[System.Runtime.InteropServices.FieldOffset(256)]
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
		public NanotagDeviceConfigParams []devices_list;       // total of 256 bytes (one EEPROM raw)

		[System.Runtime.InteropServices.FieldOffset(512)]
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
		public NanotagScheduleEntry[]schedule_entries;		// total of 256 bytes (one EEPROM raw)

/*		[System.Runtime.InteropServices.FieldOffset(0)]
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 768)]
		public byte[] Bytes;*/
	}


	[System.Runtime.InteropServices.StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct FlashGeometry
	{
		UInt32 nandSizePages;
		UInt32 pageSizeBytes;     // Page size in Bytes
		UInt32 spareSizeBytes;
		UInt16 nandSizeBlocks;
		UInt16 columnAddrMask;     // (1 << (number of bits in column address+1)) - 1
		UInt16 planeSizeBlocks;
		UInt16 blockSizePages;
		UInt16 blockSizePages2Log; // number of bitshifts in pages in block
		UInt16 blockAddressShift;
		UInt16 maxLunBadBlocks;
		UInt16 reserved;
		byte lunSizePlanes;
		byte dieSizeLuns;
		byte nandSizeDies;
		byte nandSizeGb;
	}


}
