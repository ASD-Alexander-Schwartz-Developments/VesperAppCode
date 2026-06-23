using System.IO;
using System.Text.Json;

namespace ASD.Platform
{
    /// <summary>
    /// Optional <c>plugin.json</c> shipped beside a plugin's entry DLL. It lets the host gate a
    /// pack before binding it: the ABI it was built against (see
    /// <see cref="ASD.Contracts.ContractsAbi"/>) and the platform it was compiled for. Packs
    /// produced by cg-gnss CI carry one; hand-built/dev DLLs without a manifest still load (the
    /// loader treats "no manifest" as unmanaged/dev). See docs/ARCHITECTURE.md.
    /// </summary>
    public sealed class PluginManifest
    {
        /// <summary>Display name, e.g. "ASD.Gnss".</summary>
        public string? Name { get; set; }

        /// <summary>Service kind, e.g. "gnss" | "plm" | "entitlements" (informational).</summary>
        public string? Kind { get; set; }

        /// <summary>Pack version — the upstream release (e.g. the cg-gnss version "1.0.0").</summary>
        public string? Version { get; set; }

        /// <summary>Contract ABI the pack was built against; checked against
        /// <see cref="ASD.Contracts.ContractsAbi"/>.</summary>
        public int Abi { get; set; }

        /// <summary>Target platform as "os-arch" (e.g. "win-x64", "linux-x64"); empty = any.</summary>
        public string? Platform { get; set; }

        /// <summary>The plugin's entry DLL file name; other DLLs in the folder are dependencies.</summary>
        public string? EntryDll { get; set; }

        /// <summary>Native payload that ships with the pack (e.g. geotag, the aiding-server,
        /// config files). Informational — the loader does not open these.</summary>
        public string[]? NativeFiles { get; set; }

        /// <summary>Arch the upstream considers validated (e.g. cg-gnss notes win-x86 today).</summary>
        public string? ValidatedArch { get; set; }

        /// <summary>Detached signature / token for the pack, when signing is in use (else null).</summary>
        public string? Signature { get; set; }

        private static readonly JsonSerializerOptions Options = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        /// <summary>Parse a <c>plugin.json</c>; returns null on missing/invalid content.</summary>
        public static PluginManifest? TryLoad(string path)
        {
            try
            {
                string json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<PluginManifest>(json, Options);
            }
            catch
            {
                return null;
            }
        }
    }
}
