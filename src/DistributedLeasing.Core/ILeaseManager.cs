using System;
using System.Threading;
using System.Threading.Tasks;

namespace DistributedLeasing.Core
{
    /// <summary>
    /// Defines the primary entry point for managing distributed leases.
    /// Provides factory methods for creating and acquiring leases.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The lease manager abstracts the underlying storage provider and provides a
    /// consistent API for lease operations regardless of the backing store (Azure Blob,
    /// Cosmos DB, Redis, etc.).
    /// </para>
    /// <para>
    /// Implementations should be thread-safe and suitable for use as singletons in
    /// dependency injection containers to enable connection pooling and resource sharing.
    /// </para>
    /// </remarks>
    public interface ILeaseManager
    {
        /// <summary>
        /// Attempts to acquire a lease without blocking.
        /// </summary>
        /// <param name="leaseName">
        /// The name of the resource to lease. Must be unique within the scope of the provider.
        /// </param>
        /// <param name="duration">
        /// The duration for which the lease should be held. If not specified, uses the
        /// configured default duration.
        /// </param>
        /// <param name="cancellationToken">
        /// A cancellation token to cancel the acquisition operation.
        /// </param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains
        /// the acquired <see cref="ILease"/> if successful, or <c>null</c> if the lease
        /// is currently held by another instance.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="leaseName"/> is null or empty.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="duration"/> is outside the valid range for the provider.
        /// </exception>
        /// <exception cref="LeaseAcquisitionException">
        /// Thrown when an unexpected error occurs during lease acquisition.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// Thrown when the operation is canceled via the <paramref name="cancellationToken"/>.
        /// </exception>
        /// <remarks>
        /// This method returns immediately and does not wait for the lease to become available.
        /// Use <see cref="AcquireAsync"/> if you want to wait for the lease.
        /// </remarks>
        Task<ILease?> TryAcquireAsync(
            string leaseName,
            TimeSpan? duration = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Acquires a lease, waiting for it to become available if necessary.
        /// </summary>
        /// <param name="leaseName">
        /// The name of the resource to lease. Must be unique within the scope of the provider.
        /// </param>
        /// <param name="duration">
        /// The duration for which the lease should be held. If not specified, uses the
        /// configured default duration.
        /// </param>
        /// <param name="timeout">
        /// The maximum time to wait for the lease to become available. If not specified,
        /// uses the configured default acquire timeout.
        /// </param>
        /// <param name="cancellationToken">
        /// A cancellation token to cancel the acquisition operation.
        /// </param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains
        /// the acquired <see cref="ILease"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="leaseName"/> is null or empty.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="duration"/> or <paramref name="timeout"/> is outside
        /// the valid range.
        /// </exception>
        /// <exception cref="TimeoutException">
        /// Thrown when the lease could not be acquired within the specified <paramref name="timeout"/>.
        /// </exception>
        /// <exception cref="LeaseAcquisitionException">
        /// Thrown when an unexpected error occurs during lease acquisition.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// Thrown when the operation is canceled via the <paramref name="cancellationToken"/>.
        /// </exception>
        /// <remarks>
        /// This method will retry acquisition until successful, timeout is reached, or cancellation
        /// is requested. The retry interval is determined by the provider configuration.
        /// </remarks>
        Task<ILease> AcquireAsync(
            string leaseName,
            TimeSpan? duration = null,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default);
    }
}
