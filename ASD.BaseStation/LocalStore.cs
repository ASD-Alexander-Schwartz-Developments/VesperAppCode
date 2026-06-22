using System.Text.Json;
using ASD.DeviceCore.Ble.ProxTit;

namespace ASD.BaseStation;

/// <summary>
/// SD-first local storage for downloaded field data. Files are written to disk before
/// any upload is attempted, so a connectivity outage never loses data. A small JSON
/// manifest records what has already been downloaded per tag/stream/index so cycles
/// are incremental.
/// </summary>
public sealed class LocalStore
{
    private readonly string _root;
    private readonly string _manifestPath;
    private readonly object _gate = new();
    private readonly HashSet<string> _have;

    public LocalStore(string storagePath)
    {
        _root = storagePath;
        Directory.CreateDirectory(_root);
        _manifestPath = Path.Combine(_root, "manifest.json");
        _have = LoadManifest();
    }

    private static string Key(string tag, byte sensor, uint index) => $"{tag}/{sensor}/{index}";

    /// <summary>True if this (tag, stream, index) was already downloaded in a prior cycle.</summary>
    public bool Has(string tag, byte sensor, uint index)
    {
        lock (_gate) return _have.Contains(Key(tag, sensor, index));
    }

    /// <summary>Absolute path a file will be written to. Layout mirrors the VT04 stream
    /// naming: &lt;root&gt;/&lt;tag&gt;/&lt;stream&gt;/&lt;index&gt;&lt;suffix&gt;.</summary>
    public string PathFor(string tag, byte sensor, uint index)
    {
        string dir = Path.Combine(_root, Sanitize(tag), ProxTitGatt.StreamName(sensor));
        Directory.CreateDirectory(dir);
        string name = sensor == ProxTitGatt.StreamConfig
            ? "config.json"
            : $"{index}{ProxTitGatt.StreamFileSuffix(sensor)}";
        return Path.Combine(dir, name);
    }

    /// <summary>Open a temp file stream for a download; commit on success.</summary>
    public FileStream BeginWrite(string finalPath)
        => new(finalPath + ".part", FileMode.Create, FileAccess.Write, FileShare.None);

    /// <summary>Commit a completed download and record it in the manifest.</summary>
    public void Commit(string tag, byte sensor, uint index, string finalPath)
    {
        if (File.Exists(finalPath)) File.Delete(finalPath);
        File.Move(finalPath + ".part", finalPath);
        lock (_gate)
        {
            if (sensor != ProxTitGatt.StreamConfig) // config.json re-downloads each cycle
                _have.Add(Key(tag, sensor, index));
            SaveManifest();
        }
    }

    private HashSet<string> LoadManifest()
    {
        try
        {
            if (File.Exists(_manifestPath))
            {
                string[]? items = JsonSerializer.Deserialize<string[]>(File.ReadAllText(_manifestPath));
                if (items != null) return new HashSet<string>(items);
            }
        }
        catch { /* corrupt manifest ⇒ start fresh; files on disk are still safe */ }
        return new HashSet<string>();
    }

    private void SaveManifest()
        => File.WriteAllText(_manifestPath, JsonSerializer.Serialize(_have.ToArray()));

    private static string Sanitize(string s) => string.Concat(s.Split(Path.GetInvalidFileNameChars()));
}
