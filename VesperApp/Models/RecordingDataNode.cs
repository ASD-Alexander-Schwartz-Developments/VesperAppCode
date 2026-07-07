using System.Collections.ObjectModel;
using FluentAvalonia.UI.Controls;
using ReactiveUI;

namespace VesperApp.Models
{
    /// <summary>
    /// One node in the Recordings tab data browser — a folder or a recording file under
    /// the working directory. Reactive so the live (FileSystemWatcher-driven) refresh can
    /// update nodes in place, preserving the tree's expansion and selection state.
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

        /// <summary>Right-hand caption (size for files, category/item count for folders).</summary>
        public string? Detail
        {
            get => _detail;
            set => this.RaiseAndSetIfChanged(ref _detail, value);
        }
        private string? _detail;

        public Symbol Icon
        {
            get => _icon;
            set => this.RaiseAndSetIfChanged(ref _icon, value);
        }
        private Symbol _icon = Symbol.Document;

        public bool IsFile { get; set; }

        public ObservableCollection<RecordingDataNode> Children { get; } = new();
    }
}
