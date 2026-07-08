using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Platform.Storage;
using ReactiveUI;
using VesperApp.Models;
using VesperApp.Services;

namespace VesperApp.ViewModels
{
    /// <summary>
    /// Backs the Settings tab. Edits a working copy of <see cref="AppConfig"/> (the live
    /// instance held by <see cref="SettingsService"/>) and persists it on Save. The
    /// working-directory section is fully wired (it drives the startup auto-open in the
    /// Recordings browser); the rest are persisted preferences the pipeline reads.
    /// </summary>
    public sealed class SettingsViewModel : ViewModelBase
    {
        private readonly AppConfig _cfg;

        public SettingsViewModel()
        {
            _cfg = SettingsService.Current;

            _workingDirectory = SettingsService.Instance.ResolveWorkingDirectory();
            _openWorkingDirOnStartup = _cfg.Workspace.OpenWorkingDirOnStartup;
            _autoDecodeOnImport = _cfg.Recordings.AutoDecodeOnImport;
            _hideIntermediateFiles = _cfg.Recordings.HideIntermediateFiles;
            _deleteRawAfterImport = _cfg.Recordings.DeleteRawAfterImport;

            BrowseWorkingDirectoryCommand = ReactiveCommand.CreateFromTask(BrowseWorkingDirectory);
            SaveCommand = ReactiveCommand.Create(Save);
            OpenConfigFolderCommand = ReactiveCommand.Create(OpenConfigFolder);
        }

        // ── Workspace ──
        private string _workingDirectory;
        public string WorkingDirectory
        {
            get => _workingDirectory;
            set => this.RaiseAndSetIfChanged(ref _workingDirectory, value);
        }

        private bool _openWorkingDirOnStartup;
        public bool OpenWorkingDirOnStartup
        {
            get => _openWorkingDirOnStartup;
            set => this.RaiseAndSetIfChanged(ref _openWorkingDirOnStartup, value);
        }

        // ── Recordings ──
        private bool _autoDecodeOnImport;
        public bool AutoDecodeOnImport
        {
            get => _autoDecodeOnImport;
            set => this.RaiseAndSetIfChanged(ref _autoDecodeOnImport, value);
        }

        private bool _hideIntermediateFiles;
        public bool HideIntermediateFiles
        {
            get => _hideIntermediateFiles;
            set => this.RaiseAndSetIfChanged(ref _hideIntermediateFiles, value);
        }

        private bool _deleteRawAfterImport;
        public bool DeleteRawAfterImport
        {
            get => _deleteRawAfterImport;
            set => this.RaiseAndSetIfChanged(ref _deleteRawAfterImport, value);
        }

        // ── Status / info ──
        public string ConfigPath => SettingsService.ConfigPath;

        private string _status = string.Empty;
        public string Status
        {
            get => _status;
            set => this.RaiseAndSetIfChanged(ref _status, value);
        }

        public ICommand BrowseWorkingDirectoryCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand OpenConfigFolderCommand { get; }

        private async Task BrowseWorkingDirectory()
        {
            try
            {
                var storage = App.AppTopLevel?.StorageProvider;
                if (storage is null) return;

                var folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = "Select the default working directory",
                    AllowMultiple = false,
                });

                string? path = folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
                if (!string.IsNullOrEmpty(path))
                    WorkingDirectory = path!;
            }
            catch (Exception e) { Debug.WriteLine(e); }
        }

        private void Save()
        {
            _cfg.Workspace.WorkingDirectory = string.IsNullOrWhiteSpace(WorkingDirectory) ? null : WorkingDirectory;
            _cfg.Workspace.OpenWorkingDirOnStartup = OpenWorkingDirOnStartup;
            _cfg.Recordings.AutoDecodeOnImport = AutoDecodeOnImport;
            _cfg.Recordings.HideIntermediateFiles = HideIntermediateFiles;
            _cfg.Recordings.DeleteRawAfterImport = DeleteRawAfterImport;

            SettingsService.Instance.Save();
            Status = "Saved. The working directory opens automatically on the next launch.";
        }

        private void OpenConfigFolder()
        {
            try
            {
                Directory.CreateDirectory(SettingsService.ConfigDirectory);
                Process.Start(new ProcessStartInfo
                {
                    FileName = SettingsService.ConfigDirectory,
                    UseShellExecute = true,
                });
            }
            catch (Exception e) { Debug.WriteLine(e); }
        }
    }
}
