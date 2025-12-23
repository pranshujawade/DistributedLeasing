using System;
using DistributedLeasing.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DistributedLeasing.Azure.Cosmos
{
    /// <summary>
    /// Extension methods for registering Azure Cosmos DB distributed leasing services with dependency injection.
    /// </summary>
    public static class CosmosLeaseManagerExtensions
    {
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
                return CosmosLeaseManagerFactory.Create(options);
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
                return CosmosLeaseManagerFactory.Create(options);
            });

            return services;
        }
    }
}
