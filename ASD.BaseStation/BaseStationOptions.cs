namespace ASD.BaseStation;

/// <summary>
/// Configuration for the base-station daemon (bound from the "BaseStation" section of
/// appsettings.json / environment). Secrets (the deployer key) should come from an
/// environment variable or a protected config file, not source control.
/// </summary>
public sealed class BaseStationOptions
{
    /// <summary>
    /// Serial port of the Silicon Labs BGAPI NCP adapter (COMn / /dev/ttyACM0). When
    /// null or empty, the daemon runs against the in-memory simulator so it can be
    /// exercised without hardware.
    /// </summary>
    public string? NcpPort { get; set; }

    /// <summary>NCP serial baud rate.</summary>
    public int NcpBaud { get; set; } = 115200;

    /// <summary>
    /// 32-hex-char deployer key that authenticates modem sessions (must match the key
    /// in the tags' config.json). Null/short ⇒ the daemon opens sessions without auth
    /// (only works against keyless deployments).
    /// </summary>
    public string? DeployerKeyHex { get; set; }

    /// <summary>Local SD/disk root where downloaded data is written first (source of truth).</summary>
    public string StoragePath { get; set; } = "data";

    /// <summary>Seconds to wait between scan/download cycles.</summary>
    public int CycleSeconds { get; set; } = 30;

    /// <summary>Seconds to scan for tags each cycle.</summary>
    public int ScanSeconds { get; set; } = 5;

    /// <summary>ATT MTU to request after connecting (larger ⇒ bigger chunks).</summary>
    public int DesiredMtu { get; set; } = 247;

    /// <summary>Also download each tag's config.json when present.</summary>
    public bool DownloadConfig { get; set; } = true;
}
