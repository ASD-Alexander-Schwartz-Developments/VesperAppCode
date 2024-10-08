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
using Avalonia.Collections;
using System.Text.Json;
using System.Diagnostics;
using Avalonia.Controls;
using System.IO;
using System.Reactive;
using Avalonia;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Xml;
using Avalonia.Platform.Storage;
using Avalonia.Controls.Platform;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage.FileIO;
using ASDWaveLib;
using System.Net.Http;
using MsBox.Avalonia;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Enums;
using static VesperApp.Models.ConfigurationJSON;
using System.Runtime.InteropServices;
using System.Collections;
using System.Globalization;
using Avalonia.Metadata;
using Microsoft.VisualBasic;


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
        private ConfigurationJSON configurationJSONInstance;


        public bool BinaryParserIsRunning
        {
            get => binaryParserIsRunning;
            set => this.RaiseAndSetIfChanged(ref binaryParserIsRunning, value);
        }

        private bool binaryParserIsRunning = false;

        public int BinaryParserPercent
        {
            get => binaryParserPercent;
            set => this.RaiseAndSetIfChanged(ref binaryParserPercent, value);
        }

        private int binaryParserPercent = 0;



        public SelectionModel<DeviceTypes> DevicesFiltersSelection
        { 
            get; 
            set; 
        } = new SelectionModel<DeviceTypes>();


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


		public string installerFile => "config.bat";
		public string baseUrl => "http://downloads.asd-tech.com/downloads/";

		public string onlineConfig => "updateConfig.xml";

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

        private bool __downloading_nanotag_data = false;

        public MainViewViewModel()
        {
            _globalDockAdapter = new DockAdapter();
            _globalDockAdapter.ConnectionEvent += _globalDockAdapter_ConnectionEvent;
            _deviceUsbAdapter = new DeviceUsbAdapter();
            _deviceUsbAdapter.ConnectionEvent += _deviceUsbAdapter_ConnectionEvent;

            _timer = new System.Timers.Timer();
            _timer.Elapsed += _timer_Elapsed;
            _timer.Interval = 2150;

            _timerClock = new System.Timers.Timer();
            _timerClock.Elapsed += _timerClock_Elapsed;
            _timerClock.Interval = 1000;

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
                        var result = await dockPickDialog.ShowDialog<DockDeviceInfo?>(App.MainWindow);

                        if (result != null)
                        {
                            var dockdevice = await _globalDockAdapter.GetDockBySerialNumberAsync(result.Id ?? string.Empty);

                            if (dockdevice != null)
                                await _globalDockAdapter.DockConnect(dockdevice);

                            //IsConnected = _globalDockAdapter.IsConnected;
                        }
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

                if(await messageBoxStandardWindow.ShowWindowDialogAsync(App.MainWindow) == MsBox.Avalonia.Enums.ButtonResult.Yes)
                {
                    CreateNewConfigurationInstance();
                }
            });

            SaveConfigCommand = ReactiveCommand.CreateFromTask(SaveConfiguration);

            LoadConfigCommand = ReactiveCommand.CreateFromTask(LoadConfiguration);

            #endregion

            #region Parser Commands
            BinaryFilesExtractor = ReactiveCommand.CreateFromTask(RunBinaryParser);

            ManualAudioParserCommand = ReactiveCommand.CreateFromTask(DecodeAudio);

            ManualMotionParserCommand = ReactiveCommand.CreateFromTask(DecodeMotionInnertial);

            ManualAlsParserCommand = ReactiveCommand.CreateFromTask(DecodeAls);

            ManualTprhParserCommand = ReactiveCommand.CreateFromTask(DecodeTprh);

            ManualEXG48ParserCommand = ReactiveCommand.CreateFromTask(DecodeEXG48);

            ManualEXG1292ParserCommand = ReactiveCommand.CreateFromTask(DecodeEXG1292);

            ManualLeptonParserCommand = ReactiveCommand.CreateFromTask(DecodeLepton);

            ManualGPSParserCommand = ReactiveCommand.CreateFromTask(ParseNanotagSnaps);
            #endregion


            LoggerDevices = new ObservableCollection<LoggerDevice>();
            SelectedLoggerDevice = null;
            SelectedLoggerDeviceModel = new SelectionModel<LoggerDevice>();
            SelectedLoggerDeviceModel.SelectionChanged += SelectedLoggerDeviceModel_SelectionChanged;
            configurationJSONInstance = new ConfigurationJSON();
            
            ScheduleViewModel = new ScheduleControlViewModel(configurationJSONInstance.Schedule);
            DriversViewModel = new SelectDeviceDriverViewModel(new List<ConfigurationDeviceDriver>());
            DriversViewModel.PropertyChanged += DriversViewModel_PropertyChanged;
            DriverEditorGridViewModel = new DeviceDriverPropertyGridViewModel();
			DownloadCheckCommand = ReactiveCommand.Create(DownloadCheck);
			LaterCommand = ReactiveCommand.Create(closeFlyout);

            _timer.Start();
            _timerClock.Start();

            if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                osFolder = "Windows";
            }
            else if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                osFolder = "Linux";
            }
            else if(RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                osFolder = "MacOS";
            }

            IsUpdateAvailable = CheckUpdate().Result;
		}


        //Update Software Methods 

        #region Variables 

        HttpClient httpClient;
        HttpResponseMessage response;
        HttpRequestMessage request;
		public string fileName => "VesperAppSetup.msi";
		private string latestServerBuildVersion;
		private string currentIntallBuildVersion;
		public string directoryName = "Vesper";
        public string osFolder = "Windows";
		public string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        /*http://downloads.asd-tech.com/downloads/Windows/Vesper1.0.0.15/VesperAppSetup.msi*/
        #endregion

        //Check For the Updates 

        #region Checks for the update 
        private async Task<bool> CheckUpdate()
		{
			bool status = false;
			try
			{
				XmlDocument doc1 = new XmlDocument();
				doc1.Load(baseUrl + onlineConfig);
                
                if(doc1.DocumentElement == null) 
                {
                    return false;
                }

				XmlElement Fileroot = doc1.DocumentElement;

				latestServerBuildVersion = Fileroot.InnerText;
                Version? v = Assembly.GetExecutingAssembly().GetName().Version;

                if(v == null)
                {
                    currentIntallBuildVersion = string.Empty;
                }
                else
                {
                    currentIntallBuildVersion = v.ToString();
                }

				if (latestServerBuildVersion == currentIntallBuildVersion)
				{
					return false;
				}

				#region If update is avilable checks for the versioning and remote server file exists 
				else
				{
					var latest = latestServerBuildVersion.Split(".")?.Select(int.Parse)?.ToList();
					var current = currentIntallBuildVersion.Split(".")?.Select(int.Parse)?.ToList();

                    if (latest != null && current != null)
                    {
                        for (int i = 0; i < latest.Count; i++)
                        {
                            if (latest[i] > current[i])
                            {
                                status = true;
                            }
                        }
                    }
					if (status)
					{
						#region If file is already downloaded in local System then Directly commands Install

						//Checks If the file is already downloaded in local System

						if (Directory.Exists(root))
						{
							var DownloadDirectoryName = "Vesper\\" + "Vesper" + latestServerBuildVersion + "\\" + fileName;
							var vesperAppFolder = System.IO.Path.Combine(root, DownloadDirectoryName);
							if (File.Exists(vesperAppFolder))
							{
								#region Check if File ALready file SIze

								httpClient = new HttpClient();
                                response = await httpClient.GetAsync(baseUrl + osFolder + "/" + directoryName + latestServerBuildVersion + "/" + fileName);
								Int64 SeverFileSize = Convert.ToInt64(response.Content.Headers.ContentLength);
								string downloadFile = root + "/" + directoryName + "/" + directoryName + latestServerBuildVersion;
								FileInfo file = new FileInfo(System.IO.Path.Combine(downloadFile, fileName));
								var localFileSize = file.Length;
								if (localFileSize != SeverFileSize)
								{
									DownloadContent = "Download";
									IsdownloadButtonEnable = true;
									isdownload = false;
									isdownloading = false;
									IsUpdateAvailable = false;
									IsFlyoutopen();
								}
								else
								{
									DownloadContent = "Install";
									IsdownloadButtonEnable = true;
									isdownload = false;
									isdownloading = false;
									IsUpdateAvailable = false;
									IsFlyoutopen();
								}
								#endregion
							}
						}
						#endregion

						//checks For the Server file 

						return ServerFileCheck();
					}
				}
				#endregion

				return false;

			}
			catch (Exception ex)
			{
				Debug.WriteLine(ex.Message);
				return false;
			}
		}

		#region Remote Server File Check 

		//Checks Server File Present or not
		public bool ServerFileCheck()
		{
			httpClient = new HttpClient();

            return false;

			string installerFile = baseUrl + osFolder + "/" + directoryName + latestServerBuildVersion + "/VesperAppSetup.msi";
			request = new HttpRequestMessage(HttpMethod.Get, installerFile);
			try
			{
				response = httpClient.SendAsync(request).Result;
			}
			catch (WebException ex)
			{
				if (ex.Status == WebExceptionStatus.ProtocolError)
				{
					return false;
				}
			}

			var latest = latestServerBuildVersion.Split(".");
			var current = currentIntallBuildVersion.Split(".");
			for (int i = 0; i < latest.Length; i++)
			{
				if (Convert.ToInt32(latest[i]) > Convert.ToInt32(current[i]))
				{
					VersionUpdateMessage = string.Format("version {0} Available ", latestServerBuildVersion);

					return true;
				}
			}
			return false;
		}

		#endregion

		#endregion

		#region Download And Check

		#region Download File And Install File 
		public async void DownloadCheck()
		{
			try
			{
				if (Directory.Exists(root))
				{
					string Downloadfolder = directoryName + latestServerBuildVersion + "/";
					var vesperAppFolder = System.IO.Path.Combine(root, directoryName);


					// Check For App Folder

					if (!Directory.Exists(vesperAppFolder))
					{
						Directory.CreateDirectory(vesperAppFolder);
						var subDirectory = directoryName + latestServerBuildVersion;

						Directory.CreateDirectory(System.IO.Path.Combine(vesperAppFolder, subDirectory));
						if (Directory.Exists(System.IO.Path.Combine(vesperAppFolder, subDirectory)))
						{
							var latestversionDirectory = System.IO.Path.Combine(vesperAppFolder, subDirectory);
							httpClient = new HttpClient();
							IsdownloadButtonEnable = false;
							isdownload = false;
							isdownloading = true;
							IsUpdateAvailable = false;
							IsFlyoutopen();
                            byte[] fileBytes = await httpClient.GetByteArrayAsync((new Uri(baseUrl + Downloadfolder + fileName)).ToString());
                            Wc_DownloadFileCompleted(System.IO.Path.Combine(latestversionDirectory, fileName), fileBytes);
						}
					}  
					else
					{
						//Check For Sub Directory 
						var subDirectory = directoryName + latestServerBuildVersion;
						string latestversionDirectory = System.IO.Path.Combine(vesperAppFolder, subDirectory);

						if (!Directory.Exists(System.IO.Path.Combine(vesperAppFolder, subDirectory)))
						{
							Directory.CreateDirectory(System.IO.Path.Combine(vesperAppFolder, subDirectory));

                            httpClient = new HttpClient();
							IsdownloadButtonEnable = false;
							isdownload = false;
							isdownloading = true;
							IsUpdateAvailable = false;
							IsFlyoutopen();
                            byte[] fileBytes = await httpClient.GetByteArrayAsync((new Uri(baseUrl + Downloadfolder + fileName)).ToString());
                            Wc_DownloadFileCompleted(System.IO.Path.Combine(latestversionDirectory, fileName), fileBytes);
						}
						else
						{
							if (!File.Exists(System.IO.Path.Combine(latestversionDirectory, fileName)))
							{
                                httpClient = new HttpClient();
								IsdownloadButtonEnable = false;
								isdownload = false;
								isdownloading = true;
								IsUpdateAvailable = false;
								IsFlyoutopen();
                                byte[] fileBytes = await httpClient.GetByteArrayAsync((new Uri(baseUrl + Downloadfolder + fileName)).ToString());
                                Wc_DownloadFileCompleted(System.IO.Path.Combine(latestversionDirectory, fileName), fileBytes);
                            }


                            //check file size for fail download
                            if (File.Exists(System.IO.Path.Combine(latestversionDirectory, fileName)))
							{
								string installPath = System.IO.Path.Combine(latestversionDirectory, installerFile);
								if (!File.Exists(installPath))
								{
									using (StreamWriter w = new StreamWriter(installPath))
									{
										//w.WriteLine("start cmd ");
										w.WriteLine("cd " + latestversionDirectory);
										w.WriteLine(fileName);
										w.WriteLine("exit");
										w.Close();
									}
								}
								ProcessStartInfo pi = new ProcessStartInfo(installPath);
								pi.WindowStyle = ProcessWindowStyle.Hidden;
								pi.Arguments = installPath;
								pi.UseShellExecute = true;
								pi.WorkingDirectory = installPath;
								pi.FileName = installPath;
								pi.Verb = "OPEN";
								Process.Start(pi);



								Environment.Exit(0);
							}
						}
					}
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine(ex.Message);
			}
		}
		#endregion

		#region Download File Complete 
		private void Wc_DownloadFileCompleted(string outputPath, byte[] fileBytes)
		{
			try
			{
                File.WriteAllBytes(outputPath, fileBytes);

                DownloadContent = "Install";

				IsdownloadButtonEnable = true;
				isdownload = true;
				isdownloading = false;
				IsUpdateAvailable = false;
				IsFlyoutopen();
			}
			catch (Exception ex)
			{
				Debug.WriteLine(ex.Message);
			}
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
				var laterButton = new Button() { Content = "Later", Command = LaterCommand };

				ParentStack.Children.Add(txtDownloadavalable);
				childStack.Children.Add(downloadButton);
				childStack.Children.Add(laterButton);
				ParentStack.Children.Add(childStack);

				updateFlyout.Content = ParentStack;
			}
			catch (Exception ex)
			{
				Debug.WriteLine(ex.Message);
			}
		}
        #endregion

        //END

        //Window GetWindow() => TopLevel.GetTopLevel(this) as Window ?? throw new NullReferenceException("Invalid Owner");
        //TopLevel GetTopLevel() => TopLevel.GetTopLevel(this) ?? throw new NullReferenceException("Invalid Owner");

        private async void CreateNewConfigurationInstance()
        {
            SelectedDeviceType = null;
            _selectedDeviceDriver = null;

            UpdateSelectedDeviceDriverPropertiesView(_selectedDeviceDriver);
            configurationJSONInstance.Load(new ConfigurationJSON());

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

                                    if (ScheduleViewModel.PowerOnText.Length > 0)
                                    {
                                        configurationJSONInstance.PowerOn.IsRelative = ScheduleViewModel.IsPowerOnRelative;
                                        configurationJSONInstance.PowerOn.PowerOn = ScheduleViewModel.PowerOnText;
                                    }
                                    else
                                    {
                                        configurationJSONInstance.PowerOn = new PowerOnTime();
                                    }
                                }
                                
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
                                config = JsonSerializer.Deserialize <ConfigurationJSON>(jsonString, options);
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine(ex);
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
                                if(configurationJSONInstance.Schedule.Count > 0)
                                {
                                    foreach(ConfigScheduleJSONItem jSONItem in configurationJSONInstance.Schedule)
                                    {
                                        ScheduleViewModel.ScheduleEventsList.Add(jSONItem);
                                    }
                                }

                                ScheduleViewModel.IsPowerOnRelative = configurationJSONInstance.PowerOn.IsRelative;
                                await Task.Delay(100);
                                ScheduleViewModel.PowerOnText = configurationJSONInstance.PowerOn.PowerOn;
                                await Task.Delay(100);
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


        private async Task<bool> ParseNanotagSnaps()
        {
            bool result = false;

            if (OperatingSystem.IsWindows())
            {
                FolderPickerOpenOptions options = new()
                {
                    Title = "Select FOLDER containing GPS snap .dat files to decode",
                    AllowMultiple = false,
                };

                Task<IReadOnlyList<IStorageFolder>> dialog = RootTopLevel!.StorageProvider!.OpenFolderPickerAsync(options);

                await dialog.ContinueWith(async delegate (Task<IReadOnlyList<IStorageFolder>> dialogs)
                {
                    try
                    {
                        IReadOnlyList<IStorageFolder> folders = dialog.Result;
                        string? path = null;

                        if (folders.Count > 0)
                        {
                            path = folders[0].TryGetLocalPath();
                        }

                        if (path != null && path.Length > 0)
                        {
                            System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo(Directory.GetCurrentDirectory() + @"\CG\GeoTag\GeoTag.exe");
                            psi.Arguments = "-t --download=\"" + path + "\" --decode=\"" + path + "\\decode\" --geotagengine=\"" + Directory.GetCurrentDirectory() + "\\CG\\GeoTagEngine\\GeoTagEngine.exe\" --pattern=snap.*.dat";

                            //psi.RedirectStandardOutput = true;
                            //psi.RedirectStandardError = true;
                            //psi.RedirectStandardInput = true;
                            psi.WindowStyle = System.Diagnostics.ProcessWindowStyle.Minimized;
                            psi.UseShellExecute = true;
                            psi.CreateNoWindow = false;
                            System.Diagnostics.Process? ischk = null;
                            System.IO.StreamReader ischkout;
                            System.IO.StreamReader ischkerr;

                            try
                            {
                                ischk = System.Diagnostics.Process.Start(psi);
                            }
                            catch (Exception excp)
                            {
                                ischk = null;
                            }

                            if (ischk != null)
                            {

                                string? error, msg = "";
                                int index1, index2, current;
                                double percent = 0;

                                //                        this.Log("Starting " + bar.Name, 1, true);

                                await Task.Delay(1000);
                                //ischkout = ischk.StandardOutput;
                                //ischkerr = ischk.StandardError;

                                bool done = false;

                                await Task.Run(async () =>
                                {
                                    while (ischk.HasExited == false && done == false)
                                    {
                                        await Task.Delay(200);

                                        /*                              await Task.Delay(1000);
                                                                        error = ischkerr.ReadLine();

                                                                        if (error != null)
                                                                        {
                                                                            if (error.Contains("Set: decode 00%")) // isolate the number of decoded files to calculate progress
                                                                            {

                                                                                index1 = error.IndexOf('(');
                                                                                index2 = error.IndexOf('/') - 1;

                                                                                if (index1 > 0)
                                                                                {
                                                                                    msg = error.Substring(index1 + 1, index2 - index1);
                                                                                    current = Int32.Parse(msg);
                                        //                                            percent = Math.Ceiling((current / total) * 100);

                                        //                                            if (Convert.ToInt32(percent) <= 99)
                                        //                                                bar.Percent = Convert.ToInt32(percent);
                                                                                }
                                                                            }
                                                                            else if (error.Contains("7Z.exe"))
                                                                            {
                                                                                done = true;
                                                                            }
                                                                            else if (error.Contains("Graceful termination complete"))
                                                                            {
                                                                                done = true;
                                                                            }
                                                                        }*/
                                    }
                                });
                            }
                            else
                            {
                                var messageBoxStandardWindow = MsBox.Avalonia.MessageBoxManager.GetMessageBoxStandard(
                                    new MessageBoxStandardParams
                                    {
                                        ButtonDefinitions = MsBox.Avalonia.Enums.ButtonEnum.Ok,
                                        ContentTitle = "GPS Snap Parser",
                                        ContentHeader = "Could not start parser",
                                        ContentMessage = "SNAP Parser executable not found or access denied",
                                        Icon = MsBox.Avalonia.Enums.Icon.Warning,
                                        WindowIcon = App.MainWindow?.Icon,
                                    });

                                await messageBoxStandardWindow.ShowWindowDialogAsync(App.MainWindow);

                            }

                            //bar.Percent = 100;
                            //this.Log(bar.Name + " Done!", 1, true);
                        }
                    }
                    catch { }
                });
            }
            else
            {
                var messageBoxStandardWindow = MessageBoxManager.GetMessageBoxStandard(
                    new MessageBoxStandardParams
                    {
                        ButtonDefinitions = MsBox.Avalonia.Enums.ButtonEnum.Ok,
                        ContentTitle = "GPS Snap Parser",
                        ContentHeader = "Could not start parser",
                        ContentMessage = "SNAP Parser is currently available only on MS Windows OS",
                        Icon = MsBox.Avalonia.Enums.Icon.Warning,
                        WindowIcon = App.MainWindow?.Icon,
                    });

                await messageBoxStandardWindow.ShowWindowDialogAsync(App.MainWindow);
            }

            return await Task.FromResult(result);
        }


        private async Task<bool> DecodeLepton()
        {
            bool retval = false;

            try
            {
                FilePickerOpenOptions options = new()
                {
                    Title = "Select parsed thermal camera snapshot files to convert to PNG",
                    //SuggestedStartLocation =,
                    FileTypeFilter = new List<FilePickerFileType>
                    {
                        new("Snapshot binary (.CBN) ")
                        {
                            Patterns = new[]{"*-*.CBN"},
                            MimeTypes = new[]{"bin/*"}
                        }
                    },
                    AllowMultiple = true,
                };

                Task<IReadOnlyList<IStorageFile>> dialog = RootTopLevel!.StorageProvider!.OpenFilePickerAsync(options);
                // ReSharper disable once VariableHidesOuterVariable Intentional
                await dialog.ContinueWith(async delegate (Task<IReadOnlyList<IStorageFile>> dialogs)
                {
                    try
                    {
                        IReadOnlyList<IStorageFile?> files = dialog.Result;
                        BinaryParserIsRunning = true;
                        BinaryParserPercent = 0;
                        double percentDelta = 100 / files.Count;
                        double percent = 0;
                        foreach (var file in files)
                        {
                            percent += percentDelta;
                            BinaryParserPercent = (int)percent;
                            //https://github.com/AvaloniaUI/Avalonia/blob/master/samples/ControlCatalog/Pages/DialogsPage.xaml.cs
                            if (file is not null)
                            {
                                string? lp = file.TryGetLocalPath();

                                if (lp is not null)
                                {
                                    string? currentDirectory = Path.GetDirectoryName(lp);
                                    string? currentFilename = Path.GetFileName(lp).ToUpper();
                                    string metadata = string.Empty;

                                    if (currentDirectory != null && currentFilename != null)
                                    {
                                        if (File.Exists(lp + ".txt"))                         /// Check if metadata exists
                                        {
                                            metadata = File.ReadAllText(lp + ".txt", Encoding.UTF8) ?? string.Empty;
                                        }

                                        byte[] databuf = File.ReadAllBytes(lp);

                                        LeptonReading lr = new LeptonReading(lp, databuf, 1024-16, DateTime.Now, 0, 0, LeptonFilterType.LEPTON_RAINBOW);
                                        lr.SaveAs(OutputFileType.PIC_JPG, lp);
                                    }
                                }
                            }
                        }
                        BinaryParserPercent = 100;
                        await Task.Delay(250);
                        BinaryParserIsRunning = false;

                    }
                    catch (Exception e) { Debug.WriteLine("An error has occured while trying to save the output: " + e); }
                });



            }
            catch { retval = true; }

            return retval;
        }



        private async Task<bool> RunBinaryParser()
        {
            bool retval = false;

            try
            {
                FilePickerOpenOptions options = new()
                {
                    Title = "Select binary files to extract data from...",
                    //SuggestedStartLocation =,
                    FileTypeFilter = new List<FilePickerFileType> 
                    {
                        new("All binary files (.bin) ")
                        {
                            Patterns = new[]{"*.bin"},
                            MimeTypes = new[]{"bin/*"}
                        },
                        new("GPS Snap (.bin) ")
                        {
                            Patterns = new[]{"*G.bin"},
                            MimeTypes = new[]{"bin/*"}
                        },
                        new("Audio Recording (.bin) ")
                        {
                            Patterns = new[]{"*U.bin"},
                            MimeTypes = new[]{"bin/*"}
                        },
                        new("Motion (Innertial) Recording (.bin) ")
                        {
                            Patterns = new[]{"*M.bin"},
                            MimeTypes = new[]{"bin/*"}
                        },
                        new("Ambient Light Level (Lux) Recording (.bin) ")
                        {
                            Patterns = new[]{"*L.bin"},
                            MimeTypes = new[]{"bin/*"}
                        },
                        new("Temperature and Relative Humidity Recording (.bin) ")
                        {
                            Patterns = new[]{"*R.bin"},
                            MimeTypes = new[]{"bin/*"}
                        },
                        new("Biopotentials (EEG/EMG/ECG) Recording (.bin) ")
                        {
                            Patterns = new[]{"*E.bin"},
                            MimeTypes = new[]{"bin/*"}
                        },
                        new("Aux Analog sensor Recording (.bin) ")
                        {
                            Patterns = new[]{"*S.bin"},
                            MimeTypes = new[]{"bin/*"}
                        },
                        new("Proximity Recording (.bin) ")
                        {
                            Patterns = new[]{"*X.bin"},
                            MimeTypes = new[]{"bin/*"}
                        },
                        new("Thermal Camera (Lepton) (.bin) ")
                        {
                            Patterns = new[]{"*C.bin"},
                            MimeTypes = new[]{"bin/*"}
                        },
                        new("Self Log Recording (.bin) ")
                        {
                            Patterns = new[]{"*O.bin"},
                            MimeTypes = new[]{"bin/*"}
                        },
                    },
                    AllowMultiple = true,
                };

                Task<IReadOnlyList<IStorageFile>> dialog = RootTopLevel!.StorageProvider!.OpenFilePickerAsync(options);
                // ReSharper disable once VariableHidesOuterVariable Intentional
                await dialog.ContinueWith(async delegate (Task<IReadOnlyList<IStorageFile>> dialogs)
                {
                    try
                    {
                        IReadOnlyList<IStorageFile?> files = dialog.Result;
                        BinaryParserIsRunning = true;
                        BinaryParserPercent = 0;
                        double percentDelta = 100 / files.Count;
                        double percent = 0;
                        foreach (var file in files)
                        {
                            percent += percentDelta;
                            BinaryParserPercent = (int)percent;
                            //https://github.com/AvaloniaUI/Avalonia/blob/master/samples/ControlCatalog/Pages/DialogsPage.xaml.cs
                            if (file is not null)
                            {
                                string? lp = file.TryGetLocalPath();

                                if (lp is not null)
                                {
                                    string? currentDirectory = Path.GetDirectoryName(lp);
                                    string? currentFilename = Path.GetFileName(lp).ToUpper();

                                    if (currentDirectory != null && currentFilename != null)
                                    {
                                        if (currentFilename.Contains("G.BIN"))
                                        {
                                            string fullPathOnly = Path.GetFullPath(currentDirectory);
                                            fullPathOnly += Path.DirectorySeparatorChar + "DAT";
                                            if (Directory.Exists(fullPathOnly) == false)
                                            {
                                                Directory.CreateDirectory(fullPathOnly);
                                            }

                                            await BinaryParser.ExtractVesperSnap(lp, fullPathOnly, new TimeSpan(0, 0, 0));
                                        }
                                        else if(currentFilename.Contains("U.BIN"))
                                        {
                                            string fullPathOnly = Path.GetFullPath(currentDirectory);
                                            fullPathOnly += Path.DirectorySeparatorChar + "AUD";
                                            if (Directory.Exists(fullPathOnly) == false)
                                            {
                                                Directory.CreateDirectory(fullPathOnly);
                                            }

                                            await BinaryParser.StripSplit(lp, fullPathOnly, 0);
                                        }
                                        else if (currentFilename.Contains("M.BIN"))
                                        {
                                            string fullPathOnly = Path.GetFullPath(currentDirectory);
                                            fullPathOnly += Path.DirectorySeparatorChar + "IMU";
                                            if (Directory.Exists(fullPathOnly) == false)
                                            {
                                                Directory.CreateDirectory(fullPathOnly);
                                            }

                                            await BinaryParser.StripSplit(lp, fullPathOnly, 0);
                                        }
                                        else if (currentFilename.Contains("E.BIN"))
                                        {
                                            string fullPathOnly = Path.GetFullPath(currentDirectory);
                                            fullPathOnly += Path.DirectorySeparatorChar + "EXG";
                                            if (Directory.Exists(fullPathOnly) == false)
                                            {
                                                Directory.CreateDirectory(fullPathOnly);
                                            }

                                            await BinaryParser.StripSplit(lp, fullPathOnly, 0);
                                        }
                                        else if (currentFilename.Contains("R.BIN"))
                                        {
                                            string fullPathOnly = Path.GetFullPath(currentDirectory);
                                            fullPathOnly += Path.DirectorySeparatorChar + "TPRH";
                                            if (Directory.Exists(fullPathOnly) == false)
                                            {
                                                Directory.CreateDirectory(fullPathOnly);
                                            }

                                            await BinaryParser.StripSplit(lp, fullPathOnly, 0);
                                        }
                                        else if (currentFilename.Contains("L.BIN"))
                                        {
                                            string fullPathOnly = Path.GetFullPath(currentDirectory);
                                            fullPathOnly += Path.DirectorySeparatorChar + "ALS";
                                            if (Directory.Exists(fullPathOnly) == false)
                                            {
                                                Directory.CreateDirectory(fullPathOnly);
                                            }

                                            await BinaryParser.StripSplit(lp, fullPathOnly, 0);
                                        }
                                        else if (currentFilename.Contains("X.BIN"))
                                        {
                                            string fullPathOnly = Path.GetFullPath(currentDirectory);
                                            fullPathOnly += Path.DirectorySeparatorChar + "PRX";
                                            if (Directory.Exists(fullPathOnly) == false)
                                            {
                                                Directory.CreateDirectory(fullPathOnly);
                                            }

                                            await BinaryParser.StripSplit(lp, fullPathOnly, 0);
                                        }
                                        else if (currentFilename.Contains("O.BIN"))
                                        {
                                            string fullPathOnly = Path.GetFullPath(currentDirectory);
                                            fullPathOnly += Path.DirectorySeparatorChar + "LOG";
                                            if (Directory.Exists(fullPathOnly) == false)
                                            {
                                                Directory.CreateDirectory(fullPathOnly);
                                            }

                                            await BinaryParser.StripSplit(lp, fullPathOnly, 0);
                                        }
                                        else if (currentFilename.Contains("S.BIN"))
                                        {
                                            string fullPathOnly = Path.GetFullPath(currentDirectory);
                                            fullPathOnly += Path.DirectorySeparatorChar + "SNS";
                                            if (Directory.Exists(fullPathOnly) == false)
                                            {
                                                Directory.CreateDirectory(fullPathOnly);
                                            }

                                            await BinaryParser.StripSplit(lp, fullPathOnly, 0);
                                        }
                                        else if (currentFilename.Contains("C.BIN"))
                                        {
                                            string fullPathOnly = Path.GetFullPath(currentDirectory);
                                            fullPathOnly += Path.DirectorySeparatorChar + "THCAM";
                                            if (Directory.Exists(fullPathOnly) == false)
                                            {
                                                Directory.CreateDirectory(fullPathOnly);
                                            }

                                            await BinaryParser.StripSplit(lp, fullPathOnly, 0);
                                        }
                                    }
                                }
                            }
                        }
                        BinaryParserPercent = 100;
                        await Task.Delay(250);
                        BinaryParserIsRunning = false;

                    }
                    catch (Exception e) { Debug.WriteLine("An error has occured while trying to save the output: " + e); }
                });



            }
            catch { retval = true; }

            return retval;
        }

        private async Task<bool> DecodeAudio()
        {
            bool retval = false;

            try
            {
                FilePickerOpenOptions options = new()
                {
                    Title = "Select parsed audio binary recording files to convert to WAV...",
                    //SuggestedStartLocation =,
                    FileTypeFilter = new List<FilePickerFileType>
                    {
                        new("Audio binary (.UBN) ")
                        {
                            Patterns = new[]{"*-*.UBN"},
                            MimeTypes = new[]{"bin/*"}
                        }
                    },
                    AllowMultiple = true,
                };

                Task<IReadOnlyList<IStorageFile>> dialog = RootTopLevel!.StorageProvider!.OpenFilePickerAsync(options);
                // ReSharper disable once VariableHidesOuterVariable Intentional
                await dialog.ContinueWith(async delegate (Task<IReadOnlyList<IStorageFile>> dialogs)
                {
                    try
                    {
                        IReadOnlyList<IStorageFile?> files = dialog.Result;
                        BinaryParserIsRunning = true;
                        BinaryParserPercent = 0;
                        double percentDelta = 100 / files.Count;
                        double percent = 0;
                        foreach (var file in files)
                        {
                            percent += percentDelta;
                            BinaryParserPercent = (int)percent;
                            //https://github.com/AvaloniaUI/Avalonia/blob/master/samples/ControlCatalog/Pages/DialogsPage.xaml.cs
                            if (file is not null)
                            {
                                string? lp = file.TryGetLocalPath();

                                if (lp is not null)
                                {
                                    string? currentDirectory = Path.GetDirectoryName(lp);
                                    string? currentFilename = Path.GetFileName(lp).ToUpper();
                                    string metadata = string.Empty;

                                    if (currentDirectory != null && currentFilename != null)
                                    {
                                        if(File.Exists(lp + ".txt"))                         /// Check if metadata exists
                                        {
                                            metadata = File.ReadAllText(lp + ".txt", Encoding.UTF8) ?? string.Empty;
                                        }

                                        using (WaveFile wf = new WaveFile(lp, metadata))
                                        {
                                            byte[] databuf = File.ReadAllBytes(lp);

                                            wf.Open();
                                            wf.WriteWave(databuf);
                                        }
                                    }
                                }
                            }
                        }
                        BinaryParserPercent = 100;
                        await Task.Delay(250);
                        BinaryParserIsRunning = false;

                    }
                    catch (Exception e) { Debug.WriteLine("An error has occured while trying to save the output: " + e); }
                });



            }
            catch { retval = true; }

            return retval;
        }

        private async Task<bool> DecodeMotionInnertial()
        {
            bool retval = false;

            try
            {
                FilePickerOpenOptions options = new()
                {
                    Title = "Select parsed IMU10/NanoACC binary recording files to convert to CSV...",

                    FileTypeFilter = new List<FilePickerFileType>
                    {
                        new("Inertial Motion binary files (.MBN) ")
                        {
                            Patterns = new[]{"*-*.MBN"},
                            MimeTypes = new[]{"bin/*"}
                        },
                        new("Nanotag Accelerometer binary files (.ABN) ")
                        {
                            Patterns = new[]{"*.ABN"},
                            MimeTypes = new[]{"bin/*"}
                        }

                    },
                    AllowMultiple = true,
                };

                Task<IReadOnlyList<IStorageFile>> dialog = RootTopLevel!.StorageProvider!.OpenFilePickerAsync(options);
                // ReSharper disable once VariableHidesOuterVariable Intentional
                await dialog.ContinueWith(async delegate (Task<IReadOnlyList<IStorageFile>> dialogs)
                {
                    try
                    {
                        IReadOnlyList<IStorageFile?> files = dialog.Result;
                        double percentDelta = 100 / files.Count;
                        double percent = 0;
                        foreach (var file in files)
                        {
                            percent += percentDelta;
                            //BinaryParserPercent = (int)percent;
                            //https://github.com/AvaloniaUI/Avalonia/blob/master/samples/ControlCatalog/Pages/DialogsPage.xaml.cs
                            if (file is not null)
                            {
                                if (file.Name.ToUpper().Contains(".MBN"))
                                {
                                    string? lp = file.TryGetLocalPath();

                                    if (lp is not null)
                                    {
                                        string? currentDirectory = Path.GetDirectoryName(lp);
                                        string? currentFilename = Path.GetFileName(lp).ToUpper();
                                        string? metadata = currentFilename + ".txt";
                                        uint ms_sample = 0;

                                        if (currentDirectory != null && currentFilename != null && metadata != null)
                                        {
                                            metadata = currentDirectory + "/" + metadata;
                                            if (File.Exists(metadata))
                                            {
                                                string header_metadata = File.ReadAllText(metadata);

                                                if (header_metadata.Contains("SampleRate:"))
                                                {
                                                    string[] lines = header_metadata.Split(new char[] { '\n', '\r' });

                                                    foreach (string line in lines)
                                                    {
                                                        string l = line.Trim();

                                                        if (l.Contains("SampleRate:"))
                                                        {
                                                            string val = l.Substring(l.IndexOf(":") + 1);

                                                            if (val.Length > 0)
                                                            {
                                                                uint vv = 0;
                                                                if (uint.TryParse(val, out vv))
                                                                {
                                                                    ms_sample = 1000 / vv;
                                                                    break;
                                                                }
                                                            }
                                                        }
                                                    }
                                                }
                                            }

                                            byte[] data = File.ReadAllBytes(lp);

                                            DateTime dtStart = DateTime.Now;

                                            ArrayList arrayList = Utils.scan(currentFilename, "M%d_%d_%d_%d_%d_%d_%d");

                                            if (arrayList.Count == 7)
                                            {
                                                int? year = (int?)arrayList[0];
                                                int? month = (int?)arrayList[1];
                                                int? day = (int?)arrayList[2];
                                                int? hr = (int?)arrayList[3];
                                                int? mn = (int?)arrayList[4];
                                                int? sec = (int?)arrayList[5];
                                                int? sbs = (int?)arrayList[6];

                                                if (year != null && month != null && day != null &&
                                                        hr != null && mn != null && sec != null && sbs != null)
                                                {

                                                    dtStart = new DateTime((int)year, (int)month, (int)day, (int)hr,
                                                        (int)mn, (int)sec, (int)sbs);
                                                }
                                            }

                                            using (IMU10Parser ip = new IMU10Parser(lp, data, dtStart, 1023, ms_sample))
                                            {
                                                ip.WriteFile();
                                            }
                                        }
                                    }
                                }
                                else if (file.Name.ToUpper().Contains(".ABN"))
                                {
                                    string? lp = file.TryGetLocalPath();

                                    if (lp is not null)
                                    {
                                        byte[] data = File.ReadAllBytes(lp);

                                        DateTime dtStart = DateTime.Now;
                                        string? currentFilename = Path.GetFileName(lp).ToUpper();
                                        uint ms_sample = 0;
                                        ArrayList arrayList = Utils.scan(currentFilename, "NACC%d_%d_%d_%d_%d_%d_%d");

                                        if (arrayList.Count == 7)
                                        {
                                            int? year = (int?)arrayList[0];
                                            int? month = (int?)arrayList[1];
                                            int? day = (int?)arrayList[2];
                                            int? hr = (int?)arrayList[3];
                                            int? mn = (int?)arrayList[4];
                                            int? sec = (int?)arrayList[5];
                                            int? sbs = (int?)arrayList[6];

                                            if (year != null && month != null && day != null &&
                                                    hr != null && mn != null && sec != null && sbs != null)
                                            {

                                                dtStart = new DateTime((int)year, (int)month, (int)day, (int)hr,
                                                    (int)mn, (int)sec, (int)sbs);
                                            }
                                        }

                                        using (NanoAccParser ip = new NanoAccParser(lp, data, dtStart, 1023, ms_sample))
                                        {
                                            ip.WriteFile();
                                        }
                                    }
                                }
                            }
                        }
                        //BinaryParserPercent = 100;
                        await Task.Delay(100);
                        //BinaryParserIsRunning = false;

                    }
                    catch (Exception e) { Debug.WriteLine("An error has occured while trying to save the output: " + e); }
                });



            }
            catch { retval = true; }

            return retval;
        }

        private async Task<bool> DecodeAls()
        {
            bool retval = false;

            try
            {
                FilePickerOpenOptions options = new()
                {
                    Title = "Select parsed TPRH31 binary recording files to convert to CSV...",

                    FileTypeFilter = new List<FilePickerFileType>
                    {
                        new("Ambient Light (Lux) recording files (.LBN) ")
                        {
                            Patterns = new[]{"*-*.LBN"},
                            MimeTypes = new[]{"bin/*"}
                        }
                    },
                    AllowMultiple = true,
                };

                Task<IReadOnlyList<IStorageFile>> dialog = RootTopLevel!.StorageProvider!.OpenFilePickerAsync(options);
                // ReSharper disable once VariableHidesOuterVariable Intentional
                await dialog.ContinueWith(async delegate (Task<IReadOnlyList<IStorageFile>> dialogs)
                {
                    try
                    {
                        IReadOnlyList<IStorageFile?> files = dialog.Result;
                        double percentDelta = 100 / files.Count;
                        double percent = 0;
                        foreach (var file in files)
                        {
                            percent += percentDelta;
                            //BinaryParserPercent = (int)percent;
                            if (file is not null)
                            {
                                string? lp = file.TryGetLocalPath();

                                if (lp is not null)
                                {
                                    string? currentDirectory = Path.GetDirectoryName(lp);
                                    string? currentFilename = Path.GetFileName(lp).ToUpper();
                                    string? metadata = currentFilename + ".txt";
                                    uint ms_sample = 0;

                                    if (currentDirectory != null && currentFilename != null && metadata != null)
                                    {
                                        metadata = currentDirectory + "/" + metadata;
                                        if (File.Exists(metadata))
                                        {
                                            string header_metadata = File.ReadAllText(metadata);

                                            if (header_metadata.Contains("SampleRate:"))
                                            {
                                                string[] lines = header_metadata.Split(new char[] { '\n', '\r' });

                                                foreach (string line in lines)
                                                {
                                                    string l = line.Trim();

                                                    if (l.Contains("SampleRate:"))
                                                    {
                                                        string val = l.Substring(l.IndexOf(":") + 1);

                                                        if (val.Length > 0)
                                                        {
                                                            uint vv = 0;
                                                            if (uint.TryParse(val, out vv))
                                                            {
                                                                ms_sample = vv;
                                                                break;
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }

                                        byte[] data = File.ReadAllBytes(lp);

                                        DateTime dtStart = DateTime.Now;

                                        ArrayList arrayList = Utils.scan(currentFilename, "L%d_%d_%d_%d_%d_%d_%d");

                                        if (arrayList.Count == 7)
                                        {
                                            int? year = (int?)arrayList[0];
                                            int? month = (int?)arrayList[1];
                                            int? day = (int?)arrayList[2];
                                            int? hr = (int?)arrayList[3];
                                            int? mn = (int?)arrayList[4];
                                            int? sec = (int?)arrayList[5];
                                            int? sbs = (int?)arrayList[6];

                                            if (year != null && month != null && day != null &&
                                                    hr != null && mn != null && sec != null && sbs != null)
                                            {

                                                dtStart = new DateTime((int)year, (int)month, (int)day, (int)hr,
                                                    (int)mn, (int)sec, (int)sbs);
                                            }
                                        }

                                        using (ALSParser ip = new ALSParser(lp, data, dtStart, 1023, ms_sample))
                                        {
                                            ip.WriteFile();
                                        }
                                    }
                                }
                            }
                        }
                        //BinaryParserPercent = 100;
                        await Task.Delay(100);
                        //BinaryParserIsRunning = false;

                    }
                    catch (Exception e) { Debug.WriteLine("An error has occured while trying to save the output: " + e); }
                });



            }
            catch { retval = true; }

            return retval;
        }

        private async Task<bool> DecodeTprh()
        {
            bool retval = false;

            try
            {
                FilePickerOpenOptions options = new()
                {
                    Title = "Select parsed TPRH31 binary recording files to convert to CSV...",

                    FileTypeFilter = new List<FilePickerFileType>
                    {
                        new("Temperature/Humidity binary files (.RBN) ")
                        {
                            Patterns = new[]{"*-*.RBN"},
                            MimeTypes = new[]{"bin/*"}
                        }
                    },
                    AllowMultiple = true,
                };

                Task<IReadOnlyList<IStorageFile>> dialog = RootTopLevel!.StorageProvider!.OpenFilePickerAsync(options);
                // ReSharper disable once VariableHidesOuterVariable Intentional
                await dialog.ContinueWith(async delegate (Task<IReadOnlyList<IStorageFile>> dialogs)
                {
                    try
                    {
                        IReadOnlyList<IStorageFile?> files = dialog.Result;
                        double percentDelta = 100 / files.Count;
                        double percent = 0;
                        foreach (var file in files)
                        {
                            percent += percentDelta;
                            //BinaryParserPercent = (int)percent;
                            if (file is not null)
                            {
                                string? lp = file.TryGetLocalPath();

                                if (lp is not null)
                                {
                                    string? currentDirectory = Path.GetDirectoryName(lp);
                                    string? currentFilename = Path.GetFileName(lp).ToUpper();
                                    string? metadata = currentFilename + ".txt";
                                    uint ms_sample = 0;

                                    if (currentDirectory != null && currentFilename != null && metadata != null)
                                    {
                                        metadata = currentDirectory + "/" + metadata;
                                        if (File.Exists(metadata))
                                        {
                                            string header_metadata = File.ReadAllText(metadata);

                                            if (header_metadata.Contains("SampleRate:"))
                                            {
                                                string[] lines = header_metadata.Split(new char[] { '\n', '\r' });

                                                foreach (string line in lines)
                                                {
                                                    string l = line.Trim();

                                                    if (l.Contains("SampleRate:"))
                                                    {
                                                        string val = l.Substring(l.IndexOf(":") + 1);

                                                        if (val.Length > 0)
                                                        {
                                                            uint vv = 0;
                                                            if (uint.TryParse(val, out vv))
                                                            {
                                                                ms_sample = vv;
                                                                break;
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }

                                        byte[] data = File.ReadAllBytes(lp);

                                        DateTime dtStart = DateTime.Now;

                                        ArrayList arrayList = Utils.scan(currentFilename, "R%d_%d_%d_%d_%d_%d_%d");

                                        if (arrayList.Count == 7)
                                        {
                                            int? year = (int?)arrayList[0];
                                            int? month = (int?)arrayList[1];
                                            int? day = (int?)arrayList[2];
                                            int? hr = (int?)arrayList[3];
                                            int? mn = (int?)arrayList[4];
                                            int? sec = (int?)arrayList[5];
                                            int? sbs = (int?)arrayList[6];

                                            if (year != null && month != null && day != null &&
                                                    hr != null && mn != null && sec != null && sbs != null)
                                            {

                                                dtStart = new DateTime((int)year, (int)month, (int)day, (int)hr,
                                                    (int)mn, (int)sec, (int)sbs);
                                            }
                                        }

                                        using (TPHParser ip = new TPHParser(lp, data, dtStart, 1023, ms_sample))
                                        {
                                            ip.WriteFile();
                                        }
                                    }
                                }
                            }
                        }
                        //BinaryParserPercent = 100;
                        await Task.Delay(100);
                        //BinaryParserIsRunning = false;

                    }
                    catch (Exception e) { Debug.WriteLine("An error has occured while trying to save the output: " + e); }
                });



            }
            catch { retval = true; }

            return retval;
        }


        private async Task<bool> DecodeEXG48()
        {
            bool retval = false;

            try
            {
                FilePickerOpenOptions options = new()
                {
                    Title = "Select parsed EXG48 binary recording files to convert to CSV...",

                    FileTypeFilter = new List<FilePickerFileType>
                    {
                        new("Biopotential binary files (.EBN) ")
                        {
                            Patterns = new[]{"*-*.EBN"},
                            MimeTypes = new[]{"bin/*"}
                        }
                    },
                    AllowMultiple = true,
                };

                Task<IReadOnlyList<IStorageFile>> dialog = RootTopLevel!.StorageProvider!.OpenFilePickerAsync(options);
                // ReSharper disable once VariableHidesOuterVariable Intentional
                await dialog.ContinueWith(async delegate (Task<IReadOnlyList<IStorageFile>> dialogs)
                {
                    try
                    {
                        IReadOnlyList<IStorageFile?> files = dialog.Result;
                        double percentDelta = 100 / files.Count;
                        double percent = 0;
                        foreach (var file in files)
                        {
                            percent += percentDelta;
                            //BinaryParserPercent = (int)percent;
                            //https://github.com/AvaloniaUI/Avalonia/blob/master/samples/ControlCatalog/Pages/DialogsPage.xaml.cs
                            if (file is not null)
                            {
                                string? lp = file.TryGetLocalPath();

                                if (lp is not null)
                                {
                                    string? currentDirectory = Path.GetDirectoryName(lp);
                                    string? currentFilename = Path.GetFileName(lp).ToUpper();
                                    string? metadata = currentFilename + ".txt";
                                    uint us_sample = 0;

                                    if (currentDirectory != null && currentFilename != null && metadata != null)
                                    {
                                        metadata = currentDirectory + "/" + metadata;
                                        if (File.Exists(metadata))
                                        {
                                            string header_metadata = File.ReadAllText(metadata);

                                            if (header_metadata.Contains("SampleRate:"))
                                            {
                                                string[] lines = header_metadata.Split(new char[] { '\n', '\r' });

                                                foreach (string line in lines)
                                                {
                                                    string l = line.Trim();

                                                    if (l.Contains("SampleRate:"))
                                                    {
                                                        string val = l.Substring(l.IndexOf(":") + 1);

                                                        if (val.Length > 0)
                                                        {
                                                            uint vv = 0;
                                                            if (uint.TryParse(val, out vv))
                                                            {
                                                                us_sample = 1000 / vv;
                                                                break;
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }

                                        byte[] data = File.ReadAllBytes(lp);

                                        DateTime dtStart = DateTime.Now;

                                        ArrayList arrayList = Utils.scan(currentFilename, "E%d_%d_%d_%d_%d_%d_%d");

                                        if (arrayList.Count == 7)
                                        {
                                            int? year = (int?)arrayList[0];
                                            int? month = (int?)arrayList[1];
                                            int? day = (int?)arrayList[2];
                                            int? hr = (int?)arrayList[3];
                                            int? mn = (int?)arrayList[4];
                                            int? sec = (int?)arrayList[5];
                                            int? sbs = (int?)arrayList[6];

                                            if (year != null && month != null && day != null &&
                                                    hr != null && mn != null && sec != null && sbs != null)
                                            {

                                                dtStart = new DateTime((int)year, (int)month, (int)day, (int)hr,
                                                    (int)mn, (int)sec, (int)sbs);
                                            }
                                        }

                                        using (EXG48Parser ip = new EXG48Parser(lp, data, dtStart, 1, us_sample))
                                        {
                                            ip.WriteFile();
                                        }
                                    }
                                }
                            }
                        }
                        //BinaryParserPercent = 100;
                        await Task.Delay(100);
                        //BinaryParserIsRunning = false;

                    }
                    catch (Exception e) { Debug.WriteLine("An error has occured while trying to save the output: " + e); }
                });



            }
            catch { retval = true; }

            return retval;
        }


        private async Task<bool> DecodeEXG1292()
        {
            bool retval = false;

            try
            {
                FilePickerOpenOptions options = new()
                {
                    Title = "Select parsed EXG1292 binary recording files to convert to CSV...",

                    FileTypeFilter = new List<FilePickerFileType>
                    {
                        new("Biopotential binary files (.EBN) ")
                        {
                            Patterns = new[]{"*-*.EBN"},
                            MimeTypes = new[]{"bin/*"}
                        }
                    },
                    AllowMultiple = true,
                };

                Task<IReadOnlyList<IStorageFile>> dialog = RootTopLevel!.StorageProvider!.OpenFilePickerAsync(options);
                // ReSharper disable once VariableHidesOuterVariable Intentional
                await dialog.ContinueWith(async delegate (Task<IReadOnlyList<IStorageFile>> dialogs)
                {
                    try
                    {
                        IReadOnlyList<IStorageFile?> files = dialog.Result;
                        double percentDelta = 100 / files.Count;
                        double percent = 0;
                        foreach (var file in files)
                        {
                            percent += percentDelta;
                            //BinaryParserPercent = (int)percent;
                            //https://github.com/AvaloniaUI/Avalonia/blob/master/samples/ControlCatalog/Pages/DialogsPage.xaml.cs
                            if (file is not null)
                            {
                                string? lp = file.TryGetLocalPath();

                                if (lp is not null)
                                {
                                    string? currentDirectory = Path.GetDirectoryName(lp);
                                    string? currentFilename = Path.GetFileName(lp).ToUpper();
                                    string? metadata = currentFilename + ".txt";
                                    uint us_sample = 0;

                                    if (currentDirectory != null && currentFilename != null && metadata != null)
                                    {
                                        metadata = currentDirectory + "/" + metadata;
                                        if (File.Exists(metadata))
                                        {
                                            string header_metadata = File.ReadAllText(metadata);

                                            if (header_metadata.Contains("SampleRate:"))
                                            {
                                                string[] lines = header_metadata.Split(new char[] { '\n', '\r' });

                                                foreach (string line in lines)
                                                {
                                                    string l = line.Trim();

                                                    if (l.Contains("SampleRate:"))
                                                    {
                                                        string val = l.Substring(l.IndexOf(":") + 1);

                                                        if (val.Length > 0)
                                                        {
                                                            uint vv = 0;
                                                            if (uint.TryParse(val, out vv))
                                                            {
                                                                vv--;
                                                                switch(vv)
                                                                {
                                                                    case 0:
                                                                        us_sample = 1000000 / 125;
                                                                        break;
                                                                    case 1:
                                                                        us_sample = 1000000 / 250;
                                                                        break;
                                                                    case 2:
                                                                        us_sample = 1000000 / 500;
                                                                        break;
                                                                    case 3:
                                                                        us_sample = 1000000 / 1000;
                                                                        break;
                                                                    case 4:
                                                                        us_sample = 1000000 / 2000;
                                                                        break;
                                                                    case 5:
                                                                        us_sample = 1000000 / 4000;
                                                                        break;
                                                                    case 6:
                                                                        us_sample = 1000000 / 8000;
                                                                        break;
                                                                    default:
                                                                        us_sample = 1000000 / 125;
                                                                        break;
                                                                }
                                                                break;
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }

                                        byte[] data = File.ReadAllBytes(lp);

                                        DateTime dtStart = DateTime.Now;

                                        ArrayList arrayList = Utils.scan(currentFilename, "E%d_%d_%d_%d_%d_%d_%d");

                                        if (arrayList.Count == 7)
                                        {
                                            int? year = (int?)arrayList[0];
                                            int? month = (int?)arrayList[1];
                                            int? day = (int?)arrayList[2];
                                            int? hr = (int?)arrayList[3];
                                            int? mn = (int?)arrayList[4];
                                            int? sec = (int?)arrayList[5];
                                            int? sbs = (int?)arrayList[6];

                                            if (year != null && month != null && day != null &&
                                                    hr != null && mn != null && sec != null && sbs != null)
                                            {

                                                dtStart = new DateTime((int)year, (int)month, (int)day, (int)hr,
                                                    (int)mn, (int)sec, (int)sbs);
                                            }
                                        }

                                        using (EXG1292Parser ip = new EXG1292Parser(lp, data, dtStart, 1, us_sample))
                                        {
                                            ip.WriteFile();
                                        }
                                    }
                                }
                            }
                        }
                        //BinaryParserPercent = 100;
                        await Task.Delay(100);
                        //BinaryParserIsRunning = false;

                    }
                    catch (Exception e) { Debug.WriteLine("An error has occured while trying to save the output: " + e); }
                });



            }
            catch { retval = true; }

            return retval;
        }




        private void DriversViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if(e.PropertyName == "SelectedDeviceDriver")
            {
                if (sender is not null)
                {
                    SelectDeviceDriverViewModel ? sddvm = sender as SelectDeviceDriverViewModel;

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
                        Debug.WriteLine("Added: " + logDevice.SerialNumber);
                        LoggerDevices.Add(logDevice);
                    }
                }

                LoggerDevice[] list = new LoggerDevice[LoggerDevices.Count];
                LoggerDevices.CopyTo(list, 0);

                foreach (LoggerDevice dev in list)
                {
                    if (dev.IsComportDevice)
                    {
                        Debug.WriteLine("Probing: " + dev.SerialNumber);
                        bool f = false;
                        foreach (LoggerDevice lDevice in devices)
                        {
                            Debug.WriteLine(" - : " + lDevice.SerialNumber);
                            if (lDevice == dev)
                            {
                                Debug.WriteLine("Match");
                                f = true;
                                break;
                            }
                        }

                        if (f == false && dev.IsConnected == false)
                        {
                            LoggerDevices.Remove(dev);
                            Debug.WriteLine("No Match - Remove: " + dev.SerialNumber);
                        }
                    }
                }
            }
        }

        

        private void _timer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
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


        public Avalonia.Controls.Window? MainWindowContext { get; }

        public Interaction<DockPickWindowViewModel, DockDeviceInfo?> ShowDockPickDialog { get; }
        public ICommand? ConnectDisconnectDockCommand { get; }
        public ICommand? ResetDeviceDockCommand { get; }
        public ICommand? Boot0ModeDockCommand { get; }
        public ICommand? EnableDeviceDockCommand { get; }
        public ICommand? BinaryFilesExtractor { get; }
        public ICommand? ManualAudioParserCommand { get; }
        public ICommand? ManualMotionParserCommand { get; }
        public ICommand? ManualAlsParserCommand { get; }
        public ICommand? ManualTprhParserCommand { get; }
        public ICommand? ManualEXG48ParserCommand { get; }
        public ICommand? ManualEXG1292ParserCommand { get; }
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

        public ICommand? ManualLeptonParserCommand { get; }
        public ICommand? ManualGPSParserCommand { get; }


        /*
         * Logger Devices Section
        * */
        public ObservableCollection<LoggerDevice> LoggerDevices { get; }

        public SelectionModel<LoggerDevice>? SelectedLoggerDeviceModel { get; }


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
            if (_seldeviceType != null)
            {
                //configurationJSONInstance.Name = (string)_seldeviceType.ToString();

                switch (_seldeviceType)
                {
                    case DeviceTypes.Nanotag:
                        await DriversViewModel.UpdateDeviceDriverCollection(Nanotag.SupportedDeviceDrivers);
                        configurationJSONInstance.Name = "Nanotag";
                        configurationJSONInstance.CDrift = null;
                        break;

                    case DeviceTypes.Vesper:
                        await DriversViewModel.UpdateDeviceDriverCollection(Vesper.SupportedDeviceDrivers);
                        configurationJSONInstance.Name = "Vesper";
                        configurationJSONInstance.CDrift = 32999;
                        break;
                    case DeviceTypes.Pipistrelle:
                        await DriversViewModel.UpdateDeviceDriverCollection(Pipistrelle.SupportedDeviceDrivers);
                        configurationJSONInstance.Name = "Vesper";
                        configurationJSONInstance.CDrift = 32999;
                        break;
                    case DeviceTypes.Kol:
                        await DriversViewModel.UpdateDeviceDriverCollection(Kol.SupportedDeviceDrivers);
                        configurationJSONInstance.Name = "Kol";
                        configurationJSONInstance.CDrift = 32999;
                        break;

                    default:
                        await DriversViewModel.UpdateDeviceDriverCollection(new List<ConfigurationDeviceDriver>());
                        configurationJSONInstance.Name = "Vesper";
                        configurationJSONInstance.CDrift = null;
                        break;
                }
            }
            else
            {
                await DriversViewModel.UpdateDeviceDriverCollection(new List<ConfigurationDeviceDriver>());
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
