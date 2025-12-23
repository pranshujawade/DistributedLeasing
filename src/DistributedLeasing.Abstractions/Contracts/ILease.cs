using System;
using System.Threading;
using System.Threading.Tasks;
using DistributedLeasing.Abstractions.Events;

namespace DistributedLeasing.Abstractions.Contracts
{
    /// <summary>
    /// Represents a distributed lease that provides exclusive access to a named resource.
    /// Implements the Async Dispose pattern for proper resource cleanup.
    /// </summary>
    /// <remarks>
    /// A lease is acquired from an <see cref="ILeaseManager"/> and must be properly disposed
    /// to release the lease. The lease can be renewed to extend its duration and should be
    /// released when no longer needed to allow other instances to acquire it.
    /// </remarks>
    public interface ILease : IAsyncDisposable
    {
        /// <summary>
        /// Gets the unique identifier for this lease instance.
        /// </summary>
        /// <value>
        /// A unique string identifier (typically a GUID) that identifies this specific lease acquisition.
        /// This ID is used for ownership verification during renewal and release operations.
        /// </value>
        string LeaseId { get; }

        /// <summary>
        /// Gets the logical name of the resource being leased.
        /// </summary>
        /// <value>
        /// The name that identifies the resource across all instances competing for the lease.
        /// Multiple instances attempting to acquire a lease with the same name compete for exclusive access.
        /// </value>
        string LeaseName { get; }

        /// <summary>
        /// Gets the timestamp when the lease was acquired.
        /// </summary>
        /// <value>
        /// The UTC time when this lease was successfully acquired.
        /// </value>
        DateTimeOffset AcquiredAt { get; }

        /// <summary>
        /// Gets the timestamp when the lease will expire if not renewed.
        /// </summary>
        /// <value>
        /// The UTC time when the lease is scheduled to expire. After this time,
        /// the lease may be acquired by another instance.
        /// </value>
        DateTimeOffset ExpiresAt { get; }

        /// <summary>
        /// Gets a value indicating whether the lease is currently acquired and valid.
        /// </summary>
        /// <value>
        /// <c>true</c> if the lease is held and has not expired; otherwise, <c>false</c>.
        /// This property should be checked before performing operations that require the lease.
        /// </value>
        bool IsAcquired { get; }
        
        /// <summary>
        /// Gets the number of times this lease has been successfully renewed.
        /// </summary>
        /// <value>
        /// The count of successful renewals since acquisition. This value is 0 when the lease
        /// is first acquired and increments with each successful renewal.
        /// </value>
        int RenewalCount { get; }
        
        /// <summary>
        /// Occurs when the lease is successfully renewed.
        /// </summary>
        /// <remarks>
        /// This event is raised each time the lease is successfully renewed, whether through
        /// automatic renewal or manual calls to <see cref="RenewAsync"/>.
        /// </remarks>
        event EventHandler<LeaseRenewedEventArgs>? LeaseRenewed;
        
        /// <summary>
        /// Occurs when a lease renewal attempt fails.
        /// </summary>
        /// <remarks>
        /// This event is raised when a renewal attempt fails. The event arguments indicate
        /// whether another retry will be attempted or if the lease will be marked as lost.
        /// </remarks>
        event EventHandler<LeaseRenewalFailedEventArgs>? LeaseRenewalFailed;
        
        /// <summary>
        /// Occurs when the lease is definitively lost and cannot be renewed.
        /// </summary>
        /// <remarks>
        /// This event is raised when the lease is lost due to repeated renewal failures or
        /// ownership verification failure. After this event, the lease cannot be renewed and
        /// <see cref="IsAcquired"/> will return false.
        /// </remarks>
        event EventHandler<LeaseLostEventArgs>? LeaseLost;

        /// <summary>
        /// Renews the lease, extending its expiration time.
        /// </summary>
        /// <param name="cancellationToken">
        /// A cancellation token to cancel the renewal operation.
        /// </param>
        /// <returns>
        /// A task that represents the asynchronous renewal operation.
        /// </returns>
        /// <exception cref="Exceptions.LeaseException">
        /// Thrown when the lease cannot be renewed, typically because it has already expired
        /// or another instance has acquired it.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// Thrown when the operation is canceled via the <paramref name="cancellationToken"/>.
        /// </exception>
        /// <remarks>
        /// Renewal should be performed before the lease expires to maintain continuous ownership.
        /// If renewal fails, the lease should be considered lost and operations should cease.
        /// </remarks>
        Task RenewAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Explicitly releases the lease, making it immediately available for other instances.
        /// </summary>
        /// <param name="cancellationToken">
        /// A cancellation token to cancel the release operation.
        /// </param>
        /// <returns>
        /// A task that represents the asynchronous release operation.
        /// </returns>
        /// <exception cref="OperationCanceledException">
        /// Thrown when the operation is canceled via the <paramref name="cancellationToken"/>.
        /// </exception>
        /// <remarks>
        /// Releasing a lease is optional as it will automatically expire, but explicit release
        /// allows other instances to acquire the lease more quickly. After release, this lease
        /// instance should not be used for further operations.
        /// </remarks>
        Task ReleaseAsync(CancellationToken cancellationToken = default);
    }
}
