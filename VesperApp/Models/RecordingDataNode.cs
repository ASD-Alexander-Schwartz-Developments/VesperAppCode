using System.Collections.ObjectModel;
using FluentAvalonia.UI.Controls;

namespace VesperApp.Models
{
    /// <summary>
    /// One node in the Recordings tab data browser — either a sensor-category group
    /// or a decoded/raw file under it. Built from the local import/decode folder.
    /// </summary>
    public class RecordingDataNode
    {
        public string Name { get; set; } = string.Empty;

        /// <summary>Absolute path; for files this is what "Open" launches. Null only for synthetic groups.</summary>
        public string? FullPath { get; set; }

        /// <summary>Right-hand caption (size for files, item count for groups).</summary>
        public string? Detail { get; set; }

        public Symbol Icon { get; set; } = Symbol.Document;

        public bool IsFile { get; set; }

        public ObservableCollection<RecordingDataNode> Children { get; } = new();
    }
}
