using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;


namespace VesperApp.Models
{
    public class ConfigurationDeviceDriver : INotifyPropertyChanged, IEquatable<ConfigurationDeviceDriver>
    {
        private string name;
        private string description;
        protected UInt32[] sample_rate;
        private UInt32[] window_length;
        private UInt32[] window_rate;

        protected UInt32 file_size;
        protected UInt32 mem_size;
        protected UInt32 bitmask;

        private UInt32 rawData1;
        private UInt32 rawData2;
        private UInt32 rawData3;
        private UInt32 rawData4;


        public event PropertyChangedEventHandler? PropertyChanged;

        public ConfigurationDeviceDriver(string name, string description)
        {
            this.name = name;
            this.description = description;
            this.sample_rate = new UInt32[3];
            this.window_length = new UInt32[3];
            this.window_rate = new UInt32[3];

            this.sample_rate[0] = 0;
            this.sample_rate[1] = 0;
            this.sample_rate[2] = 0;

            this.window_length[0] = 0;
            this.window_length[1] = 0;
            this.window_length[2] = 0;

            this.window_rate[0] = 0;
            this.window_rate[1] = 0;
            this.window_rate[2] = 0;

            this.rawData1 = 0;
            this.rawData2 = 0;
            this.rawData3 = 0;
            this.rawData4 = 0;
        }


        [JsonPropertyName("name"), JsonPropertyOrder(0)]
        [Browsable(false)]
        public virtual string Name
        {
            get { return this.name; }
            //set { this.name = value; }
        }

        [JsonIgnore]
        [Browsable(false)]
        public virtual string Description
        {
            get { return this.description; }
            //set { this.name = value; }
        }

        [JsonPropertyName("sampleRate")]
        [Browsable(false)]
        public virtual UInt32[] SampleRate
        {
            get
            {
                return this.sample_rate;
            }
            set
            {
                this.sample_rate = value;
            }
        }

        [CategoryAttribute("Standard configuration"),
        DescriptionAttribute("Sample Rate of the sensor operating in Config1 schedule"),
        DisplayName("Sample Rate [1]")]
        [JsonIgnore]
        [Browsable(true)]
        public virtual UInt32 SampleRate1
        {
            get => SampleRate[1];
            set
            {
                SampleRate[1] = value;
                OnPropertyChanged();
            }
        }

        [CategoryAttribute("Standard configuration"),
        DescriptionAttribute("Sample Rate of the sensor operating in Config2 schedule"),
        DisplayName("Sample Rate [2]")]
        [JsonIgnore]
        [Browsable(true)]
        public virtual UInt32 SampleRate2
        {
            get => SampleRate[2];
            set
            {
                SampleRate[2] = value;
                OnPropertyChanged();
            }
        }


        [JsonPropertyName("wLen")]
        [Browsable(false)]
        public UInt32[] WindowLength
        {
            get
            {
                return this.window_length;
            }
            set
            {
                this.window_length = value;
            }
        }


        [CategoryAttribute("Standard configuration"),
        DescriptionAttribute("Duty cycled sampling ON time in [ms] when running Config2 schedule"),
        DisplayName("Window Length [1]")]
        [JsonIgnore]
        [Browsable(true)]
        public virtual UInt32 WindowLength1
        {
            get => WindowLength[1];
            set
            {
                WindowLength[1] = value;
                OnPropertyChanged();
            }
        }

        [CategoryAttribute("Standard configuration"),
        DescriptionAttribute("Duty cycled sampling ON time in [ms] when running Config2 schedule"),
        DisplayName("Window Length [2]")]
        [JsonIgnore]
        [Browsable(true)]
        public virtual UInt32 WindowLength2
        {
            get => WindowLength[2];
            set
            {
                WindowLength[2] = value;
                OnPropertyChanged();
            }
        }


        [JsonPropertyName("wRate")]
        [Browsable(false)]
        public UInt32[] WindowRate
        {
            get
            {
                return this.window_rate;
            }
            set
            {
                this.window_rate = value;
            }
        }

        [CategoryAttribute("Standard configuration"),
        DescriptionAttribute("Duty cycled sampling OFF time in [ms] when running Config1 schedule"),
        DisplayName("Window Rate [1]")]
        [JsonIgnore]
        [Browsable(true)]
        public virtual UInt32 WindowRate1
        {
            get => WindowRate[1];
            set
            {
                WindowRate[1] = value;
                OnPropertyChanged();
            }
        }

        [CategoryAttribute("Standard configuration"),
        DescriptionAttribute("Duty cycled sampling OFF time in [ms] when running Config2 schedule"),
        DisplayName("Window Rate [2]")]
        [JsonIgnore]
        [Browsable(true)]
        public virtual UInt32 WindowRate2
        {
            get => WindowRate[2];
            set
            {
                WindowRate[2] = value;
                OnPropertyChanged();
            }
        }





        [JsonPropertyName("memorySize")]
        [Browsable(true)]
        [CategoryAttribute("Advanced configuration"),
        DescriptionAttribute("Size (in Bytes) of memory buffer containing sensor samples"),
        DisplayName("Memory Size")]
        public virtual UInt32 MemoryBufferSize
        {
            get { return this.mem_size; }
            set 
            { 
                this.mem_size = value;
                OnPropertyChanged();
            }
        }

        [JsonPropertyName("fileSize")]
        [Browsable(true)]
        [CategoryAttribute("Advanced configuration"),
        DescriptionAttribute("Size (in Bytes) of a single file containing sensor samples, once file reaches this side, it's timestamped, closed and new file is opened"),
        DisplayName("File Size")]
        public virtual UInt32 FileSize
        {
            get { return this.file_size; }
            set 
            { 
                this.file_size = value;
                OnPropertyChanged();
            }
        }

        [Browsable(false)]
        [JsonPropertyName("bitmask")]
        public virtual UInt32 Bitmask
        {
            get { return this.bitmask; }
            set 
            { 
                this.bitmask = value; 
                OnPropertyChanged(nameof(Bitmask));
            }
        }

        [Browsable(true)]
        [JsonIgnore]
        [CategoryAttribute("Standard configuration"),
        DescriptionAttribute("Should LED indicate activity is this driver"),
        DisplayName("LED Activity")]
        public virtual bool IsLEDActive
        {
            get => ((Bitmask & 0x01) == 0x01);
            set
            {
                if (value == true) 
                    Bitmask |= 0x01;
                else
                    Bitmask &= ~((UInt32)0x01);
            }
        }


        [Browsable(false)]
        [JsonIgnore]
        public virtual UInt32 RawData1
        {
            get { return this.rawData1; }
            protected set { this.rawData1 = value; OnPropertyChanged(); }
        }

        [Browsable(false)]
        [JsonIgnore]
        public virtual UInt32 RawData2
        {
            get { return this.rawData2; }
            protected set { this.rawData2 = value; OnPropertyChanged(); }
        }

        [Browsable(false)]
        [JsonIgnore]
        public virtual UInt32 RawData3
        {
            get { return this.rawData3; }
            protected set { this.rawData3 = value; OnPropertyChanged(); }
        }

        [Browsable(false)]
        [JsonIgnore]
        public virtual UInt32 RawData4
        {
            get { return this.rawData4; }
            protected set { this.rawData4 = value; OnPropertyChanged(); }
        }

        [Browsable(false)]
        [JsonIgnore]
        public bool IsChecked
        {
            get;
            set;
        } = false;

        // Create the OnPropertyChanged method to raise the event
        // The calling member's name will be used as the parameter.
        protected void OnPropertyChanged([CallerMemberName] string ? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }



        public override bool Equals(object? obj) => this.Equals(obj as ConfigurationDeviceDriver);

        public bool Equals(ConfigurationDeviceDriver? d)
        {
            if (d is null)
            {
                return false;
            }

            // Optimization for a common success case.
            if (Object.ReferenceEquals(this, d))
            {
                return true;
            }

            // If run-time types are not exactly the same, return false.
            if (this.GetType() != d.GetType())
            {
                return false;
            }

            // Return true if the fields match.
            // Note that the base class is not invoked because it is
            // System.Object, which defines Equals as reference equality.
            return (Name == Name);
        }

        public override int GetHashCode() => (nameof(ConfigurationDeviceDriver) + Name).GetHashCode();

        public static bool operator == (ConfigurationDeviceDriver? lhs, ConfigurationDeviceDriver? rhs)
        {
            if (lhs is null)
            {
                if (rhs is null)
                {
                    return true;
                }

                // Only the left side is null.
                return false;
            }
            // Equals handles case of null on right side.
            return lhs.Equals(rhs);
        }

        public static bool operator != (ConfigurationDeviceDriver? lhs, ConfigurationDeviceDriver? rhs) => !(lhs == rhs);





        public class ConfigurationDeviceDriverConverter : JsonConverter<ConfigurationDeviceDriver>
        {
            //private string TypeDiscriminator;

            public override bool CanConvert(Type type)
            {
                return typeof(ConfigurationDeviceDriver).IsAssignableFrom(type);
            }

            public override ConfigurationDeviceDriver Read(
                ref Utf8JsonReader reader,
                Type typeToConvert,
                JsonSerializerOptions options)
            {
                Utf8JsonReader rd = reader;

                if (rd.TokenType != JsonTokenType.StartObject)
                {
                    throw new JsonException();
                }

                if (!rd.Read()
                        || rd.TokenType != JsonTokenType.PropertyName
                        || rd.GetString() != "name")
                {
                    throw new JsonException();
                }

                if (!rd.Read() || rd.TokenType != JsonTokenType.String)
                {
                    throw new JsonException();
                }

                string? typeDiscriminator = rd.GetString();
                if (typeDiscriminator == null) throw new JsonException();

                ConfigurationDeviceDriver baseClass;


                switch (typeDiscriminator?.ToUpper())
                {
                    case "ACLYS":
                        baseClass = (ConfigACLYSDriver)JsonSerializer.Deserialize(ref reader, typeof(ConfigACLYSDriver));
                        break;
                    /*                case "MC34":
                                        if (!reader.Read() || reader.GetString() != "TypeValue")
                                        {
                                            throw new JsonException();
                                        }
                                        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
                                        {
                                            throw new JsonException();
                                        }
                                        baseClass = (DerivedB)JsonSerializer.Deserialize(ref reader, typeof(DerivedB));
                                        break;*/
                    default:
                        throw new NotSupportedException();
                }

                /*if (!reader.Read() || reader.TokenType != JsonTokenType.EndObject)
                {
                    throw new JsonException();
                }*/

                return baseClass;
            }

            public override void Write(
                Utf8JsonWriter writer,
                ConfigurationDeviceDriver value,
                JsonSerializerOptions options)
            {
                //            writer.WriteStartObject();

                if (value is ConfigACLYSDriver aclysDriver)
                {
                    //                writer.WriteNumber("name", (int)TypeDiscriminator.DerivedA);
                    //                writer.WritePropertyName("TypeValue");
                    JsonSerializer.Serialize(writer, aclysDriver);
                }
                /*else if (value is DerivedB derivedB)
                {
                    writer.WriteNumber("TypeDiscriminator", (int)TypeDiscriminator.DerivedB);
                    writer.WritePropertyName("TypeValue");
                    JsonSerializer.Serialize(writer, derivedB);
                }*/
                else
                {
                    throw new NotSupportedException();
                }

                //            writer.WriteEndObject();
            }
        }



    }





#if False


    [TypeConverter(typeof(ConfigTableConverter))]
    public class ConfigTable
    {
        private readonly UInt32 off = 0;
        private UInt32 config1 = 0;
        private UInt32 config2 = 0;
        private string name = "";

        public ConfigTable()
        {
            name = "";
        }

        [Browsable(false)]
        public string Name
        {
            get { return name; }
            set { name = value; }
        }

        public UInt32 Off
        {
            get { return off; }
        }

        public UInt32 Config1
        {
            get { return config1; }
            set { config1 = value; }
        }

        public UInt32 Config2
        {
            get { return config2; }
            set { config2 = value; }
        }

        // Meaningful text representation
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(this.config1);
            sb.Append(",");
            sb.Append(this.config2);
            return sb.ToString();
        }

    }

    // This is a special type converter which will be associated with the ConfigTable class.
    // It converts a ConfigTable object to string representation for use in a property grid.
    internal class ConfigTableConverter : ExpandableObjectConverter
    {
        public override object ConvertTo(ITypeDescriptorContext ? context, System.Globalization.CultureInfo ? culture, object ? value, Type destType)
        {
            if (destType == typeof(string) && value is ConfigTable)
            {
                // Cast the value to an ConfigTable type
                ConfigTable conf = (ConfigTable)value;

                // Return department and department role separated by comma.
                //if (conf.Config1 != 0 || conf.Config2 != 0)
                return "Configuration 1: " + conf.Config1 + ", Configuration 2: " + conf.Config2;
                //else
                //    return "Off";
            }
/*            if (destType == typeof(string) && value is EEGConfigTable)
            {
                // Cast the value to an ConfigTable type
                EEGConfigTable conf = (EEGConfigTable)value;

                // Return department and department role separated by comma.
                if (conf.Name.Equals("Off"))
                    return "0";
                else if (conf.Name.Equals("Config1"))
                    return "Configuration 1: " + conf.SampleRate;
                else
                    return "Configuration 2: " + conf.SampleRate;
                //else
                //    return "Off";
            }*/
            if (destType == typeof(string) && value is ConfigTableCollection)
            {
                // Return department and department role separated by comma.
                return "Basic Configurations";
            }
/*            if (destType == typeof(string) && value is EEGConfigTableCollection)
            {
                // Return department and department role separated by comma.
                return "EEG Configurations";
            }*/

            return base.ConvertTo(context, culture, value, destType);
        }
    }

    public class ConfigTableCollection : CollectionBase, ICustomTypeDescriptor
    {
        public void Add(ConfigTable conf)
        {
            this.List.Add(conf);
        }
        public void Remove(ConfigTable conf)
        {
            this.List.Remove(conf);
        }
        public ConfigTable this[int index]
        {
            get
            {
                return (ConfigTable)this.List[index];
            }
        }

        public String GetClassName()
        {
            return TypeDescriptor.GetClassName(this, true);
        }

        public AttributeCollection GetAttributes()
        {
            return TypeDescriptor.GetAttributes(this, true);
        }

        public String GetComponentName()
        {
            return TypeDescriptor.GetComponentName(this, true);
        }

        public TypeConverter GetConverter()
        {
            return TypeDescriptor.GetConverter(this, true);
        }

        public EventDescriptor GetDefaultEvent()
        {
            return TypeDescriptor.GetDefaultEvent(this, true);
        }

        public PropertyDescriptor GetDefaultProperty()
        {
            return TypeDescriptor.GetDefaultProperty(this, true);
        }

        public object GetEditor(Type editorBaseType)
        {
            return TypeDescriptor.GetEditor(this, editorBaseType, true);
        }

        public EventDescriptorCollection GetEvents(Attribute[] attributes)
        {
            return TypeDescriptor.GetEvents(this, attributes, true);
        }

        public EventDescriptorCollection GetEvents()
        {
            return TypeDescriptor.GetEvents(this, true);
        }

        public object GetPropertyOwner(PropertyDescriptor pd)
        {
            return this;
        }

        public PropertyDescriptorCollection GetProperties(Attribute[] attributes)
        {
            return GetProperties();
        }

        public PropertyDescriptorCollection GetProperties()
        {
            // Create a new collection object PropertyDescriptorCollection
            PropertyDescriptorCollection pds = new PropertyDescriptorCollection(null);

            // Iterate the list of ConfigTables
            for (int i = 0; i < this.List.Count; i++)
            {
                // For each ConfigTable create a property descriptor 
                // and add it to the 
                // PropertyDescriptorCollection instance
                CollectionPropertyDescriptor pd = new
                              CollectionPropertyDescriptor(this, i);
                pds.Add(pd);
            }
            return pds;
        }


    }

    public class CollectionPropertyDescriptor : PropertyDescriptor
    {
        private ConfigTableCollection collection = null;
        private int index = -1;

        public CollectionPropertyDescriptor(ConfigTableCollection coll,
                           int idx) : base("#" + idx.ToString(), null)
        {
            this.collection = coll;
            this.index = idx;
        }

        public override AttributeCollection Attributes
        {
            get
            {
                return new AttributeCollection(null);
            }
        }

        public override bool CanResetValue(object component)
        {
            return true;
        }

        public override Type ComponentType
        {
            get
            {
                return this.collection.GetType();
            }
        }

        public override string DisplayName
        {
            get
            {
                ConfigTable conf = this.collection[index];
                return conf.Name;
            }
        }

        public override string Description
        {
            get
            {
                ConfigTable conf = this.collection[index];
                StringBuilder sb = new StringBuilder();
                //if (conf.Config1 != 0 || conf.Config2 != 0)
                //{
                sb.Append("Configuration 1: ");
                sb.Append(conf.Config1);
                sb.Append(", Configration 2: ");
                sb.Append(conf.Config2);
                //}
                //else
                //sb.Append("Off");
                return sb.ToString();
            }
        }

        public override object GetValue(object component)
        {
            return this.collection[index];
        }

        public override bool IsReadOnly
        {
            get { return true; }
        }

        public override string Name
        {
            get { return "#" + index.ToString(); }
        }

        public override Type PropertyType
        {
            get { return this.collection[index].GetType(); }
        }

        public override void ResetValue(object component) { }

        public override bool ShouldSerializeValue(object component)
        {
            return true;
        }

        public override void SetValue(object component, object value)
        {
            // this.collection[index] = value;
        }
    }
#endif
}
