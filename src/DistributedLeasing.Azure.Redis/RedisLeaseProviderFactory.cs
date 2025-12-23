using Azure.Core;
using Azure.Identity;
using DistributedLeasing.Azure.Redis.Internal.Authentication;
using StackExchange.Redis;

namespace DistributedLeasing.Azure.Redis;

/// <summary>
/// Factory for creating <see cref="RedisLeaseProvider"/> instances with async initialization.
/// </summary>
/// <remarks>
/// This factory pattern enables proper async initialization of Redis connections, including
/// Azure AD token acquisition for managed identity scenarios, without blocking in constructors.
/// </remarks>
public static class RedisLeaseProviderFactory
{
    /// <summary>
    /// Creates a new <see cref="RedisLeaseProvider"/> instance with async connection initialization.
    /// </summary>
    /// <param name="options">Configuration options for the provider.</param>
    /// <param name="cancellationToken">Token to cancel the async initialization.</param>
    /// <returns>A fully initialized <see cref="RedisLeaseProvider"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when configuration is invalid.</exception>
    /// <remarks>
    /// <para>
    /// This method properly handles async token acquisition for Azure AD authentication,
    /// avoiding the sync-over-async anti-pattern that occurs in direct constructor usage.
    /// </para>
    /// <para>
    /// <strong>Usage Example:</strong>
    /// </para>
    /// <code>
    /// var options = new RedisLeaseProviderOptions
    /// {
    ///     HostName = "myredis.redis.cache.windows.net",
    ///     Port = 6380,
    ///     UseSsl = true,
    ///     Authentication = new AuthenticationOptions 
    ///     { 
    ///         Mode = AuthenticationModes.ManagedIdentity 
    ///     }
    /// };
    /// 
    /// var provider = await RedisLeaseProviderFactory.CreateAsync(options);
    /// </code>
    /// </remarks>
    public static async Task<RedisLeaseProvider> CreateAsync(
        RedisLeaseProviderOptions options,
        CancellationToken cancellationToken = default)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options));
        
        options.Validate();

        var connection = await CreateConnectionAsync(options, cancellationToken)
            .ConfigureAwait(false);

        return new RedisLeaseProvider(connection, options, ownsConnection: true);
    }

    /// <summary>
    /// Creates a Redis connection with async authentication support.
    /// </summary>
    private static async Task<IConnectionMultiplexer> CreateConnectionAsync(
        RedisLeaseProviderOptions options,
        CancellationToken cancellationToken)
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
                configOptions.Password = await GetAzureAccessTokenAsync(
                    options.Credential, 
                    options.HostName, 
                    cancellationToken)
                    .ConfigureAwait(false);
            }
            // Priority 3: Authentication configuration (recommended for production)
            else if (options.Authentication != null && options.HostName != null)
            {
                var factory = new AuthenticationFactory();
                var credential = factory.CreateCredential(options.Authentication);
                configOptions.Password = await GetAzureAccessTokenAsync(
                    credential, 
                    options.HostName, 
                    cancellationToken)
                    .ConfigureAwait(false);
            }
            // Fallback: DefaultAzureCredential (for managed identity without explicit config)
            else if (options.HostName != null)
            {
                var credential = new DefaultAzureCredential();
                configOptions.Password = await GetAzureAccessTokenAsync(
                    credential, 
                    options.HostName, 
                    cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        return await ConnectionMultiplexer.ConnectAsync(configOptions)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Acquires an Azure AD access token for Redis authentication.
    /// </summary>
    private static async Task<string> GetAzureAccessTokenAsync(
        TokenCredential credential,
        string hostName,
        CancellationToken cancellationToken)
    {
        // Azure Redis Cache scope for AAD authentication
        var tokenRequestContext = new TokenRequestContext(
            new[] { "https://redis.azure.com/.default" });

        var token = await credential.GetTokenAsync(tokenRequestContext, cancellationToken)
            .ConfigureAwait(false);

        return token.Token;
    }
}
