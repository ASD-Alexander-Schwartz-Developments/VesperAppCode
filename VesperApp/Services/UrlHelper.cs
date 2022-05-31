using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace VesperApp.Services
{
    internal class UrlHelper
    {
        public static void URL(string url) => Process.Start(new ProcessStartInfo()
        {
            FileName = url,
            UseShellExecute = true
        });

    }
}
