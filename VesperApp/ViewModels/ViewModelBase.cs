using ReactiveUI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Runtime.CompilerServices;
using Avalonia.Platform;
using System.IO;

namespace VesperApp.ViewModels
{
    public class ViewModelBase : ReactiveObject
    {
        protected string GetAssemblyResource(string name)
        {
            using (var stream = AssetLoader.Open(new Uri(name)))
            using (StreamReader reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }
    }


    public class MainPageViewModelBase : ViewModelBase
    {
        public string NavHeader { get; set; }

        public string IconKey { get; set; }

        public bool ShowsInFooter { get; set; }
    }

}
