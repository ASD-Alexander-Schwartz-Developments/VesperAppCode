using System;
using System.Threading;
using System.Windows.Input;

namespace VesperApp.Controls
{
    public interface IProgressStatus
    {
        /// <summary>Gets CancellationToken to use to cancel the async function.</summary>
        CancellationToken Ct { get; }

        /// <summary>Command executed when cancel button is clicked. Requests cancellation through cancellation token.</summary>
        ICommand CancelCommand { get; }

        /// <summary>Gets a value indicating whether the associated task was cancelled.</summary>
        bool IsCancelled { get; }

        /// <summary>Event published when the associated task is finished.</summary>
        event Action<IProgressStatus> Finished;

        /// <summary>Event published when the associated task is cancelled.</summary>
        event Action<IProgressStatus> Cancelled;

        /// <summary>Event published when the progress is updated.</summary>
        event Action<IProgressStatus> ProgressUpdated;

        /// <summary>Gets or sets a value indicating whether the associated task is finished.</summary>
        bool IsFinished { get; set; }

        /// <summary>Gets message to be displayed in ProgressDialog.</summary>
        string Message { get; }

        /// <summary>Gets progress in percent shown by ProgressBar in ProgressDialog.</summary>
        int ProgressPercent { get; }
        int ProgressPercent2 { get; }

        /// <summary>Update ProgressDialog.</summary>
        /// <param name="message">New message to be shown.</param>
        /// <param name="progressPercent">New progress level to be shown.</param>
        void Update(string message, int progressPercent, int pp2);
    }
}
