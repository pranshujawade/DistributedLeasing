using System;

namespace DistributedLeasing.Core.Events
{
    /// <summary>
    /// Event arguments for the LeaseLost event.
    /// </summary>
    public class LeaseLostEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LeaseLostEventArgs"/> class.
        /// </summary>
        /// <param name="leaseName">The name of the lease that was lost.</param>
        /// <param name="leaseId">The ID of the lease that was lost.</param>
        /// <param name="timestamp">The timestamp when the lease was lost.</param>
        /// <param name="reason">The reason the lease was lost.</param>
        /// <param name="lastSuccessfulRenewal">The timestamp of the last successful renewal.</param>
        public LeaseLostEventArgs(
            string leaseName,
            string leaseId,
            DateTimeOffset timestamp,
            string reason,
            DateTimeOffset lastSuccessfulRenewal)
        {
            LeaseName = leaseName ?? throw new ArgumentNullException(nameof(leaseName));
            LeaseId = leaseId ?? throw new ArgumentNullException(nameof(leaseId));
            Timestamp = timestamp;
            Reason = reason ?? throw new ArgumentNullException(nameof(reason));
            LastSuccessfulRenewal = lastSuccessfulRenewal;
        }

        /// <summary>
        /// Gets the name of the lease that was lost.
        /// </summary>
        public string LeaseName { get; }

        /// <summary>
        /// Gets the ID of the lease that was lost.
        /// </summary>
        public string LeaseId { get; }

        /// <summary>
        /// Gets the timestamp when the lease was lost.
        /// </summary>
        public DateTimeOffset Timestamp { get; }

        /// <summary>
        /// Gets the reason the lease was lost.
        /// </summary>
        public string Reason { get; }

        /// <summary>
        /// Gets the timestamp of the last successful renewal.
        /// </summary>
        public DateTimeOffset LastSuccessfulRenewal { get; }
    }
}
