using ASDWaveLib;
using Avalonia.Controls;
using Avalonia.Controls.Selection;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using MsBox.Avalonia;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Enums;
using MsBox.Avalonia.Models;
using Octokit;
using ReactiveUI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using VesperApp.Controls;
using VesperApp.Models;
using VesperApp.Services;
using VesperApp.Views;

namespace VesperApp.ViewModels
{
    public class FirmwareUpgradesViewModel : ViewModelBase
    {
        private string PAT { get; } = "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";
        private string Owner { get; } = "ASD-Alexander-Schwartz-Developments";
        private string Repo { get; } = "VesperU5";
        public ICommand? RefreshReleasesCommand { get; }
        public ObservableCollection<Release> Releases { get; } = new();
        private readonly GitHubReleaseService gitHubReleaseService;

        public SelectionModel<Release?>? SelectedFirmwareRelease { get; }


        public bool BinaryParserIsRunning
        {
            get => binaryParserIsRunning;
            set => this.RaiseAndSetIfChanged(ref binaryParserIsRunning, value);
        }

        private bool binaryParserIsRunning = false;



        public FirmwareUpgradesViewModel()
        {
            SelectedFirmwareRelease = new SelectionModel<Release?>()
            {
                SingleSelect = true,
            };
            Releases = new ObservableCollection<Release>();
            #region Updater Commands

            RefreshReleasesCommand = ReactiveCommand.CreateFromTask(RunReleasesRefresh);

            #endregion

            gitHubReleaseService = new(owner: Owner, repo: Repo, token: PAT);
        }



        private async Task<bool> RunReleasesRefresh()
        {
            if (gitHubReleaseService != null)
            {
                Releases.Clear();
                var releases = await gitHubReleaseService.GetReleasesAsync();
                foreach (var release in releases)
                    Releases.Add(release);

                return true;
            }
            return false;
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
