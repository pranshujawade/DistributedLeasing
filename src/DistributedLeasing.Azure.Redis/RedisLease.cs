using DistributedLeasing.Abstractions;
using DistributedLeasing.Abstractions.Configuration;
using DistributedLeasing.Abstractions.Contracts;
using DistributedLeasing.Abstractions.Core;
using DistributedLeasing.Abstractions.Exceptions;
using StackExchange.Redis;

namespace DistributedLeasing.Azure.Redis;

/// <summary>
/// Represents a lease acquired from Azure Redis.
/// </summary>
/// <remarks>
/// This implementation uses Redis hash storage for lease data and metadata.
/// Renewal extends the expiration time using Lua script for atomicity.
/// </remarks>
internal class RedisLease : LeaseBase
{
    private readonly IDatabase _database;
    private readonly string _redisKey;
    private readonly RedisLeaseProviderOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisLease"/> class.
    /// </summary>
    /// <param name="database">The Redis database.</param>
    /// <param name="redisKey">The Redis key for the lease.</param>
    /// <param name="leaseId">The unique identifier for this lease.</param>
    /// <param name="leaseName">The name of the lease.</param>
    /// <param name="acquiredAt">The UTC timestamp when the lease was acquired.</param>
    /// <param name="duration">The duration of the lease.</param>
    /// <param name="options">The provider options.</param>
    public RedisLease(
        IDatabase database,
        string redisKey,
        string leaseId,
        string leaseName,
        DateTimeOffset acquiredAt,
        TimeSpan duration,
        RedisLeaseProviderOptions options)
        : base(leaseId, leaseName, acquiredAt, duration, options)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _redisKey = redisKey ?? throw new ArgumentNullException(nameof(redisKey));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Renews the lease by extending the expiration time in Redis.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="LeaseRenewalException">Thrown when renewal fails.</exception>
    /// <exception cref="LeaseLostException">Thrown when the lease has been acquired by another holder.</exception>
    protected override async Task RenewLeaseAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Calculate renewal duration (original lease duration)
            var renewalDuration = ExpiresAt - AcquiredAt;
            var newExpiration = DateTimeOffset.UtcNow.Add(renewalDuration);
            var ttlMilliseconds = (long)renewalDuration.TotalMilliseconds;

            if (ttlMilliseconds <= 0)
            {
                throw new LeaseRenewalException(
                    $"Lease '{LeaseName}' has expired and cannot be renewed.",
                    null!)
                {
                    LeaseName = LeaseName,
                    LeaseId = LeaseId
                };
            }

            // Lua script for atomic renewal - only extend if we still own the lease (hash-based)
            const string renewScript = @"
                if redis.call('hget', KEYS[1], 'leaseId') == ARGV[1] then
                    return redis.call('pexpire', KEYS[1], ARGV[2])
                else
                    return 0
                end";

            var result = await _database.ScriptEvaluateAsync(
                renewScript,
                [_redisKey],
                [LeaseId, ttlMilliseconds]);

            if (result.IsNull || (int)result == 0)
            {
                throw new LeaseLostException(
                    $"Lease '{LeaseName}' is no longer held by this instance.")
                {
                    LeaseName = LeaseName,
                    LeaseId = LeaseId
                };
            }

            // CRITICAL FIX: Update expiration after successful renewal
            ExpiresAt = newExpiration;
        }
        catch (LeaseLostException)
        {
            throw;
        }
        catch (RedisException ex)
        {
            throw new LeaseRenewalException(
                $"Failed to renew lease '{LeaseName}' in Redis: {ex.Message}",
                ex)
            {
                LeaseName = LeaseName,
                LeaseId = LeaseId
            };
        }
    }

    /// <summary>
    /// Releases the lease by deleting the key from Redis.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// This operation is idempotent and will not throw if the lease is already released.
    /// Uses Lua script to ensure we only delete if we still own the lease.
    /// </remarks>
    protected override async Task ReleaseLeaseAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Lua script for atomic release - only delete if we still own the lease (hash-based)
            const string releaseScript = @"
                if redis.call('hget', KEYS[1], 'leaseId') == ARGV[1] then
                    return redis.call('del', KEYS[1])
                else
                    return 0
                end";

            await _database.ScriptEvaluateAsync(
                releaseScript,
                [_redisKey],
                [LeaseId]);
        }
        catch (Exception)
        {
            // Swallow exceptions during release for idempotency
            // Redis TTL will eventually clean up the key
        }
    }
}
