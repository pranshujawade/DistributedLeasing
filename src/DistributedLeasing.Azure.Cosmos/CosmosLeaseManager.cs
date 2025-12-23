using DistributedLeasing.Abstractions;
using DistributedLeasing.Abstractions.Contracts;
using DistributedLeasing.Abstractions.Core;
using Microsoft.Azure.Cosmos;

namespace DistributedLeasing.Azure.Cosmos;

/// <summary>
/// Lease manager implementation for Azure Cosmos DB.
/// </summary>
/// <remarks>
/// This manager uses <see cref="CosmosLeaseProvider"/> to manage leases in Cosmos DB.
/// Supports automatic renewal and retry logic with exponential backoff.
/// </remarks>
internal class CosmosLeaseManager : LeaseManagerBase, IDisposable
{
    private readonly CosmosLeaseProvider _provider;
    private readonly bool _ownsProvider;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="CosmosLeaseManager"/> class.
    /// </summary>
    /// <param name="options">Configuration options for the Cosmos DB provider.</param>
    /// <exception cref="ArgumentNullException">Thrown when options is null.</exception>
    public CosmosLeaseManager(CosmosLeaseProviderOptions options)
        : base(new CosmosLeaseProvider(options), options)
    {
        _provider = (CosmosLeaseProvider)Provider;
        _ownsProvider = true;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CosmosLeaseManager"/> class with an existing client.
    /// </summary>
    /// <param name="cosmosClient">An existing Cosmos DB client.</param>
    /// <param name="options">Configuration options for the Cosmos DB provider.</param>
    /// <exception cref="ArgumentNullException">Thrown when cosmosClient or options is null.</exception>
    public CosmosLeaseManager(CosmosClient cosmosClient, CosmosLeaseProviderOptions options)
        : base(new CosmosLeaseProvider(cosmosClient, options), options)
    {
        _provider = (CosmosLeaseProvider)Provider;
        _ownsProvider = false;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CosmosLeaseManager"/> class with an existing provider.
    /// </summary>
    /// <param name="provider">An existing Cosmos DB lease provider.</param>
    /// <param name="options">Configuration options.</param>
    /// <exception cref="ArgumentNullException">Thrown when provider or options is null.</exception>
    public CosmosLeaseManager(CosmosLeaseProvider provider, CosmosLeaseProviderOptions options)
        : base(provider, options)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _ownsProvider = false;
    }

    /// <summary>
    /// Releases all resources used by the manager.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            if (_ownsProvider)
            {
                _provider?.Dispose();
            }
            _disposed = true;
        }
    }
}
