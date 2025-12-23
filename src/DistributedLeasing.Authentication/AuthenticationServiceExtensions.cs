using System;
using Azure.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DistributedLeasing.Authentication;

/// <summary>
/// Extension methods for configuring Azure authentication services.
/// </summary>
public static class AuthenticationServiceExtensions
{
    /// <summary>
    /// Adds Azure authentication services to the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configuration">The configuration containing authentication settings.</param>
    /// <param name="sectionName">The configuration section name. Defaults to "Authentication".</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when services or configuration is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when configuration is invalid.</exception>
    /// <remarks>
    /// <para>
    /// This method configures authentication based on the settings in appsettings.json.
    /// The library automatically handles token acquisition, caching, and refresh.
    /// </para>
    /// <para>
    /// <strong>Configuration Example:</strong>
    /// <code>
    /// {
    ///   "Authentication": {
    ///     "Mode": "Auto",
    ///     "EnableDevelopmentCredentials": true
    ///   }
    /// }
    /// </code>
    /// </para>
    /// <para>
    /// <strong>Supported Modes:</strong>
    /// <list type="bullet">
    /// <item><description>Auto - Automatically detects and uses appropriate credentials</description></item>
    /// <item><description>ManagedIdentity - Use Azure Managed Identity</description></item>
    /// <item><description>WorkloadIdentity - Use Workload Identity (AKS, GitHub Actions)</description></item>
    /// <item><description>ServicePrincipal - Use Service Principal with certificate or secret</description></item>
    /// <item><description>FederatedCredential - Use Federated Identity Credential</description></item>
    /// <item><description>Development - Use development credentials (Azure CLI, VS, VS Code)</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public static IServiceCollection AddAzureAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = "Authentication")
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        if (configuration == null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        // Bind authentication options from configuration
        services.Configure<AuthenticationOptions>(configuration.GetSection(sectionName));

        // Register TokenCredential as singleton
        services.AddSingleton<TokenCredential>(serviceProvider =>
        {
            var authOptions = new AuthenticationOptions();
            configuration.GetSection(sectionName).Bind(authOptions);

            // Validate configuration
            authOptions.Validate();

            var logger = serviceProvider.GetService<ILogger<AuthenticationFactory>>();
            var factory = new AuthenticationFactory(logger);

            return factory.CreateCredential(authOptions);
        });

        return services;
    }

    /// <summary>
    /// Adds Azure authentication services to the dependency injection container with explicit options.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configureOptions">Action to configure authentication options.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when services or configureOptions is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when configuration is invalid.</exception>
    /// <remarks>
    /// <para>
    /// This overload is useful when you need to configure authentication programmatically
    /// instead of reading from appsettings.json.
    /// </para>
    /// <para>
    /// <strong>Example:</strong>
    /// <code>
    /// services.AddAzureAuthentication(options =>
    /// {
    ///     options.Mode = AuthenticationModes.ManagedIdentity;
    ///     options.ManagedIdentity = new ManagedIdentityOptions
    ///     {
    ///         ClientId = "your-client-id"
    ///     };
    /// });
    /// </code>
    /// </para>
    /// </remarks>
    public static IServiceCollection AddAzureAuthentication(
        this IServiceCollection services,
        Action<AuthenticationOptions> configureOptions)
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        if (configureOptions == null)
        {
            throw new ArgumentNullException(nameof(configureOptions));
        }

        var authOptions = new AuthenticationOptions();
        configureOptions(authOptions);

        // Validate configuration
        authOptions.Validate();

        // Register options
        services.Configure(configureOptions);

        // Register TokenCredential as singleton
        services.AddSingleton<TokenCredential>(serviceProvider =>
        {
            var logger = serviceProvider.GetService<ILogger<AuthenticationFactory>>();
            var factory = new AuthenticationFactory(logger);

            return factory.CreateCredential(authOptions);
        });

        return services;
    }
}
