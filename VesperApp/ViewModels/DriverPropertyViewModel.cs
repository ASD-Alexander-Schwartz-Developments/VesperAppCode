using System;
using System.Collections;
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
    /// <summary>
    /// How a driver property should be edited in the grid. The view decides which
    /// editor to show purely from this value, so adding a new sensor option type
    /// needs no UI code at all.
    /// </summary>
    public enum PropertyEditorKind
    {
        Text,
        Integer,
        Real,
        Boolean,
        Options
    }

    public class DriverPropertyViewModel : PropertyViewModel
    {
        // Static option lists on the sensor types are inconsistently named; accept both.
        private static readonly string[] OptionListMemberNames = { "ListOfOptions", "ListOfLength" };

        private readonly object _target;
        private Type ? _type;
        private Type? _convertToType;
        private object ? _value;
        //private string _priority;
        private string _group;
        private bool _visible;

        private readonly IEnumerable? _options;
        private readonly PropertyEditorKind _editorKind;

        public DriverPropertyViewModel(object o, PropertyInfo property)
        {
            _target = o;
            _visible = false;
            Property = property;
            _group = "General Properties";
            Name = property.Name;
            Description = "";
            _convertToType = null;

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


            TypeConverterAttribute? converterAttr = property.GetCustomAttribute<TypeConverterAttribute>(true);
            if(converterAttr != null)
            {
                _convertToType = Type.GetType(converterAttr.ConverterTypeName);
            }

            BrowsableAttribute? visibleAttr = property.GetCustomAttribute<BrowsableAttribute>(true);
            if(visibleAttr != null)
            {
                //Debug.WriteLine(property.Name + " " + visibleAttr.Browsable.ToString());
                _visible = visibleAttr.Browsable;
            }

            _type = property.PropertyType;
            _value = property.GetValue(_target, null);

            // Resolve, once, how this property is edited and (if applicable) the
            // set of choices to offer. Driven by the declared property type so it
            // stays stable regardless of the current value.
            _options = FindOptions(_type);
            _editorKind = ResolveEditorKind(_type, _options);

            Update();
        }

        public PropertyInfo Property { get; }
        public override object Key => Property;
        public override string Name { get; }

        public Type? ConvertToType => _convertToType;
        public override string Description { get; }

        public override Type ? PropType => _type;

        public bool IsText
        {
            get { return _type != null && _type == typeof(string); }
        }

        public bool IsEnum
        {
            get { return _type != null && _type.IsEnum == true; }
        }

        /// <summary>The kind of editor the value cell should present.</summary>
        public PropertyEditorKind EditorKind => _editorKind;

        /// <summary>Choices for an <see cref="PropertyEditorKind.Options"/> property (null otherwise).</summary>
        public IEnumerable? Options => _options;

        // Convenience flags so the cell template can toggle editor visibility with
        // plain bindings (no converter needed).
        public bool UseOptions => _editorKind == PropertyEditorKind.Options;
        public bool UseInteger => _editorKind == PropertyEditorKind.Integer;
        public bool UseReal => _editorKind == PropertyEditorKind.Real;
        public bool UseBoolean => _editorKind == PropertyEditorKind.Boolean;
        public bool UseText => _editorKind == PropertyEditorKind.Text;


        public override object ? Value
        {
            get => _value;
            set
            {
                try
                {
                    this.RaiseAndSetIfChanged(ref _value, value);

                    if (_value is not null)
                    {
                        Property.SetValue(_target, _value);
                        Debug.WriteLine("Property Value set: " + value!.ToString());
                    }
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
            // Re-read from the target and align option values to the actual list
            // instance so the ComboBox shows the current selection.
            _value = NormalizeToOption(Property.GetValue(_target));
            this.RaisePropertyChanged(nameof(Value));
        }

        private void SetGroup(string group)
        {
            this.RaiseAndSetIfChanged(ref _group, group, nameof(Group));
        }

        // ───────────────────────── editor resolution ─────────────────────────

        private static IEnumerable? FindOptions(Type? t)
        {
            if (t == null) return null;

            // Real CLR enums become a combo of their members automatically.
            if (t.IsEnum) return Enum.GetValues(t);

            // Sensor option wrappers expose a public static list of choices.
            foreach (var name in OptionListMemberNames)
            {
                var p = t.GetProperty(name, BindingFlags.Public | BindingFlags.Static);
                if (p != null
                    && typeof(IEnumerable).IsAssignableFrom(p.PropertyType)
                    && p.GetValue(null) is IEnumerable e)
                {
                    return e;
                }
            }

            return null;
        }

        private static PropertyEditorKind ResolveEditorKind(Type? t, IEnumerable? options)
        {
            if (options != null) return PropertyEditorKind.Options;
            if (t == typeof(bool)) return PropertyEditorKind.Boolean;
            if (t == typeof(uint)) return PropertyEditorKind.Integer;
            if (t == typeof(double)) return PropertyEditorKind.Real;
            return PropertyEditorKind.Text;
        }

        /// <summary>
        /// For option properties, swap a freshly-read value for the matching element
        /// in <see cref="Options"/> (matched on the wrapped numeric Value) so the
        /// ComboBox can pre-select it by reference. Returns the value unchanged for
        /// every other kind.
        /// </summary>
        private object? NormalizeToOption(object? value)
        {
            if (_options == null || value == null) return value;

            long? target = TryGetInnerValue(value);
            if (target == null) return value; // e.g. CLR enums match by value already

            foreach (var opt in _options)
            {
                if (TryGetInnerValue(opt) == target) return opt;
            }

            return value;
        }

        private static long? TryGetInnerValue(object o)
        {
            var vp = o.GetType().GetProperty("Value", BindingFlags.Public | BindingFlags.Instance);
            if (vp == null) return null;

            var v = vp.GetValue(o);
            try { return v == null ? (long?)null : Convert.ToInt64(v); }
            catch { return null; }
        }
    }
}
