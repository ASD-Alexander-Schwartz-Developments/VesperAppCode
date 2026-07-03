using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using ReactiveUI;

namespace VesperApp.Services
{
    public enum DecodeJobState { Running, Succeeded, Failed, Cancelled }

    /// <summary>Outcome of any decode/parse job shown in the unified Decoding Progress panel.</summary>
    public sealed record DecodeOutcome(bool Succeeded, string Message);

    /// <summary>
    /// One decoding/parsing job tracked in the unified, non-modal Decoding Progress panel
    /// (Windows-copy-dialog style). It backs two kinds of work:
    /// <list type="bullet">
    ///   <item><b>Streamed</b> jobs (GNSS snapshot decode) feed the decoder's console output through
    ///   <see cref="Log"/>; the job renders it as a live console, parses the <c>@PROGRESS</c> tokens
    ///   the plugin/geotag emit into a real bar, and filters 7-Zip banner noise.</item>
    ///   <item><b>Plain</b> jobs (Auto Decode, per-type parsers) report an optional 0–100 percentage
    ///   through <see cref="Percent"/> and status text through <see cref="Log"/>.</item>
    /// </list>
    /// Threading: progress can arrive from any thread; it is buffered lock-free and the on-disk log is
    /// written immediately, while observable UI state is refreshed on a 250 ms UI-thread timer.
    /// </summary>
    public sealed class DecodeJob : ReactiveObject
    {
        private const int MaxLiveLines = 1000;

        // @PROGRESS tokens the GNSS plugin/geotag emit: a one-shot total then a running snapshot index.
        private static readonly Regex ProgressTotal = new(@"^@PROGRESS\s+total\s+(?<n>\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex ProgressSnap = new(@"^@PROGRESS\s+snap\s+(?<k>\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly ConcurrentQueue<string> _pending = new();
        private readonly StringBuilder _live = new();
        private readonly object _fileLock = new();
        private readonly CancellationTokenSource _cts = new();
        private readonly TaskCompletionSource<DecodeOutcome> _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Stopwatch _sw = Stopwatch.StartNew();
        private readonly DispatcherTimer _timer;
        private readonly bool _streamed;
        private StreamWriter? _file;

        private volatile string _latestLine = string.Empty;
        private volatile int _percent;
        private volatile bool _indeterminate = true;
        private int _liveLineCount;
        private int _total;      // total snapshots (streamed jobs), 0 until known
        private int _snapIndex;  // running snapshot index (streamed jobs)

        internal DecodeJob(int id, string title, string logFilePath, bool streamed)
        {
            Id = id;
            Title = string.IsNullOrWhiteSpace(title) ? $"Job {id}" : title;
            LogFilePath = logFilePath;
            _streamed = streamed;

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(logFilePath)!);
                _file = new StreamWriter(logFilePath, append: false, Encoding.UTF8) { AutoFlush = true };
                _file.WriteLine($"# {Title}");
                _file.WriteLine($"# started {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            }
            catch { _file = null; }

            Log = new DirectProgress(AppendLine);
            Percent = new DirectProgressD(SetPercent);

            CancelCommand = ReactiveCommand.Create(Cancel, this.WhenAnyValue(x => x.IsRunning));
            OpenLogCommand = ReactiveCommand.Create(OpenLog);
            RemoveCommand = ReactiveCommand.Create(() => Removed?.Invoke(this),
                this.WhenAnyValue(x => x.IsRunning, r => !r));
            ToggleExpandedCommand = ReactiveCommand.Create(() => IsExpanded = !IsExpanded);

            _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(250), DispatcherPriority.Background, (_, _) => Tick());
            _timer.Start();
        }

        public int Id { get; }
        public string Title { get; }
        public string LogFilePath { get; }

        /// <summary>Streamed console/status sink (any thread). Streamed jobs also parse @PROGRESS here.</summary>
        public IProgress<string> Log { get; }

        /// <summary>0–100 percentage sink for plain jobs (any thread).</summary>
        public IProgress<double> Percent { get; }

        public System.Threading.Tasks.Task<DecodeOutcome> Completion => _completion.Task;
        public CancellationToken Token => _cts.Token;

        internal event Action<DecodeJob>? Removed;

        public ICommand CancelCommand { get; }
        public ICommand OpenLogCommand { get; }
        public ICommand RemoveCommand { get; }
        public ICommand ToggleExpandedCommand { get; }

        private DecodeJobState _state = DecodeJobState.Running;
        public DecodeJobState State { get => _state; private set => this.RaiseAndSetIfChanged(ref _state, value); }

        private bool _isRunning = true;
        public bool IsRunning { get => _isRunning; private set => this.RaiseAndSetIfChanged(ref _isRunning, value); }

        private string _statusText = "Starting…";
        public string StatusText { get => _statusText; private set => this.RaiseAndSetIfChanged(ref _statusText, value); }

        private double _progress;
        public double Progress { get => _progress; private set => this.RaiseAndSetIfChanged(ref _progress, value); }

        private bool _isIndeterminate = true;
        public bool IsIndeterminate { get => _isIndeterminate; private set => this.RaiseAndSetIfChanged(ref _isIndeterminate, value); }

        private string _elapsedText = "0:00";
        public string ElapsedText { get => _elapsedText; private set => this.RaiseAndSetIfChanged(ref _elapsedText, value); }

        private string _logTail = string.Empty;
        public string LogTail { get => _logTail; private set => this.RaiseAndSetIfChanged(ref _logTail, value); }

        private bool _isExpanded;
        public bool IsExpanded { get => _isExpanded; set => this.RaiseAndSetIfChanged(ref _isExpanded, value); }

        private bool _hasError;
        public bool HasError { get => _hasError; private set => this.RaiseAndSetIfChanged(ref _hasError, value); }

        private volatile bool _errorPending;

        // Runs on the reporting thread (not the UI thread).
        private void AppendLine(string line)
        {
            line ??= string.Empty;

            if (_streamed)
            {
                Match mt = ProgressTotal.Match(line);
                if (mt.Success) { _total = ParseInt(mt.Groups["n"].Value); WriteRaw(line); return; }
                Match ms = ProgressSnap.Match(line);
                if (ms.Success)
                {
                    _snapIndex = ParseInt(ms.Groups["k"].Value);
                    if (_total > 0) { _percent = Clamp((int)Math.Round(100.0 * _snapIndex / _total)); _indeterminate = false; }
                    _latestLine = _total > 0 ? $"Decoding snapshot {_snapIndex} of {_total}…" : $"Decoding snapshot {_snapIndex}…";
                    WriteRaw(line);   // token kept in the file, filtered from the live console
                    return;
                }
                if (IsNoise(line)) { WriteRaw(line); return; }   // 7-Zip banner etc.: log it, don't surface
            }

            if (LooksLikeError(line)) _errorPending = true;
            _latestLine = line;
            _pending.Enqueue(line);
            WriteRaw(line);
        }

        private void WriteRaw(string line) { lock (_fileLock) { _file?.WriteLine(line); } }

        // Plain-job percentage sink.
        private void SetPercent(double pct)
        {
            _percent = Clamp((int)Math.Round(pct));
            _indeterminate = false;
        }

        // UI thread, every 250 ms while active.
        private void Tick()
        {
            ElapsedText = Format(_sw.Elapsed);

            bool changed = false;
            while (_pending.TryDequeue(out string? line))
            {
                _live.Append(line).Append('\n');
                if (++_liveLineCount > MaxLiveLines) TrimLive();
                changed = true;
            }
            if (changed) LogTail = _live.ToString();

            if (_errorPending && !HasError) HasError = true;

            if (IsRunning)
            {
                IsIndeterminate = _indeterminate;
                if (!_indeterminate) Progress = _percent;
                string tail = _latestLine.Length > 120 ? _latestLine[..117] + "…" : _latestLine;
                StatusText = string.IsNullOrWhiteSpace(tail)
                    ? (_streamed ? "Decoding… (this can take several minutes)" : "Working…")
                    : tail;
            }
        }

        private void TrimLive()
        {
            string s = _live.ToString();
            int cut = s.IndexOf('\n', s.Length / 5);
            if (cut > 0)
            {
                _live.Clear();
                _live.Append("… (earlier output in log file) …\n").Append(s[(cut + 1)..]);
                _liveLineCount = MaxLiveLines * 4 / 5;
            }
        }

        internal void Complete(DecodeOutcome outcome, bool cancelled)
        {
            void Finish()
            {
                _timer.Stop();
                Tick();
                _sw.Stop();

                State = cancelled ? DecodeJobState.Cancelled
                      : outcome.Succeeded ? DecodeJobState.Succeeded
                      : DecodeJobState.Failed;
                IsRunning = false;
                IsIndeterminate = false;
                Progress = outcome.Succeeded ? 100 : Progress;
                if (State == DecodeJobState.Failed) HasError = true;

                StatusText = cancelled ? "Cancelled"
                           : outcome.Succeeded ? outcome.Message
                           : "Failed — " + outcome.Message;

                lock (_fileLock)
                {
                    _file?.WriteLine($"# finished {DateTime.Now:yyyy-MM-dd HH:mm:ss} — {State} ({ElapsedText})");
                    if (!string.IsNullOrWhiteSpace(outcome.Message)) _file?.WriteLine("# " + outcome.Message);
                    _file?.Dispose();
                    _file = null;
                }

                _completion.TrySetResult(outcome);
            }

            if (Dispatcher.UIThread.CheckAccess()) Finish();
            else Dispatcher.UIThread.Post(Finish);
        }

        private void Cancel()
        {
            if (!IsRunning) return;
            StatusText = "Cancelling…";
            try { _cts.Cancel(); } catch { }
        }

        private void OpenLog()
        {
            try { if (File.Exists(LogFilePath)) Process.Start(new ProcessStartInfo(LogFilePath) { UseShellExecute = true }); }
            catch { }
        }

        // 7-Zip banner / archive chatter that geotag's KMZ step leaks; noise in the live console.
        private static bool IsNoise(string line)
        {
            string t = line.TrimStart();
            return t.StartsWith("7-Zip ", StringComparison.OrdinalIgnoreCase)
                || t.StartsWith("Scanning the drive", StringComparison.OrdinalIgnoreCase)
                || t.StartsWith("Creating archive", StringComparison.OrdinalIgnoreCase)
                || t.StartsWith("Add new data to archive", StringComparison.OrdinalIgnoreCase)
                || t.StartsWith("Files read from disk", StringComparison.OrdinalIgnoreCase)
                || t.StartsWith("Archive size", StringComparison.OrdinalIgnoreCase)
                || t.Equals("Everything is Ok", StringComparison.OrdinalIgnoreCase);
        }

        private static bool LooksLikeError(string line) =>
            line.Contains("error", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("fail", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("exception", StringComparison.OrdinalIgnoreCase);

        private static int ParseInt(string s) => int.TryParse(s, out int v) ? v : 0;
        private static int Clamp(int v) => v < 0 ? 0 : v > 100 ? 100 : v;
        private static string Format(TimeSpan t) =>
            t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}" : $"{t.Minutes}:{t.Seconds:00}";

        private sealed class DirectProgress : IProgress<string>
        {
            private readonly Action<string> _a;
            public DirectProgress(Action<string> a) => _a = a;
            public void Report(string value) => _a(value);
        }

        private sealed class DirectProgressD : IProgress<double>
        {
            private readonly Action<double> _a;
            public DirectProgressD(Action<double> a) => _a = a;
            public void Report(double value) => _a(value);
        }
    }
}
