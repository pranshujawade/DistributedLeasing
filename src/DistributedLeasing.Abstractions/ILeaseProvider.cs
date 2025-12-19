using System;
using System.Threading;
using System.Threading.Tasks;
using DistributedLeasing.Core;

namespace DistributedLeasing.Abstractions
{
    /// <summary>
    /// Defines the contract for storage-specific lease provider implementations.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Implementations of this interface are responsible for the actual storage operations
    /// required to acquire, renew, and release leases. Each provider encapsulates the logic
    /// specific to its storage backend (e.g., Azure Blob Storage, Cosmos DB, Redis).
    /// </para>
    /// <para>
    /// This interface follows the Strategy pattern, allowing different storage implementations
    /// to be used interchangeably through the common <see cref="ILeaseManager"/> interface.
    /// </para>
    /// </remarks>
    public interface ILeaseProvider
    {
        /// <summary>
        /// Attempts to acquire a lease on the specified resource.
        /// </summary>
        /// <param name="leaseName">
        /// The unique name of the resource to lease.
        /// </param>
        /// <param name="duration">
        /// The duration for which the lease should be held.
        /// </param>
        /// <param name="cancellationToken">
        /// A cancellation token to cancel the operation.
        /// </param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains
        /// the <see cref="ILease"/> if acquisition was successful, or <c>null</c> if the
        /// lease is currently held by another instance.
        /// </returns>
        /// <remarks>
        /// Implementations must handle provider-specific lease acquisition logic, including:
        /// <list type="bullet">
        /// <item>Checking if the resource is available</item>
        /// <item>Atomically acquiring the lease if available</item>
        /// <item>Setting appropriate expiration/TTL</item>
        /// <item>Returning null if another instance holds the lease</item>
        /// <item>Throwing appropriate exceptions for unexpected errors</item>
        /// </list>
        /// </remarks>
        Task<ILease?> AcquireLeaseAsync(
            string leaseName,
            TimeSpan duration,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Renews an existing lease, extending its expiration time.
        /// </summary>
        /// <param name="lease">
        /// The lease to renew.
        /// </param>
        /// <param name="cancellationToken">
        /// A cancellation token to cancel the operation.
        /// </param>
        /// <returns>
        /// A task that represents the asynchronous renewal operation.
        /// </returns>
        /// <remarks>
        /// Implementations must:
        /// <list type="bullet">
        /// <item>Verify ownership of the lease (using lease ID)</item>
        /// <item>Extend the expiration time atomically</item>
        /// <item>Throw <see cref="Core.Exceptions.LeaseRenewalException"/> if renewal fails</item>
        /// <item>Handle race conditions where the lease has expired</item>
        /// </list>
        /// </remarks>
        Task RenewLeaseAsync(
            ILease lease,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Releases a lease, making it immediately available for other instances.
        /// </summary>
        /// <param name="lease">
        /// The lease to release.
        /// </param>
        /// <param name="cancellationToken">
        /// A cancellation token to cancel the operation.
        /// </param>
        /// <returns>
        /// A task that represents the asynchronous release operation.
        /// </returns>
        /// <remarks>
        /// Implementations must:
        /// <list type="bullet">
        /// <item>Verify ownership of the lease before releasing</item>
        /// <item>Atomically release the lease</item>
        /// <item>Be idempotent (releasing an already-released lease should not error)</item>
        /// <item>Not throw exceptions for normal release scenarios</item>
        /// </list>
        /// </remarks>
        Task ReleaseLeaseAsync(
            ILease lease,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Forcibly breaks a lease, regardless of ownership.
        /// </summary>
        /// <param name="leaseName">
        /// The name of the lease to break.
        /// </param>
        /// <param name="cancellationToken">
        /// A cancellation token to cancel the operation.
        /// </param>
        /// <returns>
        /// A task that represents the asynchronous break operation.
        /// </returns>
        /// <remarks>
        /// <para>
        /// This is an administrative operation that should be used with caution.
        /// Breaking a lease held by another instance can cause that instance's operations
        /// to fail unexpectedly.
        /// </para>
        /// <para>
        /// Implementations should:
        /// <list type="bullet">
        /// <item>Break the lease without ownership verification</item>
        /// <item>Make the lease immediately available</item>
        /// <item>Log the break operation for audit purposes</item>
        /// </list>
        /// </para>
        /// </remarks>
        Task BreakLeaseAsync(
            string leaseName,
            CancellationToken cancellationToken = default);
    }
}
