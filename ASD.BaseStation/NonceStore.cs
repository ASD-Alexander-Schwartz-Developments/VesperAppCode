using System.Globalization;

namespace ASD.BaseStation;

/// <summary>
/// Persists the monotonic MODEM_OPEN nonce across daemon restarts. The VT04 verifier
/// requires each session's nonce to be strictly greater than the last it honoured
/// (replay protection), so the base station must never reuse or go backwards — even
/// after a reboot. Kept as a tiny file next to the data store.
/// </summary>
public sealed class NonceStore
{
    private readonly string _path;
    private readonly object _gate = new();
    private uint _current;

    public NonceStore(string storagePath)
    {
        Directory.CreateDirectory(storagePath);
        _path = Path.Combine(storagePath, "modem.nonce");
        if (File.Exists(_path) &&
            uint.TryParse(File.ReadAllText(_path).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out uint v))
            _current = v;
    }

    /// <summary>Reserve and persist the next nonce. Monotonic and durable.</summary>
    public uint Next()
    {
        lock (_gate)
        {
            _current = checked(_current + 1);
            File.WriteAllText(_path, _current.ToString(CultureInfo.InvariantCulture));
            return _current;
        }
    }
}
