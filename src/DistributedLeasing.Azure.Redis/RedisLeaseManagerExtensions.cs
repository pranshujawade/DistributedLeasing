using System;
using DistributedLeasing.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DistributedLeasing.Azure.Redis
{
    /// <summary>
    /// Extension methods for registering Azure Redis distributed leasing services with dependency injection.
    /// </summary>
    public static class RedisLeaseManagerExtensions
    {
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
                return RedisLeaseManagerFactory.Create(options);
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
                return RedisLeaseManagerFactory.Create(options);
            });

            return services;
        }
    }
}
