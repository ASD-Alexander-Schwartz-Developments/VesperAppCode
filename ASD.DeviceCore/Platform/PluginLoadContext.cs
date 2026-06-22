using System;
using System.Reflection;
using System.Runtime.Loader;

namespace ASD.Platform
{
    /// <summary>
    /// Isolated load context for a proprietary plugin assembly. Resolves the plugin's
    /// own managed and native dependencies from its folder, but lets shared contract
    /// assemblies (anything already loaded in the default context, e.g.
    /// <c>ASD.Contracts</c>) resolve from the host — so a plugin's
    /// <c>IGnssDecoder</c> is the <i>same</i> type the shell checks against. This is
    /// what makes a binary plugin (whose source is not in this repo) bind cleanly.
    /// </summary>
    internal sealed class PluginLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver;

        public PluginLoadContext(string pluginPath) : base(isCollectible: false)
        {
            _resolver = new AssemblyDependencyResolver(pluginPath);
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            // If the assembly is already loaded in the default context (the contract
            // assemblies, the BCL), return null so it unifies there — preserving type
            // identity across the plugin boundary.
            foreach (Assembly a in Default.Assemblies)
                if (a.GetName().Name == assemblyName.Name)
                    return null;

            string? path = _resolver.ResolveAssemblyToPath(assemblyName);
            return path != null ? LoadFromAssemblyPath(path) : null;
        }

        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            string? path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            return path != null ? LoadUnmanagedDllFromPath(path) : IntPtr.Zero;
        }
    }
}
