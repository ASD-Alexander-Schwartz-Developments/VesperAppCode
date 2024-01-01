using Avalonia.Data.Converters;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace VesperApp.Models
{
    public class EnumDescriptionConverter : IValueConverter
    {
        private string GetEnumDescription(Enum enumObj)
        {
            FieldInfo ?fieldInfo = enumObj.GetType().GetField(enumObj.ToString());

            if (fieldInfo != null)
            {
                object[] attribArray = fieldInfo.GetCustomAttributes(false);

                if (attribArray.Length == 0)
                    return enumObj.ToString();
                else
                {
                    DescriptionAttribute ? attrib = null;

                    foreach (var att in attribArray)
                    {
                        if (att is DescriptionAttribute)
                            attrib = att as DescriptionAttribute;
                    }

                    if (attrib != null)
                        return attrib.Description;

                    return enumObj.ToString();
                }
            }
            return enumObj.ToString();
        }

        object IValueConverter.Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value != null)
            {
                if (value.GetType().IsEnum)
                {
                    Enum myEnum = (Enum)value;
                    string description = GetEnumDescription(myEnum);
                    return description;
                }
                else if(value.GetType() == typeof(List<AclysSnapLength>))
                {
                    return new List<string> { "23" };
                }
                else
                {
                    return value;
                }
            }
            else
            {
                return string.Empty;
            }
        }

        object IValueConverter.ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return string.Empty;
        }
    }


}
