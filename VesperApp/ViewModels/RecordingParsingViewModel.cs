using Avalonia.Controls;
using Avalonia.Controls.Selection;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using VesperApp.Models;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using ASDWaveLib;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Collections.ObjectModel;
using FluentAvalonia.UI.Controls;
using VesperApp.Controls;
using VesperApp.Services;
using VesperApp.Views;
using MsBox.Avalonia.Models;

namespace VesperApp.ViewModels
{
    public class RecordingParsingViewModel : ViewModelBase
    {
        public ICommand? BinaryFilesExtractor { get; }
        public ICommand? BinaryAutoFilesExtractor { get; }
        public ICommand? AutoImporterCommand { get; }
        public ICommand? ManualAudioParserCommand { get; }
        public ICommand? ManualMotionParserCommand { get; }
        public ICommand? ManualAlsParserCommand { get; }
        public ICommand? ManualTprhParserCommand { get; }
        public ICommand? ManualEXG48ParserCommand { get; }
        public ICommand? ManualEXG1292ParserCommand { get; }
        public ICommand? ManualLeptonParserCommand { get; }
        public ICommand? ManualGPSParserCommand { get; }
        public ICommand? ShowDecodeJobsCommand { get; }

        // Central data browser (live view of the working directory)
        public ICommand? OpenDataFolderCommand { get; }
        public ICommand? OpenSelectedCommand { get; }
        public ICommand? ActivateSelectedCommand { get; }

        // For cross-tab actions (open a config file in the Configuration editor).
        private readonly MainViewViewModel? _main;
        public ICommand? DecodeSelectedCommand { get; }
        public ICommand? ParseSelectedCommand { get; }
        public ICommand? ShowInExplorerCommand { get; }
        public ICommand? DeleteSelectedCommand { get; }

        public ObservableCollection<RecordingDataNode> ImportedData { get; } = new();

        // ───────────────────────── browser column sorting ─────────────────────────
        // Click a column header to sort (toggle asc/desc). Folders always sort before
        // files; the comparer is applied per tree level on every (re)scan, so live
        // refreshes keep the chosen order.

        private enum DataSortColumn { Name, Kind, Size, Modified }

        private DataSortColumn _sortColumn = DataSortColumn.Name;
        private bool _sortAscending = true;

        public ICommand? SortByCommand { get; }

        public string NameHeader => HeaderText("Name", DataSortColumn.Name);
        public string TypeHeader => HeaderText("Type", DataSortColumn.Kind);
        public string SizeHeader => HeaderText("Size", DataSortColumn.Size);
        public string ModifiedHeader => HeaderText("Modified", DataSortColumn.Modified);

        private string HeaderText(string title, DataSortColumn col) =>
            _sortColumn == col ? title + (_sortAscending ? "  ▲" : "  ▼") : title;

        private void SortBy(string column)
        {
            var col = Enum.Parse<DataSortColumn>(column);
            if (col == _sortColumn) _sortAscending = !_sortAscending;
            else { _sortColumn = col; _sortAscending = true; }

            this.RaisePropertyChanged(nameof(NameHeader));
            this.RaisePropertyChanged(nameof(TypeHeader));
            this.RaisePropertyChanged(nameof(SizeHeader));
            this.RaisePropertyChanged(nameof(ModifiedHeader));

            LoadDataFolder(CurrentDataFolder);
        }

        private int CompareNodes(RecordingDataNode a, RecordingDataNode b)
        {
            if (a.IsFile != b.IsFile) return a.IsFile ? 1 : -1;   // folders first, always

            int c = _sortColumn switch
            {
                DataSortColumn.Kind => string.Compare(a.Kind, b.Kind, StringComparison.OrdinalIgnoreCase),
                DataSortColumn.Size => a.SizeSort.CompareTo(b.SizeSort),
                DataSortColumn.Modified => Nullable.Compare(a.Modified, b.Modified),
                _ => 0,
            };
            if (c == 0) c = string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            return _sortAscending ? c : -c;
        }

        private bool _hasData;
        public bool HasData
        {
            get => _hasData;
            private set
            {
                this.RaiseAndSetIfChanged(ref _hasData, value);
                this.RaisePropertyChanged(nameof(ShowDataBrowser));
                this.RaisePropertyChanged(nameof(ShowIdleHint));
            }
        }

        public bool ShowDataBrowser => !BinaryParserIsRunning && HasData;
        public bool ShowIdleHint => !BinaryParserIsRunning && !HasData;

        private string? _currentDataFolder;
        public string? CurrentDataFolder
        {
            get => _currentDataFolder;
            private set => this.RaiseAndSetIfChanged(ref _currentDataFolder, value);
        }

        private RecordingDataNode? _selectedDataNode;
        public RecordingDataNode? SelectedDataNode
        {
            get => _selectedDataNode;
            set => this.RaiseAndSetIfChanged(ref _selectedDataNode, value);
        }


        public bool BinaryParserIsRunning
        {
            get => binaryParserIsRunning;
            set
            {
                this.RaiseAndSetIfChanged(ref binaryParserIsRunning, value);
                this.RaisePropertyChanged(nameof(ShowDataBrowser));
                this.RaisePropertyChanged(nameof(ShowIdleHint));
            }
        }

        private bool binaryParserIsRunning = false;

        public int BinaryParserPercent
        {
            get => binaryParserPercent;
            set => this.RaiseAndSetIfChanged(ref binaryParserPercent, value);
        }

        private int binaryParserPercent = 0;

        /// <summary>True while a decoder reports no per-file percentage, so the UI shows an
        /// indeterminate (animated) bar instead of a 0% one.</summary>
        public bool BinaryParserIndeterminate
        {
            get => binaryParserIndeterminate;
            set => this.RaiseAndSetIfChanged(ref binaryParserIndeterminate, value);
        }

        private bool binaryParserIndeterminate = false;

        /// <summary>One-line result of the last Auto Decode / Auto Import run, shown in the central area.</summary>
        public string LastSummary
        {
            get => lastSummary;
            set => this.RaiseAndSetIfChanged(ref lastSummary, value);
        }

        private string lastSummary = string.Empty;

        /// <summary>Runs a decode action with the busy indicator on (indeterminate). Used for the
        /// decoders that don't report a per-file percentage, so they still show activity.</summary>
        private async Task<bool> WithBusy(Func<Task<bool>> action)
        {
            try
            {
                BinaryParserIsRunning = true;
                BinaryParserIndeterminate = true;
                BinaryParserPercent = 0;
                return await action();
            }
            finally
            {
                BinaryParserIsRunning = false;
                BinaryParserIndeterminate = false;
            }
        }


        // Parameterless ctor for the XAML designer / ViewLocator fallback.
        public RecordingParsingViewModel() : this(null) { }

        public RecordingParsingViewModel(MainViewViewModel? main)
        {
            _main = main;

            #region Parser Commands
            AutoImporterCommand = ReactiveCommand.CreateFromTask(RunDataImporter);

            BinaryFilesExtractor = ReactiveCommand.CreateFromTask(
                () => DecodeJobManager.Instance.Track("Parse binaries", RunBinaryParser));

            BinaryAutoFilesExtractor = ReactiveCommand.CreateFromTask(RunAutoDecode);

            // Every per-type parser is surfaced as a job in the unified Decoding Progress panel.
            ManualAudioParserCommand = ReactiveCommand.CreateFromTask(
                () => DecodeJobManager.Instance.Track("Audio decode", DecodeAudio));

            ManualMotionParserCommand = ReactiveCommand.CreateFromTask(
                () => DecodeJobManager.Instance.Track("Motion decode", () => WithBusy(DecodeMotionInnertial)));

            ManualAlsParserCommand = ReactiveCommand.CreateFromTask(
                () => DecodeJobManager.Instance.Track("Ambient-light decode", () => WithBusy(DecodeAls)));

            ManualTprhParserCommand = ReactiveCommand.CreateFromTask(
                () => DecodeJobManager.Instance.Track("Temp/Humidity decode", () => WithBusy(DecodeTprh)));

            ManualEXG48ParserCommand = ReactiveCommand.CreateFromTask(
                () => DecodeJobManager.Instance.Track("EXG48 decode", () => WithBusy(DecodeEXG48)));

            ManualEXG1292ParserCommand = ReactiveCommand.CreateFromTask(
                () => DecodeJobManager.Instance.Track("EXG1292 decode", () => WithBusy(DecodeEXG1292)));

            ManualLeptonParserCommand = ReactiveCommand.CreateFromTask(
                () => DecodeJobManager.Instance.Track("Camera decode", DecodeLepton));

            // GNSS is special: ParseNanotagSnaps starts one streamed GNSS job per picked folder itself.
            ManualGPSParserCommand = ReactiveCommand.CreateFromTask(() => WithBusy(ParseNanotagSnaps));

            // Re-open the unified, non-modal Decoding Progress panel (jobs keep running whether it is open or not).
            ShowDecodeJobsCommand = ReactiveCommand.Create(() => DecodeJobManager.PanelOpener?.Invoke());
            #endregion

            OpenDataFolderCommand = ReactiveCommand.CreateFromTask(BrowseDataFolder);
            OpenSelectedCommand = ReactiveCommand.Create(() => OpenNode(SelectedDataNode));
            ActivateSelectedCommand = ReactiveCommand.CreateFromTask(() => ActivateNode(SelectedDataNode));
            SortByCommand = ReactiveCommand.Create<string>(SortBy);

            // Selection actions (context menu) — same pipeline the toolbar pickers use,
            // just fed from the tree selection instead of a file dialog.
            DecodeSelectedCommand = ReactiveCommand.CreateFromTask(DecodeSelection);
            ParseSelectedCommand = ReactiveCommand.CreateFromTask(ParseSelection);
            ShowInExplorerCommand = ReactiveCommand.Create(ShowSelectionInExplorer);
            DeleteSelectedCommand = ReactiveCommand.CreateFromTask(DeleteSelection);

            // Live view: filesystem events are debounced into a rescan, so the browser
            // follows imports, decodes and external changes without a manual refresh.
            _fsDebounce.Elapsed += (_, _) => LoadDataFolder(CurrentDataFolder);

            // Re-scan the current folder when settings change (e.g. the "hide intermediate
            // files" toggle) so the browser reflects the new preference immediately.
            SettingsService.Instance.Changed += (_, _) => LoadDataFolder(CurrentDataFolder);
        }

        // Auto Decode: pick raw logger .bin files, then parse + decode them in one step
        // (BinaryParser strip/split, then the right per-type decoder). Replaces the old
        // "Auto Parse" which only did the strip/split half.
        private async Task<bool> RunAutoDecode()
        {
            try
            {
                FilePickerOpenOptions options = new()
                {
                    Title = "Select raw logger .bin files to auto-decode (parse + decode)…",
                    FileTypeFilter = new List<FilePickerFileType>
                    {
                        new("Raw logger recordings (.bin)")
                        {
                            // Both cases: devices write uppercase ".BIN" and Linux
                            // file-picker patterns are case-sensitive.
                            Patterns = new[] { "*.bin", "*.BIN" },
                            MimeTypes = new[] { "bin/*" }
                        }
                    },
                    AllowMultiple = true,
                };

                IReadOnlyList<IStorageFile> files = await App.AppTopLevel!.StorageProvider!.OpenFilePickerAsync(options);
                List<string> paths = files
                    .Select(f => f.TryGetLocalPath())
                    .Where(p => !string.IsNullOrEmpty(p))
                    .Select(p => p!)
                    .ToList();

                if (paths.Count == 0) return false;

                await RunAutoDecodePipeline(paths);
            }
            catch (Exception e) { Debug.WriteLine(e); }

            return true;
        }

        // Shared parse+decode driver used by Auto Decode (picked files) and Auto Import
        // (copied files). Runs as a job in the unified Decoding Progress panel with a real
        // 0–100% bar; the heavy work runs off the UI thread. Returns the outcome so the
        // import path can act on success (e.g. delete-raw-after-import).
        private async Task<DecodeOutcome> RunAutoDecodePipeline(IReadOnlyList<string> rawPaths)
        {
            LastSummary = string.Empty;

            DecodeJob job = DecodeJobManager.Instance.Run(
                $"Auto decode ({rawPaths.Count} file{(rawPaths.Count == 1 ? "" : "s")})",
                async (log, pct, ct) =>
                {
                    var progress = new Progress<int>(p => pct.Report(p));
                    DecodeSummary summary = await RecordingPipeline.AutoDecodeAsync(rawPaths, progress);
                    log.Report(summary.ToString());
                    return new DecodeOutcome(summary.Failed == 0, summary.ToString());
                });

            DecodeOutcome outcome = await job.Completion;
            LastSummary = outcome.Message;

            // Surface what landed locally in the central data browser.
            LoadDataFolder(rawPaths.Count > 0 ? Path.GetDirectoryName(rawPaths[0]) : CurrentDataFolder);

            return outcome;
        }

        // ───────────────────────── data browser ─────────────────────────

        private async Task<bool> BrowseDataFolder()
        {
            try
            {
                FolderPickerOpenOptions options = new()
                {
                    Title = "Select a folder of imported / decoded recordings to browse",
                    AllowMultiple = false,
                };
                IReadOnlyList<IStorageFolder> folders = await App.AppTopLevel!.StorageProvider!.OpenFolderPickerAsync(options);
                string? path = folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
                if (!string.IsNullOrEmpty(path))
                    LoadDataFolder(path);
            }
            catch (Exception e) { Debug.WriteLine(e); }

            return true;
        }

        /// <summary>Scan a local folder and present its full recording tree. When re-scanning
        /// the same folder (live refresh), nodes are merged in place so the tree's expansion
        /// and selection state survive; switching folders rebuilds from scratch.</summary>
        public void LoadDataFolder(string? folder)
        {
            List<RecordingDataNode> nodes = BuildDataNodes(folder);

            void Apply()
            {
                if (string.Equals(CurrentDataFolder, folder, StringComparison.OrdinalIgnoreCase))
                {
                    SyncNodes(ImportedData, nodes);
                }
                else
                {
                    ImportedData.Clear();
                    foreach (RecordingDataNode n in nodes) ImportedData.Add(n);
                }
                CurrentDataFolder = folder;
                HasData = ImportedData.Count > 0;
                WatchFolder(folder);
            }

            if (Dispatcher.UIThread.CheckAccess()) Apply();
            else Dispatcher.UIThread.Post(Apply);
        }

        // ─────────────────── live updates (FileSystemWatcher) ───────────────────

        private FileSystemWatcher? _watcher;
        private readonly System.Timers.Timer _fsDebounce = new(600) { AutoReset = false };

        /// <summary>(Re)attach the recursive watcher to the browsed folder. Any filesystem
        /// event just restarts the debounce timer; a quiet 600 ms triggers one rescan.</summary>
        private void WatchFolder(string? folder)
        {
            if (_watcher != null && string.Equals(_watcher.Path, folder, StringComparison.OrdinalIgnoreCase))
                return;

            _watcher?.Dispose();
            _watcher = null;

            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return;

            try
            {
                _watcher = new FileSystemWatcher(folder)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.Size | NotifyFilters.LastWrite,
                };
                _watcher.Created += OnFsEvent;
                _watcher.Changed += OnFsEvent;
                _watcher.Deleted += OnFsEvent;
                _watcher.Renamed += OnFsEvent;
                _watcher.Error += (_, _) => { _fsDebounce.Stop(); _fsDebounce.Start(); };
                _watcher.EnableRaisingEvents = true;
            }
            catch { _watcher = null; }   // e.g. folder vanished mid-attach; next load retries
        }

        private void OnFsEvent(object? sender, FileSystemEventArgs e)
        {
            _fsDebounce.Stop();
            _fsDebounce.Start();
        }

        /// <summary>Merge a freshly scanned node list into the displayed one, keyed by path:
        /// update details in place, add what appeared, drop what disappeared, keep order.</summary>
        private static void SyncNodes(ObservableCollection<RecordingDataNode> current, IList<RecordingDataNode> fresh)
        {
            for (int i = current.Count - 1; i >= 0; i--)
                if (!fresh.Any(f => SameNode(f, current[i])))
                    current.RemoveAt(i);

            for (int i = 0; i < fresh.Count; i++)
            {
                RecordingDataNode f = fresh[i];
                RecordingDataNode? existing = current.FirstOrDefault(c => SameNode(f, c));

                if (existing == null)
                {
                    current.Insert(Math.Min(i, current.Count), f);
                }
                else
                {
                    existing.Name = f.Name;
                    existing.Kind = f.Kind;
                    existing.SizeText = f.SizeText;
                    existing.SizeSort = f.SizeSort;
                    existing.Modified = f.Modified;
                    existing.Icon = f.Icon;
                    SyncNodes(existing.Children, f.Children);

                    int ci = current.IndexOf(existing);
                    if (ci != i && i < current.Count) current.Move(ci, i);
                }
            }
        }

        private static bool SameNode(RecordingDataNode a, RecordingDataNode b) =>
            a.IsFile == b.IsFile &&
            string.Equals(a.FullPath, b.FullPath, StringComparison.OrdinalIgnoreCase);

        // Friendly category per known sensor subfolder the parse phase produces.
        private static readonly (string sub, string label, Symbol icon)[] DataCategories =
        {
            ("AUD", "Audio", Symbol.Microphone),
            ("KOL-AUD", "Audio (KOL)", Symbol.Microphone),
            ("IMU", "Motion", Symbol.Directions),
            ("EXG", "Biopotential", Symbol.Document),
            ("TPRH", "Temperature / Humidity", Symbol.WeatherFog),
            ("ALS", "Light", Symbol.WeatherSunnyLow),
            ("THCAM", "Camera", Symbol.Camera),
            ("DAT", "GNSS", Symbol.Globe),
        };

        // Binary intermediates the parse phase emits; the decode phase turns these into
        // WAV/CSV/images. Hidden from the browser when Settings → Recordings → "Hide
        // intermediate files" is on, leaving only the decoded outputs visible.
        private static readonly HashSet<string> IntermediateExtensions =
            new(StringComparer.OrdinalIgnoreCase) { ".UBN", ".MBN", ".ABN", ".LBN", ".RBN", ".EBN", ".CBN" };

        /// <summary>Build the full recursive tree of the browsed folder: every session
        /// folder and file (minus hidden sidecars / intermediates per Settings), with
        /// known sensor folders labelled and iconed via <see cref="DataCategories"/>.
        /// Each level is sorted by the active column (folders first).</summary>
        private List<RecordingDataNode> BuildDataNodes(string? folder)
        {
            var list = new List<RecordingDataNode>();
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return list;

            try
            {
                var root = new RecordingDataNode { FullPath = folder };
                AddFolderFiles(root, folder);
                list.AddRange(root.Children);
            }
            catch { }

            return list;
        }

        private void AddFolderFiles(RecordingDataNode parent, string folderPath)
        {
            try
            {
                var children = new List<RecordingDataNode>();
                bool hideIntermediates = SettingsService.Current.Recordings.HideIntermediateFiles;
                foreach (string f in Directory.EnumerateFiles(folderPath))
                {
                    if (f.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)) continue; // hide metadata sidecars
                    if (hideIntermediates && IntermediateExtensions.Contains(Path.GetExtension(f))) continue;
                    children.Add(MakeFileNode(f));
                }
                foreach (string d in Directory.EnumerateDirectories(folderPath))
                {
                    string dirName = Path.GetFileName(d);
                    var known = DataCategories.FirstOrDefault(c => string.Equals(c.sub, dirName, StringComparison.OrdinalIgnoreCase));

                    var sub = new RecordingDataNode
                    {
                        Name = dirName,
                        Icon = known.sub != null ? known.icon : Symbol.Folder,
                        FullPath = d,
                        Kind = known.sub != null ? known.label : "Folder",
                    };
                    try { sub.Modified = Directory.GetLastWriteTime(d); } catch { }
                    AddFolderFiles(sub, d);
                    if (sub.Children.Count > 0)
                    {
                        sub.SizeSort = sub.Children.Count;
                        sub.SizeText = sub.Children.Count + (sub.Children.Count == 1 ? " item" : " items");
                        children.Add(sub);
                    }
                }

                children.Sort(CompareNodes);
                foreach (RecordingDataNode c in children) parent.Children.Add(c);
            }
            catch { }
        }

        private static RecordingDataNode MakeFileNode(string path)
        {
            long size = 0;
            DateTime? modified = null;
            try
            {
                var fi = new FileInfo(path);
                size = fi.Length;
                modified = fi.LastWriteTime;
            }
            catch { }

            return new RecordingDataNode
            {
                Name = Path.GetFileName(path),
                FullPath = path,
                IsFile = true,
                Icon = IconForFile(path),
                Kind = KindForFile(path),
                SizeText = HumanSize(size),
                SizeSort = size,
                Modified = modified,
            };
        }

        // Sensor label per raw-recording type letter (the letter before ".bin"), matching
        // the on-device naming — same mapping the legacy app showed in its Type column.
        private static readonly Dictionary<char, string> RawTypeLetters = new()
        {
            ['G'] = "GPS", ['U'] = "Audio", ['M'] = "Motion", ['E'] = "EXG",
            ['R'] = "Temp/RH", ['L'] = "Light", ['X'] = "Proximity",
            ['O'] = "Log", ['S'] = "Analog", ['C'] = "Camera",
        };

        private static string KindForFile(string path)
        {
            string name = Path.GetFileNameWithoutExtension(path).ToUpperInvariant();
            switch (Path.GetExtension(path).ToUpperInvariant())
            {
                case ".BIN":
                    // Raw logger recording: classify by the trailing type letter (U0-U3 = KOL audio).
                    char last = name.Length > 0 ? name[^1] : ' ';
                    if (char.IsDigit(last) && name.Length > 1 && name[^2] == 'U') return "Audio (raw)";
                    return RawTypeLetters.TryGetValue(last, out string? label) ? label + " (raw)" : "Raw";
                case ".UBN": return "Audio (parsed)";
                case ".MBN": return "Motion (parsed)";
                case ".ABN": return "Acceleration (parsed)";
                case ".LBN": return "Light (parsed)";
                case ".RBN": return "Temp/RH (parsed)";
                case ".EBN": return "EXG (parsed)";
                case ".CBN": return "Camera (parsed)";
                case ".WAV": return "Audio (WAV)";
                case ".CSV": return "Data (CSV)";
                case ".JPG": case ".JPEG": case ".PNG": return "Image";
                case ".DAT": return "GNSS snapshot";
                default: return Path.GetExtension(path).TrimStart('.').ToUpperInvariant();
            }
        }

        private static Symbol IconForFile(string path) => Path.GetExtension(path).ToUpperInvariant() switch
        {
            ".WAV" => Symbol.Audio,
            ".CSV" => Symbol.Document,
            ".JPG" or ".JPEG" or ".PNG" => Symbol.Image,
            ".DAT" => Symbol.Globe,
            ".BIN" => Symbol.Folder,
            _ => Symbol.Document,
        };

        private static string HumanSize(long bytes)
        {
            if (bytes >= 1024 * 1024) return (bytes / (1024.0 * 1024.0)).ToString("0.0") + " MB";
            if (bytes >= 1024) return (bytes / 1024.0).ToString("0.0") + " KB";
            return bytes + " B";
        }

        private static void OpenNode(RecordingDataNode? node)
        {
            if (node?.FullPath == null) return;
            try
            {
                Process.Start(new ProcessStartInfo(node.FullPath) { UseShellExecute = true });
            }
            catch (Exception e) { Debug.WriteLine(e); }
        }

        /// <summary>Double-click behaviour: folders expand/collapse (handled by the tree
        /// itself); a .json that parses as a device configuration offers to open in the
        /// Configuration editor; anything else opens with the OS default app.</summary>
        private async Task ActivateNode(RecordingDataNode? node)
        {
            if (node?.FullPath == null || !node.IsFile) return;

            if (node.FullPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase) &&
                await TryOpenConfigInEditor(node.FullPath))
                return;

            OpenNode(node);
        }

        /// <summary>If <paramref name="path"/> holds a device configuration, ask the user
        /// and load it into the Configuration editor. Returns true when the double-click
        /// was handled (config recognised), false to fall back to the OS default app.</summary>
        private async Task<bool> TryOpenConfigInEditor(string path)
        {
            string json;
            try { json = File.ReadAllText(path); }
            catch { return false; }

            ConfigurationJSON? config = ConfigurationJSON.TryParse(json);
            if (config == null) return false;

            var confirm = MessageBoxManager.GetMessageBoxStandard(new MessageBoxStandardParams
            {
                ButtonDefinitions = ButtonEnum.YesNo,
                ContentTitle = "Open in Configuration editor",
                ContentHeader = $"\"{Path.GetFileName(path)}\" is a device configuration ({config.Name}).",
                ContentMessage = "Open it in the Configuration editor? Unsaved changes in the editor will be lost.",
                Icon = Icon.Question,
                SizeToContent = SizeToContent.WidthAndHeight,
                WindowIcon = App.MainWindow?.Icon,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
            });

            if (await confirm.ShowWindowDialogAsync(App.MainWindow) == ButtonResult.Yes && _main != null)
                await _main.OpenConfigurationInEditor(json);

            return true;   // recognised as a config — never fall through to the OS app
        }

        // ─────────────────── selection actions (context menu) ───────────────────
        // The tree selection feeds the SAME pipeline as the toolbar's file pickers —
        // two ways in (selection or dialog), one decode path. Mirrors the legacy
        // VesperStudio design where checked-list and OpenFileDialog shared the parsers.

        private IReadOnlyList<RecordingDataNode> _selection = Array.Empty<RecordingDataNode>();

        /// <summary>Called from the view whenever the tree selection changes.</summary>
        public void SetSelection(IReadOnlyList<RecordingDataNode> nodes) => _selection = nodes;

        private IReadOnlyList<RecordingDataNode> EffectiveSelection() =>
            _selection.Count > 0 ? _selection
            : SelectedDataNode != null ? new[] { SelectedDataNode }
            : Array.Empty<RecordingDataNode>();

        /// <summary>Classify the selection into decode inputs: raw .bin files (need
        /// parse + decode) and decodable targets (intermediates / DAT snap folders).
        /// Folders contribute everything under them.</summary>
        private (List<string> Raws, List<string> Decodables) CollectSelectionTargets()
        {
            var raws = new List<string>();
            var dec = new List<string>();

            foreach (RecordingDataNode n in EffectiveSelection())
            {
                if (n.FullPath == null) continue;

                if (n.IsFile)
                {
                    if (RecordingPipeline.IsRawBin(n.FullPath)) raws.Add(n.FullPath);
                    else if (RecordingPipeline.IsDecodableIntermediate(n.FullPath)) dec.Add(n.FullPath);
                    else if (n.FullPath.EndsWith(".dat", StringComparison.OrdinalIgnoreCase))
                    {
                        // A picked snapshot decodes as its containing DAT set.
                        string? dir = Path.GetDirectoryName(n.FullPath);
                        if (dir != null && RecordingPipeline.IsGnssSnapFolder(dir)) dec.Add(dir);
                    }
                }
                else if (Directory.Exists(n.FullPath))
                {
                    if (RecordingPipeline.IsGnssSnapFolder(n.FullPath)) { dec.Add(n.FullPath); continue; }

                    raws.AddRange(RecordingPipeline.FindRawBinFiles(n.FullPath));
                    try
                    {
                        dec.AddRange(Directory.EnumerateFiles(n.FullPath, "*", SearchOption.AllDirectories)
                            .Where(RecordingPipeline.IsDecodableIntermediate));
                        dec.AddRange(Directory.EnumerateDirectories(n.FullPath, "*", SearchOption.AllDirectories)
                            .Where(RecordingPipeline.IsGnssSnapFolder));
                    }
                    catch { }
                }
            }

            return (raws.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                    dec.Distinct(StringComparer.OrdinalIgnoreCase).ToList());
        }

        private static void Merge(DecodeSummary into, DecodeSummary from)
        {
            into.RawParsed += from.RawParsed;
            into.Wav += from.Wav;
            into.Csv += from.Csv;
            into.Image += from.Image;
            into.GnssSets += from.GnssSets;
            into.Skipped += from.Skipped;
            into.Failed += from.Failed;
        }

        /// <summary>Decode whatever is selected: raw .bin → parse + decode; intermediates
        /// and DAT folders → decode. Runs as a job in the Decoding Progress panel.</summary>
        private async Task DecodeSelection()
        {
            var (raws, decodables) = CollectSelectionTargets();
            int count = raws.Count + decodables.Count;
            if (count == 0) return;

            DecodeJob job = DecodeJobManager.Instance.Run(
                $"Decode selection ({count} item{(count == 1 ? "" : "s")})",
                async (log, pct, ct) =>
                {
                    var sum = new DecodeSummary();
                    var progress = new Progress<int>(p => pct.Report(p));
                    if (raws.Count > 0) Merge(sum, await RecordingPipeline.AutoDecodeAsync(raws, progress));
                    if (decodables.Count > 0) Merge(sum, await RecordingPipeline.DecodeFilesAsync(decodables, progress));
                    log.Report(sum.ToString());
                    return new DecodeOutcome(sum.Failed == 0, sum.ToString());
                });

            DecodeOutcome outcome = await job.Completion;
            LastSummary = outcome.Message;
        }

        /// <summary>Parse only: strip/split the selected raw .bin files into intermediates
        /// without decoding — the selection-driven twin of the Binary Parser button.</summary>
        private async Task ParseSelection()
        {
            var (raws, _) = CollectSelectionTargets();
            if (raws.Count == 0) return;

            DecodeJob job = DecodeJobManager.Instance.Run(
                $"Parse selection ({raws.Count} file{(raws.Count == 1 ? "" : "s")})",
                async (log, pct, ct) =>
                {
                    var progress = new Progress<int>(p => pct.Report(p));
                    DecodeSummary sum = await RecordingPipeline.ParseRawFilesAsync(raws, progress);
                    string msg = $"Parsed {sum.RawParsed} raw file(s)" + (sum.Failed > 0 ? $" · {sum.Failed} failed" : "") + ".";
                    log.Report(msg);
                    return new DecodeOutcome(sum.Failed == 0, msg);
                });

            DecodeOutcome outcome = await job.Completion;
            LastSummary = outcome.Message;
        }

        private void ShowSelectionInExplorer()
        {
            RecordingDataNode? node = EffectiveSelection().FirstOrDefault();
            if (node?.FullPath == null) return;
            string? folder = node.IsFile ? Path.GetDirectoryName(node.FullPath) : node.FullPath;
            if (folder == null) return;
            try
            {
                Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
            }
            catch (Exception e) { Debug.WriteLine(e); }
        }

        private async Task DeleteSelection()
        {
            var targets = EffectiveSelection().Where(n => n.FullPath != null).ToList();
            if (targets.Count == 0) return;

            var confirm = MessageBoxManager.GetMessageBoxStandard(new MessageBoxStandardParams
            {
                ButtonDefinitions = ButtonEnum.YesNo,
                ContentTitle = "Delete from disk",
                ContentHeader = $"Delete {targets.Count} selected item{(targets.Count == 1 ? "" : "s")}?",
                ContentMessage = string.Join("\n", targets.Take(8).Select(t => t.Name))
                                 + (targets.Count > 8 ? $"\n… and {targets.Count - 8} more" : ""),
                Icon = Icon.Warning,
                SizeToContent = SizeToContent.WidthAndHeight,
                WindowIcon = App.MainWindow?.Icon,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
            });

            if (await confirm.ShowWindowDialogAsync(App.MainWindow) != ButtonResult.Yes) return;

            foreach (RecordingDataNode t in targets)
            {
                try
                {
                    if (t.IsFile) File.Delete(t.FullPath!);
                    else if (Directory.Exists(t.FullPath!)) Directory.Delete(t.FullPath!, true);
                }
                catch (Exception e) { Debug.WriteLine("Delete failed: " + e.Message); }
            }

            LoadDataFolder(CurrentDataFolder);   // watcher would catch it; refresh now for snappiness
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

                Task<IReadOnlyList<IStorageFile>> dialog = App.AppTopLevel!.StorageProvider!.OpenFilePickerAsync(options);
                // ReSharper disable once VariableHidesOuterVariable Intentional
                await dialog.ContinueWith(async delegate (Task<IReadOnlyList<IStorageFile>> dialogs)
                {
                    try
                    {
                        IReadOnlyList<IStorageFile?> files = dialog.Result;
                        BinaryParserIsRunning = true;
                        BinaryParserPercent = 0;
                        double percentDelta = files.Count > 0 ? 100.0 / files.Count : 100;
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

                                        LeptonReading lr = new LeptonReading(lp, databuf, 1024 - 16, DateTime.Now, 0, 0, LeptonFilterType.LEPTON_RAINBOW);
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
                        // Every filter lists both cases: devices write uppercase ".BIN"
                        // (FAT 8.3) and Linux file-picker patterns are case-sensitive.
                        new("All binary files (.bin) ")
                        {
                            Patterns = new[]{"*.bin", "*.BIN"},
                            MimeTypes = new[]{"bin/*"}
                        },
                        new("GPS Snap (.bin) ")
                        {
                            Patterns = new[]{"*G.bin", "*G.BIN"},
                            MimeTypes = new[]{"bin/*"}
                        },
                        new("Audio Recording (.bin) ")
                        {
                            Patterns = new[]{"*U.bin", "*U0.bin", "*U1.bin", "*U2.bin", "*U3.bin",
                                             "*U.BIN", "*U0.BIN", "*U1.BIN", "*U2.BIN", "*U3.BIN"},
                            MimeTypes = new[]{"bin/*"}
                        },
                        new("Motion (Innertial) Recording (.bin) ")
                        {
                            Patterns = new[]{"*M.bin", "*M.BIN"},
                            MimeTypes = new[]{"bin/*"}
                        },
                        new("Ambient Light Level (Lux) Recording (.bin) ")
                        {
                            Patterns = new[]{"*L.bin", "*L.BIN"},
                            MimeTypes = new[]{"bin/*"}
                        },
                        new("Temperature and Relative Humidity Recording (.bin) ")
                        {
                            Patterns = new[]{"*R.bin", "*R.BIN"},
                            MimeTypes = new[]{"bin/*"}
                        },
                        new("Biopotentials (EEG/EMG/ECG) Recording (.bin) ")
                        {
                            Patterns = new[]{"*E.bin", "*E.BIN"},
                            MimeTypes = new[]{"bin/*"}
                        },
                        new("Aux Analog sensor Recording (.bin) ")
                        {
                            Patterns = new[]{"*S.bin", "*S.BIN"},
                            MimeTypes = new[]{"bin/*"}
                        },
                        new("Proximity Recording (.bin) ")
                        {
                            Patterns = new[]{"*X.bin", "*X.BIN"},
                            MimeTypes = new[]{"bin/*"}
                        },
                        new("Thermal Camera (Lepton) (.bin) ")
                        {
                            Patterns = new[]{"*C.bin", "*C.BIN"},
                            MimeTypes = new[]{"bin/*"}
                        },
                        new("Self Log Recording (.bin) ")
                        {
                            Patterns = new[]{"*O.bin", "*O.BIN"},
                            MimeTypes = new[]{"bin/*"}
                        },
                    },
                    AllowMultiple = true,
                };

                Task<IReadOnlyList<IStorageFile>> dialog = App.AppTopLevel!.StorageProvider!.OpenFilePickerAsync(options);
                // ReSharper disable once VariableHidesOuterVariable Intentional
                await dialog.ContinueWith(async delegate (Task<IReadOnlyList<IStorageFile>> dialogs)
                {
                    try
                    {
                        IReadOnlyList<IStorageFile?> files = dialog.Result;
                        BinaryParserIsRunning = true;
                        BinaryParserPercent = 0;
                        double percentDelta = files.Count > 0 ? 100.0 / files.Count : 100;
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
                                        else if (currentFilename.Contains("U.BIN"))
                                        {
                                            string fullPathOnly = Path.GetFullPath(currentDirectory);
                                            fullPathOnly += Path.DirectorySeparatorChar + "AUD";
                                            if (Directory.Exists(fullPathOnly) == false)
                                            {
                                                Directory.CreateDirectory(fullPathOnly);
                                            }

                                            await BinaryParser.StripSplit(lp, fullPathOnly, 0);
                                        }
                                        else if (currentFilename.Contains("U0.BIN"))
                                        {
                                            string fullPathOnly = Path.GetFullPath(currentDirectory);
                                            fullPathOnly += Path.DirectorySeparatorChar + "KOL-AUD";
                                            if (Directory.Exists(fullPathOnly) == false)
                                            {
                                                Directory.CreateDirectory(fullPathOnly);
                                            }

                                            await BinaryParser.StripSplitEx(lp, '0', fullPathOnly, 0);
                                        }
                                        else if (currentFilename.Contains("U1.BIN"))
                                        {
                                            string fullPathOnly = Path.GetFullPath(currentDirectory);
                                            fullPathOnly += Path.DirectorySeparatorChar + "KOL-AUD";
                                            if (Directory.Exists(fullPathOnly) == false)
                                            {
                                                Directory.CreateDirectory(fullPathOnly);
                                            }

                                            await BinaryParser.StripSplitEx(lp, '1', fullPathOnly, 0);
                                        }
                                        else if (currentFilename.Contains("U2.BIN"))
                                        {
                                            string fullPathOnly = Path.GetFullPath(currentDirectory);
                                            fullPathOnly += Path.DirectorySeparatorChar + "KOL-AUD";
                                            if (Directory.Exists(fullPathOnly) == false)
                                            {
                                                Directory.CreateDirectory(fullPathOnly);
                                            }

                                            await BinaryParser.StripSplitEx(lp, '2', fullPathOnly, 0);
                                        }
                                        else if (currentFilename.Contains("U3.BIN"))
                                        {
                                            string fullPathOnly = Path.GetFullPath(currentDirectory);
                                            fullPathOnly += Path.DirectorySeparatorChar + "KOL-AUD";
                                            if (Directory.Exists(fullPathOnly) == false)
                                            {
                                                Directory.CreateDirectory(fullPathOnly);
                                            }

                                            await BinaryParser.StripSplitEx(lp, '3', fullPathOnly, 0);
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

                Task<IReadOnlyList<IStorageFile>> dialog = App.AppTopLevel!.StorageProvider!.OpenFilePickerAsync(options);
                // ReSharper disable once VariableHidesOuterVariable Intentional
                await dialog.ContinueWith(async delegate (Task<IReadOnlyList<IStorageFile>> dialogs)
                {
                    try
                    {
                        IReadOnlyList<IStorageFile?> files = dialog.Result;
                        BinaryParserIsRunning = true;
                        BinaryParserPercent = 0;
                        double percentDelta = files.Count > 0 ? 100.0 / files.Count : 100;
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

                Task<IReadOnlyList<IStorageFile>> dialog = App.AppTopLevel!.StorageProvider!.OpenFilePickerAsync(options);
                // ReSharper disable once VariableHidesOuterVariable Intentional
                await dialog.ContinueWith(async delegate (Task<IReadOnlyList<IStorageFile>> dialogs)
                {
                    try
                    {
                        IReadOnlyList<IStorageFile?> files = dialog.Result;
                        double percentDelta = files.Count > 0 ? 100.0 / files.Count : 100;
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

                                            ArrayList arrayList = Utils.scan(currentFilename, "NACC.%d_%d_%d_%d_%d_%d_%d.ABN");

                                            if (arrayList.Count >= 6)
                                            {
                                                int? year = (int?)arrayList[0];
                                                int? month = (int?)arrayList[1];
                                                int? day = (int?)arrayList[2];
                                                int? hr = (int?)arrayList[3];
                                                int? mn = (int?)arrayList[4];
                                                int? sec = (int?)arrayList[5];
                                                int? sbs = (int?)0;

                                                if(arrayList.Count == 7)
                                                {
                                                    sbs = (int?)arrayList[6];
                                                }

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

                Task<IReadOnlyList<IStorageFile>> dialog = App.AppTopLevel!.StorageProvider!.OpenFilePickerAsync(options);
                // ReSharper disable once VariableHidesOuterVariable Intentional
                await dialog.ContinueWith(async delegate (Task<IReadOnlyList<IStorageFile>> dialogs)
                {
                    try
                    {
                        IReadOnlyList<IStorageFile?> files = dialog.Result;
                        double percentDelta = files.Count > 0 ? 100.0 / files.Count : 100;
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

                Task<IReadOnlyList<IStorageFile>> dialog = App.AppTopLevel!.StorageProvider!.OpenFilePickerAsync(options);
                // ReSharper disable once VariableHidesOuterVariable Intentional
                await dialog.ContinueWith(async delegate (Task<IReadOnlyList<IStorageFile>> dialogs)
                {
                    try
                    {
                        IReadOnlyList<IStorageFile?> files = dialog.Result;
                        double percentDelta = files.Count > 0 ? 100.0 / files.Count : 100;
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

                Task<IReadOnlyList<IStorageFile>> dialog = App.AppTopLevel!.StorageProvider!.OpenFilePickerAsync(options);
                // ReSharper disable once VariableHidesOuterVariable Intentional
                await dialog.ContinueWith(async delegate (Task<IReadOnlyList<IStorageFile>> dialogs)
                {
                    try
                    {
                        IReadOnlyList<IStorageFile?> files = dialog.Result;
                        double percentDelta = files.Count > 0 ? 100.0 / files.Count : 100;
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

                Task<IReadOnlyList<IStorageFile>> dialog = App.AppTopLevel!.StorageProvider!.OpenFilePickerAsync(options);
                // ReSharper disable once VariableHidesOuterVariable Intentional
                await dialog.ContinueWith(async delegate (Task<IReadOnlyList<IStorageFile>> dialogs)
                {
                    try
                    {
                        IReadOnlyList<IStorageFile?> files = dialog.Result;
                        double percentDelta = files.Count > 0 ? 100.0 / files.Count : 100;
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
                                                                switch (vv)
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


        private IProgressStatus? _copyStatus;

        public IProgressStatus? CopyStatus
        {
            get => _copyStatus;
            private set
            {
                this.RaiseAndSetIfChanged(ref _copyStatus, value);
            }
        }

        private async Task<bool> RunDataImporter()
        {
            bool result = false;
            DirectoryInfo? sourceDirectoryInfo = null;
            DirectoryInfo? destinationDirectoryInfo = null;

            if (App.MainWindow == null)
                return false;

            // Guided import: step 1 pick the auto-detected device drive, step 2 confirm the
            // pre-filled structured target (<working dir>/<device id>/<recording date>).
            // Everything on the drive is copied, incl. config + UID.txt (self-documenting).
            ImportDeviceWindow importDialog = new();
            importDialog.DataContext = new ImportDeviceWindowViewModel();
            ImportDeviceRequest? request = await importDialog.ShowDialog<ImportDeviceRequest?>(App.MainWindow);

            if (request == null)
                return false;

            try
            {
                sourceDirectoryInfo = new DirectoryInfo(request.SourcePath);
                destinationDirectoryInfo = Directory.CreateDirectory(request.TargetPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Import target could not be created: " + ex.Message);
                return false;
            }

            if (destinationDirectoryInfo != null && sourceDirectoryInfo != null && App.MainWindow != null)
            {
                // Raw files already in the target before this import: exempt from
                // delete-raw-after-import — only files THIS session copies may be deleted.
                var preExistingRaws = new HashSet<string>(
                    RecordingPipeline.FindRawBinFiles(destinationDirectoryInfo.FullName),
                    StringComparer.OrdinalIgnoreCase);

                IProgressStatus progressStatus = new ProgressStatus();
                progressStatus.ProgressUpdated += HandleProgessUpdatedEvent;
                progressStatus.Finished += HandleFinishedEvent;
                progressStatus.Cancelled += HandleCancelledEvent;
                totaln = sourceDirectoryInfo.GetDirectories().Length + 1;
                totalc = 0;
                curc = 0;
                curn = 0;
                Task _cp = CopyTo(sourceDirectoryInfo, destinationDirectoryInfo, progressStatus);  // TODO: Add progress bar

                ProgressDialogWindow progressDialogW = new ProgressDialogWindow("Copy Progress", progressStatus, App.MainWindow);
                Task progressWindowTask = progressDialogW.ShowDialog(App.MainWindow);

                Dispatcher.UIThread.Post(async () =>
                {
                    try
                    {
                        await _cp;
                    }
                    catch (OperationCanceledException)
                    {
                        // handle canceled operation
                    }

                    // close the window
                    progressDialogW.Close();
                    await progressWindowTask;

                    // Auto Import = import + (per Settings) Auto Decode the recordings just
                    // copied in. Either way, surface the imported session in the browser.
                    var importedRaws = RecordingPipeline.FindRawBinFiles(destinationDirectoryInfo!.FullName);
                    if (importedRaws.Count > 0 && SettingsService.Current.Recordings.AutoDecodeOnImport)
                    {
                        DecodeOutcome outcome = await RunAutoDecodePipeline(importedRaws);

                        // Settings → "Delete raw .bin files after a successful import":
                        // reclaim space only when EVERY imported raw decoded cleanly, and
                        // only for files this import copied in — never pre-existing raws
                        // in the target, never anything on the source device drive.
                        if (outcome.Succeeded && SettingsService.Current.Recordings.DeleteRawAfterImport)
                        {
                            DeleteImportedRaws(importedRaws.Where(r => !preExistingRaws.Contains(r)).ToList());
                            LoadDataFolder(CurrentDataFolder);   // reflect the removals right away
                        }
                    }
                    else
                    {
                        LoadDataFolder(destinationDirectoryInfo.FullName);
                    }

                }, DispatcherPriority.Background);
            }
            else
            {
                result = false;
            }

            return await Task.FromResult(result);
        }

        #region DemonstrateEvents
        private string _progressUpdatedEventLast = "Never";
        private string _finishedEventLast = "Never";
        private string _cancelledEventLast = "Never";
        // Properties for the view and handler functions for the events to demonstrate the operation of the events.

        public string ProgressUpdatedEventLast
        {
            get => _progressUpdatedEventLast;
            set
            {
                _progressUpdatedEventLast = value;
                this.RaiseAndSetIfChanged(ref _progressUpdatedEventLast, value);
            }
        }

        public string FinishedEventLast
        {
            get => _finishedEventLast;
            set
            {
                _finishedEventLast = value;
                this.RaiseAndSetIfChanged(ref _finishedEventLast, value);
            }
        }

        public string CancelledEventLast
        {
            get => _cancelledEventLast;
            set
            {
                _cancelledEventLast = value;
                this.RaiseAndSetIfChanged(ref _cancelledEventLast, value);
            }
        }


        private int totaln = 0;
        private int totalc = 0;
        private int curn = 0;
        private int curc = 0;


        private void HandleCancelledEvent(IProgressStatus progressStatus) => CancelledEventLast = DateTime.Now.ToString();

        private void HandleFinishedEvent(IProgressStatus progressStatus) => FinishedEventLast = DateTime.Now.ToString();

        private void HandleProgessUpdatedEvent(IProgressStatus progressStatus) => ProgressUpdatedEventLast = DateTime.Now.ToString();
        #endregion

        /// <summary>A purely numeric folder name — the device's auto-incrementing
        /// 256-file chunk folders that are flattened away on import.</summary>
        private static bool IsChunkFolder(string name) =>
            name.Length > 0 && name.All(char.IsAsciiDigit);

        /// <summary>Delete raw .bin files this import session copied in, after they all
        /// decoded successfully (Settings → "Delete raw .bin files after a successful
        /// import"). Per-file failures are non-fatal; the count lands in the summary.</summary>
        private void DeleteImportedRaws(IReadOnlyList<string> rawPaths)
        {
            int deleted = 0;
            foreach (string p in rawPaths)
            {
                try
                {
                    File.Delete(p);
                    deleted++;
                }
                catch (Exception e) { Debug.WriteLine("Raw delete after import failed: " + e.Message); }
            }

            if (deleted > 0)
                LastSummary = (LastSummary.Length > 0 ? LastSummary + " " : "")
                              + $"Deleted {deleted} imported raw file{(deleted == 1 ? "" : "s")} (per Settings).";
        }

        private async Task<bool> CopyTo(DirectoryInfo source, DirectoryInfo destination, IProgressStatus progressStatus)
        {
            try
            {
                if (source.Exists)
                {
                    if (destination.Exists == false)
                    {
                        destination.Create();
                    }

                    curn = source.GetFiles().Length;
                    curc = 0;
                    totalc++;

                    bool overwriteall = false;

                    foreach (FileInfo fileInfo in source.GetFiles())
                    {
                        var to = Path.Combine(destination.FullName, fileInfo.Name);
                        bool overwrite = true;
                        bool abort = false;

                        curc++;

                        if (File.Exists(to) && overwriteall == false)
                        {
                            await Dispatcher.UIThread.InvokeAsync(async () =>
                            {
                                MessageBoxCustomParams parm = new()
                                {
                                    ButtonDefinitions = new List<ButtonDefinition>
                                    {
                                        new () { Name = "Yes All", IsDefault = true, },
                                        new () { Name = "Yes", },
                                        new () { Name = "No", },
                                        new () { Name = "Cancel", IsCancel=true }
                                    },
                                    ContentTitle = "Overwrite ?",
                                    Icon = Icon.Question,
                                    Topmost = true,
                                    ShowInCenter = true,
                                    SizeToContent = SizeToContent.WidthAndHeight,
                                    ContentHeader = "File Exists: " + fileInfo.Name,
                                    ContentMessage = to,
                                    CanResize = false,
                                    Markdown = false,
                                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                                };

                                var msgbox = MessageBoxManager.GetMessageBoxCustom(parm);
                                if (msgbox != null)
                                {
                                    string dialogm = await msgbox.ShowAsync();

                                    if (dialogm.Equals("Cancel")) { abort = true; }
                                    overwriteall = (dialogm.Equals("Yes All")) ? true : false;
                                    overwrite = (dialogm.Equals("Yes")) ? true : false;
                                }
                            }, DispatcherPriority.Input);
                        }

                        if (abort == true)
                        {
                            progressStatus.IsFinished = true;
                            return false;
                        }

                        progressStatus.Update("File Copy " + to, (int)((curc / curn) * 100.0), (int)((totalc / totaln) * 100.0));

                        try
                        {
                            fileInfo.CopyTo(to, overwrite || overwriteall);
                        }
                        catch (IOException ioecx)
                        {
                            if (overwrite = !false)
                            {
                                progressStatus.IsFinished = true;
                                return false;
                            }
                        }

                        await Task.Delay(100);
                    }

                    foreach (DirectoryInfo drs in source.GetDirectories())
                    {
                        // Per-sensor folders (gps, aud, imu, …) are preserved. The
                        // auto-incrementing chunk folders inside them (0, 1, 2, … with up
                        // to 256 files each — a FAT directory-size workaround on the
                        // device) are flattened away, so all of a sensor's recordings
                        // land directly in that sensor's folder.
                        DirectoryInfo sub = IsChunkFolder(drs.Name)
                            ? destination
                            : new DirectoryInfo(Path.Combine(destination.FullName, drs.Name));
                        if (await CopyTo(drs, sub, progressStatus) == false)
                        {
                            progressStatus.IsFinished = true;

                            return false;
                        }
                    }
                }

                progressStatus.Update("File Copy Done", (int)((curc / curn) * 100.0), (int)((totalc / totaln) * 100.0));
                await Task.Delay(3000);
                progressStatus.Ct.ThrowIfCancellationRequested();
                progressStatus.IsFinished = true;
                return true;
            }
            catch (Exception ex)
            {
                progressStatus.IsFinished = true;
                return false;
            }
        }



        private async Task<bool> ParseNanotagSnaps()
        {
            // GNSS decode runs through the cross-platform IGnssDecoder plugin (ASD.Gnss over
            // cg-gnss), so this is no longer Windows-only. Each picked folder becomes its own
            // non-modal job in the GNSS decode panel (Windows-copy-dialog style): long decodes
            // no longer block the UI, several can run at once, and progress / console output /
            // errors are shown live per job (full output also written to a log file). If no
            // decoder plugin is installed the job reports it. See docs/ARCHITECTURE.md.
            try
            {
                FolderPickerOpenOptions options = new()
                {
                    Title = "Select FOLDER(s) containing GPS snap .dat files to decode",
                    AllowMultiple = true,
                };

                IReadOnlyList<IStorageFolder> folders =
                    await App.AppTopLevel!.StorageProvider!.OpenFolderPickerAsync(options);

                int started = 0;
                foreach (IStorageFolder folder in folders)
                {
                    string? path = folder.TryGetLocalPath();
                    if (string.IsNullOrEmpty(path)) continue;

                    // Fire-and-forget: the panel owns progress/outcome; we don't await here so
                    // the picker returns immediately and jobs run concurrently.
                    DecodeJobManager.Instance.StartGnss(path!);
                    started++;
                }

                return started > 0;
            }
            catch (Exception e)
            {
                Debug.WriteLine("ParseNanotagSnaps failed: " + e);
                return false;
            }
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
