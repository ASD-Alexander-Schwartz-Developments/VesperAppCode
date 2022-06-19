using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Text;
using MessageBox.Avalonia;
using System.Reactive.Linq;
using System.Windows.Input;
using VesperApp.Views;
using VesperApp.Services;
using VesperApp.Models;
using System.ComponentModel;
using System.Collections.ObjectModel;
using Avalonia.Threading;
using Avalonia.Controls.Selection;
using System.Threading.Tasks;
using Avalonia.Collections;
using System.Text.Json;
using System.Diagnostics;
using Avalonia.Controls;
using System.IO;
using MessageBox.Avalonia.DTO;

/// <summary>
/// //// {Binding Description, StringFormat='Description: {0}'}
/// </summary>



namespace VesperApp.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        private DockAdapter? _globalDockAdapter;
        private DeviceUsbAdapter? _deviceUsbAdapter;
        private ConfigurationJSON configurationJSONInstance;


        public bool IsConnected
        {
            get => _isConnected;
            set => this.RaiseAndSetIfChanged(ref _isConnected, value);
        }

        private bool _isConnected = false;


        public bool IsDeviceConnected
        {
            get => _isDeviceConnected;
            set => this.RaiseAndSetIfChanged(ref _isDeviceConnected, value);
        }

        private bool _isDeviceConnected = false;


        public bool IsDeviceEnabled
        {
            get => _isDeviceEnabled;
            set => this.RaiseAndSetIfChanged(ref _isDeviceEnabled, value);
        }

        private bool _isDeviceEnabled = false;

        public bool IsDeviceBoot0Mode
        {
            get => _isDeviceBoot0Mode;
            set => this.RaiseAndSetIfChanged(ref _isDeviceBoot0Mode, value);
        }

        private bool _isDeviceBoot0Mode = false;

        public bool IsClockUTC
        {
            get => _isClockUTC;
            set => this.RaiseAndSetIfChanged(ref _isClockUTC, value);
        }

        private bool _isClockUTC = true;


        public string TextDateTimeNow
        {
            get => _textDateTimeNow;
            set => this.RaiseAndSetIfChanged(ref _textDateTimeNow, value);
        }

        private string _textDateTimeNow = (DateTime.UtcNow.ToShortDateString() + " " + DateTime.UtcNow.ToLongTimeString());


        public LoggerDevice? SelectedLoggerDevice { get; set; }


        private System.Timers.Timer? _timer;
        private bool IsClosing = false;

        public MainWindowViewModel()
        {
            _globalDockAdapter = null;
            MainWindowContext = null;
            ShowDockPickDialog = new Interaction<DockPickWindowViewModel, DockDeviceInfo?>();

            ConnectDisconnectDockCommand = null;
            EnableDeviceDockCommand = null;
            Boot0ModeDockCommand = null;
            ResetDeviceDockCommand = null;
            BootloaderDeviceCommand = null;
            NewConfigCommand = null;
            LoadConfigCommand = null;
            SaveConfigCommand = null;

            _timer = null;
            LoggerDevices = new ObservableCollection<LoggerDevice>();
            _deviceUsbAdapter = null;
            SelectedLoggerDevice = null;
            configurationJSONInstance = new ConfigurationJSON();
            ScheduleViewModel = new ScheduleControlViewModel(configurationJSONInstance.Schedule);
            DriversViewModel = new SelectDeviceDriverViewModel(new List<ConfigurationDeviceDriver>());
            DriverEditorGridViewModel = new DeviceDriverPropertyGridViewModel();
        }

        public MainWindowViewModel(Avalonia.Controls.Window mainWindowContext)
        {
            MainWindowContext = mainWindowContext;
            _globalDockAdapter = new DockAdapter();

            ShowDockPickDialog = new Interaction<DockPickWindowViewModel, DockDeviceInfo?>();

            #region Dock Commands
            ConnectDisconnectDockCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                if (_isConnected == true)
                {
                    await _globalDockAdapter.DockDisconnect();

                    //IsConnected = _globalDockAdapter.IsConnected;
                }
                else
                {
                    var dockPick = new DockPickWindowViewModel(_globalDockAdapter);

                    var result = await ShowDockPickDialog.Handle(dockPick);

                    if (result != null)
                    {
                        var dockdevice = await _globalDockAdapter.GetDockBySerialNumberAsync(result.Id ?? string.Empty);

                        if (dockdevice != null)
                            await _globalDockAdapter.DockConnect(dockdevice);

                        //IsConnected = _globalDockAdapter.IsConnected;
                    }
                }
            });

            EnableDeviceDockCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                if (await _globalDockAdapter.SetEnableDevice(!this.IsDeviceEnabled) == true)
                {
                    IsDeviceEnabled = !IsDeviceEnabled;
                }
                else
                {
                    await _globalDockAdapter.DockDisconnect();
                }
            });

            Boot0ModeDockCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                if (await _globalDockAdapter.SetBoot0Mode(!this.IsDeviceBoot0Mode) == true)
                {
                    IsDeviceBoot0Mode = !IsDeviceBoot0Mode;
                }
                else
                {
                    await _globalDockAdapter.DockDisconnect();
                }
            });

            ResetDeviceDockCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                if (await _globalDockAdapter.ResetDevice() == false)
                {
                    await _globalDockAdapter.DockDisconnect();
                }
            });


            #endregion

            #region Device Commands
            ConnectDisconnectDeviceCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                if (_isDeviceConnected == true && _deviceUsbAdapter != null && IsClosing == false)
                {
                    try
                    {
                        IsClosing = true;
                        await _deviceUsbAdapter.DeviceDisconnect();
                    }
                    catch (Exception ex)
                    { }
                    finally
                    {
                        IsClosing = false;
                    }
                }
                else if (_deviceUsbAdapter != null && _isDeviceConnected == false && SelectedLoggerDevice != null)
                {
                    try
                    {
                        await _deviceUsbAdapter.DeviceConnect(SelectedLoggerDevice);
                    }
                    catch (Exception ex)
                    { }

                }
            });

            DownloadDeviceData = ReactiveCommand.CreateFromTask(async () =>
            {
                if (SelectedLoggerDevice != null)
                {
                    OpenFolderDialog openFolderDialog = new OpenFolderDialog();
                    openFolderDialog.Title = "Select output folder for downloaded data";

                    string ? path = await openFolderDialog.ShowAsync(MainWindowContext);

                    await SelectedLoggerDevice.DownloadPages(path);
                }
            });


            BootloaderDeviceCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                if (SelectedLoggerDevice != null)
                {
                    await SelectedLoggerDevice.Bootloader();
                }
            });


            UploadDeviceConfig = ReactiveCommand.CreateFromTask(async () =>
            {
                if (SelectedLoggerDevice != null)
                {
                    OpenFileDialog openFileDialog = new OpenFileDialog();

                    openFileDialog.Title = "Choose Configuration File to Upload";
                    openFileDialog.AllowMultiple = false;
                    string [] ? files = await openFileDialog.ShowAsync(MainWindowContext);

                    if (files != null && files[0] != null)
                    {
                        try
                        {
                            string jsonString = File.ReadAllText(files[0]);
                            await SelectedLoggerDevice.UploadConfigFile(jsonString);
                        }
                        catch (Exception e) { }
                    }

                }
            });

            SetDateTimeDeviceCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                if (SelectedLoggerDevice != null)
                {
                    DateTime sdt = (IsClockUTC) ? DateTime.UtcNow : DateTime.Now;
                    await SelectedLoggerDevice.SetDateTime(sdt);
                }
            });

            SleepDeviceCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                if (SelectedLoggerDevice != null)
                {
                    await SelectedLoggerDevice.Sleep(false);
                }
            });


            ArmSleepDeviceCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                if (SelectedLoggerDevice != null)
                {
                    await SelectedLoggerDevice.Sleep(true);
                }
            });

            FormatDeviceCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                if (SelectedLoggerDevice != null)
                {
                    await SelectedLoggerDevice.FormatDisk();
                }
            });
            #endregion

            #region Configuration Commands

            NewConfigCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                var messageBoxStandardWindow = MessageBox.Avalonia.MessageBoxManager.GetMessageBoxStandardWindow(
                new MessageBoxStandardParams
                {
                    ButtonDefinitions = MessageBox.Avalonia.Enums.ButtonEnum.YesNoCancel,
                    ContentTitle = "New Configuration",
                    ContentHeader = "Do you want to open new configuration?",
                    ContentMessage = "By pressing Yes you will loose and unsaved changes.",
                    WindowIcon = MainWindowContext.Icon,
                    Icon = MessageBox.Avalonia.Enums.Icon.Question
                });

                if(await messageBoxStandardWindow.ShowDialog(MainWindowContext) == MessageBox.Avalonia.Enums.ButtonResult.Yes)
                {
                    CreateNewConfigurationInstance();
                }
            });

            SaveConfigCommand = ReactiveCommand.CreateFromTask(SaveConfiguration);

            LoadConfigCommand = ReactiveCommand.CreateFromTask(LoadConfiguration);

            #endregion


            _timer = new System.Timers.Timer();
            _timer.Elapsed += _timer_Elapsed;
            _timer.Interval = 1000;
            _timer.Start();

            _globalDockAdapter.ConnectionEvent += _globalDockAdapter_ConnectionEvent;
            _deviceUsbAdapter = null;

            LoggerDevices = new ObservableCollection<LoggerDevice>();
            SelectedLoggerDevice = null;
            SelectedLoggerDeviceModel = new SelectionModel<LoggerDevice>();
            SelectedLoggerDeviceModel.SelectionChanged += SelectedLoggerDeviceModel_SelectionChanged;
            configurationJSONInstance = new ConfigurationJSON();
            
            ScheduleViewModel = new ScheduleControlViewModel(configurationJSONInstance.Schedule);
            DriversViewModel = new SelectDeviceDriverViewModel(new List<ConfigurationDeviceDriver>());
            DriversViewModel.PropertyChanged += DriversViewModel_PropertyChanged;
            DriverEditorGridViewModel = new DeviceDriverPropertyGridViewModel();
        }



        private async void CreateNewConfigurationInstance()
        {
            SelectedDeviceType = null;
            _selectedDeviceDriver = null;

            UpdateSelectedDeviceDriverPropertiesView(_selectedDeviceDriver);
            configurationJSONInstance.Load(new ConfigurationJSON());

            await DriversViewModel.UpdateDeviceDriverCollection(new List<ConfigurationDeviceDriver>());
        }


        private async Task<bool> SaveConfiguration()
        {
            bool ok = false;

            SaveFileDialog saveFileDialog = new SaveFileDialog();

            saveFileDialog.Title = "Save Configuration...";

            string? file = await saveFileDialog.ShowAsync(MainWindowContext);
            string error = "";

            if (file != null && file.Length > 0 && configurationJSONInstance != null)
            {
                var options = new JsonSerializerOptions();
                options.WriteIndented = false;
                options.Converters.Add(new ConfigurationJSON.ScheduleTypesEnumConverter());
                options.Converters.Add(new ConfigurationDeviceDriver.ConfigurationDeviceDriverConverter());
                configurationJSONInstance.DeviceDrivers.Clear();
                if (DriversViewModel != null)
                {
                    foreach(ConfigurationDeviceDriver d in DriversViewModel.DeviceDriversCollection)
                        if(d.IsChecked) configurationJSONInstance.DeviceDrivers.Add(d);
                }

                configurationJSONInstance.Schedule.Clear();
                if (ScheduleViewModel != null)
                {
                    foreach(ConfigScheduleJSONItem item in ScheduleViewModel.ScheduleEventsList)
                        configurationJSONInstance.Schedule.Add(item);
                }

                string json = JsonSerializer.Serialize<ConfigurationJSON>(configurationJSONInstance, options);

                if (json.Length > 0)
                {
                    try
                    {
                        await File.WriteAllTextAsync(file, json);
                    }
                    catch (Exception ex)
                    {
                        error = ex.Message;
                    }
                }

                if (error.Length > 0 || json.Length == 0)
                {
                    var messageBoxStandardWindow = MessageBox.Avalonia.MessageBoxManager.GetMessageBoxStandardWindow(
                        new MessageBoxStandardParams
                        {
                            ButtonDefinitions = MessageBox.Avalonia.Enums.ButtonEnum.Ok,
                            ContentTitle = "Error Saving Configuration",
                            ContentHeader = "Configuration Not saved",
                            ContentMessage = error,
                            Icon = MessageBox.Avalonia.Enums.Icon.Error,
                            WindowIcon = MainWindowContext.Icon,
                        });

                    await messageBoxStandardWindow.ShowDialog(MainWindowContext);
                }
            }

            ok = true;

            return await Task.FromResult(ok);
        }


        private async Task<bool> LoadConfiguration()
        {
            bool ok = false;

            OpenFileDialog openFileDialog = new OpenFileDialog();

            openFileDialog.Title = "Choose Configuration File to load";
            openFileDialog.AllowMultiple = false;
            string[]? files = await openFileDialog.ShowAsync(MainWindowContext);

            if (files != null && files[0] != null)
            {
                string jsonString = "";
                try
                {
                    jsonString = File.ReadAllText(files[0]);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                    jsonString = "";
                }

                var options = new JsonSerializerOptions();
                options.WriteIndented = false;
                options.Converters.Add(new ConfigurationJSON.ScheduleTypesEnumConverter());
                options.Converters.Add(new ConfigurationDeviceDriver.ConfigurationDeviceDriverConverter());
                ConfigurationJSON? config = null;
                try
                {
                    config = JsonSerializer.Deserialize<ConfigurationJSON>(jsonString, options)!;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                }
                finally
                { }

                if (config != null)
                {
                    if (config.Name == DeviceTypes.Nanotag.ToString())                   // it's a nanotag configuration
                    {
                        this.SelectedDeviceType = DeviceTypes.Nanotag;
                    }
                    else if (config.Name == DeviceTypes.Vesper.ToString())
                    {
                        this.SelectedDeviceType = DeviceTypes.Vesper;
                    }
                    else if (config.Name == DeviceTypes.Pipistrelle.ToString())
                    {
                        this.SelectedDeviceType = DeviceTypes.Pipistrelle;
                    }
                    else
                    {
                        this.SelectedDeviceType = null;
                    }


                    if (this.SelectedDeviceType != null && DriversViewModel != null)
                    {
                        configurationJSONInstance = config;

                        //DriversViewModel.DeviceDriversCollection.Clear();
                        DriversViewModel.ActiveDeviceDriversCollection.Clear();

                        foreach (ConfigurationDeviceDriver drv in config.DeviceDrivers)
                        {
                            int index = DriversViewModel.DeviceDriversCollection.IndexOf(drv);

                            if (index >= 0 && index < DriversViewModel.DeviceDriversCollection.Count)
                            {
                                DriversViewModel.DeviceDriversCollection[index].Load(drv);
                                DriversViewModel.DeviceDriversCollection[index].IsChecked = true;
                            }
                        }
                    }
                }
            }

            ok = true;

            return await Task.FromResult(ok);
        }



        private void DriversViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if(sender != null && e.PropertyName == "SelectedDeviceDriver")
            {
                ConfigurationDeviceDriver ? _selected = (sender as SelectDeviceDriverViewModel).SelectedDeviceDriver;

                Debug.WriteLine("Selected " + _selected.Name + " " + _selected.GetType().FullName);

                if (_selected != _selectedDeviceDriver)
                {
                    UpdateSelectedDeviceDriverPropertiesView(_selected);

                    _selectedDeviceDriver = _selected;
                }
            }
        }

        private void SelectedLoggerDeviceModel_SelectionChanged(object? sender, SelectionModelSelectionChangedEventArgs<LoggerDevice> e)
        {
            if (e.SelectedItems != null && e.SelectedItems.Count > 0)
                SelectedLoggerDevice = e.SelectedItems[0];
        }

        private void _globalDockAdapter_ConnectionEvent(object? sender, DockConnectionEventArgs e)
        {
            if (e.IsConnected != IsConnected)
            {
                if (e.IsConnected == true && e.Dock != null)
                {
                    _deviceUsbAdapter = new DeviceUsbAdapter(e.Dock);
                    _deviceUsbAdapter.ConnectionEvent += _deviceUsbAdapter_ConnectionEvent;
                }
                else
                {
                    if (_deviceUsbAdapter != null)
                    {
                        try
                        {
                            _deviceUsbAdapter.ConnectionEvent -= _deviceUsbAdapter_ConnectionEvent;
                            _deviceUsbAdapter.Dispose();
                            _deviceUsbAdapter = null;
                        }
                        catch { }
                    }
                }

                IsConnected = e.IsConnected;
            }
        }

        private void _deviceUsbAdapter_ConnectionEvent(object? sender, DeviceConnectionEventArgs e)
        {
            IsDeviceConnected = e.IsConnected;
        }

        private void _timer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            if (IsClockUTC == false)
                TextDateTimeNow = DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToLongTimeString();
            else
                TextDateTimeNow = DateTime.UtcNow.ToShortDateString() + " " + DateTime.UtcNow.ToLongTimeString();

            if (IsConnected == true && IsDeviceConnected == false && _deviceUsbAdapter != null && IsClosing == false)
            {
                var scan = Task.Run(async () =>
                {
                    var devices = await _deviceUsbAdapter.ScanDevicesAsync(0x04d8, 0xfe57, true);
                    foreach (LoggerDevice logDevice in devices)
                    {
                        if (LoggerDevices.Contains(logDevice) == false)
                            LoggerDevices.Add(logDevice);
                    }

                    LoggerDevice[] list = new LoggerDevice[LoggerDevices.Count];
                    LoggerDevices.CopyTo(list, 0);

                    foreach (LoggerDevice dev in list)
                    {
                        bool f = false;
                        foreach (LoggerDevice lDevice in devices)
                        {
                            if (lDevice == dev)
                            {
                                f = true;
                                break;
                            }
                        }

                        if (f == false) LoggerDevices.Remove(dev);
                    }
                });

            }

            if(IsConnected == true && IsDeviceConnected == true)
            {
                var ping_device = Task.Run(async () =>
                {
                    if (SelectedLoggerDevice != null)
                    {
                        await SelectedLoggerDevice.GetInfo();
                    }
                });
            }
        }



        public Avalonia.Controls.Window? MainWindowContext { get; }

        public Interaction<DockPickWindowViewModel, DockDeviceInfo?> ShowDockPickDialog { get; }
        public ICommand? ConnectDisconnectDockCommand { get; }
        public ICommand? ResetDeviceDockCommand { get; }
        public ICommand? Boot0ModeDockCommand { get; }
        public ICommand? EnableDeviceDockCommand { get; }




        /*
         * Logger Devices Section
         * */
        public ObservableCollection<LoggerDevice> LoggerDevices { get; }

        public SelectionModel<LoggerDevice>? SelectedLoggerDeviceModel { get; }

        public ICommand? ConnectDisconnectDeviceCommand { get; }
        public ICommand? SleepDeviceCommand { get; }
        public ICommand? ArmSleepDeviceCommand { get; }
        public ICommand? FormatDeviceCommand { get; }
        
        public ICommand? BootloaderDeviceCommand { get; }
        public ICommand? SetDateTimeDeviceCommand { get; }
        public ICommand? UploadDeviceConfig { get; }
        public ICommand? DownloadDeviceData { get; }



        public ICommand? NewConfigCommand { get; }
        public ICommand? SaveConfigCommand { get; }
        public ICommand? LoadConfigCommand { get; }




        public ConfigurationJSON Configuration { get => _config; }
        private ConfigurationJSON _config = new ConfigurationJSON();


        private DeviceTypes ? _selectedDeviceType;
        public DeviceTypes ? SelectedDeviceType
        {
            get => _selectedDeviceType;
            set
            {
                if (_selectedDeviceType != value)
                {
                    UpdateSelectedDeviceType(value);
                }

                this.RaiseAndSetIfChanged(ref _selectedDeviceType, value);
            }
        }

        private async void UpdateSelectedDeviceType(DeviceTypes? _seldeviceType)
        {
            if(_seldeviceType == null)
            {
                await DriversViewModel.UpdateDeviceDriverCollection(new List<ConfigurationDeviceDriver>());
            }
            else
            {
                configurationJSONInstance.Name = _seldeviceType.ToString();

                switch (_seldeviceType)
                {
                    case DeviceTypes.Nanotag:
                        await DriversViewModel.UpdateDeviceDriverCollection(Nanotag.SupportedDeviceDrivers);
                        break;

                    case DeviceTypes.Vesper:
                    case DeviceTypes.Pipistrelle:
                    default:
                        await DriversViewModel.UpdateDeviceDriverCollection(new List<ConfigurationDeviceDriver>());
                        break;
                }
            }
        }

        private async void UpdateSelectedDeviceDriverPropertiesView(ConfigurationDeviceDriver ? d)
        {
            await DriverEditorGridViewModel.UpdateDeviceDriverPropertyGrid(d);
        }

        #region "Config.Scheduler"

        public ScheduleControlViewModel ScheduleViewModel { get; private set; }

        #endregion

        #region "Config.DeviceDrivers"

        DeviceDriverPropertyGridViewModel DriverEditorGridViewModel { get; set; }

        public SelectDeviceDriverViewModel DriversViewModel { get; private set; }
        
        private ConfigurationDeviceDriver? _selectedDeviceDriver;
        #endregion
    }
}
