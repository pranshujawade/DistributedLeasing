using DistributedLeasing.Abstractions;
using DistributedLeasing.Core;
using DistributedLeasing.Core.Configuration;
using DistributedLeasing.Core.Exceptions;
using Microsoft.Azure.Cosmos;

namespace DistributedLeasing.Azure.Cosmos;

/// <summary>
/// Represents a lease acquired from Azure Cosmos DB.
/// </summary>
/// <remarks>
/// This implementation uses optimistic concurrency control via ETag.
/// Renewal updates the expiration time and requires matching the current ETag.
/// </remarks>
internal class CosmosLease : LeaseBase
{
    private readonly Container _container;
    private readonly string _partitionKey;
    private string _etag;

    /// <summary>
    /// Initializes a new instance of the <see cref="CosmosLease"/> class.
    /// </summary>
    /// <param name="container">The Cosmos DB container.</param>
    /// <param name="leaseId">The unique identifier for this lease.</param>
    /// <param name="leaseName">The name of the lease.</param>
    /// <param name="acquiredAt">The UTC timestamp when the lease was acquired.</param>
    /// <param name="duration">The duration of the lease.</param>
    /// <param name="etag">The ETag of the lease document.</param>
    /// <param name="partitionKey">The partition key value.</param>
    /// <param name="options">Optional lease configuration including auto-renewal settings.</param>
    public CosmosLease(
        Container container,
        string leaseId,
        string leaseName,
        DateTimeOffset acquiredAt,
        TimeSpan duration,
        string etag,
        string partitionKey,
        CosmosLeaseProviderOptions? options = null)
        : base(leaseId, leaseName, acquiredAt, duration, options)
    {
        _container = container ?? throw new ArgumentNullException(nameof(container));
        _etag = etag ?? throw new ArgumentNullException(nameof(etag));
        _partitionKey = partitionKey ?? throw new ArgumentNullException(nameof(partitionKey));
    }

    /// <summary>
    /// Renews the lease by updating the expiration time in Cosmos DB.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="LeaseRenewalException">Thrown when renewal fails.</exception>
    /// <exception cref="LeaseLostException">Thrown when the lease has been acquired by another holder.</exception>
    protected override async Task RenewLeaseAsync(CancellationToken cancellationToken)
    {
        try
        {
            var newExpiration = DateTimeOffset.UtcNow.Add(ExpiresAt - AcquiredAt);

            // Read the current document
            var response = await _container.ReadItemAsync<Models.LeaseDocument>(
                _partitionKey,
                new PartitionKey(_partitionKey),
                cancellationToken: cancellationToken);

            var document = response.Resource;

            // Verify we still own the lease
            if (document.LeaseId != LeaseId)
            {
                throw new LeaseLostException($"Lease '{LeaseName}' has been acquired by another holder.")
                {
                    LeaseName = LeaseName,
                    LeaseId = LeaseId
                };
            }

            // Update expiration and renewal metadata
            document.ExpiresAt = newExpiration;
            document.LastRenewedAt = DateTimeOffset.UtcNow;
            document.RenewalCount++;

            // Perform conditional update with ETag
            var requestOptions = new ItemRequestOptions
            {
                IfMatchEtag = _etag
            };

            var updateResponse = await _container.ReplaceItemAsync(
                document,
                document.Id,
                new PartitionKey(_partitionKey),
                requestOptions,
                cancellationToken);

            // Update our local ETag and expiration
            _etag = updateResponse.ETag;
            //UpdateExpiration(newExpiration);
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.PreconditionFailed)
        {
            // ETag mismatch - someone else modified the lease
            throw new LeaseLostException($"Lease '{LeaseName}' was modified by another process during renewal.", ex)
            {
                LeaseName = LeaseName,
                LeaseId = LeaseId
            };
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Lease document was deleted
            var leaseLostException = new LeaseLostException(
                $"Lease '{LeaseName}' document was deleted.",
                ex)
            {
                LeaseName = LeaseName,
                LeaseId = LeaseId
            };
            throw leaseLostException;
        }
        catch (CosmosException ex)
        {
            throw new LeaseRenewalException(
                $"Failed to renew lease '{LeaseName}' in Cosmos DB: {ex.Message}",
                ex)
            {
                LeaseName = LeaseName,
                LeaseId = LeaseId
            };
        }
    }

    /// <summary>
    /// Releases the lease by deleting the document from Cosmos DB.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// This operation is idempotent and will not throw if the lease is already released.
    /// </remarks>
    protected override async Task ReleaseLeaseAsync(CancellationToken cancellationToken)
    {
        try
        {
            // First, verify we still own the lease
            var response = await _container.ReadItemAsync<Models.LeaseDocument>(
                _partitionKey,
                new PartitionKey(_partitionKey),
                cancellationToken: cancellationToken);

            var document = response.Resource;

            // Only delete if we still own it
            if (document.LeaseId == LeaseId)
            {
                var requestOptions = new ItemRequestOptions
                {
                    IfMatchEtag = _etag
                };

                await _container.DeleteItemAsync<Models.LeaseDocument>(
                    _partitionKey,
                    new PartitionKey(_partitionKey),
                    requestOptions,
                    cancellationToken);
            }
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Lease already deleted - idempotent behavior
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.PreconditionFailed)
        {
            // ETag mismatch - someone else modified/deleted the lease
            // This is acceptable during release
        }
        catch (Exception)
        {
            // Swallow exceptions during release for idempotency
            // The TTL will eventually clean up the document
        }
    }
}
