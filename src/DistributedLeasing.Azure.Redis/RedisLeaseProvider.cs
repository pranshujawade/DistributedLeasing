using Azure.Core;
using Azure.Identity;
using DistributedLeasing.Abstractions;
using DistributedLeasing.Abstractions.Authentication;
using DistributedLeasing.Abstractions.Contracts;
using DistributedLeasing.Abstractions.Exceptions;
using StackExchange.Redis;
using System.Linq;

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
    /// Initializes a new instance of the <see cref="RedisLeaseProvider"/> class with an existing connection.
    /// </summary>
    /// <param name="connection">An existing Redis connection multiplexer.</param>
    /// <param name="options">Configuration options for the provider.</param>
    /// <param name="ownsConnection">Whether this provider owns the connection and should dispose it.</param>
    /// <exception cref="ArgumentNullException">Thrown when connection or options is null.</exception>
    internal RedisLeaseProvider(IConnectionMultiplexer connection, RedisLeaseProviderOptions options, bool ownsConnection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _options.Validate();

        _database = _connection.GetDatabase(_options.Database);
        _ownsConnection = ownsConnection;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisLeaseProvider"/> class with an existing connection.
    /// </summary>
    /// <param name="connection">An existing Redis connection multiplexer.</param>
    /// <param name="options">Configuration options for the provider.</param>
    /// <exception cref="ArgumentNullException">Thrown when connection or options is null.</exception>
    public RedisLeaseProvider(IConnectionMultiplexer connection, RedisLeaseProviderOptions options)
        : this(connection, options, ownsConnection: false)
    {
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
        var redisKey = GetRedisKey(leaseName);
        var now = DateTimeOffset.UtcNow;

        try
        {
            // Use Lua script for atomic acquire with hash-based metadata storage
            const string acquireScript = @"
                if redis.call('exists', KEYS[1]) == 0 then
                    redis.call('hset', KEYS[1], 'leaseId', ARGV[1])
                    redis.call('hset', KEYS[1], 'acquiredAt', ARGV[2])
                    redis.call('pexpire', KEYS[1], ARGV[3])
                    return 1
                else
                    return 0
                end";

            var acquired = (int)await _database.ScriptEvaluateAsync(
                acquireScript,
                new RedisKey[] { redisKey },
                new RedisValue[] { 
                    leaseId, 
                    now.ToString("o"),
                    (long)duration.TotalMilliseconds 
                });

            if (acquired == 0)
            {
                return null;
            }

            // Store user metadata if provided
            if (_options.Metadata != null && _options.Metadata.Any())
            {
                var hashEntries = _options.Metadata
                    .Select(kvp => new HashEntry($"meta_{kvp.Key}", kvp.Value))
                    .ToArray();
                
                await _database.HashSetAsync(redisKey, hashEntries);
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
            configOptions = ConfigurationOptions.Parse(options.ConnectionString!);
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

            // Priority 1: Access key (for development)
            if (!string.IsNullOrWhiteSpace(options.AccessKey))
            {
                configOptions.Password = options.AccessKey;
            }
            // Priority 2: Direct credential injection (for advanced scenarios)
            else if (options.Credential != null && options.HostName != null)
            {
                configOptions.Password = GetAzureAccessToken(options.Credential, options.HostName).GetAwaiter().GetResult();
            }
            // Priority 3: Authentication configuration (recommended for production)
            else if (options.Authentication != null && options.HostName != null)
            {
                var factory = new AuthenticationFactory();
                var credential = factory.CreateCredential(options.Authentication);
                configOptions.Password = GetAzureAccessToken(credential, options.HostName).GetAwaiter().GetResult();
            }
            // Fallback: DefaultAzureCredential (for managed identity without explicit config)
            else if (options.HostName != null)
            {
                var credential = new DefaultAzureCredential();
                configOptions.Password = GetAzureAccessToken(credential, options.HostName).GetAwaiter().GetResult();
            }
        }

        return ConnectionMultiplexer.Connect(configOptions);
    }

    private static async Task<string> GetAzureAccessToken(TokenCredential credential, string hostName)
    {
        // Azure Redis Cache scope for AAD authentication
        var tokenRequestContext = new TokenRequestContext(
            new[] { "https://redis.azure.com/.default" });

        var token = await credential.GetTokenAsync(tokenRequestContext, default);
        return token.Token;
    }

    private async Task ReleaseLeaseInternalAsync(string redisKey, string leaseId)
    {
        try
        {
            // Updated script to work with hash-based storage
            const string releaseScript = @"
                if redis.call('hget', KEYS[1], 'leaseId') == ARGV[1] then
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
