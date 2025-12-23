using System;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs.Specialized;
using DistributedLeasing.Abstractions;
using DistributedLeasing.Abstractions.Contracts;
using DistributedLeasing.Abstractions.Core;
using DistributedLeasing.Abstractions.Configuration;
using DistributedLeasing.Abstractions.Exceptions;

namespace DistributedLeasing.Azure.Blob
{
    /// <summary>
    /// Represents a lease on an Azure Blob Storage blob.
    /// </summary>
    /// <remarks>
    /// This class wraps the Azure Blob Storage lease client and provides the distributed lease interface.
    /// It handles renewal and release operations using the native blob lease capabilities.
    /// </remarks>
    internal sealed class BlobLease : LeaseBase
    {
        private readonly BlobLeaseClient _leaseClient;
        private readonly TimeSpan _leaseDuration;

        /// <summary>
        /// Initializes a new instance of the <see cref="BlobLease"/> class.
        /// </summary>
        /// <param name="leaseClient">The Azure blob lease client.</param>
        /// <param name="leaseName">The name of the lease.</param>
        /// <param name="duration">The lease duration.</param>
        /// <param name="options">Optional lease configuration including auto-renewal settings.</param>
        public BlobLease(
            BlobLeaseClient leaseClient, 
            string leaseName, 
            TimeSpan duration,
            LeaseOptions? options = null)
            : base(leaseClient.LeaseId, leaseName, DateTimeOffset.UtcNow, duration, options)
        {
            _leaseClient = leaseClient ?? throw new ArgumentNullException(nameof(leaseClient));
            _leaseDuration = duration;
        }

        /// <summary>
        /// Renews the blob lease.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        protected override async Task RenewLeaseAsync(CancellationToken cancellationToken)
        {
            try
            {
                var response = await _leaseClient.RenewAsync(cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                // Update expiration time after successful renewal
                ExpiresAt = DateTimeOffset.UtcNow + _leaseDuration;
            }
            catch (RequestFailedException ex) when (ex.Status == 409)
            {
                // 409 Conflict - lease has been lost
                throw new LeaseRenewalException(
                    $"Failed to renew lease '{LeaseName}'. The lease may have expired or been acquired by another instance.",
                    ex)
                {
                    LeaseName = LeaseName,
                    LeaseId = LeaseId
                };
            }
            catch (RequestFailedException ex) when (ex.Status == 412)
            {
                // 412 Precondition Failed - lease ID mismatch
                throw new LeaseRenewalException(
                    $"Failed to renew lease '{LeaseName}'. Lease ID mismatch - the lease may have been broken.",
                    ex)
                {
                    LeaseName = LeaseName,
                    LeaseId = LeaseId
                };
            }
            catch (RequestFailedException ex)
            {
                throw new LeaseRenewalException(
                    $"Failed to renew lease '{LeaseName}' due to Azure Storage error: {ex.Message}",
                    ex)
                {
                    LeaseName = LeaseName,
                    LeaseId = LeaseId
                };
            }
        }

        /// <summary>
        /// Releases the blob lease.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        protected override async Task ReleaseLeaseAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _leaseClient.ReleaseAsync(cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                // 404 Not Found - blob or lease doesn't exist, consider it released
                // This is idempotent behavior
            }
            catch (RequestFailedException ex) when (ex.Status == 409)
            {
                // 409 Conflict - lease already released or expired, consider it successful
                // This is idempotent behavior
            }
            catch (RequestFailedException)
            {
                // Suppress other exceptions during release
                // The lease will expire naturally
            }
        }
    }
}
