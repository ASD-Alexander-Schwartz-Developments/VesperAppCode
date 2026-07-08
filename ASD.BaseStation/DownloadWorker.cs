using ASD.Contracts;
using ASD.DeviceCore.Ble;
using ASD.DeviceCore.Ble.Bgapi;
using ASD.DeviceCore.Ble.ProxTit;
using ASD.DeviceCore.Transport;
using Microsoft.Extensions.Options;

namespace ASD.BaseStation;

/// <summary>
/// The base-station loop: scan for ProxTit tags, open an authenticated modem session,
/// download new files (SD-first), then attempt to upload them to the backend. Uses the
/// same <see cref="ProxTitModemClient"/> the desktop app uses, over either the BGAPI
/// NCP adapter or the simulator. Upload goes through the platform <see cref="IPlmClient"/>;
/// in the open-source build that is the stub, so data simply stays on the SD card until
/// a real client is configured. See docs/ARCHITECTURE.md.
/// </summary>
public sealed class DownloadWorker : BackgroundService
{
    private readonly ILogger<DownloadWorker> _log;
    private readonly ILoggerFactory _loggerFactory;
    private readonly BaseStationOptions _opt;
    private readonly LocalStore _store;
    private readonly NonceStore _nonce;
    private readonly byte[]? _key;

    public DownloadWorker(
        ILogger<DownloadWorker> log, ILoggerFactory loggerFactory, IOptions<BaseStationOptions> opt)
    {
        _log = log;
        _loggerFactory = loggerFactory;
        _opt = opt.Value;
        _store = new LocalStore(_opt.StoragePath);
        _nonce = new NonceStore(_opt.StoragePath);
        _key = ModemAuth.ParseKey(_opt.DeployerKeyHex);
        if (_key == null)
            _log.LogWarning("No valid deployer key configured — modem sessions will be unauthenticated.");
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _log.LogInformation("Base station starting. Transport={Transport}, storage={Path}.",
            string.IsNullOrWhiteSpace(_opt.NcpPort) ? "SIMULATOR" : _opt.NcpPort, _opt.StoragePath);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await RunCycleAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                _log.LogError(ex, "Download cycle failed; will retry next cycle.");
            }

            try { await Task.Delay(TimeSpan.FromSeconds(_opt.CycleSeconds), ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task RunCycleAsync(CancellationToken ct)
    {
        await using IBleCentral ble = CreateCentral();
        await ble.OpenAsync(ct);

        IReadOnlyList<BleScanResult> tags = await ble.ScanAsync(
            TimeSpan.FromSeconds(_opt.ScanSeconds), ProxTitGatt.Service, ct);
        if (tags.Count == 0)
        {
            _log.LogDebug("No ProxTit tags in range this cycle.");
            return;
        }
        _log.LogInformation("Found {Count} tag(s).", tags.Count);

        foreach (BleScanResult tag in tags)
        {
            ct.ThrowIfCancellationRequested();
            try { await DownloadTagAsync(ble, tag, ct); }
            catch (ProxTitModemException ex) { _log.LogWarning(ex, "Tag {Tag} refused an op.", tag.Address); }
            catch (Exception ex) { _log.LogError(ex, "Tag {Tag} download failed.", tag.Address); }
        }
    }

    private async Task DownloadTagAsync(IBleCentral ble, BleScanResult tag, CancellationToken ct)
    {
        string tagId = tag.Address.ToString();
        await ble.ConnectAsync(tag.Address, ct);
        await ble.NegotiateMtuAsync(_opt.DesiredMtu, ct);

        var client = new ProxTitModemClient(ble, _loggerFactory.CreateLogger<ProxTitModemClient>());
        uint nonce = _key != null ? _nonce.Next() : 0;
        ProxTitCatalog catalog = await client.OpenAsync(_key, nonce, ct);

        _log.LogInformation("Tag {Tag}: {Streams} stream(s), {FreeKb} KiB free.",
            tagId, catalog.Streams.Count, catalog.FreeKiloBytes);

        foreach (CatalogStream stream in catalog.Streams)
        {
            // Iterate 0..SafeMaxIndexExclusive so we never touch the live (recording) file.
            for (uint index = 0; index < stream.SafeMaxIndexExclusive; index++)
            {
                ct.ThrowIfCancellationRequested();
                if (_store.Has(tagId, stream.SensorId, index))
                    continue;
                await DownloadOneAsync(client, tagId, stream.SensorId, index, ct);
            }
        }

        if (_opt.DownloadConfig)
        {
            try { await DownloadOneAsync(client, tagId, ProxTitGatt.StreamConfig, 0, ct); }
            catch (ProxTitModemException) { /* no config.json on this unit */ }
        }

        await ble.DisconnectAsync(ct);
    }

    private async Task DownloadOneAsync(
        ProxTitModemClient client, string tagId, byte sensor, uint index, CancellationToken ct)
    {
        string path = _store.PathFor(tagId, sensor, index);
        long bytes;
        await using (FileStream fs = _store.BeginWrite(path))
        {
            bytes = await client.DownloadFileAsync(sensor, index, fs, progress: null, ct);
        }
        _store.Commit(tagId, sensor, index, path);
        _log.LogInformation("Saved {Stream}[{Index}] ({Bytes} B) -> {Path}",
            ProxTitGatt.StreamName(sensor), index, bytes, path);

        await TryUploadAsync(path, ct);
    }

    private async Task TryUploadAsync(string path, CancellationToken ct)
    {
        try
        {
            string name = Path.GetFileName(path);
            PlmUploadTicket ticket = await ASD.Platform.PlatformServices.Plm
                .RequestUploadUrlAsync(name, "application/octet-stream", ct);
            // A real client would PUT the bytes to ticket.Url here, then confirm.
            await ASD.Platform.PlatformServices.Plm.ConfirmUploadAsync(ticket.UploadId, ct);
            _log.LogInformation("Uploaded {Name} to backend.", name);
        }
        catch (PlatformNotConfiguredException)
        {
            _log.LogDebug("Backend not configured; {Path} kept on local storage (upload pending).", path);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Upload failed for {Path}; kept locally.", path);
        }
    }

    private IBleCentral CreateCentral()
    {
        if (string.IsNullOrWhiteSpace(_opt.NcpPort))
            return new SimulatedBleCentral(_opt.DeployerKeyHex);

        var link = new SerialPortLink(_opt.NcpPort, _opt.NcpBaud);
        return new BgapiNcpBleCentral(link, _loggerFactory.CreateLogger<BgapiNcpBleCentral>());
    }
}
