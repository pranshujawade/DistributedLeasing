using System;

namespace DistributedLeasing.Core.Events
{
    /// <summary>
    /// Event arguments for the LeaseRenewed event.
    /// </summary>
    public class LeaseRenewedEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LeaseRenewedEventArgs"/> class.
        /// </summary>
        /// <param name="leaseName">The name of the lease that was renewed.</param>
        /// <param name="leaseId">The ID of the lease that was renewed.</param>
        /// <param name="timestamp">The timestamp when the renewal occurred.</param>
        /// <param name="newExpiration">The new expiration time after renewal.</param>
        /// <param name="renewalDuration">The duration by which the lease was extended.</param>
        public LeaseRenewedEventArgs(
            string leaseName,
            string leaseId,
            DateTimeOffset timestamp,
            DateTimeOffset newExpiration,
            TimeSpan renewalDuration)
        {
            LeaseName = leaseName ?? throw new ArgumentNullException(nameof(leaseName));
            LeaseId = leaseId ?? throw new ArgumentNullException(nameof(leaseId));
            Timestamp = timestamp;
            NewExpiration = newExpiration;
            RenewalDuration = renewalDuration;
        }

        /// <summary>
        /// Gets the name of the lease that was renewed.
        /// </summary>
        public string LeaseName { get; }

        /// <summary>
        /// Gets the ID of the lease that was renewed.
        /// </summary>
        public string LeaseId { get; }

        /// <summary>
        /// Gets the timestamp when the renewal occurred.
        /// </summary>
        public DateTimeOffset Timestamp { get; }

        /// <summary>
        /// Gets the new expiration time after renewal.
        /// </summary>
        public DateTimeOffset NewExpiration { get; }

        /// <summary>
        /// Gets the duration by which the lease was extended.
        /// </summary>
        public TimeSpan RenewalDuration { get; }
    }
}
