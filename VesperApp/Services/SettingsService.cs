using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using VesperApp.Models;

namespace VesperApp.Services
{
    /// <summary>
    /// Loads and persists <see cref="AppConfig"/> at
    /// <c>%LOCALAPPDATA%/VesperApp/config.json</c> — cross-platform via
    /// <see cref="Environment.SpecialFolder.LocalApplicationData"/>, the same root
    /// <c>PluginLoader</c> uses for its per-user plugins folder. Tolerant of a missing or
    /// corrupt file (falls back to defaults so the app always starts) and writes atomically
    /// (temp file + replace) so a crash mid-write never leaves a truncated config.
    /// </summary>
    public sealed class SettingsService
    {
        private static readonly Lazy<SettingsService> _instance = new(() => new SettingsService());

        /// <summary>The process-wide settings service (loads the file on first access).</summary>
        public static SettingsService Instance => _instance.Value;

        /// <summary>Convenience accessor for the loaded config.</summary>
        public static AppConfig Current => Instance.Config;

        public AppConfig Config { get; private set; } = new();

        /// <summary>Raised after a successful <see cref="Save"/> so views can react.</summary>
        public event EventHandler? Changed;

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
        };

        /// <summary>The <c>%LOCALAPPDATA%/VesperApp</c> folder that holds config + plugins.</summary>
        public static string ConfigDirectory => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VesperApp");

        public static string ConfigPath => Path.Combine(ConfigDirectory, "config.json");

        /// <summary>The out-of-the-box working directory when none is configured:
        /// <c>~/Documents/MyVesperData</c> (matches the legacy VesperOutputFolder root).</summary>
        public static string DefaultWorkingDirectory => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MyVesperData");

        private SettingsService() => Load();

        /// <summary>(Re)load the config from disk. Never throws — falls back to defaults.</summary>
        public void Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    AppConfig? loaded = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(ConfigPath), JsonOpts);
                    if (loaded != null)
                    {
                        Config = loaded;
                        return;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[SettingsService] load failed, using defaults: {e.Message}");
            }

            Config = new AppConfig();
        }

        /// <summary>Persist the current config atomically and raise <see cref="Changed"/>.</summary>
        public void Save()
        {
            try
            {
                Directory.CreateDirectory(ConfigDirectory);
                string json = JsonSerializer.Serialize(Config, JsonOpts);

                string tmp = ConfigPath + ".tmp";
                File.WriteAllText(tmp, json);

                if (File.Exists(ConfigPath))
                    File.Replace(tmp, ConfigPath, null); // atomic on the same volume
                else
                    File.Move(tmp, ConfigPath);
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[SettingsService] save failed: {e.Message}");
            }

            Changed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>The effective working directory: the configured one, or the default when
        /// unset. The folder is created if missing so callers can scan it immediately.</summary>
        public string ResolveWorkingDirectory()
        {
            string dir = Config.Workspace.WorkingDirectory ?? string.Empty;
            if (string.IsNullOrWhiteSpace(dir))
                dir = DefaultWorkingDirectory;

            try { Directory.CreateDirectory(dir); }
            catch (Exception e) { Debug.WriteLine($"[SettingsService] could not create working dir '{dir}': {e.Message}"); }

            return dir;
        }
    }
}
