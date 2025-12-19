using Azure.Core;
using DistributedLeasing.Core.Configuration;

namespace DistributedLeasing.Azure.Redis;

/// <summary>
/// Configuration options for the Redis lease provider.
/// </summary>
/// <remarks>
/// This class extends <see cref="LeaseOptions"/> with Redis-specific settings.
/// Supports authentication via connection string, managed identity, or explicit credentials.
/// </remarks>
public class RedisLeaseProviderOptions : LeaseOptions
{
    /// <summary>
    /// Gets or sets the Redis connection string.
    /// </summary>
    /// <remarks>
    /// Use only for local development. In production, prefer managed identity.
    /// Example: "localhost:6379,ssl=false" or "mycache.redis.cache.windows.net:6380,ssl=true"
    /// </remarks>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the Redis host name.
    /// </summary>
    /// <remarks>
    /// Required when using managed identity.
    /// Example: "mycache.redis.cache.windows.net"
    /// </remarks>
    public string? HostName { get; set; }

    /// <summary>
    /// Gets or sets the Redis port number.
    /// </summary>
    /// <remarks>
    /// Default is 6380 for Azure Redis (SSL enabled).
    /// </remarks>
    public int Port { get; set; } = 6380;

    /// <summary>
    /// Gets or sets a value indicating whether to use SSL/TLS.
    /// </summary>
    /// <remarks>
    /// Should be true for Azure Redis. May be false for local development.
    /// </remarks>
    public bool UseSsl { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to use Azure Managed Identity for authentication.
    /// </summary>
    /// <remarks>
    /// When true, uses Azure AD authentication with DefaultAzureCredential.
    /// Requires HostName to be set and Azure Cache for Redis with AAD enabled.
    /// </remarks>
    public bool UseManagedIdentity { get; set; }

    /// <summary>
    /// Gets or sets an explicit TokenCredential for authentication.
    /// </summary>
    /// <remarks>
    /// Allows custom credential implementations (e.g., WorkloadIdentityCredential).
    /// Takes precedence over UseManagedIdentity if set.
    /// </remarks>
    public TokenCredential? Credential { get; set; }

    /// <summary>
    /// Gets or sets the access key for authentication.
    /// </summary>
    /// <remarks>
    /// Use only for local development. In production, prefer managed identity.
    /// </remarks>
    public string? AccessKey { get; set; }

    /// <summary>
    /// Gets or sets the key prefix for lease keys in Redis.
    /// </summary>
    /// <remarks>
    /// Useful for namespacing leases in shared Redis instances.
    /// </remarks>
    public string KeyPrefix { get; set; } = "lease:";

    /// <summary>
    /// Gets or sets the database number to use.
    /// </summary>
    /// <remarks>
    /// Default is 0. Azure Redis supports databases 0-15 (Standard/Premium tiers).
    /// </remarks>
    public int Database { get; set; } = 0;

    /// <summary>
    /// Gets or sets the connect timeout in milliseconds.
    /// </summary>
    public int ConnectTimeout { get; set; } = 5000;

    /// <summary>
    /// Gets or sets the sync timeout in milliseconds.
    /// </summary>
    public int SyncTimeout { get; set; } = 5000;

    /// <summary>
    /// Gets or sets a value indicating whether to abort on connect failure.
    /// </summary>
    /// <remarks>
    /// When false, allows connection to continue even if some endpoints fail.
    /// </remarks>
    public bool AbortOnConnectFail { get; set; } = false;

    /// <summary>
    /// Gets or sets the clock drift factor for Redlock algorithm.
    /// </summary>
    /// <remarks>
    /// Accounts for clock drift between servers. Default is 0.01 (1%).
    /// </remarks>
    public double ClockDriftFactor { get; set; } = 0.01;

    /// <summary>
    /// Gets or sets the minimum validity time for a lease.
    /// </summary>
    /// <remarks>
    /// If remaining validity is less than this, lease acquisition is considered failed.
    /// </remarks>
    public TimeSpan MinimumValidity { get; set; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Validates the configuration options.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the configuration is invalid.</exception>
    /// <exception cref="ArgumentException">Thrown when required properties are missing or invalid.</exception>
    public override void Validate()
    {
        base.Validate();

        // Validate authentication configuration
        if (Credential == null && 
            !UseManagedIdentity && 
            string.IsNullOrWhiteSpace(ConnectionString) && 
            string.IsNullOrWhiteSpace(AccessKey))
        {
            throw new InvalidOperationException(
                "No authentication method configured. Set Credential, UseManagedIdentity=true, ConnectionString, or AccessKey.");
        }

        // Validate hostname for managed identity
        if ((Credential != null || UseManagedIdentity) && string.IsNullOrWhiteSpace(HostName))
        {
            throw new InvalidOperationException(
                "HostName is required when using Credential or UseManagedIdentity.");
        }

        // Validate key prefix
        if (string.IsNullOrWhiteSpace(KeyPrefix))
        {
            throw new ArgumentException("KeyPrefix cannot be null or whitespace.", nameof(KeyPrefix));
        }

        // Validate port
        if (Port <= 0 || Port > 65535)
        {
            throw new ArgumentOutOfRangeException(
                nameof(Port),
                "Port must be between 1 and 65535.");
        }

        // Validate database
        if (Database < 0 || Database > 15)
        {
            throw new ArgumentOutOfRangeException(
                nameof(Database),
                "Database must be between 0 and 15.");
        }

        // Validate timeouts
        if (ConnectTimeout <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ConnectTimeout),
                "ConnectTimeout must be positive.");
        }

        if (SyncTimeout <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(SyncTimeout),
                "SyncTimeout must be positive.");
        }

        // Validate clock drift factor
        if (ClockDriftFactor < 0 || ClockDriftFactor > 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ClockDriftFactor),
                "ClockDriftFactor must be between 0 and 1.");
        }

        // Validate minimum validity
        if (MinimumValidity <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MinimumValidity),
                "MinimumValidity must be positive.");
        }
    }
}
