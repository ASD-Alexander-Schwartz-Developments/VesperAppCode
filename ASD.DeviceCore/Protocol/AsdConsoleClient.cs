using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using ASD.DeviceCore.Transport;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ASD.DeviceCore.Protocol
{
    /// <summary>
    /// Async console client over an <see cref="ISerialLink"/> for ASD CDC devices
    /// (KOL / Vesper / Pipistrelle). Replaces the legacy <c>SerialMessage</c> three-
    /// thread model (read thread + send thread + dispatch thread, joined without
    /// timeout) with a single cancellable read loop and proper request/response
    /// correlation. No per-device threads; cancellation-safe; one allocation-light
    /// parser. Same wire format as the firmware (<see cref="AsdFrame"/>).
    /// </summary>
    public sealed class AsdConsoleClient : IAsyncDisposable
    {
        private readonly ISerialLink _link;
        private readonly ILogger _log;
        private readonly AsdFrameParser _parser = new();
        private readonly ConcurrentDictionary<byte, TaskCompletionSource<AsdMessage>> _waiters = new();

        private CancellationTokenSource? _cts;
        private Task? _reader;

        public AsdConsoleClient(ISerialLink link, ILogger? log = null)
        {
            _link = link ?? throw new ArgumentNullException(nameof(link));
            _log = log ?? NullLogger.Instance;
        }

        /// <summary>Raised for every decoded message (including ones also satisfying a
        /// pending <see cref="RequestAsync"/>). Marshal to the UI thread in the handler.</summary>
        public event Action<AsdMessage>? MessageReceived;

        public bool IsRunning => _reader is { IsCompleted: false };

        /// <summary>Open the link and start the background reader. Idempotent.</summary>
        public void Start()
        {
            if (IsRunning) return;
            if (!_link.IsOpen) _link.Open();
            _cts = new CancellationTokenSource();
            _reader = Task.Run(() => ReadLoopAsync(_cts.Token));
        }

        /// <summary>Send a message and wait for the next reply of the same type (or
        /// <paramref name="expectType"/> when the reply uses a different type). Returns
        /// null on timeout.</summary>
        public async Task<AsdMessage?> RequestAsync(
            byte type, byte[] payload, int timeoutMs = 3000, byte? expectType = null, byte id = 0,
            CancellationToken ct = default)
        {
            byte key = expectType ?? type;
            var tcs = new TaskCompletionSource<AsdMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            _waiters[key] = tcs; // last writer wins; one outstanding request per reply-type

            await SendAsync(type, payload, id, ct).ConfigureAwait(false);

            using var timeout = new CancellationTokenSource(timeoutMs);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, ct);
            using (linked.Token.Register(() => tcs.TrySetCanceled()))
            {
                try { return await tcs.Task.ConfigureAwait(false); }
                catch (OperationCanceledException) { return null; }
                finally { _waiters.TryRemove(key, out _); }
            }
        }

        /// <summary>Fire-and-forget send (no response awaited).</summary>
        public Task SendAsync(byte type, byte[] payload, byte id = 0, CancellationToken ct = default)
        {
            byte[] frame = AsdFrame.Build(type, payload, id);
            return _link.WriteAsync(frame, ct);
        }

        private async Task ReadLoopAsync(CancellationToken ct)
        {
            var buffer = new byte[512];
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    int n = await _link.ReadAsync(buffer.AsMemory(), ct).ConfigureAwait(false);
                    if (n <= 0) continue;
                    foreach (AsdMessage msg in _parser.Push(buffer.AsMemory(0, n)))
                    {
                        if (_waiters.TryRemove(msg.Type, out var tcs))
                            tcs.TrySetResult(msg);
                        try { MessageReceived?.Invoke(msg); }
                        catch (Exception ex) { _log.LogWarning(ex, "Console message handler threw."); }
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { _log.LogError(ex, "Console read loop terminated."); }
        }

        public async ValueTask DisposeAsync()
        {
            _cts?.Cancel();
            if (_reader != null) { try { await _reader.ConfigureAwait(false); } catch { } }
            foreach (var w in _waiters.Values) w.TrySetCanceled();
            _waiters.Clear();
            await _link.DisposeAsync().ConfigureAwait(false);
            _cts?.Dispose();
        }
    }
}
