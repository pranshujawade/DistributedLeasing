using Azure.Identity;
using DistributedLeasing.Abstractions;
using DistributedLeasing.Azure.Cosmos.Models;
using DistributedLeasing.Core;
using DistributedLeasing.Core.Exceptions;
using Microsoft.Azure.Cosmos;

namespace DistributedLeasing.Azure.Cosmos;

/// <summary>
/// Azure Cosmos DB implementation of <see cref="ILeaseProvider"/>.
/// </summary>
/// <remarks>
/// This provider uses optimistic concurrency control via ETag for distributed locking.
/// Leases are stored as documents in a Cosmos DB container with automatic TTL cleanup.
/// </remarks>
public class CosmosLeaseProvider : ILeaseProvider, IDisposable
{
    private readonly CosmosLeaseProviderOptions _options;
    private readonly CosmosClient _cosmosClient;
    private readonly Container _container;
    private readonly bool _ownsClient;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="CosmosLeaseProvider"/> class.
    /// </summary>
    /// <param name="options">Configuration options for the provider.</param>
    /// <exception cref="ArgumentNullException">Thrown when options is null.</exception>
    public CosmosLeaseProvider(CosmosLeaseProviderOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _options.Validate();

        _cosmosClient = CreateCosmosClient(options);
        _ownsClient = true;
        _container = InitializeContainerAsync().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CosmosLeaseProvider"/> class with an existing client.
    /// </summary>
    /// <param name="cosmosClient">An existing Cosmos DB client.</param>
    /// <param name="options">Configuration options for the provider.</param>
    /// <exception cref="ArgumentNullException">Thrown when cosmosClient or options is null.</exception>
    public CosmosLeaseProvider(CosmosClient cosmosClient, CosmosLeaseProviderOptions options)
    {
        _cosmosClient = cosmosClient ?? throw new ArgumentNullException(nameof(cosmosClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _options.Validate();

        _ownsClient = false;
        _container = InitializeContainerAsync().GetAwaiter().GetResult();
    }

    /// <inheritdoc/>
    public async Task<ILease?> AcquireLeaseAsync(
        string leaseName,
        TimeSpan duration,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(leaseName))
        {
            throw new ArgumentException("Lease name cannot be null or whitespace.", nameof(leaseName));
        }

        if (duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), "Duration must be positive.");
        }

        if (_disposed)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        var leaseId = Guid.NewGuid().ToString();
        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.Add(duration);
        var documentId = GetDocumentId(leaseName);

        try
        {
            // Try to read existing lease
            var readResponse = await _container.ReadItemAsync<LeaseDocument>(
                documentId,
                new PartitionKey(documentId),
                cancellationToken: cancellationToken);

            var existingLease = readResponse.Resource;

            // Check if expired
            if (existingLease.ExpiresAt > DateTimeOffset.UtcNow)
            {
                // Lease is still active
                return null;
            }

            // Lease expired, try to acquire it by updating
            existingLease.LeaseId = leaseId;
            existingLease.AcquiredAt = now;
            existingLease.ExpiresAt = expiresAt;
            existingLease.DurationSeconds = (int)duration.TotalSeconds;
            existingLease.Owner = Environment.MachineName;
            existingLease.LastRenewedAt = null;
            existingLease.RenewalCount = 0;

            var requestOptions = new ItemRequestOptions
            {
                IfMatchEtag = readResponse.ETag
            };

            var updateResponse = await _container.ReplaceItemAsync(
                existingLease,
                documentId,
                new PartitionKey(documentId),
                requestOptions,
                cancellationToken);

            return new CosmosLease(
                _container,
                leaseId,
                leaseName,
                now,
                duration,
                updateResponse.ETag,
                documentId,
                _options);
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Lease doesn't exist, create it
            var newDocument = new LeaseDocument
            {
                Id = documentId,
                LeaseName = leaseName,
                LeaseId = leaseId,
                AcquiredAt = now,
                ExpiresAt = expiresAt,
                DurationSeconds = (int)duration.TotalSeconds,
                Owner = Environment.MachineName,
                TimeToLive = _options.DefaultTimeToLive,
                RenewalCount = 0
            };

            try
            {
                var createResponse = await _container.CreateItemAsync(
                    newDocument,
                    new PartitionKey(documentId),
                    cancellationToken: cancellationToken);

                return new CosmosLease(
                    _container,
                    leaseId,
                    leaseName,
                    now,
                    duration,
                    createResponse.ETag,
                    documentId,
                    _options);
            }
            catch (CosmosException createEx) when (createEx.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                // Someone else created it first
                return null;
            }
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.PreconditionFailed)
        {
            // ETag mismatch - someone else updated it
            return null;
        }
        catch (CosmosException ex)
        {
            throw new LeaseAcquisitionException(
                $"Failed to acquire lease '{leaseName}' from Cosmos DB: {ex.Message}",
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
        {
            throw new ArgumentException("Lease name cannot be null or whitespace.", nameof(leaseName));
        }

        if (_disposed)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        var documentId = GetDocumentId(leaseName);

        try
        {
            await _container.DeleteItemAsync<LeaseDocument>(
                documentId,
                new PartitionKey(documentId),
                cancellationToken: cancellationToken);
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Lease doesn't exist - already broken
        }
    }

    /// <summary>
    /// Releases all resources used by the provider.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            if (_ownsClient)
            {
                _cosmosClient?.Dispose();
            }
            _disposed = true;
        }
    }

    private static CosmosClient CreateCosmosClient(CosmosLeaseProviderOptions options)
    {
        var clientOptions = new CosmosClientOptions
        {
            SerializerOptions = new CosmosSerializationOptions
            {
                PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
            }
        };

        // Set consistency level if specified
        if (!string.IsNullOrWhiteSpace(options.ConsistencyLevel) &&
            Enum.TryParse<ConsistencyLevel>(options.ConsistencyLevel, out var consistencyLevel))
        {
            clientOptions.ConsistencyLevel = consistencyLevel;
        }

        // Prioritize authentication methods
        if (options.Credential != null)
        {
            return new CosmosClient(options.AccountEndpoint!.ToString(), options.Credential, clientOptions);
        }

        if (options.UseManagedIdentity)
        {
            return new CosmosClient(options.AccountEndpoint!.ToString(), new DefaultAzureCredential(), clientOptions);
        }

        if (!string.IsNullOrWhiteSpace(options.AccountKey))
        {
            return new CosmosClient(options.AccountEndpoint!.ToString(), options.AccountKey, clientOptions);
        }

        if (!string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            return new CosmosClient(options.ConnectionString, clientOptions);
        }

        throw new InvalidOperationException("No valid authentication method configured.");
    }

    private async Task<Container> InitializeContainerAsync()
    {
        Database database;

        if (_options.CreateDatabaseIfNotExists)
        {
            var databaseResponse = await _cosmosClient.CreateDatabaseIfNotExistsAsync(
                _options.DatabaseName,
                _options.ProvisionedThroughput);
            database = databaseResponse.Database;
        }
        else
        {
            database = _cosmosClient.GetDatabase(_options.DatabaseName);
        }

        Container container;

        if (_options.CreateContainerIfNotExists)
        {
            var containerProperties = new ContainerProperties
            {
                Id = _options.ContainerName,
                PartitionKeyPath = _options.PartitionKeyPath
            };

            if (_options.DefaultTimeToLive.HasValue)
            {
                containerProperties.DefaultTimeToLive = _options.DefaultTimeToLive.Value;
            }

            var containerResponse = await database.CreateContainerIfNotExistsAsync(
                containerProperties,
                _options.ProvisionedThroughput);
            container = containerResponse.Container;
        }
        else
        {
            container = database.GetContainer(_options.ContainerName);
        }

        return container;
    }

    private static string GetDocumentId(string leaseName)
    {
        // Use lease name as document ID (normalized for safety)
        return leaseName.ToLowerInvariant().Replace(" ", "-");
    }
}
