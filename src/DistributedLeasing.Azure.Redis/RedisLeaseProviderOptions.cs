using Azure.Core;
using DistributedLeasing.Azure.Redis.Internal.Authentication;
using DistributedLeasing.Core.Configuration;

namespace DistributedLeasing.Azure.Redis;

/// <summary>
/// Configuration options for the Redis lease provider.
/// </summary>
/// <remarks>
/// <para>
/// This class extends <see cref="LeaseOptions"/> with Redis-specific settings.
/// Supports authentication via connection string, managed identity, or explicit credentials.
/// </para>
/// <para>
/// <strong>AppSettings.json Example:</strong>
/// </para>
/// <code>
/// {
///   "Leasing": {
///     "Endpoint": "mycache.redis.cache.windows.net:6380",
///     "KeyPrefix": "myapp:",
///     "UseManagedIdentity": true,
///     "UseSsl": true,
///     "Database": 0,
///     "DefaultLeaseDuration": "00:00:30",
///     "AutoRenew": true,
///     "AutoRenewInterval": "00:00:20"
///   }
/// }
/// </code>
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
    /// Gets or sets the service endpoint (standardized property for host and port).
    /// </summary>
    /// <value>
    /// The service endpoint in format "hostname:port". This provides consistency across providers.
    /// </value>
    /// <remarks>
    /// <para>
    /// This property provides a consistent naming convention across all providers.
    /// When setting, you can specify just the hostname or hostname:port format.
    /// </para>
    /// <para>
    /// Examples:
    /// <list type="bullet">
    /// <item>"mycache.redis.cache.windows.net:6380"</item>
    /// <item>"mycache.redis.cache.windows.net" (uses default port)</item>
    /// </list>
    /// </para>
    /// </remarks>
    public string? Endpoint
    {
        get => string.IsNullOrWhiteSpace(HostName) ? null : $"{HostName}:{Port}";
        set
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                var parts = value!.Split(':');
                HostName = parts[0];
                if (parts.Length > 1 && int.TryParse(parts[1], out var port))
                {
                    Port = port;
                }
            }
            else
            {
                HostName = null;
            }
        }
    }

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
    /// Gets or sets the authentication configuration.
    /// </summary>
    /// <value>
    /// Authentication options, or <c>null</c> to use connection string or access key.
    /// </value>
    /// <remarks>
    /// <para>
    /// When using managed identity or other token-based authentication, configure this property
    /// and provide <see cref="HostName"/> instead of <see cref="ConnectionString"/> or <see cref="AccessKey"/>.
    /// </para>
    /// <para>
    /// For development, you can omit this and provide ConnectionString or AccessKey instead.
    /// </para>
    /// </remarks>
    public AuthenticationOptions? Authentication { get; set; }

    /// <summary>
    /// Gets or sets the credential to use for authentication.
    /// </summary>
    /// <value>
    /// The token credential, or <c>null</c> to use connection string, access key, or authentication configuration.
    /// </value>
    /// <remarks>
    /// <para>
    /// This property allows direct injection of a credential, bypassing the authentication configuration.
    /// </para>
    /// <para>
    /// If set, this takes precedence over <see cref="Authentication"/>, <see cref="ConnectionString"/>, and <see cref="AccessKey"/>.
    /// </para>
    /// </remarks>
    public TokenCredential? Credential { get; set; }

    /// <summary>
    /// Validates the configuration options.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the configuration is invalid.</exception>
    /// <exception cref="ArgumentException">Thrown when required properties are missing or invalid.</exception>
    public override void Validate()
    {
        base.Validate();

        // Validate authentication configuration
        ValidateAuthenticationConfigured(
            hasConnectionString: !string.IsNullOrWhiteSpace(ConnectionString) || !string.IsNullOrWhiteSpace(AccessKey),
            hasAlternativeAuth: !string.IsNullOrWhiteSpace(HostName) || Credential != null || Authentication != null,
            providerName: "Azure Redis");

        // If using credential or authentication, require HostName
        if ((Credential != null || Authentication != null) && string.IsNullOrWhiteSpace(HostName))
        {
            throw new InvalidOperationException(
                "HostName is required when using Credential or Authentication configuration.");
        }

        // Validate authentication options if provided
        if (Authentication != null)
        {
            Authentication.Validate();
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

    /// <summary>
    /// Validates that authentication is properly configured.
    /// </summary>
    private void ValidateAuthenticationConfigured(bool hasConnectionString, bool hasAlternativeAuth, string providerName)
    {
        if (!hasConnectionString && !hasAlternativeAuth)
        {
            throw new InvalidOperationException(
                $"Authentication must be configured for {providerName}. " +
                "Either provide ConnectionString/AccessKey, or configure Authentication/Credential with the required HostName.");
        }
    }
}
