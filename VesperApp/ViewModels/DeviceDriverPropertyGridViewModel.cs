using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Selection;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using System.ComponentModel;
using VesperApp.Models;
using System.Collections.ObjectModel;

namespace VesperApp.ViewModels
{
    public class DeviceDriverPropertyGridViewModel : ViewModelBase, IDisposable
    {
        //private readonly IVisual _control;
        private readonly IDictionary<object, List<DriverPropertyViewModel>> _propertyIndex;
        private DriverPropertyViewModel ? _selectedProperty;
        object ? _devicedriver;

        public DeviceDriverPropertyGridViewModel()
        {
            //var view = new DataGridCollectionView(new List<object>());
            //this.PropertiesView = view;

            //this._propertyIndex = new Dictionary<object, List<DriverPropertyViewModel>>();
            
            /*_devicedriver = new ConfigACLYSDriver();
            Type to = _devicedriver.GetType();
            */
            List<DriverPropertyViewModel> properties = new List<DriverPropertyViewModel>();//to.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                //.Select(x => new DriverPropertyViewModel(_devicedriver, x))
                //.OrderBy(x => x, PropertyComparer.Instance)
                //.ThenBy(x => x.Name)
                //.ToList();

            _propertyIndex = properties.GroupBy(x => x.Key).ToDictionary(x => x.Key, x => x.ToList());


            PropertiesCollectionView = new ObservableCollection<DriverPropertyViewModel>(properties);

            var view = new DataGridCollectionView(PropertiesCollectionView);
            view.GroupDescriptions.Add(new DataGridPathGroupDescription(nameof(DriverPropertyViewModel.Group)));
            //view.Filter = FilterProperty;
            PropertiesView = view;          

            _devicedriver = null;
            _selectedProperty = null;
           // PropertiesView = null;
        }


        public ObservableCollection<DriverPropertyViewModel> PropertiesCollectionView { get; }

        public async Task<bool> UpdateDeviceDriverPropertyGrid(object ? devicedriver)
        {
            if(_devicedriver != null)
            {
                if (_devicedriver is INotifyPropertyChanged inpc)
                {
                    inpc.PropertyChanged -= ControlPropertyChanged;
                }
            }

            _devicedriver = devicedriver;

            if (devicedriver != null)
            {
                Type to = devicedriver.GetType();

                var properties = to.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(x => x.GetCustomAttribute<BrowsableAttribute>(true)?.Browsable == true)
                    .Select(x => new DriverPropertyViewModel(devicedriver, x))
                    .OrderBy(x => x, PropertyComparer.Instance)
                    .ThenBy(x => x.Name)
                    .ToList();

                IDictionary<object, List<DriverPropertyViewModel>> _propertyI = properties.GroupBy(x => x.Key).ToDictionary(x => x.Key, x => x.ToList());
                //_propertyIndex = properties.GroupBy(x => x.Key).ToDictionary(x => x.Key, x => x.ToList());

                _propertyIndex.Clear();
                foreach (var propI in _propertyI)
                    _propertyIndex.Add(propI);

                PropertiesCollectionView.Clear();
                foreach(DriverPropertyViewModel d in properties)
                    PropertiesCollectionView.Add(d);

                if (devicedriver is INotifyPropertyChanged inpc)
                {
                    inpc.PropertyChanged += ControlPropertyChanged;
                }
            }
            else
                PropertiesCollectionView.Clear();

            return await Task.FromResult(true);
        }

        //public TreePageViewModel TreePage { get; }

        public DataGridCollectionView? PropertiesView 
        {
            get => _propView;
            protected set => this.RaiseAndSetIfChanged(ref _propView, value); 
        }

        private DataGridCollectionView? _propView;


        public DriverPropertyViewModel ? SelectedProperty
        {
            get => _selectedProperty;
            set => this.RaiseAndSetIfChanged(ref _selectedProperty, value);
        }

        public void Dispose()
        {
            
            if (_devicedriver is INotifyPropertyChanged inpc)
            {
                inpc.PropertyChanged -= ControlPropertyChanged;
            }
            /*
            if (_control is AvaloniaObject ao)
            {
                ao.PropertyChanged -= ControlPropertyChanged;
            }*/
        }
        /*
        private IEnumerable<PropertyViewModel> GetAvaloniaProperties(object o)
        {
            if (o is AvaloniaObject ao)
            {
                return AvaloniaPropertyRegistry.Instance.GetRegistered(ao)
                    .Union(AvaloniaPropertyRegistry.Instance.GetRegisteredAttached(ao.GetType()))
                    .Select(x => new AvaloniaPropertyViewModel(ao, x));
            }
            else
            {
                return Enumerable.Empty<AvaloniaPropertyViewModel>();
            }
        }

        private IEnumerable<PropertyViewModel> GetClrProperties(object o)
        {
            foreach (var p in GetClrProperties(o, o.GetType()))
            {
                yield return p;
            }

            foreach (var i in o.GetType().GetInterfaces())
            {
                foreach (var p in GetClrProperties(o, i))
                {
                    yield return p;
                }
            }
        }

        private IEnumerable<PropertyViewModel> GetClrProperties(object o, Type t)
        {
            return t.GetProperties()
                .Where(x => x.GetIndexParameters().Length == 0)
                .Select(x => new ClrPropertyViewModel(o, x));
        }

        private void ControlPropertyChanged(object sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (_propertyIndex.TryGetValue(e.Property, out var properties))
            {
                foreach (var property in properties)
                {
                    property.Update();
                }
            }
        }
        */
        private void ControlPropertyChanged(object ? sender, PropertyChangedEventArgs e)
        {
            if (sender != null && e.PropertyName != null)
            {
                foreach (var item in _propertyIndex)
                {
                    if (item.Key != null)
                    {
                        if ((item.Key as PropertyInfo)?.Name == e.PropertyName)
                        {
                            if (_propertyIndex.TryGetValue(item.Key, out var properties))
                            {
                                foreach (var property in properties)
                                {
                                    property.Update();
                                }
                            }

                            break;
                        }
                    }
                }
            }
        }
        /*
        private bool FilterProperty(object arg)
        {
            if (!string.IsNullOrWhiteSpace(TreePage.PropertyFilter) && arg is PropertyViewModel property)
            {
                if (TreePage.UseRegexFilter)
                {
                    return TreePage.FilterRegex?.IsMatch(property.Name) ?? true;
                }

                return property.Name.IndexOf(TreePage.PropertyFilter, StringComparison.OrdinalIgnoreCase) != -1;
            }

            return true;
        }*/

        private class PropertyComparer : IComparer<PropertyViewModel>
        {
            public static PropertyComparer Instance { get; } = new PropertyComparer();

            public int Compare(PropertyViewModel? x, PropertyViewModel? y)
            {
                if(x == null && y == null) return 0;
                if(y == null) return -1;
                if(x == null) return 1;

                var groupX = GroupIndex(x.Group);
                var groupY = GroupIndex(y.Group);

                if (groupX != groupY)
                {
                    return groupX - groupY;
                }
                else
                {
                    return string.CompareOrdinal(x.Name, y.Name);
                }
            }

            private int GroupIndex(string group)
            {
                switch (group)
                {
                    case "Properties": return 0;
                    //case "Attached Properties": return 1;
                    //case "CLR Properties": return 2;
                    default: return 3;
                }
            }
        }
    }
}
