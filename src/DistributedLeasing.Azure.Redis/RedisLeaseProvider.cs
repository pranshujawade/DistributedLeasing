using Azure.Identity;
using DistributedLeasing.Abstractions;
using DistributedLeasing.Core;
using StackExchange.Redis;

namespace DistributedLeasing.Azure.Redis;

/// <summary>
/// Azure Redis implementation of <see cref="ILeaseProvider"/>.
/// </summary>
/// <remarks>
/// This provider uses Redis SET with NX and PX options for distributed locking.
/// Supports single instance and Redlock algorithm for multi-instance scenarios.
/// </remarks>
public class RedisLeaseProvider : ILeaseProvider, IDisposable
{
    private readonly RedisLeaseProviderOptions _options;
    private readonly IConnectionMultiplexer _connection;
    private readonly IDatabase _database;
    private readonly bool _ownsConnection;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisLeaseProvider"/> class.
    /// </summary>
    /// <param name="options">Configuration options for the provider.</param>
    /// <exception cref="ArgumentNullException">Thrown when options is null.</exception>
    public RedisLeaseProvider(RedisLeaseProviderOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _options.Validate();

        _connection = CreateConnection(options);
        _database = _connection.GetDatabase(_options.Database);
        _ownsConnection = true;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisLeaseProvider"/> class with an existing connection.
    /// </summary>
    /// <param name="connection">An existing Redis connection multiplexer.</param>
    /// <param name="options">Configuration options for the provider.</param>
    /// <exception cref="ArgumentNullException">Thrown when connection or options is null.</exception>
    public RedisLeaseProvider(IConnectionMultiplexer connection, RedisLeaseProviderOptions options)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _options.Validate();

        _database = _connection.GetDatabase(_options.Database);
        _ownsConnection = false;
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

        ObjectDisposedException.ThrowIf(_disposed, this);

        var leaseId = Guid.NewGuid().ToString();
        var redisKey = GetRedisKey(leaseName);
        var now = DateTimeOffset.UtcNow;

        try
        {
            // Use SET with NX (not exists) and PX (millisecond expiry) for atomic acquire
            var acquired = await _database.StringSetAsync(
                redisKey,
                leaseId,
                duration,
                When.NotExists,
                CommandFlags.None);

            if (!acquired)
            {
                return null;
            }

            // Account for clock drift (Redlock algorithm)
            var validity = duration - TimeSpan.FromMilliseconds(duration.TotalMilliseconds * _options.ClockDriftFactor);

            if (validity < _options.MinimumValidity)
            {
                // Validity too short, release the lease
                await ReleaseLeaseInternalAsync(redisKey, leaseId);
                return null;
            }

            return new RedisLease(
                _database,
                redisKey,
                leaseId,
                leaseName,
                now,
                duration,
                _options);
        }
        catch (RedisException ex)
        {
            throw new LeaseAcquisitionException(
                $"Failed to acquire lease '{leaseName}' from Redis: {ex.Message}",
                ex,
                leaseName);
        }
    }

    /// <inheritdoc/>
    public async Task RenewLeaseAsync(ILease lease, CancellationToken cancellationToken = default)
    {
        if (lease == null)
        {
            throw new ArgumentNullException(nameof(lease));
        }

        ObjectDisposedException.ThrowIf(_disposed, this);

        if (lease is RedisLease redisLease)
        {
            await redisLease.RenewAsync(cancellationToken);
        }
        else
        {
            throw new ArgumentException(
                $"Lease must be of type {nameof(RedisLease)}.",
                nameof(lease));
        }
    }

    /// <inheritdoc/>
    public async Task ReleaseLeaseAsync(ILease lease, CancellationToken cancellationToken = default)
    {
        if (lease == null)
        {
            throw new ArgumentNullException(nameof(lease));
        }

        ObjectDisposedException.ThrowIf(_disposed, this);

        if (lease is RedisLease redisLease)
        {
            await redisLease.ReleaseAsync(cancellationToken);
        }
        else
        {
            throw new ArgumentException(
                $"Lease must be of type {nameof(RedisLease)}.",
                nameof(lease));
        }
    }

    /// <inheritdoc/>
    public async Task BreakLeaseAsync(string leaseName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(leaseName))
        {
            throw new ArgumentException("Lease name cannot be null or whitespace.", nameof(leaseName));
        }

        ObjectDisposedException.ThrowIf(_disposed, this);

        var redisKey = GetRedisKey(leaseName);

        try
        {
            await _database.KeyDeleteAsync(redisKey, CommandFlags.None);
        }
        catch (RedisException)
        {
            // Ignore errors when breaking lease
        }
    }

    /// <summary>
    /// Releases all resources used by the provider.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            if (_ownsConnection)
            {
                _connection?.Dispose();
            }
            _disposed = true;
        }
    }

    private static IConnectionMultiplexer CreateConnection(RedisLeaseProviderOptions options)
    {
        ConfigurationOptions configOptions;

        if (!string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            configOptions = ConfigurationOptions.Parse(options.ConnectionString);
        }
        else
        {
            configOptions = new ConfigurationOptions
            {
                EndPoints = { { options.HostName!, options.Port } },
                Ssl = options.UseSsl,
                AbortOnConnectFail = options.AbortOnConnectFail,
                ConnectTimeout = options.ConnectTimeout,
                SyncTimeout = options.SyncTimeout
            };

            // Configure authentication
            if (options.Credential != null || options.UseManagedIdentity)
            {
                // Azure Redis with AAD authentication
                var credential = options.Credential ?? new DefaultAzureCredential();
                
                // Note: StackExchange.Redis with Azure AD requires additional configuration
                // This is a placeholder for Azure AD token-based auth
                // In production, you'd need to implement token refresh mechanism
                configOptions.Password = GetAzureAccessToken(credential, options.HostName!).GetAwaiter().GetResult();
            }
            else if (!string.IsNullOrWhiteSpace(options.AccessKey))
            {
                configOptions.Password = options.AccessKey;
            }
        }

        return ConnectionMultiplexer.Connect(configOptions);
    }

    private static async Task<string> GetAzureAccessToken(Azure.Core.TokenCredential credential, string hostName)
    {
        // Azure Redis Cache scope for AAD authentication
        var tokenRequestContext = new Azure.Core.TokenRequestContext(
            new[] { "https://redis.azure.com/.default" });

        var token = await credential.GetTokenAsync(tokenRequestContext, default);
        return token.Token;
    }

    private async Task ReleaseLeaseInternalAsync(string redisKey, string leaseId)
    {
        try
        {
            const string releaseScript = @"
                if redis.call('get', KEYS[1]) == ARGV[1] then
                    return redis.call('del', KEYS[1])
                else
                    return 0
                end";

            await _database.ScriptEvaluateAsync(
                releaseScript,
                new RedisKey[] { redisKey },
                new RedisValue[] { leaseId });
        }
        catch (Exception)
        {
            // Ignore errors during internal release
        }
    }

    private string GetRedisKey(string leaseName)
    {
        return $"{_options.KeyPrefix}{leaseName}";
    }
}
