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
using System.Collections.ObjectModel;
using VesperApp.Models;
using System.Diagnostics;

namespace VesperApp.ViewModels
{
    public class SelectDeviceDriverViewModel : ViewModelBase
    {
        private ConfigurationDeviceDriver? _selectedDeviceDriver;

        public SelectDeviceDriverViewModel(List<ConfigurationDeviceDriver> supporteddevicedrivers)
        {
            DeviceDriversCollection = new ObservableCollection<ConfigurationDeviceDriver>(supporteddevicedrivers);
            SelectedDriverModel = new SelectionModel<ConfigurationDeviceDriver>();
            SelectedDriverModel.SelectionChanged += SelectedDriverModel_SelectionChanged;
            _selectedDeviceDriver = null;
        }

        private void SelectedDriverModel_SelectionChanged(object? sender, SelectionModelSelectionChangedEventArgs<ConfigurationDeviceDriver> e)
        {
            if (e.SelectedIndexes != null && e.SelectedIndexes.Count > 0)
            {

                var selectedDriver = DeviceDriversCollection[e.SelectedIndexes[0]];
                this.SelectedDeviceDriver = selectedDriver;
                /*Debug.WriteLine("Selected " + selectedDriver.Name + " " + selectedDriver.GetType().FullName);

                if(selectedDriver != _selectedDeviceDriver)
                {
                    _selectedDeviceDriver = selectedDriver;
                    this.RaisePropertyChanged();
                }*/
            }
        }

        //public TreePageViewModel TreePage { get; }

        public ObservableCollection<ConfigurationDeviceDriver> DeviceDriversCollection { get; }

        public async Task<bool> UpdateDeviceDriverCollection(List<ConfigurationDeviceDriver> supporteddevicedrivers)
        {
            DeviceDriversCollection.Clear();
            foreach (ConfigurationDeviceDriver d in supporteddevicedrivers)
                DeviceDriversCollection.Add(d);

            return await Task.FromResult(true);
        }

        public SelectionModel<ConfigurationDeviceDriver>? SelectedDriverModel { get; }

        public ConfigurationDeviceDriver? SelectedDeviceDriver
        {
            get => _selectedDeviceDriver;
            private set => this.RaiseAndSetIfChanged(ref _selectedDeviceDriver, value);
        }

    }



}
