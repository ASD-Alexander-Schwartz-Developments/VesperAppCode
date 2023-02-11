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
using System.Reactive;
using Avalonia;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Xml;
using Avalonia.Platform.Storage;
using Avalonia.Controls.Platform;
using Avalonia.Controls.ApplicationLifetimes;

/// <summary>
/// //// {Binding Description, StringFormat='Description: {0}'}
/// </summary>



namespace VesperApp.ViewModels
{
    public class MainViewViewModel : ViewModelBase
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
			set => this.RaiseAndSetIfChanged(ref _IsdownloadButtonEnable
, value);
		}

		private bool _IsdownloadButtonEnable
= true;
		private Flyout _updateFlyout;
		public Flyout updateFlyout
		{
			get => _updateFlyout;
			set => this.RaiseAndSetIfChanged(ref _updateFlyout, value);
		}

		#endregion
/*
		public MainViewViewModel()
        {
            _globalDockAdapter = null;
            MainWindowContext = null;
            //ShowDockPickDialog = new Interaction<DockPickWindowViewModel, DockDeviceInfo?>();

            ConnectDisconnectDockCommand = null;
            EnableDeviceDockCommand = null;
            Boot0ModeDockCommand = null;
            ResetDeviceDockCommand = null;
            BootloaderDeviceCommand = null;
            NewConfigCommand = null;
            LoadConfigCommand = null;
            SaveConfigCommand = null;
            ManualGPSParserCommand = null;

            _timer = null;
            LoggerDevices = new ObservableCollection<LoggerDevice>();
            _deviceUsbAdapter = null;
            SelectedLoggerDevice = null;
            configurationJSONInstance = new ConfigurationJSON();
            ScheduleViewModel = new ScheduleControlViewModel(configurationJSONInstance.Schedule);
            DriversViewModel = new SelectDeviceDriverViewModel(new List<ConfigurationDeviceDriver>());
            DriverEditorGridViewModel = new DeviceDriverPropertyGridViewModel();
			DownloadCheckCommand = ReactiveCommand.Create(DownloadCheck);
			LaterCommand = ReactiveCommand.Create(closeFlyout);
			IsUpdateAvailable = CheckUpdate();
		}*/

        public MainViewViewModel(/*Avalonia.Controls.Window mainWindowContext*/)
        {
            //MainWindowContext = App.MainWindow;
            _globalDockAdapter = new DockAdapter();

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
                    OpenFolderDialog openFolderDialog = new OpenFolderDialog();
                    openFolderDialog.Title = "Select output folder for downloaded data";

                    string ? path = await openFolderDialog.ShowAsync(MainWindowContext);

                    bool ok = await SelectedLoggerDevice.DownloadPages(path);

                    if(!ok)
                    {
                        ///// show error
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

            #region Parser Commands
            ManualGPSParserCommand = ReactiveCommand.CreateFromTask(ParseNanotagSnaps);
            #endregion

            _timer = new System.Timers.Timer();
            _timer.Elapsed += _timer_Elapsed;
            _timer.Interval = 1500;
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
			DownloadCheckCommand = ReactiveCommand.Create(DownloadCheck);
			LaterCommand = ReactiveCommand.Create(closeFlyout);
			IsUpdateAvailable = CheckUpdate();
		}
		//Update Software Methods 

		#region Variables 

		WebClient webClient;
		WebResponse response;
		WebRequest request;
		public string fileName => "vesperAppSetup.msi";
		private string latestServerBuildVersion;
		private string currentIntallBuildVersion;
		public string directoryName = "Vesper";
		public string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

		#endregion

		//Check For the Updates 

		#region Checks for the update 
		private bool CheckUpdate()
		{
			bool status = false;
			try
			{
				XmlDocument doc1 = new XmlDocument();
				doc1.Load(baseUrl + onlineConfig);
				XmlElement Fileroot = doc1.DocumentElement;

				latestServerBuildVersion = Fileroot.InnerText;
				currentIntallBuildVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();

				if (latestServerBuildVersion == currentIntallBuildVersion)
				{
					return false;
				}

				#region If update is avilable checks for the versioning and remote server file exists 
				else
				{
					var latest = latestServerBuildVersion.Split(".")?.Select(int.Parse)?.ToList();
					var current = currentIntallBuildVersion.Split(".")?.Select(int.Parse)?.ToList();
					for (int i = 0; i < latest.Count; i++)
					{
						if (latest[i] > current[i])
						{
							status = true;
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


								webClient = new System.Net.WebClient();
								webClient.OpenRead(baseUrl + directoryName + latestServerBuildVersion + "/" + fileName);
								Int64 SeverFileSize = Convert.ToInt64(webClient.ResponseHeaders["Content-Length"]);
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
			webClient = new WebClient();

			string installerFile = baseUrl + "Vesper" + latestServerBuildVersion + "/vesperAppSetup.msi";
			request = WebRequest.Create(new Uri(installerFile));
			try
			{
				response = request.GetResponse();
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
		public void DownloadCheck()
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
							//Path.Combine(vesperAppFolder + subDirectory);
							var latestversionDirectory = System.IO.Path.Combine(vesperAppFolder, subDirectory);
							webClient = new WebClient();
							webClient.DownloadFileAsync(new Uri(baseUrl + Downloadfolder + fileName), System.IO.Path.Combine(latestversionDirectory, fileName));
							IsdownloadButtonEnable = false;
							isdownload = false;
							isdownloading = true;
							IsUpdateAvailable = false;
							IsFlyoutopen();
							webClient.DownloadFileCompleted += Wc_DownloadFileCompleted;
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

							webClient = new WebClient();
							webClient.DownloadFileAsync(new Uri(baseUrl + Downloadfolder + fileName), System.IO.Path.Combine(latestversionDirectory, fileName));
							IsdownloadButtonEnable = false;
							isdownload = false;
							isdownloading = true;
							IsUpdateAvailable = false;
							IsFlyoutopen();
							webClient.DownloadFileCompleted += Wc_DownloadFileCompleted;
						}
						else
						{
							if (!File.Exists(System.IO.Path.Combine(latestversionDirectory, fileName)))
							{
								webClient = new WebClient();
								webClient.DownloadFileAsync(new Uri(baseUrl + Downloadfolder + fileName), System.IO.Path.Combine(latestversionDirectory, fileName));
								IsdownloadButtonEnable = false;
								isdownload = false;
								isdownloading = true;
								IsUpdateAvailable = false;
								IsFlyoutopen();
								webClient.DownloadFileCompleted += Wc_DownloadFileCompleted;
							}


							//check file size for fail download
							if (File.Exists(System.IO.Path.Combine(latestversionDirectory, fileName)))
							{
								//var localFileSize = (((byte)Path.Combine(latestversionDirectory, fileName).Length));
								System.Net.WebClient wc = new System.Net.WebClient();
								wc.OpenRead(baseUrl + Downloadfolder + fileName);
								Int64 SeverFileSize = Convert.ToInt64(wc.ResponseHeaders["Content-Length"]);

								FileInfo file = new FileInfo(System.IO.Path.Combine(latestversionDirectory, fileName));
								var localFileSize = file.Length;
								if (localFileSize != SeverFileSize)
								{
									string filePath = System.IO.Path.Combine(latestversionDirectory, fileName);
									if (File.Exists(filePath))
										File.Delete(filePath);

									wc.DownloadFileAsync(new Uri(baseUrl + Downloadfolder + fileName), System.IO.Path.Combine(latestversionDirectory, fileName));
									IsdownloadButtonEnable = false;
									isdownload = false;
									isdownloading = true;
									IsUpdateAvailable = false;
									IsFlyoutopen();
									wc.DownloadFileCompleted += Wc_DownloadFileCompleted;
								}

								else
								{
									if (localFileSize == SeverFileSize)
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
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine(ex.Message);
			}
		}
		#endregion

		#region Download File Complete 
		private void Wc_DownloadFileCompleted(object? sender, AsyncCompletedEventArgs e)

		{
			try
			{
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
				updateFlyout = new Flyout() { Placement = FlyoutPlacementMode.Bottom };
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
                        if (file is not null && file.CanOpenWrite)
                        {
                            file.OpenWriteAsync().ContinueWith(delegate (Task<Stream> task)
                            {
                                var options = new JsonSerializerOptions();
                                options.WriteIndented = false;
                                options.Converters.Add(new ConfigurationJSON.ScheduleTypesEnumConverter());
                                options.Converters.Add(new ConfigurationDeviceDriver.ConfigurationDeviceDriverConverter());
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


        private async Task<bool> ParseNanotagSnaps()
        {
            bool result = false;

            if (OperatingSystem.IsWindows())
            {
                OpenFolderDialog openFolderDialog = new OpenFolderDialog();

                openFolderDialog.Title = "Select Folder holdeing the snaps";

                string? path = await openFolderDialog.ShowAsync(MainWindowContext ?? App.MainWindow);

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
                    catch(Exception excp)
                    {
                        ischk = null;
                    }

                    if (ischk != null)
                    {

                        string ?error, msg = "";
                        int index1, index2, current;
                        double percent = 0;

//                        this.Log("Starting " + bar.Name, 1, true);

                        await Task.Delay(1000);
                        //ischkout = ischk.StandardOutput;
                        //ischkerr = ischk.StandardError;

                        bool done = false;

                        await Task.Run( async () =>
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
                        var messageBoxStandardWindow = MessageBox.Avalonia.MessageBoxManager.GetMessageBoxStandardWindow(
                            new MessageBoxStandardParams
                            {
                                ButtonDefinitions = MessageBox.Avalonia.Enums.ButtonEnum.Ok,
                                ContentTitle = "GPS Snap Parser",
                                ContentHeader = "Could not start parser",
                                ContentMessage = "SNAP Parser executable not found or access denied",
                                Icon = MessageBox.Avalonia.Enums.Icon.Warning,
                                WindowIcon = App.MainWindow.Icon,
                            });

                        await messageBoxStandardWindow.ShowDialog(MainWindowContext);

                    }

                    //bar.Percent = 100;
                    //this.Log(bar.Name + " Done!", 1, true);
                }
            }
            else
            {
                var messageBoxStandardWindow = MessageBox.Avalonia.MessageBoxManager.GetMessageBoxStandardWindow(
                    new MessageBoxStandardParams
                    {
                        ButtonDefinitions = MessageBox.Avalonia.Enums.ButtonEnum.Ok,
                        ContentTitle = "GPS Snap Parser",
                        ContentHeader = "Could not start parser",
                        ContentMessage = "SNAP Parser is currently available only on MS Windows OS",
                        Icon = MessageBox.Avalonia.Enums.Icon.Warning,
                        WindowIcon = App.MainWindow.Icon,
                    });

                await messageBoxStandardWindow.ShowDialog(MainWindowContext);
            }

            return await Task.FromResult(result);
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
            if (IsClockUTC == false)
                TextDateTimeNow = DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToLongTimeString();
            else
                TextDateTimeNow = DateTime.UtcNow.ToShortDateString() + " " + DateTime.UtcNow.ToLongTimeString();

            if (IsConnected == true && IsDeviceConnected == false && _deviceUsbAdapter != null && IsClosing == false)
            {
                var scan_nano = Task.Run( async () => await ScanFor(Nanotag.VendorId, Nanotag.ProductId));
                var scan_comport = Task.Run(async () => await ScanForComport());
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


        public ICommand? ManualGPSParserCommand { get; }


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
                _storageProvider = mainWindow != null ? mainWindow.StorageProvider
                    : RootTopLevel != null ? AvaloniaLocator.Current.GetService<IStorageProviderFactory>()
                        ?.CreateProvider(RootTopLevel)
                                           : null;

                if (_storageProvider == null)
                    throw new InvalidOperationException("StorageProvider platform implementation is not available.");

                return _storageProvider;
            }
            set => _storageProvider = value;
        }

    }
}
