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
                if (_devicedriver is IDynamicBrowsable dyn && !ReferenceEquals(_devicedriver, devicedriver))
                {
                    dyn.LayoutChanged -= DriverLayoutChanged;
                }
            }

            bool newDriver = !ReferenceEquals(_devicedriver, devicedriver);
            _devicedriver = devicedriver;

            if (devicedriver != null)
            {
                Type to = devicedriver.GetType();

                var dynamic = devicedriver as IDynamicBrowsable;
                var properties = to.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(x => x.GetCustomAttribute<BrowsableAttribute>(true)?.Browsable == true)
                    .Where(x => dynamic?.IsPropertyVisible(x.Name) ?? true)
                    .Select(x => new DriverPropertyViewModel(devicedriver, x))
                    .ToList();
                properties = Order(to, properties);

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
                if (newDriver && dynamic != null)
                {
                    dynamic.LayoutChanged += DriverLayoutChanged;
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

        /* Rows depend on the driver's settings (sound-triggered mode relabels the window rows): rebuild after the
           current edit has finished - not inside the ComboBox binding that caused it. */
        private void DriverLayoutChanged(object? sender, EventArgs e)
        {
            object? driver = _devicedriver;
            Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
            {
                if (ReferenceEquals(driver, _devicedriver))
                    await UpdateDeviceDriverPropertyGrid(driver);
            });
        }

        public void Dispose()
        {
            if (_devicedriver is INotifyPropertyChanged inpc)
            {
                inpc.PropertyChanged -= ControlPropertyChanged;
            }
            if (_devicedriver is IDynamicBrowsable dyn)
            {
                dyn.LayoutChanged -= DriverLayoutChanged;
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
        public const string StandardGroup = "Standard configuration";
        public const string AdvancedGroup = "Advanced configuration";

        /* Every driver pane has the same shape: the standard rows, then the sensor's own groups, then the advanced
           rows (file size ...). Inside a group the config [1] rows come first, then [2], then the rest - each block
           sample rate first, then in the order the driver declares them; the sensor's groups follow that order too.
           Declaration order walks the most derived class first, so a sensor's own settings come before the rows
           its base adds (the microphones' gain before the sound trigger). The DataGrid groups by first appearance. */
        public static List<DriverPropertyViewModel> Order(Type driver, IReadOnlyCollection<DriverPropertyViewModel> rows)
        {
            var rank = new Dictionary<string, int>();
            for (Type? t = driver; t != null; t = t.BaseType)
                foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                             .OrderBy(p => p.MetadataToken))
                    rank.TryAdd(p.Name, rank.Count);

            int Rank(DriverPropertyViewModel r) => rank.TryGetValue(r.Property.Name, out int i) ? i : int.MaxValue;
            var groupRank = rows.GroupBy(r => r.Group).ToDictionary(g => g.Key, g => g.Min(Rank));

            static int Section(string group) => group == StandardGroup ? 0 : group == AdvancedGroup ? 2 : 1;
            static int Slot(string name) => name.EndsWith("[1]", StringComparison.Ordinal) ? 1 : name.EndsWith("[2]", StringComparison.Ordinal) ? 2 : 3;

            return rows.OrderBy(r => Section(r.Group))
                       .ThenBy(r => groupRank[r.Group])
                       .ThenBy(r => Slot(r.Name))
                       .ThenBy(r => r.Name.StartsWith("Sample Rate", StringComparison.Ordinal) ? 0 : 1)
                       .ThenBy(Rank)
                       .ToList();
        }
    }
}
