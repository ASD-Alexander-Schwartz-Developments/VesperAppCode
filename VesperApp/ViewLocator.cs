using Avalonia.Controls;
using Avalonia.Controls.Templates;
using System;
using VesperApp.ViewModels;

namespace VesperApp
{
    public class ViewLocator : IDataTemplate
    {
        public Control Build(object? data)
        {
            var name = data?.GetType().FullName!.Replace("ViewModel", "View");

            Type? type = null;
            if(name is not null)
            {
                type = Type.GetType(name);
            }

            if (type is not null)
            {
                return (Control)Activator.CreateInstance(type)!;
            }
            else
            {
                return new TextBlock { Text = "Not Found: " + name };
            }
        }

        public bool Match(object? data)
        {
            return data is ViewModelBase;
        }
    }
}
