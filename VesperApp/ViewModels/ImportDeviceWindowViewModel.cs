using Avalonia.Platform.Storage;
using Avalonia.Threading;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using VesperApp.Services;

namespace VesperApp.ViewModels
{
    /// <summary>What the import dialog hands back: where to copy from and to.</summary>
    public sealed record ImportDeviceRequest(string SourcePath, string TargetPath);

    /// <summary>A removable drive candidate shown in the import dialog.</summary>
    public sealed class ImportDriveInfo : IEquatable<ImportDriveInfo>
    {
        public string RootPath { get; init; } = string.Empty;   // e.g. "E:\"
        public string VolumeLabel { get; init; } = string.Empty;
        public string? Uid { get; init; }                        // from UID.txt on the drive root
        public int RecordingCount { get; init; }                 // raw *.bin files on the drive
        public DateTime? RecordingDate { get; init; }            // newest raw file write time
        public string SizeText { get; init; } = string.Empty;

        public string Title => $"{RootPath}  {(string.IsNullOrEmpty(VolumeLabel) ? "(no label)" : VolumeLabel)}";
        public string DeviceText => Uid != null ? $"Device ID: {Uid}" : "Device ID: unknown (no UID.txt)";
        public string ContentText => RecordingCount > 0
            ? $"{RecordingCount} recording file{(RecordingCount == 1 ? "" : "s")}, {SizeText}" +
              (RecordingDate != null ? $" — recorded {RecordingDate:yyyy-MM-dd HH:mm}" : "")
            : $"No recordings found, {SizeText}";

        public bool Equals(ImportDriveInfo? other) =>
            other != null && RootPath == other.RootPath && Uid == other.Uid &&
            RecordingCount == other.RecordingCount && RecordingDate == other.RecordingDate;
        public override bool Equals(object? obj) => Equals(obj as ImportDriveInfo);
        public override int GetHashCode() => RootPath.GetHashCode();
    }

    /// <summary>
    /// Guided import dialog: step 1 pick the device drive (removable drives are
    /// auto-detected and identified via the UID.txt the firmware writes on the drive),
    /// step 2 confirm the target folder, pre-filled as
    /// <c>&lt;working dir&gt;/&lt;device id&gt;/&lt;recording date&gt;</c>.
    /// Everything on the drive is imported, including the configuration file and
    /// UID.txt, so each import session is self-documenting.
    /// </summary>
    public class ImportDeviceWindowViewModel : ViewModelBase
    {
        private readonly System.Timers.Timer _scanTimer;
        private bool _scanning;

        public ObservableCollection<ImportDriveInfo> Drives { get; } = new();

        public ImportDeviceWindowViewModel()
        {
            BrowseSourceCommand = ReactiveCommand.CreateFromTask(BrowseSource);
            BrowseTargetCommand = ReactiveCommand.CreateFromTask(BrowseTarget);

            _scanTimer = new System.Timers.Timer(2500) { AutoReset = true };
            _scanTimer.Elapsed += (_, _) => ScanDrives();
            _scanTimer.Start();
            ScanDrives();
        }

        public ICommand BrowseSourceCommand { get; }
        public ICommand BrowseTargetCommand { get; }

        public ImportDriveInfo? SelectedDrive
        {
            get => _selectedDrive;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedDrive, value);
                if (value != null)
                {
                    SourcePath = value.RootPath;
                    TargetPath = BuildSuggestedTarget(value.Uid ?? Nonempty(value.VolumeLabel, "UNKNOWN"), value.RecordingDate);
                }
            }
        }
        private ImportDriveInfo? _selectedDrive;

        /// <summary>The folder that will be copied. Set by drive selection or manual browse.</summary>
        public string SourcePath
        {
            get => _sourcePath;
            set { this.RaiseAndSetIfChanged(ref _sourcePath, value); this.RaisePropertyChanged(nameof(CanImport)); }
        }
        private string _sourcePath = string.Empty;

        /// <summary>Editable import destination, pre-filled with the structured default.</summary>
        public string TargetPath
        {
            get => _targetPath;
            set { this.RaiseAndSetIfChanged(ref _targetPath, value); this.RaisePropertyChanged(nameof(CanImport)); }
        }
        private string _targetPath = string.Empty;

        public bool CanImport =>
            SourcePath.Length > 0 && Directory.Exists(SourcePath) && TargetPath.Trim().Length > 0;

        public ImportDeviceRequest? BuildRequest() =>
            CanImport ? new ImportDeviceRequest(SourcePath, TargetPath.Trim()) : null;

        public void TerminateScan()
        {
            _scanTimer.Stop();
            _scanTimer.Dispose();
        }

        // ───────────────────────── drive detection ─────────────────────────

        private void ScanDrives()
        {
            if (_scanning) return;
            _scanning = true;
            try
            {
                // Removable drives only; devices identify themselves via the UID.txt the
                // firmware writes on the drive root. VESPER-labelled / UID-bearing drives
                // sort first so the right drive is one click (or zero) away.
                List<ImportDriveInfo> found = DriveInfo.GetDrives()
                    .Where(d => d.DriveType == DriveType.Removable && d.IsReady)
                    .Select(Describe)
                    .OrderByDescending(d => d.Uid != null)
                    .ThenByDescending(d => d.RecordingCount)
                    .ToList();

                Dispatcher.UIThread.Post(() =>
                {
                    foreach (ImportDriveInfo d in found)
                        if (!Drives.Contains(d))
                        {
                            // Replace a stale entry for the same root (content changed) or add.
                            ImportDriveInfo? stale = Drives.FirstOrDefault(x => x.RootPath == d.RootPath);
                            if (stale != null) Drives[Drives.IndexOf(stale)] = d;
                            else Drives.Add(d);
                        }

                    foreach (ImportDriveInfo d in Drives.ToArray())
                        if (found.All(x => x.RootPath != d.RootPath))
                            Drives.Remove(d);

                    // Zero-click default: pre-select the most likely device drive.
                    if (SelectedDrive == null || Drives.All(x => x.RootPath != SelectedDrive.RootPath))
                        SelectedDrive = Drives.FirstOrDefault();
                });
            }
            catch { /* drive may vanish mid-scan; next tick recovers */ }
            finally { _scanning = false; }
        }

        private static ImportDriveInfo Describe(DriveInfo d)
        {
            (string? uid, int count, DateTime? newest) = Probe(d.RootDirectory);

            double usedGb = 0;
            try { usedGb = (d.TotalSize - d.TotalFreeSpace) / 1e9; } catch { }

            return new ImportDriveInfo
            {
                RootPath = d.RootDirectory.FullName,
                VolumeLabel = Safe(() => d.VolumeLabel) ?? string.Empty,
                Uid = uid,
                RecordingCount = count,
                RecordingDate = newest,
                SizeText = $"{usedGb:0.0} GB used",
            };
        }

        /// <summary>Identify a device folder: UID from the firmware-written UID.txt,
        /// recording count and the newest recording timestamp.</summary>
        private static (string? Uid, int Count, DateTime? Newest) Probe(DirectoryInfo root)
        {
            string? uid = null;
            int count = 0;
            DateTime? newest = null;

            try
            {
                string uidFile = Path.Combine(root.FullName, "UID.txt");
                if (File.Exists(uidFile))
                {
                    string line = File.ReadLines(uidFile).FirstOrDefault(l => l.Trim().Length > 0)?.Trim() ?? "";
                    if (line.Contains('=')) line = line.Split('=', 2)[1].Trim();
                    line = string.Concat(line.Where(c => !Path.GetInvalidFileNameChars().Contains(c)));
                    if (line.Length > 0) uid = line;
                }

                foreach (FileInfo f in root.EnumerateFiles("*.bin", SearchOption.AllDirectories))
                {
                    count++;
                    if (newest == null || f.LastWriteTime > newest) newest = f.LastWriteTime;
                }
            }
            catch { /* unreadable drive — show what we have */ }

            return (uid, count, newest);
        }

        private static string? Safe(Func<string> f) { try { return f(); } catch { return null; } }
        private static string Nonempty(string s, string fallback) => s.Trim().Length > 0 ? s.Trim() : fallback;

        /// <summary>Structured default target: working dir / device id / recording date.
        /// Uses the newest recording's timestamp (what the data IS) rather than the import
        /// time, falling back to now for an empty drive.</summary>
        private static string BuildSuggestedTarget(string deviceId, DateTime? recordingDate) =>
            Path.Combine(SettingsService.Instance.ResolveWorkingDirectory(),
                         deviceId,
                         (recordingDate ?? DateTime.Now).ToString("yyyy-MM-dd_HH-mm-ss"));

        // ───────────────────────── manual fallbacks ─────────────────────────

        private async Task BrowseSource()
        {
            string? path = await PickFolder("Select the device drive / folder to import from");
            if (path != null)
            {
                SelectedDrive = null;
                SourcePath = path;
                // Identify the folder the same way a drive is identified.
                DirectoryInfo di = new(path);
                (string? uid, _, DateTime? newest) = Probe(di);
                TargetPath = BuildSuggestedTarget(uid ?? di.Name, newest);
            }
        }

        private async Task BrowseTarget()
        {
            string? path = await PickFolder("Select the folder to import into");
            if (path != null)
                TargetPath = path;
        }

        private static async Task<string?> PickFolder(string title)
        {
            try
            {
                IStorageProvider? sp = App.AppTopLevel?.StorageProvider;
                if (sp == null) return null;

                IStorageFolder? start = await sp.TryGetFolderFromPathAsync(
                    SettingsService.Instance.ResolveWorkingDirectory());
                IReadOnlyList<IStorageFolder> folders = await sp.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = title,
                    AllowMultiple = false,
                    SuggestedStartLocation = start,
                });
                return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
            }
            catch { return null; }
        }
    }
}
