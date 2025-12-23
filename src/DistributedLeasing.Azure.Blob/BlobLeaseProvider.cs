using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using DistributedLeasing.Abstractions;
using DistributedLeasing.Abstractions.Contracts;
using DistributedLeasing.Abstractions.Authentication;
using DistributedLeasing.Abstractions.Exceptions;
using Microsoft.Extensions.Logging;

namespace DistributedLeasing.Azure.Blob
{
    /// <summary>
    /// Azure Blob Storage implementation of <see cref="ILeaseProvider"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This provider uses native Azure Blob Storage lease capabilities to implement distributed leasing.
    /// Each lease is represented by a blob in a designated container, and the blob's lease state
    /// provides the distributed locking mechanism.
    /// </para>
    /// <para>
    /// Azure Blob Storage leases support durations from 15 to 60 seconds, or infinite leases.
    /// The provider automatically creates empty blobs for leases and manages their lifecycle.
    /// </para>
    /// </remarks>
    public class BlobLeaseProvider : ILeaseProvider
    {
        private readonly BlobServiceClient _blobServiceClient;
        private readonly BlobLeaseProviderOptions _options;
        private readonly BlobContainerClient _containerClient;
        private volatile bool _containerInitialized;
        private readonly SemaphoreSlim _containerInitLock = new SemaphoreSlim(1, 1);

        /// <summary>
        /// Initializes a new instance of the <see cref="BlobLeaseProvider"/> class.
        /// </summary>
        /// <param name="options">The configuration options.</param>
        /// <exception cref="ArgumentNullException">Thrown when options is null.</exception>
        public BlobLeaseProvider(BlobLeaseProviderOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _options.Validate();

            _blobServiceClient = CreateBlobServiceClient(options);
            _containerClient = _blobServiceClient.GetBlobContainerClient(_options.ContainerName);
        }

        /// <inheritdoc/>
        public async Task<ILease?> AcquireLeaseAsync(
            string leaseName,
            TimeSpan duration,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(leaseName))
                throw new ArgumentNullException(nameof(leaseName));

            ValidateDuration(duration);

            try
            {
                // Ensure container exists
                await EnsureContainerExistsAsync(cancellationToken).ConfigureAwait(false);

                // Get or create the blob for this lease
                var blobClient = await GetOrCreateBlobAsync(leaseName, cancellationToken)
                    .ConfigureAwait(false);

                // Attempt to acquire the lease
                var leaseClient = blobClient.GetBlobLeaseClient();
                
                var leaseDuration = duration == Timeout.InfiniteTimeSpan
                    ? TimeSpan.FromSeconds(-1) // Azure uses -1 for infinite
                    : duration;

                var response = await leaseClient.AcquireAsync(
                    leaseDuration,
                    cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                // Pass options to enable auto-renewal support
                return new BlobLease(leaseClient, leaseName, duration, _options);
            }
            catch (RequestFailedException ex) when (ex.Status == 409)
            {
                // 409 Conflict - lease is already held by another instance
                return null;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                throw new ProviderUnavailableException(
                    $"Storage container '{_options.ContainerName}' not found.",
                    ex)
                {
                    ProviderName = "BlobLeaseProvider"
                };
            }
            catch (RequestFailedException ex) when (ex.Status >= 500)
            {
                throw new ProviderUnavailableException(
                    $"Azure Blob Storage is experiencing issues: {ex.Message}",
                    ex)
                {
                    ProviderName = "BlobLeaseProvider"
                };
            }
            catch (RequestFailedException ex)
            {
                throw new LeaseAcquisitionException(
                    $"Failed to acquire lease '{leaseName}': {ex.Message}",
                    ex)
                {
                    LeaseName = leaseName
                };
            }
        }

        /// <inheritdoc/>
        public async Task BreakLeaseAsync(string leaseName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(leaseName))
                throw new ArgumentNullException(nameof(leaseName));

            try
            {
                var blobName = GetBlobName(leaseName);
                var blobClient = _containerClient.GetBlobClient(blobName);
                var leaseClient = blobClient.GetBlobLeaseClient();

                await leaseClient.BreakAsync(
                    breakPeriod: TimeSpan.Zero,
                    cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                // Blob doesn't exist - nothing to break
            }
            catch (RequestFailedException ex)
            {
                throw new LeaseException(
                    $"Failed to break lease '{leaseName}': {ex.Message}",
                    ex)
                {
                    LeaseName = leaseName
                };
            }
        }

        /// <summary>
        /// Creates a BlobServiceClient based on the configured authentication method.
        /// </summary>
        private static BlobServiceClient CreateBlobServiceClient(BlobLeaseProviderOptions options)
        {
            // Priority 1: Connection string (simplest, for development)
            if (!string.IsNullOrEmpty(options.ConnectionString))
            {   
                return new BlobServiceClient(options.ConnectionString);
            }
            
            // Priority 2: Direct credential injection (for advanced scenarios)
            if (options.Credential != null && options.StorageAccountUri != null)
            {
                return new BlobServiceClient(options.StorageAccountUri, options.Credential);
            }
            
            // Priority 3: Authentication configuration (recommended for production)
            if (options.Authentication != null && options.StorageAccountUri != null)
            {
                var factory = new AuthenticationFactory();
                var credential = factory.CreateCredential(options.Authentication);
                return new BlobServiceClient(options.StorageAccountUri, credential);
            }
            
            // Fallback: DefaultAzureCredential (for managed identity without explicit config)
            if (options.StorageAccountUri != null)
            {
                return new BlobServiceClient(options.StorageAccountUri, new DefaultAzureCredential());
            }

            throw new InvalidOperationException(
                "No valid authentication method configured. " +
                "Provide ConnectionString, or configure Authentication/Credential with StorageAccountUri.");
        }

        /// <summary>
        /// Ensures the container exists, creating it if necessary.
        /// </summary>
        private async Task EnsureContainerExistsAsync(CancellationToken cancellationToken)
        {
            if (_containerInitialized)
                return;

            await _containerInitLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_containerInitialized)
                    return;

                if (_options.CreateContainerIfNotExists)
                {
                    await _containerClient.CreateIfNotExistsAsync(
                        cancellationToken: cancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    // Verify container exists
                    var exists = await _containerClient.ExistsAsync(cancellationToken)
                        .ConfigureAwait(false);
                    
                    if (!exists.Value)
                    {
                        throw new ProviderUnavailableException(
                            $"Container '{_options.ContainerName}' does not exist and CreateContainerIfNotExists is false.")
                        {
                            ProviderName = "BlobLeaseProvider"
                        };
                    }
                }

                _containerInitialized = true;
            }
            finally
            {
                _containerInitLock.Release();
            }
        }

        /// <summary>
        /// Gets or creates the blob for the specified lease name.
        /// </summary>
        private async Task<BlobClient> GetOrCreateBlobAsync(
            string leaseName,
            CancellationToken cancellationToken)
        {
            var blobName = GetBlobName(leaseName);
            var blobClient = _containerClient.GetBlobClient(blobName);

            try
            {
                // Check if blob exists
                var exists = await blobClient.ExistsAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (!exists.Value)
                {
                    // Create an empty blob with metadata
                    var metadata = new System.Collections.Generic.Dictionary<string, string>
                    {
                        { "leaseName", leaseName },
                        { "createdAt", DateTimeOffset.UtcNow.ToString("o") }
                    };

                    using var emptyStream = new MemoryStream(Array.Empty<byte>());
                    await blobClient.UploadAsync(
                        emptyStream,
                        metadata: metadata,
                        cancellationToken: cancellationToken)
                        .ConfigureAwait(false);
                }
            }
            catch (RequestFailedException ex) when (ex.Status == 409)
            {
                // Blob was created by another instance concurrently - this is fine
            }

            return blobClient;
        }

        /// <summary>
        /// Gets the blob name for a lease name.
        /// </summary>
        private string GetBlobName(string leaseName)
        {
            return $"{_options.BlobPrefix}{leaseName}";
        }

        /// <summary>
        /// Validates the lease duration against Azure Blob Storage limits.
        /// </summary>
        private void ValidateDuration(TimeSpan duration)
        {
            if (duration != Timeout.InfiniteTimeSpan)
            {
                if (duration < _options.MinLeaseDuration)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(duration),
                        $"Duration must be at least {_options.MinLeaseDuration.TotalSeconds} seconds for Azure Blob Storage.");
                }

                if (duration > _options.MaxLeaseDuration)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(duration),
                        $"Duration must be at most {_options.MaxLeaseDuration.TotalSeconds} seconds for Azure Blob Storage.");
                }
            }
        }
    }
}
