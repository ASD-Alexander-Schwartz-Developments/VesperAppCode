using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;

namespace VesperApp.Models
{
    public class ColorConverter : IValueConverter
    {
        public static readonly ColorConverter Instance = new();

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not string s) return Colors.Transparent;
            return s switch
            {
                "Config1" => Colors.DarkGreen,
                "Config2" => Colors.LightGreen,
                _ => Colors.Transparent
            };
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }


    public class Appointment
    {

        public Appointment()
        {
            _Title = "Config1";
        }

        private DateTime _StartDate;

        public DateTime StartDate
        {
            get
            {
                return _StartDate;
            }
            set
            {
                _StartDate = value;
            }
        }

        private DateTime _EndDate;

        public DateTime EndDate
        {
            get
            {
                return _EndDate;
            }
            set
            {
                _EndDate = value;
            }
        }

        private string _Title = "";

        [System.ComponentModel.DefaultValue("")]
        public string Title
        {
            get
            {
                return _Title;
            }
            set
            {
                _Title = value;
            }
        }
    }
}
