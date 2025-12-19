using System;
using DistributedLeasing.Azure.Blob;
using DistributedLeasing.Azure.Cosmos;
using DistributedLeasing.Azure.Redis;
using DistributedLeasing.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DistributedLeasing.Extensions.DependencyInjection
{
    /// <summary>
    /// Extension methods for registering distributed leasing services with dependency injection.
    /// </summary>
    /// <remarks>
    /// These extensions follow Microsoft's dependency injection best practices and support
    /// configuration binding from appsettings.json or other configuration sources.
    /// </remarks>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds Azure Blob Storage distributed leasing services to the service collection.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configure">A delegate to configure the blob lease provider options.</param>
        /// <returns>The service collection for chaining.</returns>
        /// <example>
        /// <code>
        /// services.AddBlobLeaseManager(options =>
        /// {
        ///     options.StorageAccountUri = new Uri("https://mystorageaccount.blob.core.windows.net");
        ///     options.UseManagedIdentity = true;
        ///     options.ContainerName = "leases";
        ///     options.CreateContainerIfNotExists = true;
        /// });
        /// </code>
        /// </example>
        public static IServiceCollection AddBlobLeaseManager(
            this IServiceCollection services,
            Action<BlobLeaseProviderOptions> configure)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));
            if (configure == null)
                throw new ArgumentNullException(nameof(configure));

            services.Configure(configure);
            services.TryAddSingleton<ILeaseManager>(serviceProvider =>
            {
                var options = new BlobLeaseProviderOptions();
                configure(options);
                return new BlobLeaseManager(options);
            });

            return services;
        }

        /// <summary>
        /// Adds Azure Blob Storage distributed leasing services to the service collection
        /// using configuration binding.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configuration">The configuration section containing blob lease options.</param>
        /// <returns>The service collection for chaining.</returns>
        /// <example>
        /// <code>
        /// // In appsettings.json:
        /// {
        ///   "BlobLeasing": {
        ///     "StorageAccountUri": "https://mystorageaccount.blob.core.windows.net",
        ///     "UseManagedIdentity": true,
        ///     "ContainerName": "leases",
        ///     "CreateContainerIfNotExists": true,
        ///     "DefaultLeaseDuration": "00:00:30"
        ///   }
        /// }
        /// 
        /// // In Startup.cs or Program.cs:
        /// services.AddBlobLeaseManager(configuration.GetSection("BlobLeasing"));
        /// </code>
        /// </example>
        public static IServiceCollection AddBlobLeaseManager(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            services.Configure<BlobLeaseProviderOptions>(configuration);
            services.TryAddSingleton<ILeaseManager>(serviceProvider =>
            {
                var options = new BlobLeaseProviderOptions();
                configuration.Bind(options);
                return new BlobLeaseManager(options);
            });

            return services;
        }

        /// <summary>
        /// Adds Azure Blob Storage distributed leasing services as a named service.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="name">The name for this lease manager instance.</param>
        /// <param name="configure">A delegate to configure the blob lease provider options.</param>
        /// <returns>The service collection for chaining.</returns>
        /// <remarks>
        /// Use named services when you need multiple lease managers with different configurations.
        /// Retrieve using <c>IServiceProvider.GetRequiredKeyedService&lt;ILeaseManager&gt;(name)</c>.
        /// </remarks>
        public static IServiceCollection AddNamedBlobLeaseManager(
            this IServiceCollection services,
            string name,
            Action<BlobLeaseProviderOptions> configure)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));
            if (string.IsNullOrEmpty(name))
                throw new ArgumentNullException(nameof(name));
            if (configure == null)
                throw new ArgumentNullException(nameof(configure));

            var options = new BlobLeaseProviderOptions();
            configure(options);

            // Store as a factory in the service collection
            services.AddSingleton(serviceProvider => 
                new NamedLeaseManager(name, new BlobLeaseManager(options)));

            return services;
        }

        /// <summary>
        /// Adds Azure Cosmos DB distributed leasing services to the service collection.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configure">A delegate to configure the Cosmos lease provider options.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddCosmosLeaseManager(
            this IServiceCollection services,
            Action<CosmosLeaseProviderOptions> configure)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));
            if (configure == null)
                throw new ArgumentNullException(nameof(configure));

            services.Configure(configure);
            services.TryAddSingleton<ILeaseManager>(serviceProvider =>
            {
                var options = new CosmosLeaseProviderOptions();
                configure(options);
                return new CosmosLeaseManager(options);
            });

            return services;
        }

        /// <summary>
        /// Adds Azure Cosmos DB distributed leasing services to the service collection
        /// using configuration binding.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configuration">The configuration section containing Cosmos lease options.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddCosmosLeaseManager(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            services.Configure<CosmosLeaseProviderOptions>(configuration);
            services.TryAddSingleton<ILeaseManager>(serviceProvider =>
            {
                var options = new CosmosLeaseProviderOptions();
                configuration.Bind(options);
                return new CosmosLeaseManager(options);
            });

            return services;
        }

        /// <summary>
        /// Adds Azure Redis distributed leasing services to the service collection.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configure">A delegate to configure the Redis lease provider options.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddRedisLeaseManager(
            this IServiceCollection services,
            Action<RedisLeaseProviderOptions> configure)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));
            if (configure == null)
                throw new ArgumentNullException(nameof(configure));

            services.Configure(configure);
            services.TryAddSingleton<ILeaseManager>(serviceProvider =>
            {
                var options = new RedisLeaseProviderOptions();
                configure(options);
                return new RedisLeaseManager(options);
            });

            return services;
        }

        /// <summary>
        /// Adds Azure Redis distributed leasing services to the service collection
        /// using configuration binding.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configuration">The configuration section containing Redis lease options.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddRedisLeaseManager(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            services.Configure<RedisLeaseProviderOptions>(configuration);
            services.TryAddSingleton<ILeaseManager>(serviceProvider =>
            {
                var options = new RedisLeaseProviderOptions();
                configuration.Bind(options);
                return new RedisLeaseManager(options);
            });

            return services;
        }
    }

    /// <summary>
    /// Internal wrapper for named lease managers.
    /// </summary>
    internal class NamedLeaseManager
    {
        public string Name { get; }
        public ILeaseManager Manager { get; }

        public NamedLeaseManager(string name, ILeaseManager manager)
        {
            Name = name;
            Manager = manager;
        }
    }
}
