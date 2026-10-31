using Avalonia.Platform.Storage;
using Avalonia.Threading;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;
using VesperApp.Models;
using VesperApp.Services;

namespace VesperApp.ViewModels
{
    /// <summary>
    /// Nanotag page: disk health (GET_DISKHEALTH, firmware 1.39), the config the tag holds (read back over USB),
    /// and its text console (the CLI on the tag's USB serial port, next to the vendor interface VesperApp uses
    /// for everything else; both can be open at once).
    /// </summary>
    public class NanotagToolsViewModel : ViewModelBase
    {
        private const int NanotagVid = 0x04D8, NanotagPid = 0xFE57;
        private const int ConsoleMaxChars = 200_000;

        private readonly MainViewViewModel? _main;
        private readonly ConsoleHistory _history = new();
        private readonly StringBuilder _console = new();
        private SerialPort? _port;
        private string _lastConfigJson = string.Empty;

        public ObservableCollection<string> Ports { get; } = new();

        private string? _selectedPort;
        public string? SelectedPort { get => _selectedPort; set => this.RaiseAndSetIfChanged(ref _selectedPort, value); }

        private string _healthText = "Connect a Nanotag (firmware 1.39 or later) and press Refresh.";
        public string HealthText { get => _healthText; set => this.RaiseAndSetIfChanged(ref _healthText, value); }

        private string _configText = "Press Read to show the configuration the connected Nanotag holds.";
        public string ConfigText { get => _configText; set => this.RaiseAndSetIfChanged(ref _configText, value); }

        private string _consoleText = string.Empty;
        public string ConsoleText { get => _consoleText; set => this.RaiseAndSetIfChanged(ref _consoleText, value); }

        private string _inputLine = string.Empty;
        public string InputLine { get => _inputLine; set => this.RaiseAndSetIfChanged(ref _inputLine, value); }

        private bool _isConsoleOpen;
        public bool IsConsoleOpen
        {
            get => _isConsoleOpen;
            set { this.RaiseAndSetIfChanged(ref _isConsoleOpen, value); this.RaisePropertyChanged(nameof(ConsoleButtonText)); }
        }
        public string ConsoleButtonText => IsConsoleOpen ? "Close" : "Open";

        private string _statusText = string.Empty;
        public string StatusText { get => _statusText; set => this.RaiseAndSetIfChanged(ref _statusText, value); }

        private bool _hasConfig;
        public bool HasConfig { get => _hasConfig; set => this.RaiseAndSetIfChanged(ref _hasConfig, value); }

        public ICommand RefreshHealthCommand { get; }
        public ICommand ReadConfigCommand { get; }
        public ICommand SaveConfigCommand { get; }
        public ICommand RefreshPortsCommand { get; }
        public ICommand ToggleConsoleCommand { get; }
        public ICommand SendCommand { get; }
        public ICommand ClearConsoleCommand { get; }

        public NanotagToolsViewModel(MainViewViewModel? main)
        {
            _main = main;
            RefreshHealthCommand = ReactiveCommand.CreateFromTask(RefreshHealthAsync);
            ReadConfigCommand = ReactiveCommand.CreateFromTask(ReadConfigAsync);
            SaveConfigCommand = ReactiveCommand.CreateFromTask(SaveConfigAsync, this.WhenAnyValue(x => x.HasConfig));
            RefreshPortsCommand = ReactiveCommand.Create(RefreshPorts);
            ToggleConsoleCommand = ReactiveCommand.Create(ToggleConsole);
            SendCommand = ReactiveCommand.Create(Send, this.WhenAnyValue(x => x.IsConsoleOpen));
            ClearConsoleCommand = ReactiveCommand.Create(() => { lock (_console) _console.Clear(); ConsoleText = string.Empty; });
            RefreshPorts();
        }

        private LoggerDevice? Nanotag()
        {
            LoggerDevice? d = _main?.SelectedLoggerDevice;
            return d != null && d.IsNanotagVendor ? d : null;
        }

        // ───────────────────────── disk health ─────────────────────────

        private async Task RefreshHealthAsync()
        {
            LoggerDevice? dev = Nanotag();
            if (dev == null) { HealthText = "No Nanotag selected in the device list."; return; }
            if (dev.FirmwareVersion != null && dev.FirmwareVersion < LoggerDevice.NanotagDiskHealthFirmware)
            {
                HealthText = $"Disk health needs Nanotag firmware {LoggerDevice.NanotagDiskHealthFirmware} or later (this tag: {dev.FirmwareVersion}).";
                return;
            }
            NanotagDiskHealth? h = await dev.GetDiskHealthAsync();
            HealthText = h == null ? "No reply to the disk health request." : NanotagService.Describe(h);
            if (dev.DownloadPagesCorrected + dev.DownloadPagesUncorrectable + dev.DownloadPagesReadErrors > 0)
                HealthText += Environment.NewLine + $"Last download: {dev.DownloadPagesCorrected} pages ECC-corrected, " +
                              $"{dev.DownloadPagesUncorrectable} uncorrectable, {dev.DownloadPagesReadErrors} read errors " +
                              "(list in download-read-status.txt).";
        }

        // ───────────────────────── config read-back ─────────────────────────

        private async Task ReadConfigAsync()
        {
            LoggerDevice? dev = Nanotag();
            if (dev == null) { ConfigText = "No Nanotag selected in the device list."; HasConfig = false; return; }
            var rows = await dev.ReadNanotagConfigAsync();
            if (rows == null) { ConfigText = "The configuration could not be read."; HasConfig = false; return; }
            var (g, d, s) = rows.Value;
            _lastConfigJson = NanotagService.DescribeConfig(g, d, s).ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            ConfigText = _lastConfigJson;
            HasConfig = true;
        }

        private async Task SaveConfigAsync()
        {
            var top = MainViewViewModel.RootTopLevel;
            if (top == null || _lastConfigJson.Length == 0) return;
            string sn = Nanotag()?.SerialNumber ?? "nanotag";
            IStorageFile? f = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save the configuration read from the Nanotag",
                SuggestedFileName = $"{sn}-on-tag.json",
                DefaultExtension = "json",
            });
            string? path = f?.TryGetLocalPath();
            if (path == null) return;
            await File.WriteAllTextAsync(path, _lastConfigJson);
            StatusText = "Saved " + path;
        }

        // ───────────────────────── console ─────────────────────────

        private void RefreshPorts()
        {
            List<ASD.DeviceCore.Transport.SerialPortInfo> list;
            try { list = ASD.DeviceCore.Transport.SerialEnumerator.List().ToList(); }
            catch { list = new(); }
            string? sn = Nanotag()?.SerialNumber;
            var ordered = list.OrderByDescending(p => p.Matches(NanotagVid, NanotagPid)
                                                      && (sn == null || string.Equals(p.SerialNumber, sn, StringComparison.OrdinalIgnoreCase)))
                              .ThenByDescending(p => p.Matches(NanotagVid, NanotagPid))
                              .ToList();
            Ports.Clear();
            foreach (var p in ordered) Ports.Add(p.ToString());
            if (SelectedPort == null || !Ports.Contains(SelectedPort))
                SelectedPort = Ports.FirstOrDefault();
        }

        private void ToggleConsole()
        {
            if (IsConsoleOpen) { CloseConsole(); return; }
            string? name = SelectedPort?.Split(' ')[0];
            if (string.IsNullOrEmpty(name)) { StatusText = "No serial port."; return; }
            try
            {
                _port = new SerialPort(name, 115200) { NewLine = "\r", DtrEnable = true, RtsEnable = true, Encoding = Encoding.ASCII };
                _port.DataReceived += OnData;
                _port.Open();
                IsConsoleOpen = true;
                StatusText = $"Console open on {name}. Type help for the commands.";
                _port.Write("\r");                                   // prompt
            }
            catch (Exception ex)
            {
                StatusText = $"{name} could not be opened: {ex.Message}";
                _port?.Dispose();
                _port = null;
            }
        }

        public void CloseConsole()
        {
            try { if (_port != null) { _port.DataReceived -= OnData; _port.Close(); } } catch { }
            _port?.Dispose();
            _port = null;
            IsConsoleOpen = false;
        }

        private void OnData(object sender, SerialDataReceivedEventArgs e)
        {
            string text;
            try { text = _port?.ReadExisting() ?? string.Empty; } catch { return; }
            if (text.Length == 0) return;
            string all;
            lock (_console)
            {
                _console.Append(text.Replace("\r\n", "\n").Replace("\n\r", "\n").Replace('\r', '\n'));
                if (_console.Length > ConsoleMaxChars) _console.Remove(0, _console.Length - ConsoleMaxChars);
                all = _console.ToString();
            }
            Dispatcher.UIThread.Post(() => ConsoleText = all);
        }

        private void Send()
        {
            string line = InputLine;
            _history.Add(line);
            InputLine = string.Empty;
            try { _port?.Write(line + "\r"); }                       // the Harmony command processor ends a line at CR
            catch (Exception ex) { StatusText = "Send failed: " + ex.Message; CloseConsole(); }
        }

        public void HistoryUp() { string? s = _history.Previous(); if (s != null) InputLine = s; }
        public void HistoryDown() => InputLine = _history.Next();
    }
}
