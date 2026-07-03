using System.Collections.Generic;

namespace VesperApp.Models
{
    /// <summary>
    /// User-level application configuration, persisted as JSON at
    /// <c>%LOCALAPPDATA%/VesperApp/config.json</c> (see <see cref="VesperApp.Services.SettingsService"/>).
    /// Every field has a sensible default, so a missing or partial file still yields a
    /// usable config and the app always starts.
    /// <para>
    /// SECURITY: secrets (e.g. the backend refresh token) NEVER live here — only a
    /// reference key into the OS secret store (<see cref="AccountConfig.CredentialKey"/>).
    /// The authoritative account tier / entitlements come from the signed Cognito JWT each
    /// session, not from this file. See docs/ARCHITECTURE.md and the tiers plan.
    /// </para>
    /// </summary>
    public sealed class AppConfig
    {
        /// <summary>Bumped when the on-disk shape changes in a way that needs migration.</summary>
        public int SchemaVersion { get; set; } = 1;

        public WorkspaceConfig Workspace { get; set; } = new();
        public UiConfig Ui { get; set; } = new();
        public RecordingsConfig Recordings { get; set; } = new();
        public AccountConfig Account { get; set; } = new();
    }

    /// <summary>The local data workspace the user works against.</summary>
    public sealed class WorkspaceConfig
    {
        /// <summary>Folder auto-opened in the Recordings data browser at startup. Null/empty
        /// falls back to the default (~/Documents/MyVesperData).</summary>
        public string? WorkingDirectory { get; set; }

        /// <summary>Auto-load <see cref="WorkingDirectory"/> into the data browser on launch,
        /// so the user doesn't pick a folder each session.</summary>
        public bool OpenWorkingDirOnStartup { get; set; } = true;

        /// <summary>Recently browsed/decoded folders, most-recent first (UI convenience).</summary>
        public List<string> RecentFolders { get; set; } = new();
    }

    /// <summary>Shell appearance + startup. Theme fields are reserved for when theme
    /// switching lands (the app is dark-locked today); StartupCategory is honoured now.</summary>
    public sealed class UiConfig
    {
        public string ThemeVariant { get; set; } = "Dark";   // reserved (Dark | Light)
        public string AccentColor { get; set; } = "#6366F1"; // reserved
        public string StartupCategory { get; set; } = "Recordings";
    }

    /// <summary>Defaults for the recordings parse/decode pipeline.</summary>
    public sealed class RecordingsConfig
    {
        public bool AutoDecodeOnImport { get; set; } = true;
        public bool HideIntermediateFiles { get; set; }   // hide .UBN/.MBN, show only WAV/CSV/…
        public bool DeleteRawAfterImport { get; set; }
    }

    /// <summary>Cached identity hints for a friendlier launch. NOT a credential store —
    /// the token lives in the OS secret store, referenced by <see cref="CredentialKey"/>.</summary>
    public sealed class AccountConfig
    {
        public string? LastEmail { get; set; }
        public string? LastOrgName { get; set; }
        public string LastTier { get; set; } = "Free"; // hint only; re-derived on sign-in
        public string? CredentialKey { get; set; }      // pointer into DPAPI/Keychain/libsecret
    }
}
