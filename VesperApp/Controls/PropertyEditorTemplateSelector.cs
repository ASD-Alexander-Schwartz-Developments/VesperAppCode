using Avalonia.Controls;
using Avalonia.Controls.Templates;
using VesperApp.ViewModels;

namespace VesperApp.Controls
{
    /// <summary>
    /// Chooses the editor template for a driver property from its
    /// <see cref="DriverPropertyViewModel.EditorKind"/>. Only the matching editor is
    /// ever instantiated, which is essential: if every editor were realised at once,
    /// the inactive ones (an empty ComboBox, an unchecked CheckBox, …) would push
    /// their default values back through their TwoWay bindings and clobber the real
    /// property value to 0 / null / unchecked.
    /// </summary>
    public class PropertyEditorTemplateSelector : IDataTemplate
    {
        public IDataTemplate? Options { get; set; }
        public IDataTemplate? Integer { get; set; }
        public IDataTemplate? Real { get; set; }
        public IDataTemplate? Boolean { get; set; }
        public IDataTemplate? Text { get; set; }

        public bool Match(object? data) => data is DriverPropertyViewModel;

        public Control? Build(object? data)
        {
            var kind = (data as DriverPropertyViewModel)?.EditorKind ?? PropertyEditorKind.Text;

            IDataTemplate? template = kind switch
            {
                PropertyEditorKind.Options => Options,
                PropertyEditorKind.Integer => Integer,
                PropertyEditorKind.Real => Real,
                PropertyEditorKind.Boolean => Boolean,
                _ => Text,
            };

            template ??= Text;

            var control = template?.Build(data);
            if (control != null)
                control.DataContext = data;

            return control;
        }
    }
}
