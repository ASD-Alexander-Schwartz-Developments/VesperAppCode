using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ASD.Contracts;
using Avalonia.Threading;
using ReactiveUI;

namespace VesperApp.Services
{
    /// <summary>
    /// Tracks every decoding/parsing job and shows them in one non-modal Decoding Progress panel
    /// (Windows-copy-dialog style). Entry points:
    /// <list type="bullet">
    ///   <item><see cref="StartGnss"/> — a streamed GNSS snapshot decode via <see cref="IGnssDecoder"/>
    ///   (live console + @PROGRESS bar). Concurrent-safe; returns a job whose
    ///   <see cref="DecodeJob.Completion"/> resolves when it finishes.</item>
    ///   <item><see cref="Run"/> — a background job with an optional 0–100 percentage (e.g. Auto Decode).</item>
    ///   <item><see cref="Track"/> — wrap an existing UI-thread parser (per-type decoders) so it shows
    ///   as a job without changing its internals.</item>
    /// </list>
    /// </summary>
    public sealed class DecodeJobManager : ReactiveObject
    {
        public static DecodeJobManager Instance { get; } = new();

        /// <summary>Set once by the view layer so Services need not reference Views.</summary>
        public static Action? PanelOpener;

        private int _counter;

        private DecodeJobManager() { }

        public ObservableCollection<DecodeJob> Jobs { get; } = new();

        public static string LogDir => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VesperApp", "logs", "gnss");

        private int _activeCount;
        public int ActiveCount { get => _activeCount; private set => this.RaiseAndSetIfChanged(ref _activeCount, value); }

        private bool _hasJobs;
        public bool HasJobs { get => _hasJobs; private set => this.RaiseAndSetIfChanged(ref _hasJobs, value); }

        private string _summary = "No decode jobs yet.";
        public string Summary { get => _summary; private set => this.RaiseAndSetIfChanged(ref _summary, value); }

        public ICommand ClearFinishedCommand => _clear ??= ReactiveCommand.Create(ClearFinished);
        private ICommand? _clear;

        // ───────────────────────── entry points ─────────────────────────

        /// <summary>Queue a GNSS snapshot-folder decode (streamed console + progress). Thread-safe.</summary>
        public DecodeJob StartGnss(string sourceFolder, string? outputFolder = null)
        {
            string outPath = outputFolder ?? Path.Combine(sourceFolder, "decode");
            DecodeJob job = Create("GNSS decode — " + Path.GetFileName(sourceFolder.TrimEnd('\\', '/')), streamed: true, sourceFolder);
            RequestShowPanel();
            _ = RunGnssAsync(job, sourceFolder, outPath);
            return job;
        }

        /// <summary>Run a background decode/parse job that reports its own 0–100 percentage + log.</summary>
        public DecodeJob Run(string title, Func<IProgress<string>, IProgress<double>, CancellationToken, Task<DecodeOutcome>> work)
        {
            DecodeJob job = Create(title, streamed: false, title);
            RequestShowPanel();
            _ = RunGenericAsync(job, work);
            return job;
        }

        /// <summary>Wrap an existing per-type decoder (which runs on the UI thread and shows its own
        /// pickers) so it appears as a job. Indeterminate; returns the decoder's own bool result.</summary>
        public async Task<bool> Track(string title, Func<Task<bool>> work)
        {
            DecodeJob job = Create(title, streamed: false, title);
            RequestShowPanel();

            bool ok = false;
            DecodeOutcome outcome;
            try { ok = await work(); outcome = new DecodeOutcome(true, ok ? "Done." : "Nothing to decode."); }
            catch (OperationCanceledException) { outcome = new DecodeOutcome(false, "Cancelled."); }
            catch (Exception ex) { outcome = new DecodeOutcome(false, ex.Message); }

            job.Complete(outcome, false);
            RecomputeAggregates();
            return ok;
        }

        // ───────────────────────── runners ─────────────────────────

        private async Task RunGnssAsync(DecodeJob job, string src, string outPath)
        {
            DecodeOutcome outcome;
            try
            {
                IGnssDecoder? decoder = ASD.Platform.PlatformServices.Gnss;
                if (decoder is null || !decoder.IsAvailable)
                {
                    job.Log.Report("GNSS decoder plugin not installed.");
                    outcome = new DecodeOutcome(false,
                        "GNSS decoder plugin not installed. Install the ASD.Gnss plugin from the Plugins page.");
                }
                else
                {
                    GnssDecodeResult res = await decoder.DecodeAsync(
                        new GnssDecodeRequest(src, outPath), job.Log, job.Token).ConfigureAwait(false);
                    outcome = new DecodeOutcome(res.Succeeded, res.Message ?? (res.Succeeded ? "Done." : "Decode failed."));
                }
            }
            catch (OperationCanceledException) { outcome = new DecodeOutcome(false, "GNSS decode cancelled."); }
            catch (Exception ex) { outcome = new DecodeOutcome(false, "Decode error: " + ex.Message); }

            job.Complete(outcome, job.Token.IsCancellationRequested);
            RunOnUi(RecomputeAggregates);
        }

        private async Task RunGenericAsync(DecodeJob job,
            Func<IProgress<string>, IProgress<double>, CancellationToken, Task<DecodeOutcome>> work)
        {
            DecodeOutcome outcome;
            try
            {
                outcome = await Task.Run(() => work(job.Log, job.Percent, job.Token)).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { outcome = new DecodeOutcome(false, "Cancelled."); }
            catch (Exception ex) { outcome = new DecodeOutcome(false, ex.Message); }

            job.Complete(outcome, job.Token.IsCancellationRequested);
            RunOnUi(RecomputeAggregates);
        }

        // ───────────────────────── plumbing ─────────────────────────

        private DecodeJob Create(string title, bool streamed, string nameForLog)
        {
            int id = Interlocked.Increment(ref _counter);
            string logPath = Path.Combine(LogDir, $"{DateTime.Now:yyyyMMdd-HHmmss}-{id}-{SafeName(nameForLog)}.log");

            DecodeJob Build()
            {
                var job = new DecodeJob(id, title, logPath, streamed);
                job.Removed += RemoveJob;
                Jobs.Insert(0, job);
                RecomputeAggregates();
                return job;
            }
            return Dispatcher.UIThread.CheckAccess() ? Build() : Dispatcher.UIThread.Invoke(Build);
        }

        private void RemoveJob(DecodeJob job) => RunOnUi(() => { Jobs.Remove(job); RecomputeAggregates(); });

        private void ClearFinished() => RunOnUi(() =>
        {
            foreach (DecodeJob j in Jobs.Where(j => !j.IsRunning).ToList()) Jobs.Remove(j);
            RecomputeAggregates();
        });

        private void RecomputeAggregates()
        {
            int running = Jobs.Count(j => j.IsRunning);
            int done = Jobs.Count - running;
            ActiveCount = running;
            HasJobs = Jobs.Count > 0;
            Summary = Jobs.Count == 0 ? "No decode jobs yet." : $"{running} running · {done} finished";
        }

        private void RequestShowPanel() => RunOnUi(() => PanelOpener?.Invoke());

        private static void RunOnUi(Action a)
        {
            if (Dispatcher.UIThread.CheckAccess()) a();
            else Dispatcher.UIThread.Post(a);
        }

        private static string SafeName(string s)
        {
            string name = Path.GetFileName(s.TrimEnd('\\', '/'));
            if (string.IsNullOrEmpty(name)) name = s;
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return name.Length > 40 ? name[..40] : name;
        }
    }
}
