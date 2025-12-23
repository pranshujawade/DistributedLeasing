using System;

namespace DistributedLeasing.Abstractions.Events
{
    /// <summary>
    /// Event arguments for the LeaseRenewalFailed event.
    /// </summary>
    public class LeaseRenewalFailedEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LeaseRenewalFailedEventArgs"/> class.
        /// </summary>
        /// <param name="leaseName">The name of the lease that failed to renew.</param>
        /// <param name="leaseId">The ID of the lease that failed to renew.</param>
        /// <param name="timestamp">The timestamp when the failure occurred.</param>
        /// <param name="attemptNumber">The number of this renewal attempt.</param>
        /// <param name="exception">The exception that caused the failure.</param>
        /// <param name="willRetry">Indicates whether another retry will be attempted.</param>
        public LeaseRenewalFailedEventArgs(
            string leaseName,
            string leaseId,
            DateTimeOffset timestamp,
            int attemptNumber,
            Exception exception,
            bool willRetry)
        {
            LeaseName = leaseName ?? throw new ArgumentNullException(nameof(leaseName));
            LeaseId = leaseId ?? throw new ArgumentNullException(nameof(leaseId));
            Timestamp = timestamp;
            AttemptNumber = attemptNumber;
            Exception = exception;
            WillRetry = willRetry;
        }

        /// <summary>
        /// Gets the name of the lease that failed to renew.
        /// </summary>
        public string LeaseName { get; }

        /// <summary>
        /// Gets the ID of the lease that failed to renew.
        /// </summary>
        public string LeaseId { get; }

        /// <summary>
        /// Gets the timestamp when the failure occurred.
        /// </summary>
        public DateTimeOffset Timestamp { get; }

        /// <summary>
        /// Gets the number of this renewal attempt.
        /// </summary>
        public int AttemptNumber { get; }

        /// <summary>
        /// Gets the exception that caused the failure.
        /// </summary>
        public Exception Exception { get; }

        /// <summary>
        /// Gets a value indicating whether another retry will be attempted.
        /// </summary>
        public bool WillRetry { get; }
    }
}
