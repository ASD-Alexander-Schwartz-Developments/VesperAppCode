using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
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
    /// <para>
    /// Two folders are searched, in precedence order: the per-user <see cref="DataPluginsDir"/>
    /// (where independently-updated packs install — a Velopack shell update never touches it),
    /// then the <see cref="BundledPluginsDir"/> next to the binary (dev / bundled builds). A
    /// pack may ship a <c>plugin.json</c> (<see cref="PluginManifest"/>); when present, the
    /// loader gates the pack on contract ABI (<see cref="ContractsAbi"/>) and platform before
    /// binding, so a mismatched pack is rejected with a clear log line instead of failing later.
    /// </para>
    /// </summary>
    public static class PluginLoader
    {
        /// <summary>Per-user plugins folder that survives shell self-updates; this is where
        /// independently-updated packs install (<c>%LOCALAPPDATA%/VesperApp/plugins</c> on
        /// Windows, the platform-equivalent local app-data path elsewhere).</summary>
        public static string DataPluginsDir => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VesperApp", "plugins");

        /// <summary>Plugins folder next to the running binary (dev / bundled builds). On a
        /// Velopack install this lives in the managed app directory.</summary>
        public static string BundledPluginsDir => Path.Combine(AppContext.BaseDirectory, "plugins");

        /// <summary>Back-compat alias: now points at the user-data location packs update into.</summary>
        public static string DefaultPluginsDir => DataPluginsDir;

        /// <summary>
        /// Load plugins and bind any platform service implementations they expose (a public
        /// type with a parameterless constructor implementing <see cref="IGnssDecoder"/>,
        /// <see cref="IPlmClient"/>, or <see cref="IEntitlementProvider"/>). With no argument,
        /// scans <see cref="DataPluginsDir"/> then <see cref="BundledPluginsDir"/> (data folder
        /// wins for a given DLL name). Must be called before
        /// <see cref="PlatformServices.Initialize"/>. Returns the number of services bound.
        /// </summary>
        public static int LoadFrom(string? pluginsDir = null, ILogger? log = null)
        {
            log ??= NullLogger.Instance;

            IEnumerable<string> dirs = pluginsDir != null
                ? new[] { pluginsDir }
                : new[] { DataPluginsDir, BundledPluginsDir };

            int bound = 0;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase); // DLL file names already handled

            foreach (string dir in dirs)
            {
                if (!Directory.Exists(dir))
                {
                    log.LogInformation("No plugins folder at {Dir}.", dir);
                    continue;
                }

                // Repair packs whose native executables lost their execute bit in
                // zip transit — otherwise Process.Start fails with EACCES on Unix.
                EnsureUnixExecutables(dir, log);

                foreach (string dll in Directory.EnumerateFiles(dir, "*.dll", SearchOption.AllDirectories))
                {
                    // A higher-precedence folder already provided this DLL name; don't double-load.
                    if (!seen.Add(Path.GetFileName(dll)))
                    {
                        log.LogInformation("Skipping {Dll}; a higher-precedence folder already provided it.",
                            Path.GetFileName(dll));
                        continue;
                    }

                    try
                    {
                        bound += LoadPlugin(dll, log);
                    }
                    catch (Exception ex)
                    {
                        log.LogWarning(ex, "Failed to load plugin {Dll}.", dll);
                    }
                }
            }

            log.LogInformation("Plugin load complete: {Count} service(s) bound.", bound);
            return bound;
        }

        private static int LoadPlugin(string dllPath, ILogger log)
        {
            // Manifest gate: if a plugin.json sits next to the DLL, honor its ABI + platform and
            // only bind the DLL it names as the entry (other DLLs are dependencies).
            string manifestPath = Path.Combine(Path.GetDirectoryName(dllPath) ?? ".", "plugin.json");
            if (File.Exists(manifestPath))
            {
                PluginManifest? m = PluginManifest.TryLoad(manifestPath);
                if (m is null)
                {
                    log.LogWarning("Plugin {Dll}: plugin.json present but unreadable; skipping.",
                        Path.GetFileName(dllPath));
                    return 0;
                }

                if (!string.IsNullOrEmpty(m.EntryDll) &&
                    !string.Equals(m.EntryDll, Path.GetFileName(dllPath), StringComparison.OrdinalIgnoreCase))
                {
                    // This DLL is a dependency of the pack, not its entry point.
                    return 0;
                }

                if (!IsAbiSupported(m.Abi))
                {
                    log.LogWarning(
                        "Plugin {Name} was built for contract ABI {Abi}, but this host supports {Min}..{Cur}; skipping.",
                        m.Name ?? Path.GetFileName(dllPath), m.Abi, ContractsAbi.MinSupported, ContractsAbi.Version);
                    return 0;
                }

                if (!IsPlatformMatch(m.Platform))
                {
                    log.LogWarning(
                        "Plugin {Name} targets platform {Platform}, but this host is {Host}; skipping.",
                        m.Name ?? Path.GetFileName(dllPath), m.Platform, CurrentPlatform());
                    return 0;
                }

                log.LogInformation(
                    "Plugin {Name} v{Version} (abi {Abi}, {Platform}) passed manifest checks.",
                    m.Name, m.Version, m.Abi, m.Platform ?? "any");
            }

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

        /// <summary>
        /// Restore the execute bit on native executables (ELF / Mach-O / shebang scripts)
        /// under a plugin folder. Zip archives don't reliably carry Unix permissions, so a
        /// pack extracted on Linux/macOS can ship its <c>geotag</c>/<c>cg-aiding</c> binaries
        /// non-executable — making <c>Process.Start</c> fail with EACCES. Idempotent, cheap
        /// (only files with the bit missing are sniffed), no-op on Windows.
        /// </summary>
        public static void EnsureUnixExecutables(string dir, ILogger? log = null)
        {
            if (OperatingSystem.IsWindows() || !Directory.Exists(dir)) return;
            log ??= NullLogger.Instance;

            foreach (string f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            {
                try
                {
                    UnixFileMode mode = File.GetUnixFileMode(f);
                    if ((mode & UnixFileMode.UserExecute) != 0) continue;
                    if (!LooksExecutable(f)) continue;

                    File.SetUnixFileMode(f,
                        mode | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
                    log.LogInformation("Restored execute permission on {File}.", f);
                }
                catch (Exception ex)
                {
                    log.LogWarning(ex, "Could not inspect/repair execute bit on {File}.", f);
                }
            }
        }

        /// <summary>True when the file starts with an ELF/Mach-O magic or a shebang.</summary>
        private static bool LooksExecutable(string path)
        {
            Span<byte> b = stackalloc byte[4];
            using FileStream fs = File.OpenRead(path);
            if (fs.Read(b) < 4) return false;

            if (b[0] == 0x7F && b[1] == (byte)'E' && b[2] == (byte)'L' && b[3] == (byte)'F') return true; // ELF
            if (b[0] == (byte)'#' && b[1] == (byte)'!') return true;                                       // script
            uint m = BitConverter.ToUInt32(b);
            return m is 0xFEEDFACE or 0xFEEDFACF or 0xCEFAEDFE or 0xCFFAEDFE or 0xCAFEBABE or 0xBEBAFECA; // Mach-O
        }

        private static bool IsAbiSupported(int abi) =>
            abi >= ContractsAbi.MinSupported && abi <= ContractsAbi.Version;

        private static bool IsPlatformMatch(string? platform)
        {
            if (string.IsNullOrWhiteSpace(platform))
                return true; // unspecified = any platform
            return string.Equals(platform, CurrentPlatform(), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Runtime identifier as "os-arch" (e.g. "win-x64"), used to match plugin packs.</summary>
        public static string CurrentPlatform()
        {
            string os =
                OperatingSystem.IsWindows() ? "win" :
                OperatingSystem.IsMacOS() ? "osx" :
                OperatingSystem.IsLinux() ? "linux" : "unknown";

            string arch = RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.X64 => "x64",
                Architecture.X86 => "x86",
                Architecture.Arm64 => "arm64",
                Architecture.Arm => "arm",
                _ => RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant(),
            };

            return $"{os}-{arch}";
        }

        private static Type[] SafeGetTypes(Assembly asm)
        {
            try { return asm.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t != null).ToArray()!; }
        }
    }
}
