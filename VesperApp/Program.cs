using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Logging;
using Avalonia.ReactiveUI;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Velopack;

namespace VesperApp
{
    internal class Program
    {
        public static MemoryLogger Log { get; private set; }

        /// <summary>Per-user log folder — always writable, unlike the process CWD (an
        /// installed app can be launched with CWD anywhere, incl. read-only locations).</summary>
        public static string LogDir
        {
            get
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "VesperApp", "logs");
                try { Directory.CreateDirectory(dir); return dir; }
                catch { return Path.GetTempPath(); }
            }
        }

        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static void Main(string[] args)
        {
            // Avalonia is wired to LogToTrace, so the listener must never be able to
            // crash startup: fixed culture-safe name, writable folder, and a swallow —
            // the app must run even if logging can't.
            try
            {
                string logname = Path.Combine(LogDir, "VesperApp_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm") + ".log");
                var lst = new TextWriterTraceListener(logname, "VesperAppLogListener")
                {
                    TraceOutputOptions = TraceOptions.DateTime
                };
                Trace.Listeners.Add(lst);
                Trace.AutoFlush = true;
            }
            catch { /* logging must never prevent startup */ }

            // Logging is essential for debugging! Ideally you should write it to a file.
            Log = new MemoryLogger();

            Log.LogUpdated += Log_LogUpdated;

            AppDomain.CurrentDomain.UnhandledException +=
                (_, e) => ReportCrash(e.ExceptionObject as Exception, "unhandled exception");
            System.Threading.Tasks.TaskScheduler.UnobservedTaskException +=
                (_, e) => { try { Trace.TraceError("Unobserved task exception: " + e.Exception); } catch { } };

            try
            {
                if (Design.IsDesignMode == false)
                {
                    VelopackApp.Build().Run();
                }
                BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

                Trace.TraceInformation("VesperApp Opened");
            }
            catch (Exception ex)
            {
                ReportCrash(ex, "startup");
                throw;
            }
            finally
            {
                // You must close or flush the trace to empty the output buffer.
                Trace.Flush();
            }
        }

        /// <summary>Last-resort crash reporting: write the full exception to a crash file
        /// and (on Windows) show a native message box — the one UI primitive that needs
        /// nothing initialized. Without this a startup failure is completely silent.</summary>
        private static void ReportCrash(Exception? ex, string origin)
        {
            string detail = $"VesperApp {Assembly.GetExecutingAssembly().GetName().Version} crash ({origin}) at {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n{ex}";
            string path = "";
            try
            {
                path = Path.Combine(LogDir, $"crash-{DateTime.Now:yyyyMMdd-HHmmss}.log");
                File.WriteAllText(path, detail + Environment.NewLine);
            }
            catch { }
            try { Trace.TraceError(detail); Trace.Flush(); } catch { }

            if (OperatingSystem.IsWindows())
            {
                try
                {
                    _ = MessageBoxW(IntPtr.Zero,
                        "VesperApp failed to start.\n\n" +
                        (ex?.GetBaseException().Message ?? "Unknown error") +
                        (path.Length > 0 ? $"\n\nDetails were written to:\n{path}" : ""),
                        "VesperApp", 0x10 /* MB_ICONERROR */);
                }
                catch { }
            }
            else
            {
                try { Console.Error.WriteLine(detail); } catch { }
            }
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);

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
/*
 * 
 * Release:
 * dotnet publish -c Release -r win-x64 -o publish
 * vpk pack --packId VesperApp --packVersion 1.0.28 --packDir publish --mainExe VesperApp.exe --channel win-x64-stable --icon VesperApp\Assets\bat.ico
 * 
 * */