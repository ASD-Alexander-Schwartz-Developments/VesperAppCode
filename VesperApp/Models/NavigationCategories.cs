using Avalonia.Controls.Templates;
using Avalonia.Metadata;
using FluentAvalonia.UI.Controls;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace VesperApp.Models
{
    public abstract class CategoryBase : ReactiveUI.ReactiveObject { }

    public class Category : CategoryBase
    {
        public string? Name { get; set; }
        public string? ToolTip { get; set; }
        public Symbol Icon { get; set; }
        public Type Page { get; set; }
        public object? DataContext { get; set; }

        /// <summary>Shown in the navigation menu (e.g. Nanotag Console only while a Nanotag is connected).</summary>
        public bool IsVisible
        {
            get => _isVisible;
            set => ReactiveUI.IReactiveObjectExtensions.RaiseAndSetIfChanged(this, ref _isVisible, value);
        }
        private bool _isVisible = true;
    }

    public class Separator : CategoryBase
    {

    }

    public class MenuItemTemplateSelector : DataTemplateSelector
    {
        [Content]
        public IDataTemplate ItemTemplate { get; set; }

        public IDataTemplate SeparatorTemplate { get; set; }

        protected override IDataTemplate SelectTemplateCore(object item)
        {
            return item is Separator ? SeparatorTemplate : ItemTemplate;
        }
    }

}
