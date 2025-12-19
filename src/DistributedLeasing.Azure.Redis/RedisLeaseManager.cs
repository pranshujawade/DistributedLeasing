using DistributedLeasing.Abstractions;
using StackExchange.Redis;

namespace DistributedLeasing.Azure.Redis;

/// <summary>
/// Lease manager implementation for Azure Redis.
/// </summary>
/// <remarks>
/// This manager uses <see cref="RedisLeaseProvider"/> to manage leases in Redis.
/// Supports automatic renewal and retry logic with exponential backoff.
/// </remarks>
public class RedisLeaseManager : LeaseManagerBase, IDisposable
{
    private readonly RedisLeaseProvider _provider;
    private readonly bool _ownsProvider;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisLeaseManager"/> class.
    /// </summary>
    /// <param name="options">Configuration options for the Redis provider.</param>
    /// <exception cref="ArgumentNullException">Thrown when options is null.</exception>
    public RedisLeaseManager(RedisLeaseProviderOptions options)
        : base(new RedisLeaseProvider(options), options)
    {
        _provider = (RedisLeaseProvider)Provider;
        _ownsProvider = true;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisLeaseManager"/> class with an existing connection.
    /// </summary>
    /// <param name="connection">An existing Redis connection multiplexer.</param>
    /// <param name="options">Configuration options for the Redis provider.</param>
    /// <exception cref="ArgumentNullException">Thrown when connection or options is null.</exception>
    public RedisLeaseManager(IConnectionMultiplexer connection, RedisLeaseProviderOptions options)
        : base(new RedisLeaseProvider(connection, options), options)
    {
        _provider = (RedisLeaseProvider)Provider;
        _ownsProvider = false;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisLeaseManager"/> class with an existing provider.
    /// </summary>
    /// <param name="provider">An existing Redis lease provider.</param>
    /// <param name="options">Configuration options.</param>
    /// <exception cref="ArgumentNullException">Thrown when provider or options is null.</exception>
    public RedisLeaseManager(RedisLeaseProvider provider, RedisLeaseProviderOptions options)
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
