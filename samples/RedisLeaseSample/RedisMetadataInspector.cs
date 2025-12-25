using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Text.Json;

namespace RedisLeaseSample;

/// <summary>
/// Utility class for inspecting Redis key state and lease metadata.
/// </summary>
public class RedisMetadataInspector
{
    private readonly IConnectionMultiplexer? _connection;
    private readonly ILogger<RedisMetadataInspector> _logger;
    private readonly string _cacheName;
    private readonly int _database;

    public RedisMetadataInspector(
        IConnectionMultiplexer? connection,
        ILogger<RedisMetadataInspector> logger,
        string cacheName,
        int database)
    {
        _connection = connection;
        _logger = logger;
        _cacheName = cacheName;
        _database = database;
    }

    /// <summary>
    /// Inspects the current state of a Redis key (lease).
    /// </summary>
    public async Task<RedisKeyState?> InspectKeyStateAsync(string keyName, CancellationToken cancellationToken = default)
    {
        if (_connection == null)
        {
            _logger.LogDebug("Redis connection not available for inspection");
            return null;
        }

        try
        {
            var db = _connection.GetDatabase(_database);
            
            // Get the key value
            var value = await db.StringGetAsync(keyName);
            if (!value.HasValue)
            {
                return new RedisKeyState
                {
                    Exists = false,
                    KeyName = keyName
                };
            }

            // Get TTL
            var ttl = await db.KeyTimeToLiveAsync(keyName);
            
            // Try to parse the JSON value
            LeaseData? leaseData = null;
            try
            {
                leaseData = JsonSerializer.Deserialize<LeaseData>(value.ToString());
            }
            catch
            {
                _logger.LogDebug("Could not parse lease data as JSON");
            }

            return new RedisKeyState
            {
                Exists = true,
                KeyName = keyName,
                Value = value.ToString(),
                TimeToLive = ttl,
                LeaseData = leaseData
            };
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to inspect Redis key state");
            return null;
        }
    }

    /// <summary>
    /// Displays formatted lease state information.
    /// </summary>
    public void DisplayKeyState(RedisKeyState? state)
    {
        if (state == null)
        {
            _logger.LogDebug("No state information available");
            return;
        }

        if (!state.Exists)
        {
            _logger.LogInformation("Key '{KeyName}' does not exist", state.KeyName);
            return;
        }

        _logger.LogInformation("Redis Key State:");
        _logger.LogInformation("  Key: {KeyName}", state.KeyName);
        _logger.LogInformation("  TTL: {TTL}", state.TimeToLive?.TotalSeconds.ToString("F1") ?? "No expiration");
        
        if (state.LeaseData != null)
        {
            _logger.LogInformation("  Owner: {OwnerId}", state.LeaseData.OwnerId);
            _logger.LogInformation("  Lease ID: {LeaseId}", state.LeaseData.LeaseId);
            _logger.LogInformation("  Acquired: {AcquiredAt}", state.LeaseData.AcquiredAt);
            _logger.LogInformation("  Expires: {ExpiresAt}", state.LeaseData.ExpiresAt);
            
            if (state.LeaseData.Metadata != null)
            {
                _logger.LogInformation("  Metadata:");
                foreach (var kvp in state.LeaseData.Metadata)
                {
                    _logger.LogInformation("    {Key}: {Value}", kvp.Key, kvp.Value);
                }
            }
        }
    }
}

/// <summary>
/// Represents the state of a Redis key.
/// </summary>
public class RedisKeyState
{
    public required bool Exists { get; init; }
    public required string KeyName { get; init; }
    public string? Value { get; init; }
    public TimeSpan? TimeToLive { get; init; }
    public LeaseData? LeaseData { get; init; }
}

/// <summary>
/// Represents lease data stored in Redis.
/// </summary>
public class LeaseData
{
    public string? LeaseId { get; set; }
    public string? OwnerId { get; set; }
    public DateTimeOffset AcquiredAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
}
