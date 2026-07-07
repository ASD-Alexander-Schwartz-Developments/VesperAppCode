using Avalonia.Controls;
using Avalonia.Controls.Selection;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using VesperApp.Models;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using static VesperApp.Models.ConfigurationJSON;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.ComponentModel;
using VesperApp.Views;

namespace VesperApp.ViewModels
{
    public class ScheduleEditorViewModel : ViewModelBase
    {
        private ConfigurationJSON configurationJSONInstance;

        public ICommand? NewConfigCommand { get; }
        public ICommand? SaveConfigCommand { get; }
        public ICommand? LoadConfigCommand { get; }

        public ConfigurationJSON Configuration { get => _config; }
        private ConfigurationJSON _config;


        private DeviceTypes? _selectedDeviceType;
        public DeviceTypes? SelectedDeviceType
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


        #region General device settings

        public string DeviceName
        {
            get => _deviceName;
            set => this.RaiseAndSetIfChanged(ref _deviceName, value);
        }
        private string _deviceName = string.Empty;

        public string MinimumHardware
        {
            get => _minimumHardware;
            set => this.RaiseAndSetIfChanged(ref _minimumHardware, value);
        }
        private string _minimumHardware = string.Empty;

        public bool IsMagnetOffEnabled
        {
            get => _isMagnetOffEnabled;
            set => this.RaiseAndSetIfChanged(ref _isMagnetOffEnabled, value);
        }
        private bool _isMagnetOffEnabled = true;

        public decimal BatteryCapacity
        {
            get => _batteryCapacity;
            set => this.RaiseAndSetIfChanged(ref _batteryCapacity, value);
        }
        private decimal _batteryCapacity;

        public decimal? ClockDrift
        {
            get => _clockDrift;
            set => this.RaiseAndSetIfChanged(ref _clockDrift, value);
        }
        private decimal? _clockDrift;

        public bool IsPowerOnRelative
        {
            get => _isPowerOnRelative;
            set
            {
                this.RaiseAndSetIfChanged(ref _isPowerOnRelative, value);
                PowerOnEditMask = value ? "00 00:00:00" : "0000-00-00 00:00:00";
            }
        }
        private bool _isPowerOnRelative = true;

        public string PowerOnText
        {
            get => _powerOnText;
            set => this.RaiseAndSetIfChanged(ref _powerOnText, value);
        }
        private string _powerOnText = string.Empty;

        public string PowerOnEditMask
        {
            get => _powerOnEditMask;
            set => this.RaiseAndSetIfChanged(ref _powerOnEditMask, value);
        }
        private string _powerOnEditMask = "00 00:00:00";

        /// <summary>Reflect the general device settings of <paramref name="config"/> in the editor.</summary>
        private async Task ShowGeneralSettings(ConfigurationJSON config)
        {
            DeviceName = config.Name;
            MinimumHardware = config.MinimumSupportedHardware;
            IsMagnetOffEnabled = config.IsMagnetOffEnabled;
            BatteryCapacity = config.BatteryCapacity;
            ClockDrift = config.CDrift;

            // Switch the mask first and let it apply before setting the text,
            // otherwise the MaskedTextBox drops the new value.
            IsPowerOnRelative = config.PowerOn.IsRelative;
            await Task.Delay(100);
            PowerOnText = config.PowerOn.PowerOn;
            await Task.Delay(100);
        }

        /// <summary>Write the general device settings shown in the editor into <paramref name="config"/>.</summary>
        private void ApplyGeneralSettings(ConfigurationJSON config)
        {
            config.Name = DeviceName;
            config.MinimumSupportedHardware = MinimumHardware;
            config.IsMagnetOffEnabled = IsMagnetOffEnabled;
            config.BatteryCapacity = (UInt32)BatteryCapacity;
            config.CDrift = ClockDrift.HasValue ? (UInt32)ClockDrift.Value : null;

            if (PowerOnText.Length > 0)
            {
                config.PowerOn.IsRelative = IsPowerOnRelative;
                config.PowerOn.PowerOn = PowerOnText;
            }
            else
            {
                config.PowerOn = new PowerOnTime();
            }
        }

        #endregion

        public ScheduleControlViewModel ScheduleViewModel { get; private set; }
        DeviceDriverPropertyGridViewModel DriverEditorGridViewModel { get; set; }
        public SelectDeviceDriverViewModel DriversViewModel { get; private set; }

        private ConfigurationDeviceDriver? _selectedDeviceDriver;

        public ScheduleEditorViewModel()
        {
            configurationJSONInstance = new ConfigurationJSON();
            _config = new ConfigurationJSON();

            DeviceName = configurationJSONInstance.Name;
            MinimumHardware = configurationJSONInstance.MinimumSupportedHardware;
            IsMagnetOffEnabled = configurationJSONInstance.IsMagnetOffEnabled;
            BatteryCapacity = configurationJSONInstance.BatteryCapacity;
            ClockDrift = configurationJSONInstance.CDrift;

            ScheduleViewModel = new ScheduleControlViewModel(configurationJSONInstance.Schedule);
            DriversViewModel = new SelectDeviceDriverViewModel(new List<ConfigurationDeviceDriver>());
            DriversViewModel.PropertyChanged += DriversViewModel_PropertyChanged;
            DriverEditorGridViewModel = new DeviceDriverPropertyGridViewModel();

            #region Configuration Commands

            NewConfigCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                var messageBoxStandardWindow = MessageBoxManager.GetMessageBoxStandard(
                new MessageBoxStandardParams
                {
                    ButtonDefinitions = MsBox.Avalonia.Enums.ButtonEnum.YesNoCancel,
                    ContentTitle = "New Configuration",
                    ContentHeader = "Do you want to open new configuration?",
                    ContentMessage = "By pressing Yes you will loose and unsaved changes.",
                    WindowIcon = App.MainWindow?.Icon,
                    Icon = MsBox.Avalonia.Enums.Icon.Question
                });

                if (await messageBoxStandardWindow.ShowWindowDialogAsync(App.MainWindow) == MsBox.Avalonia.Enums.ButtonResult.Yes)
                {
                    CreateNewConfigurationInstance();
                }
            });

            SaveConfigCommand = ReactiveCommand.CreateFromTask(SaveConfiguration);

            LoadConfigCommand = ReactiveCommand.CreateFromTask(LoadConfiguration);

            #endregion
        }


        private async void CreateNewConfigurationInstance()
        {
            SelectedDeviceType = null;
            _selectedDeviceDriver = null;

            UpdateSelectedDeviceDriverPropertiesView(_selectedDeviceDriver);
            configurationJSONInstance.Load(new ConfigurationJSON());
            await ShowGeneralSettings(configurationJSONInstance);

            await DriversViewModel.UpdateDeviceDriverCollection(new List<ConfigurationDeviceDriver>());
            ScheduleViewModel.SelectedScheduleType = ScheduleTypes.Continues;
            ScheduleViewModel.ScheduleEventsList.Clear();
        }

        private async Task<bool> SaveConfiguration()
        {
            bool ok = false;

            string json = string.Empty;
            string error = string.Empty;

            try
            {
                FilePickerSaveOptions options = new()
                {
                    Title = "Save configuration file as...",
                    SuggestedFileName = "config.json",
                    DefaultExtension = ".json",
                    FileTypeChoices = new List<FilePickerFileType>{ new("Configuration JSON file (.json)")
                    {
                        Patterns = new[]{"*.json"},
                        MimeTypes = new[]{"JSON/*"},
                        AppleUniformTypeIdentifiers = new[]{"utf8PlainText"}
                    },
                },
                    ShowOverwritePrompt = true
                };

                Task<IStorageFile?> dialog = StorageProvider!.SaveFilePickerAsync(options);
                // ReSharper disable once VariableHidesOuterVariable Intentional
                await dialog.ContinueWith(delegate (Task<IStorageFile?> dialog)
                {
                    try
                    {
                        IStorageFile? file = dialog.Result;

                        //https://github.com/AvaloniaUI/Avalonia/blob/master/samples/ControlCatalog/Pages/DialogsPage.xaml.cs
                        if (file is not null)
                        {
                            file.OpenWriteAsync().ContinueWith(delegate (Task<Stream> task)
                            {
                                var options = new JsonSerializerOptions();
                                options.WriteIndented = false;
                                options.Converters.Add(new ConfigurationJSON.ScheduleTypesEnumConverter());
                                options.Converters.Add(new ConfigurationDeviceDriver.ConfigurationDeviceDriverConverter());
                                options.Converters.Add(new VesperDateTimeConverter());
                                options.Converters.Add(new VesperPowerOnConverter());
                                options.Converters.Add(new VesperDateTimeAlarmConverter());
                                configurationJSONInstance.DeviceDrivers.Clear();
                                if (DriversViewModel != null)
                                {
                                    foreach (ConfigurationDeviceDriver d in DriversViewModel.DeviceDriversCollection)
                                        if (d.IsChecked) configurationJSONInstance.DeviceDrivers.Add(d);
                                }

                                configurationJSONInstance.Schedule.Clear();
                                if (ScheduleViewModel != null)
                                {
                                    foreach (ConfigScheduleJSONItem item in ScheduleViewModel.ScheduleEventsList)
                                        configurationJSONInstance.Schedule.Add(item);

                                    configurationJSONInstance.ScheduleType = ScheduleViewModel.SelectedScheduleType;
                                }

                                ApplyGeneralSettings(configurationJSONInstance);

                                json = JsonSerializer.Serialize<ConfigurationJSON>(configurationJSONInstance, options);

                                if (json.Length > 0)
                                {
                                    using StreamWriter reader = new(task.Result);
                                    reader.WriteLineAsync(json);
                                }
                                ok = true;
                            });
                        }
                    }
                    catch (Exception e) { error = e.Message; Debug.WriteLine("An error has occured while trying to save the output: " + e); }
                });
            }
            catch (Exception e) { error = e.Message; Debug.WriteLine("An error has occured for the output save dialog: " + e); }

            /*            if (error.Length > 0 || json.Length == 0)
                        {
                            var messageBoxStandardWindow = MessageBox.Avalonia.MessageBoxManager.GetMessageBoxStandardWindow(
                                new MessageBoxStandardParams
                                {
                                    ButtonDefinitions = MessageBox.Avalonia.Enums.ButtonEnum.Ok,
                                    ContentTitle = "Error Saving Configuration",
                                    ContentHeader = "Configuration Not saved",
                                    ContentMessage = error,
                                    Icon = MessageBox.Avalonia.Enums.Icon.Error,
                                    WindowIcon = App.MainWindow.Icon,
                                });

                            await messageBoxStandardWindow.ShowDialog(App.MainWindow);
                        }

                        */

            return await Task.FromResult(ok);
        }

        private async Task<bool> LoadConfiguration()
        {
            bool ok = false;
            FilePickerOpenOptions foptions = new()
            {
                Title = "Load configuration file ...",
                AllowMultiple = false,

                FileTypeFilter = new List<FilePickerFileType>
                {
                    new("Configuration JSON file (.json)")
                    {
                        Patterns = new[]{"*.json"},
                        MimeTypes = new[]{"JSON/*"},
                        AppleUniformTypeIdentifiers = new[]{"utf8PlainText"}
                    }
                },
            };

            Task<IReadOnlyList<IStorageFile>> dialog = App.AppTopLevel!.StorageProvider!.OpenFilePickerAsync(foptions);

            await dialog.ContinueWith(async delegate (Task<IReadOnlyList<IStorageFile>> dialogs)
            {
                string exception = string.Empty;

                try
                {
                    IReadOnlyList<IStorageFile?> files = dialog.Result;

                    if (files != null)
                    {
                        var file = files.FirstOrDefault();

                        if (file != null)
                        {
                            string jsonString = string.Empty;
                            try
                            {
                                string? lp = file.TryGetLocalPath();
                                if (lp != null)
                                {
                                    jsonString = File.ReadAllText(lp);
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine(ex.Message);
                                jsonString = string.Empty;
                                exception = ex.Message;
                            }

                            var options = new JsonSerializerOptions();
                            options.WriteIndented = false;
                            options.Converters.Add(new ScheduleTypesEnumConverter());
                            options.Converters.Add(new ConfigurationDeviceDriver.ConfigurationDeviceDriverConverter());
                            options.Converters.Add(new VesperDateTimeConverter());
                            options.Converters.Add(new VesperPowerOnConverter());
                            options.Converters.Add(new VesperDateTimeAlarmConverter());
                            ConfigurationJSON? config = null;
                            try
                            {
                                config = JsonSerializer.Deserialize<ConfigurationJSON>(jsonString, options);
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine(ex);
                                exception = ex.Message;
                            }
                            finally
                            { }

                            if (config != null)
                            {
                                string cn = config.Name.ToLower();

                                if (cn == DeviceTypes.Nanotag.ToString().ToLower())                   // it's a nanotag configuration
                                {
                                    this.SelectedDeviceType = DeviceTypes.Nanotag;
                                }
                                else if (cn == DeviceTypes.Vesper.ToString().ToLower())
                                {
                                    this.SelectedDeviceType = DeviceTypes.Vesper;
                                }
                                else if (cn == DeviceTypes.Pipistrelle.ToString().ToLower())
                                {
                                    this.SelectedDeviceType = DeviceTypes.Pipistrelle;
                                }
                                else if (cn == DeviceTypes.Kol.ToString().ToLower())
                                {
                                    this.SelectedDeviceType = DeviceTypes.Kol;
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

                                ScheduleViewModel.ScheduleEventsList.Clear();
                                ScheduleViewModel.SelectedScheduleType = configurationJSONInstance.ScheduleType;
                                if (configurationJSONInstance.Schedule.Count > 0)
                                {
                                    foreach (ConfigScheduleJSONItem jSONItem in configurationJSONInstance.Schedule)
                                    {
                                        ScheduleViewModel.ScheduleEventsList.Add(jSONItem);
                                    }
                                }

                                await ShowGeneralSettings(config);
                            }
                            else
                            {
                                Dispatcher.UIThread.Post(() =>
                                {
                                    MessageBoxStandardParams parm = new()
                                    {
                                        ContentTitle = "Load Configuration Error",
                                        Icon = Icon.Error,
                                        EnterDefaultButton = ClickEnum.Ok,
                                        ButtonDefinitions = ButtonEnum.Ok,
                                        ShowInCenter = true,
                                        SizeToContent = SizeToContent.WidthAndHeight,
                                        ContentMessage = exception
                                    };

                                    var msgbox = MessageBoxManager.GetMessageBoxStandard(parm);
                                    if (msgbox != null)
                                    {
                                        Task<ButtonResult> dialogm = msgbox.ShowAsync();
                                    }

                                }, DispatcherPriority.Background);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {

                }
            });

            ok = true;

            return ok;
        }


        private void DriversViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "SelectedDeviceDriver")
            {
                if (sender is not null)
                {
                    SelectDeviceDriverViewModel? sddvm = sender as SelectDeviceDriverViewModel;

                    ConfigurationDeviceDriver? _selected = sddvm?.SelectedDeviceDriver;

                    Debug.WriteLine("Selected " + _selected?.Name + " " + _selected?.GetType().FullName);

                    if (_selected != _selectedDeviceDriver)
                    {
                        UpdateSelectedDeviceDriverPropertiesView(_selected);

                        _selectedDeviceDriver = _selected;
                    }
                }
            }
        }

        private async void UpdateSelectedDeviceType(DeviceTypes? _seldeviceType)
        {
            if (_seldeviceType != null)
            {
                //configurationJSONInstance.Name = (string)_seldeviceType.ToString();

                switch (_seldeviceType)
                {
                    case DeviceTypes.Nanotag:
                        await DriversViewModel.UpdateDeviceDriverCollection(Nanotag.SupportedDeviceDrivers);
                        DeviceName = "Nanotag";
                        ClockDrift = null;
                        break;

                    case DeviceTypes.Vesper:
                        await DriversViewModel.UpdateDeviceDriverCollection(Vesper.SupportedDeviceDrivers);
                        DeviceName = "Vesper";
                        ClockDrift = 32999;
                        break;
                    case DeviceTypes.Pipistrelle:
                        await DriversViewModel.UpdateDeviceDriverCollection(Pipistrelle.SupportedDeviceDrivers);
                        DeviceName = "Vesper";
                        ClockDrift = 32999;
                        break;
                    case DeviceTypes.Kol:
                        await DriversViewModel.UpdateDeviceDriverCollection(Kol.SupportedDeviceDrivers);
                        DeviceName = "Kol";
                        ClockDrift = 32999;
                        break;

                    default:
                        await DriversViewModel.UpdateDeviceDriverCollection(new List<ConfigurationDeviceDriver>());
                        DeviceName = "Vesper";
                        ClockDrift = 32999;
                        break;
                }
            }
            else
            {
                await DriversViewModel.UpdateDeviceDriverCollection(new List<ConfigurationDeviceDriver>());
            }
        }

        private async void UpdateSelectedDeviceDriverPropertiesView(ConfigurationDeviceDriver? d)
        {
            await DriverEditorGridViewModel.UpdateDeviceDriverPropertyGrid(d);
        }


        private static IStorageProvider? _storageProvider;
        public static IStorageProvider? StorageProvider
        {
            get
            {
                if (_storageProvider != null)
                    return _storageProvider;

                IStorageProvider? rootTopLevelStorageProvider = App.AppTopLevel?.StorageProvider;
                if (rootTopLevelStorageProvider != null)
                {
                    _storageProvider = rootTopLevelStorageProvider;
                    return _storageProvider;
                }

                //If mainWindow is available (for example for the Desktop variant), we use it to get a storage provider.
                // If not, then we try getting the provider from the root TopLevel instance. (Web, the designer preview,...)
                //TODO doesn't work. I have ho idea how to get a TopLevel instance in a Web, preview or Android/iOS environment.
                MainWindow? mainWindow = (MainWindow?)App.MainWindow;
                _storageProvider = mainWindow != null ? mainWindow.StorageProvider : null;

                if (_storageProvider == null)
                    throw new InvalidOperationException("StorageProvider platform implementation is not available.");

                return _storageProvider;
            }
            set => _storageProvider = value;
        }

    }
}
