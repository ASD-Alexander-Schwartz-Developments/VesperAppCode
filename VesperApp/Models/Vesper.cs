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

namespace VesperApp.Models
{
    public class Vesper
    {
		public static readonly int DEVICE_NAME_LENGTH = 16;
		public static readonly int MAX_LOADED_DEVICES = 4;
		public static readonly int MAX_SCHEDULE_SIZE = 64;
		public static readonly int REFERENCE_YEAR = 2020;

		private static readonly int TICKS_IN_SECOND = 1024;

		public static readonly int VendorId = 1155;
		public static readonly int ProductId = 22288;

        public static readonly List<ConfigurationDeviceDriver> SupportedDeviceDrivers = new List<ConfigurationDeviceDriver>
		{
			{ new ConfigACLYSDriver() },
            { new ConfigSPH0641Driver() },
            { new ConfigLEDDriver() },
            { new ConfigIMU10Driver() },
            { new ConfigIMU10HTDriver() },
            { new ConfigTPRH31Driver() },
			{ new ConfigALS3001DDriver() },
            { new ConfigEXG48Driver() },
            { new ConfigEXG2Driver() },
            { new ConfigProxtitDriver() }
        };

		public static UInt32 ToTimestamp(DateTime dt)
        {
			UInt32 ret = 0;

			int year = (dt.Year > REFERENCE_YEAR) ? dt.Year - REFERENCE_YEAR : REFERENCE_YEAR;

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
				dt = new DateTime(years, months, days, hours, minutes, seconds, (int)(((double)ms / TICKS_IN_SECOND) * 1000));
			}
			catch (Exception ex)
			{
				dt = DateTime.Now;
			}

			return dt;
		}
	}
}
