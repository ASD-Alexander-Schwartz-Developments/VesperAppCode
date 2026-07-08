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
            List<DriverPropertyViewModel> properties = new List<DriverPropertyViewModel>();

            _propertyIndex = properties.GroupBy(x => x.Key).ToDictionary(x => x.Key, x => x.ToList());

            PropertiesCollectionView = new ObservableCollection<DriverPropertyViewModel>(properties);

            var view = new DataGridCollectionView(PropertiesCollectionView);
            view.GroupDescriptions.Add(new DataGridPathGroupDescription(nameof(DriverPropertyViewModel.Group)));
            PropertiesView = view;

            _devicedriver = null;
            _selectedProperty = null;
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
        }

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
                    default: return 3;
                }
            }
        }
    }
}
