using ASD.BaseStation;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<BaseStationOptions>(builder.Configuration.GetSection("BaseStation"));
builder.Services.AddHostedService<DownloadWorker>();

// Open-core platform seam: the daemon shares the same platform services as the desktop.
// Discover proprietary plugins (e.g. ASD.PlmClient for backend upload, ASD.Gnss) from
// the plugins/ folder; with none present the stub services stand and downloads stay on
// local storage. See docs/ARCHITECTURE.md.
using (ILoggerFactory bootstrapLog = LoggerFactory.Create(b => b.AddSimpleConsole()))
    ASD.Platform.PluginLoader.LoadFrom(log: bootstrapLog.CreateLogger("PluginLoader"));
ASD.Platform.PlatformServices.Initialize();

IHost host = builder.Build();
host.Run();
