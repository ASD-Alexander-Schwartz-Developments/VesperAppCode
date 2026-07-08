using System;
using System.Collections.ObjectModel;
using FluentAvalonia.UI.Controls;
using ReactiveUI;

namespace VesperApp.Models
{
    /// <summary>
    /// One node in the Recordings tab data browser — a folder or a recording file under
    /// the working directory. Reactive so the live (FileSystemWatcher-driven) refresh can
    /// update nodes in place, preserving the tree's expansion and selection state.
    /// Column values (Kind / Size / Modified) feed the browser's TreeDataGrid.
    /// </summary>
    public class RecordingDataNode : ReactiveObject
    {
        public string Name
        {
            get => _name;
            set => this.RaiseAndSetIfChanged(ref _name, value);
        }
        private string _name = string.Empty;

        /// <summary>Absolute path; for files this is what "Open" launches. Null only for synthetic groups.</summary>
        public string? FullPath { get; set; }

        /// <summary>Type column: sensor/category label for folders and raw recordings,
        /// output kind for decoded files.</summary>
        public string? Kind
        {
            get => _kind;
            set => this.RaiseAndSetIfChanged(ref _kind, value);
        }
        private string? _kind;

        /// <summary>Size column display: human size for files, item count for folders.</summary>
        public string? SizeText
        {
            get => _sizeText;
            set => this.RaiseAndSetIfChanged(ref _sizeText, value);
        }
        private string? _sizeText;

        /// <summary>Sort key behind <see cref="SizeText"/> (bytes for files, item count for folders).</summary>
        public long SizeSort { get; set; }

        /// <summary>Last write time (files; null when unknown).</summary>
        public DateTime? Modified
        {
            get => _modified;
            set
            {
                this.RaiseAndSetIfChanged(ref _modified, value);
                this.RaisePropertyChanged(nameof(ModifiedText));
            }
        }
        private DateTime? _modified;

        public string ModifiedText => Modified?.ToString("yyyy-MM-dd HH:mm") ?? string.Empty;

        public Symbol Icon
        {
            get => _icon;
            set => this.RaiseAndSetIfChanged(ref _icon, value);
        }
        private Symbol _icon = Symbol.Document;

        public bool IsFile { get; set; }

        /// <summary>Tree expansion state, bound TwoWay by the browser so it survives the
        /// live in-place refresh and can be toggled programmatically (folder double-click).</summary>
        public bool IsExpanded
        {
            get => _isExpanded;
            set => this.RaiseAndSetIfChanged(ref _isExpanded, value);
        }
        private bool _isExpanded;

        public ObservableCollection<RecordingDataNode> Children { get; } = new();
    }
}
