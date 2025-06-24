using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Text;
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
using System.Text.Json;
using System.Diagnostics;
using Avalonia.Controls;
using System.IO;
using System.Reactive;
using Avalonia;
using System.Linq;
using Avalonia.Platform.Storage;
using ASDWaveLib;
using MsBox.Avalonia;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Enums;
using System.Collections;
using System.Globalization;
using Velopack;
using Microsoft.Extensions.Logging;
using MsBox.Avalonia.Base;
using VesperApp.Controls;
using Octokit;
using MsBox.Avalonia.Models;
using FluentAvalonia.UI.Controls;
using static VesperApp.Models.ConfigurationJSON;

/// <summary>
/// //// {Binding Description, StringFormat='Description: {0}'}
/// </summary>
//////
///@ECHO OFF
//SET "OLD_PATH=%PATH%"
//SET "PATH=C:\Program Files\STMicroelectronics\STM32Cube\STM32CubeProgrammer\bin;%PATH%"
//SET PROG_CLI=STM32_Programmer_CLI.exe
//SET PROG_ARGS=-c port=SWD freq=4000
//SET START_ADDR=0x80000000
//SET VERIFY=-v

//%PROG_CLI% %PROG_ARGS% -w "%1" %START_ADDR% %VERIFY%

//SET "PATH=%OLD_PATH%"
//ECHO ON
/////
//To launch command line interface, call 
//    macOS: STM32CubeProgrammer.app / Contents / MacOs / bin / STM32_Programmer_CLI 
//    Windows: ..\STMicroelectronics\STM32Cube\STM32CubeProgrammer\bin \STM32_Programmer_CLI.exe 
//    Linux: .. / STMicroelectronics / STM32Cube / STM32CubeProgrammer / bin / STM32_Programmer_CLI






namespace VesperApp.ViewModels
{
    public class MainViewViewModel : ViewModelBase
    {
        private DockAdapter _globalDockAdapter;
        private DeviceUsbAdapter _deviceUsbAdapter;

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
        private System.Timers.Timer? _timerClock;
        private bool IsClosing = false;

		#region Update Properties

		private bool _IsUpdateAvailable = false;

		public bool IsUpdateAvailable
		{
			get => _IsUpdateAvailable;
			set
			{
				this.RaiseAndSetIfChanged(ref _IsUpdateAvailable, value);
				IsFlyoutopen();
			}
		}
		private bool _isdownload = false;

		public bool isdownload
		{
			get => _isdownload;
			set
			{
				this.RaiseAndSetIfChanged(ref _isdownload, value);
				IsFlyoutopen();
			}
		}
		private bool _isdownloading = false;

		public bool isdownloading
		{
			get => _isdownloading;
			set
			{
				this.RaiseAndSetIfChanged(ref _isdownloading, value);
				IsFlyoutopen();
			}
		}

		private string _VersionUpdateMessage;
		public string VersionUpdateMessage
		{
			get => _VersionUpdateMessage;
			set => this.RaiseAndSetIfChanged(ref _VersionUpdateMessage, value);
		}
		private string _DownloadContent = "Download Now";
		public string DownloadContent
		{
			get => _DownloadContent;
			set => this.RaiseAndSetIfChanged(ref _DownloadContent, value);
		}
		public ReactiveCommand<Unit, Unit> DownloadCheckCommand { get; }
		public ReactiveCommand<Unit, Unit> LaterCommand { get; }

		private string updateBaseUrl => "https://vesperapp.asd-tech.com/";

		public bool IsdownloadButtonEnable
		{
			get => _IsdownloadButtonEnable
;
			set => this.RaiseAndSetIfChanged(ref _IsdownloadButtonEnable, value);
		}

		private bool _IsdownloadButtonEnable = true;
		private Flyout _updateFlyout;
		public Flyout updateFlyout
		{
			get => _updateFlyout;
			set => this.RaiseAndSetIfChanged(ref _updateFlyout, value);
		}


        #endregion

        private string textMessageBottom;
        public string TextMessageBottom
        {
            get => textMessageBottom;
            set => this.RaiseAndSetIfChanged(ref textMessageBottom, value);
        }

        public SelectionModel<LoggerDevice>? SelectedLoggerDeviceModel { get; }


        private bool __downloading_nanotag_data = false;

        public MainViewViewModel()
        {
            _globalDockAdapter = new DockAdapter();
            _globalDockAdapter.ConnectionEvent += _globalDockAdapter_ConnectionEvent;
            _deviceUsbAdapter = new DeviceUsbAdapter();
            _deviceUsbAdapter.ConnectionEvent += _deviceUsbAdapter_ConnectionEvent;

            SelectedLoggerDeviceModel = new SelectionModel<LoggerDevice>();
            SelectedLoggerDeviceModel.SelectionChanged += SelectedLoggerDeviceModel_SelectionChanged;

            _timer = new System.Timers.Timer();
            _timer.Elapsed += _timer_Elapsed;
            _timer.Interval = 2150;

            _timerClock = new System.Timers.Timer();
            _timerClock.Elapsed += _timerClock_Elapsed;
            _timerClock.Interval = 1000;

            Categories = new List<CategoryBase>();

            Categories.Add(new Category { Name = "Recordings", Page = typeof(RecordingsParsing), DataContext = new RecordingParsingViewModel(), Icon = Symbol.ContactInfo, ToolTip = "Import, Parse and decode recordings" });
            Categories.Add(new Category { Name = "Configuration", Page = typeof(ScheduleEditor), DataContext = new ScheduleEditorViewModel(), Icon = Symbol.TargetEdit, ToolTip = "Edit configuration file" });
            Categories.Add(new VesperApp.Models.Separator());
            Categories.Add(new Category { Name = "Software Upgrades", Page = typeof(UpdateChecker), DataContext = new UpdateCheckerViewModel(), Icon = Symbol.New, ToolTip = "Software Upgrades" });
            Categories.Add(new Category { Name = "Firmware Upgrades", Page = typeof(FirmwareUpgrades), DataContext = new FirmwareUpgradesViewModel(), Icon = Symbol.Upload, ToolTip = "Firmware Upgrades" });
            Categories.Add(new Category { Name = "Help", Icon = Symbol.Help, ToolTip = "Help Documentation" });

            SelectedCategory = Categories[0];

            //ShowDockPickDialog = new Interaction<DockPickWindowViewModel, DockDeviceInfo?>();

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
                    DockPickWindow dockPickDialog = new DockPickWindow();
                    var dockPickVM = new DockPickWindowViewModel(_globalDockAdapter);
                    dockPickDialog.DataContext = dockPickVM;

                    if (App.MainWindow != null)
                    {
                        _timer.Stop();
                        await Task.Delay(100);
                        var result = await dockPickDialog.ShowDialog<DockDeviceInfo?>(App.MainWindow);

                        if (result != null)
                        {
                            var dockdevice = await _globalDockAdapter.GetDockBySerialNumberAsync(result.Id ?? string.Empty);

                            if (dockdevice != null)
                                await _globalDockAdapter.DockConnect(dockdevice);

                            //IsConnected = _globalDockAdapter.IsConnected;
                        }
                        _timer.Start();
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
                    if (this.SelectedLoggerDevice.IsComportDevice)
                    {
                        this._timer?.Stop();
                        await Task.Delay(250);
                    }
                    try
                    {
                        await _deviceUsbAdapter.DeviceConnect(SelectedLoggerDevice);
                    }
                    catch (Exception ex)
                    { }
                    finally
                    {
                        if (this._timer?.Enabled == false)
                            this._timer.Start();
                    }

                }
            });

            DownloadDeviceData = ReactiveCommand.CreateFromTask(async () =>
            {
                if (SelectedLoggerDevice != null)
                {
                    FolderPickerOpenOptions foptions = new()
                    {
                        Title = "Selec Folder to download data into ...",
                        AllowMultiple = false,
                    };


                    Task<IReadOnlyList<IStorageFolder>> dialog = RootTopLevel!.StorageProvider!.OpenFolderPickerAsync(foptions);

                    if (dialog.Result.Count > 0)
                    {
                        await dialog.ContinueWith(async delegate (Task<IReadOnlyList<IStorageFolder>> dialogs)
                        {
                            __downloading_nanotag_data = true;

                            try
                            {
                                IReadOnlyList<IStorageFolder> folders = dialog.Result;
                                string? path = null;

                                if (folders.Count > 0)
                                {
                                    path = folders[0].TryGetLocalPath();
                                }

                                bool ok = await SelectedLoggerDevice.DownloadPages(path);

                                if (!ok)
                                {
                                    ///// show error
                                }
                            }
                            catch (Exception ex)
                            {

                            }
                            finally
                            {
                                __downloading_nanotag_data = false;
                            }
                        });
                    }
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
                // if (SelectedLoggerDevice != null)
                // {

                FilePickerOpenOptions foptions = new()
                {
                    Title = "Upload configuration file ...",
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

                Task<IReadOnlyList<IStorageFile>> dialog = RootTopLevel!.StorageProvider!.OpenFilePickerAsync(foptions);

                await dialog.ContinueWith(async delegate (Task<IReadOnlyList<IStorageFile>> dialogs)
                {

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
                                }

                                await SelectedLoggerDevice!.UploadConfigFile(jsonString);
                            }
                        }
                    }
                    catch (Exception ec)
                    {
                        Debug.WriteLine(ec.Message);
                    }

                });
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

            LoggerDevices = new ObservableCollection<LoggerDevice>();
            SelectedLoggerDevice = null;
            
			DownloadCheckCommand = ReactiveCommand.Create(DownloadCheck);
			LaterCommand = ReactiveCommand.Create(closeFlyout);

            _timer.Start();
            _timerClock.Start();

            _um = new UpdateManager(updateBaseUrl);

            TextMessageBottom = string.Empty;
		}

        /*
         * 
         *  rem win-x64-stable
            rem win-x64-beta
            rem win-arm64-stable
            rem win-arm64-beta
            rem osx-x64-stable
            rem osx-x64-beta
            rem osx-arm64-stable
            rem osx-arm64-beta
         */


        //Update Software Methods 

        #region Variables 
        private UpdateManager _um;
        private UpdateInfo? _updateInfo;

		public string updateFileName => "VesperAppSetup.msi";
		public string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        #endregion

        //Check For the Updates 

        #region Checks for the update 

        private void UpdateStatus()
        {
            Trace.TraceInformation("Update Status");
            StringBuilder sb = new StringBuilder();
            sb.Append($"Velopack: {VelopackRuntimeInfo.VelopackNugetVersion}");
            sb.Append($" This app: {(_um.IsInstalled ? _um.CurrentVersion : "(n/a - not installed)")}");
            sb.AppendLine();
            Trace.TraceInformation("Update Status: " + sb.ToString());

            if (_updateInfo != null)
            {
                sb.Append($"Update available: {_updateInfo.TargetFullRelease.Version}");
                IsdownloadButtonEnable = true;
            }
            else
            {
                IsdownloadButtonEnable = false;
            }
            Trace.TraceInformation("2) Update Status: " + sb.ToString());


            if (_um.UpdatePendingRestart != null)
            {
                sb.Append(" Update ready, pending restart to install");
                IsUpdateAvailable = true;
            }
            else
            {
                IsUpdateAvailable = false;
            }
            Trace.TraceInformation("3) Update Status: " + sb.ToString());


            TextMessageBottom += sb.ToString();
            //BtnCheckUpdate.IsEnabled = true;
        }


        private async Task<bool> CheckUpdate()
		{
            _um = new UpdateManager(updateBaseUrl, new UpdateOptions
            {
                ExplicitChannel = "win-x64-stable",
                AllowVersionDowngrade = true,
            });
            
            try
            {
                _updateInfo = await _um.CheckForUpdatesAsync();
                UpdateStatus();
            }
            catch (Exception ex)
			{
				//Debug.WriteLine(ex.Message);
                TextMessageBottom = ex.Message;
                IsUpdateAvailable = false;
			}

            return IsUpdateAvailable;
		}

		#endregion

		#region Download And Check

		#region Download File And Install File 
		public async void DownloadCheck()
		{              
            try
            {
                // ConfigureAwait(true) so that UpdateStatus() is called on the UI thread
                if (_updateInfo != null)
                {
                    await _um.DownloadUpdatesAsync(_updateInfo, Progress).ConfigureAwait(true);
                }
            }
            catch (Exception ex)
            {
                Program.Log.LogError(ex, "Error downloading updates");
            }
            UpdateStatus();
            Wc_DownloadFileCompleted();
        }
        #endregion

        #region Download File Complete 
        private void Wc_DownloadFileCompleted()
		{
			try
			{
                DownloadContent = "Install";

				IsdownloadButtonEnable = true;
				isdownload = true;
				isdownloading = false;
				IsUpdateAvailable = false;
				IsFlyoutopen();
                RestartApply();
            }
			catch (Exception ex)
			{
				//Debug.WriteLine(ex.Message);
			}
		}

        private void RestartApply()
        {
            if(_updateInfo != null)
                _um.ApplyUpdatesAndRestart(_updateInfo);
        }



        private void Progress(int percent)
        {
            // progress can be sent from other threads
            Dispatcher.UIThread.InvokeAsync(() => {
                Console.WriteLine($"Downloading ({percent}%)...");
            });
        }


        private void Working()
        {
            Program.Log.LogInformation("");
            //BtnCheckUpdate.IsEnabled = false;
            //BtnDownloadUpdate.IsEnabled = false;
            //BtnRestartApply.IsEnabled = false;
            //TextStatus.Text = "Working...";
        }

        #endregion

        #endregion

        #region Flyouts
        public void closeFlyout()
		{
			updateFlyout.Hide();
		}
		public void IsFlyoutopen()
		{
			try
			{
				updateFlyout = new Flyout() /*{ Placement = Flyout.P  FlyoutPlacementMode.Bottom }*/;
				var ParentStack = new StackPanel() { Orientation = Avalonia.Layout.Orientation.Vertical, Spacing = 3, Height = 60, Width = 200 };
				var txtDownloadavalable = new TextBlock() { Text = VersionUpdateMessage, FontWeight = Avalonia.Media.FontWeight.ExtraBold, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center };
				var childStack = new StackPanel() { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 20, Margin = new Thickness(0, 10, 0, 0), HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center };
				var downloadButton = new Button() { Content = DownloadContent, Command = DownloadCheckCommand, IsEnabled = IsdownloadButtonEnable };
				var laterButton = new Button() { Content = "Do Later", Command = LaterCommand };

				ParentStack.Children.Add(txtDownloadavalable);
				childStack.Children.Add(downloadButton);
				childStack.Children.Add(laterButton);
				ParentStack.Children.Add(childStack);

				updateFlyout.Content = ParentStack;
			}
			catch (Exception ex)
			{
				//Debug.WriteLine(ex.Message);
			}
		}
        #endregion

        //END

        //Window GetWindow() => TopLevel.GetTopLevel(this) as Window ?? throw new NullReferenceException("Invalid Owner");
        //TopLevel GetTopLevel() => TopLevel.GetTopLevel(this) ?? throw new NullReferenceException("Invalid Owner");



        private void _globalDockAdapter_ConnectionEvent(object? sender, DockConnectionEventArgs e)
        {
            if (e.IsConnected != IsConnected)
            {
                if (e.IsConnected == true && e.Dock != null)
                {                  
                }
                else
                {
                }

                IsConnected = e.IsConnected;
            }
        }

        private void _deviceUsbAdapter_ConnectionEvent(object? sender, DeviceConnectionEventArgs e)
        {
            IsDeviceConnected = e.IsConnected;
        }



        private async Task ScanFor(int vid, int pid)
        {
            if (_deviceUsbAdapter != null)
            {
                var devices = await _deviceUsbAdapter.ScanDevicesAsync((uint)vid,(uint)pid, true);
                foreach (LoggerDevice logDevice in devices)
                {
                    if (LoggerDevices.Contains(logDevice) == false)
                        LoggerDevices.Add(logDevice);
                }

                LoggerDevice[] list = new LoggerDevice[LoggerDevices.Count];
                LoggerDevices.CopyTo(list, 0);

                foreach (LoggerDevice dev in list)
                {
                    if (dev.IsComportDevice == false)
                    {
                        bool f = false;
                        foreach (LoggerDevice lDevice in devices)
                        {
                            if (lDevice.Equals(dev))
                            {
                                f = true;
                                break;
                            }
                        }

                        if (f == false && dev.IsConnected == false) LoggerDevices.Remove(dev);
                    }
                }
            }
        }
        private async Task ScanForComport()
        {
            if (_deviceUsbAdapter != null)
            {
                var devices = await _deviceUsbAdapter.ScanComPortsAsync(false);
                foreach (LoggerDevice logDevice in devices)
                {
                    if (LoggerDevices.Contains(logDevice) == false)
                    {
                        //Debug.WriteLine("Added: " + logDevice.SerialNumber);
                        LoggerDevices.Add(logDevice);
                    }
                }

                LoggerDevice[] list = new LoggerDevice[LoggerDevices.Count];
                LoggerDevices.CopyTo(list, 0);

                foreach (LoggerDevice dev in list)
                {
                    if (dev.IsComportDevice)
                    {
                        //Debug.WriteLine("Probing: " + dev.SerialNumber);
                        bool f = false;
                        foreach (LoggerDevice lDevice in devices)
                        {
                            //Debug.WriteLine(" - : " + lDevice.SerialNumber);
                            if (lDevice == dev)
                            {
                                //Debug.WriteLine("Match");
                                f = true;
                                break;
                            }
                        }

                        if (f == false && dev.IsConnected == false)
                        {
                            LoggerDevices.Remove(dev);
                            //Debug.WriteLine("No Match - Remove: " + dev.SerialNumber);
                        }
                    }
                }
            }
        }


        private uint cccupdate = 0;
        private void _timer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            if (cccupdate == 0)
            {
                cccupdate = 30;
                Task.Run(async () => IsUpdateAvailable = await CheckUpdate());
            }
            else
            {
                cccupdate--;
            }

            if (IsDeviceConnected == false && _deviceUsbAdapter != null && IsClosing == false && __downloading_nanotag_data == false)
            {
                    var scan_nano = Task.Run(async () => await ScanFor(Nanotag.VendorId, Nanotag.ProductId));
                    var scan_comport = Task.Run(async () => await ScanForComport());
            }
        }

        private void _timerClock_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            if (IsClockUTC == false)
                TextDateTimeNow = DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToLongTimeString();
            else
                TextDateTimeNow = DateTime.UtcNow.ToShortDateString() + " " + DateTime.UtcNow.ToLongTimeString();

            if (IsDeviceConnected == true && SelectedLoggerDevice != null && __downloading_nanotag_data == false)
            {
                var ping_device = Task.Run(async () =>
                {
                    if (SelectedLoggerDevice != null)
                    {
                        if(await SelectedLoggerDevice.GetInfo() == false)
                        {
                            await SelectedLoggerDevice.Disconnect();
                            SelectedLoggerDevice = null;
                        }
                    }
                });
            }
        }

        private void SelectedLoggerDeviceModel_SelectionChanged(object? sender, SelectionModelSelectionChangedEventArgs<LoggerDevice> e)
        {
            if (e.SelectedItems != null && e.SelectedItems.Count > 0)
                SelectedLoggerDevice = e.SelectedItems[0];
        }


        public Interaction<DockPickWindowViewModel, DockDeviceInfo?> ShowDockPickDialog { get; }
        public ICommand? ConnectDisconnectDockCommand { get; }
        public ICommand? ResetDeviceDockCommand { get; }
        public ICommand? Boot0ModeDockCommand { get; }
        public ICommand? EnableDeviceDockCommand { get; }
        public ICommand? ConnectDisconnectDeviceCommand { get; }
        public ICommand? SleepDeviceCommand { get; }
        public ICommand? ArmSleepDeviceCommand { get; }
        public ICommand? FormatDeviceCommand { get; }
        public ICommand? BootloaderDeviceCommand { get; }
        public ICommand? SetDateTimeDeviceCommand { get; }
        public ICommand? UploadDeviceConfig { get; }
        public ICommand? DownloadDeviceData { get; }

        /*
         * Logger Devices Section
        * */
        public ObservableCollection<LoggerDevice> LoggerDevices { get; }


        public List<CategoryBase> Categories { get; }

        public object SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedCategory, value);
                SetCurrentPage();
            }
        }
        public Control CurrentPage
        {
            get => _currentPage;
            set => this.RaiseAndSetIfChanged(ref _currentPage, value);
        }

        private void SetCurrentPage()
        {
            if (SelectedCategory is Category cat)
            {
                if (cat.Page != null)
                {
                    var pg = Activator.CreateInstance(cat.Page);
                    CurrentPage = (Control)pg;
                    CurrentPage.DataContext = cat.DataContext;
                }
            }
            else if (SelectedCategory is NavigationViewItem nvi)
            {
                //var smpPage = $"FAControlsGallery.Pages.NVSamplePages.NVSamplePageSettings";
                //var pg = Activator.CreateInstance(Type.GetType(smpPage));
                //CurrentPage = (Control)pg;
            }
        }

        private object _selectedCategory;
        private Control _currentPage = new RecordingsParsing();


        public static TopLevel? RootTopLevel { get; set; }
        private static IStorageProvider? _storageProvider;
        public static IStorageProvider? StorageProvider
        {
            get
            {
                if (_storageProvider != null)
                    return _storageProvider;

                IStorageProvider? rootTopLevelStorageProvider = RootTopLevel?.StorageProvider;
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
