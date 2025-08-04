using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Logging;
using Avalonia.ReactiveUI;
using System;
using System.Diagnostics;
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
            string logname = "VesperApp" + "_" + DateTime.Now.ToShortDateString() + "_" + DateTime.Now.ToShortTimeString() + ".log";
            logname = logname.Replace('/', '_');
            logname = logname.Replace(':', '_');

            TextWriterTraceListener lst = new TextWriterTraceListener(logname, "VesperAppLogListener");
            lst.TraceOutputOptions = TraceOptions.DateTime;

            Trace.Listeners.Add(lst);
            Trace.AutoFlush = true;
            // Logging is essential for debugging! Ideally you should write it to a file.
            Log = new MemoryLogger();

            Log.LogUpdated += Log_LogUpdated;

            if (Design.IsDesignMode == false)
            {
                VelopackApp.Build().Run();
            }
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

            Trace.TraceInformation("VesperApp Opened");
            // You must close or flush the trace to empty the output buffer.
            Trace.Flush();
        }

        private static void Log_LogUpdated(object? sender, LogUpdatedEventArgs e)
        {
            Trace.TraceInformation(e.Text);
            Trace.Flush();
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
#if DEBUG
                .LogToTrace(LogEventLevel.Debug, LogArea.Property, LogArea.Binding)
#else
                .LogToTrace(LogEventLevel.Information)
#endif
                .UseReactiveUI();
    }
}
