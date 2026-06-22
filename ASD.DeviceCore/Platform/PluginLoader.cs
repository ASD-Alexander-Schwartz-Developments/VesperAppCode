using System;
using System.IO;
using System.Linq;
using System.Reflection;
using ASD.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ASD.Platform
{
    /// <summary>
    /// Discovers proprietary platform plugins at runtime and binds them into
    /// <see cref="PlatformServices"/>. This is the open-core delivery mechanism: the
    /// shell and daemon ship only the contracts + stubs; the closed implementations
    /// (e.g. <c>ASD.Gnss</c> over cg-gnss, <c>ASD.PlmClient</c>) are dropped into a
    /// <c>plugins/</c> folder — whose <b>source is not in this repo</b> — and loaded
    /// here. With no plugins present, the stub bindings stand and those features are
    /// simply unavailable. See docs/ARCHITECTURE.md.
    /// </summary>
    public static class PluginLoader
    {
        /// <summary>Default plugins folder next to the running binary.</summary>
        public static string DefaultPluginsDir => Path.Combine(AppContext.BaseDirectory, "plugins");

        /// <summary>
        /// Load every <c>*.dll</c> under <paramref name="pluginsDir"/> and bind any
        /// platform service implementations they expose (a public type with a
        /// parameterless constructor implementing <see cref="IGnssDecoder"/>,
        /// <see cref="IPlmClient"/>, or <see cref="IEntitlementProvider"/>). Must be
        /// called before <see cref="PlatformServices.Initialize"/>. Returns the number
        /// of services bound.
        /// </summary>
        public static int LoadFrom(string? pluginsDir = null, ILogger? log = null)
        {
            log ??= NullLogger.Instance;
            pluginsDir ??= DefaultPluginsDir;

            if (!Directory.Exists(pluginsDir))
            {
                log.LogInformation("No plugins folder at {Dir}; running with stub platform services.", pluginsDir);
                return 0;
            }

            int bound = 0;
            foreach (string dll in Directory.EnumerateFiles(pluginsDir, "*.dll", SearchOption.AllDirectories))
            {
                try
                {
                    bound += LoadPlugin(dll, log);
                }
                catch (Exception ex)
                {
                    log.LogWarning(ex, "Failed to load plugin {Dll}.", dll);
                }
            }
            log.LogInformation("Plugin load complete: {Count} service(s) bound from {Dir}.", bound, pluginsDir);
            return bound;
        }

        private static int LoadPlugin(string dllPath, ILogger log)
        {
            var ctx = new PluginLoadContext(dllPath);
            Assembly asm = ctx.LoadFromAssemblyPath(dllPath);
            int bound = 0;

            foreach (Type t in SafeGetTypes(asm))
            {
                if (t.IsAbstract || t.IsInterface || t.GetConstructor(Type.EmptyTypes) == null)
                    continue;

                if (typeof(IGnssDecoder).IsAssignableFrom(t))
                {
                    PlatformServices.Bind(gnss: (IGnssDecoder)Activator.CreateInstance(t)!);
                    log.LogInformation("Bound IGnssDecoder from {Type} ({Dll}).", t.FullName, Path.GetFileName(dllPath));
                    bound++;
                }
                else if (typeof(IPlmClient).IsAssignableFrom(t))
                {
                    PlatformServices.Bind(plm: (IPlmClient)Activator.CreateInstance(t)!);
                    log.LogInformation("Bound IPlmClient from {Type} ({Dll}).", t.FullName, Path.GetFileName(dllPath));
                    bound++;
                }
                else if (typeof(IEntitlementProvider).IsAssignableFrom(t))
                {
                    PlatformServices.Bind(entitlements: (IEntitlementProvider)Activator.CreateInstance(t)!);
                    log.LogInformation("Bound IEntitlementProvider from {Type} ({Dll}).", t.FullName, Path.GetFileName(dllPath));
                    bound++;
                }
            }
            return bound;
        }

        private static Type[] SafeGetTypes(Assembly asm)
        {
            try { return asm.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t != null).ToArray()!; }
        }
    }
}
