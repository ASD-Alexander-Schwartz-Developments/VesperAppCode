using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Logging;
using Avalonia.ReactiveUI;
using System;
using System.Reflection;
using Velopack;

namespace VesperApp
{
    internal class Program
    {
        public static MemoryLogger Log { get; private set; }

        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static void Main(string[] args)
        {
            // Logging is essential for debugging! Ideally you should write it to a file.
            Log = new MemoryLogger();

            VelopackApp.Build()
                .WithFirstRun((v) => { /* Your first run code here */ })
                .Run(Log);
            VelopackApp.Build().Run();
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
#if DEBUG
                .LogToTrace(LogEventLevel.Debug, LogArea.Property, LogArea.Binding)
#else
                .LogToTrace(LogEventLevel.Warning)
#endif
                .UseReactiveUI();
    }
}
