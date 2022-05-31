using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Selection;
using ReactiveUI;

namespace VesperApp.ViewModels
{
    public class DriverPropertyViewModel : PropertyViewModel
    {
        private readonly object _target;
        private string ? _type;
        private object ? _value;
        //private string _priority;
        private string _group;

        public DriverPropertyViewModel(object o, PropertyInfo property)
        {
            _target = o;
            Property = property;
            _group = "General Properties";
            Name = property.Name;
            Description = "";
            object[] attrs = property.GetCustomAttributes(true);
            foreach (object attr in attrs)
            {
                DescriptionAttribute ? descAttr = attr as DescriptionAttribute;
                if (descAttr != null)
                {
                    Description = descAttr.Description;
                }

                DisplayNameAttribute? nameAttr = attr as DisplayNameAttribute;
                if (nameAttr != null)
                {
                    Name = nameAttr.DisplayName;
                }

                CategoryAttribute? groupAttr = attr as CategoryAttribute;
                if (groupAttr != null)
                {
                    _group = groupAttr.Category;
                }
            }

            BrowsableAttribute? visibleAttr = property.GetCustomAttribute<BrowsableAttribute>(true);
            if(visibleAttr != null)
            {
                Debug.WriteLine(property.Name + " " + visibleAttr.Browsable.ToString());
            }


            Update();
        }

        public PropertyInfo Property { get; }
        public override object Key => Property;
        public override string Name { get; }

        public override string Description { get; }

        //public bool IsAttached => Property.PropertyType.is;

        /*public string Priority
        {
            get => _priority;
            private set => this.RaiseAndSetIfChanged(ref _priority, value);
        }*/

        public override string Type => ((_type == null) ? "" : _type);

        public override string Value
        {
            get => ConvertToString(_value);
            set
            {
                try
                {
                    var convertedValue = ConvertFromString(value, Property.PropertyType);
                    Property.SetValue(_target, convertedValue, null);
                    //_target.SetValue(Property, convertedValue);
                }
                catch { }
            }
        }

        public override string Group
        {
            get => _group;
        }

        public override void Update()
        {
            this.RaiseAndSetIfChanged(ref _value, Property.GetValue(_target), nameof(Value));
            this.RaiseAndSetIfChanged(ref _type, _value?.GetType().Name, nameof(Type));
        }

        private void SetGroup(string group)
        {
            this.RaiseAndSetIfChanged(ref _group, group, nameof(Group));
        }
    }

    public class DeviceDriverProperties : ObservableCollection<DriverPropertyViewModel>
    {

    }
}
