using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using DistributedLeasing.Abstractions.Contracts;
using DistributedLeasing.ChaosEngineering.Configuration;
using DistributedLeasing.ChaosEngineering.Faults.Abstractions;
using DistributedLeasing.ChaosEngineering.Lifecycle;
using DistributedLeasing.ChaosEngineering.Observability;
using DistributedLeasing.ChaosEngineering.Policies.Abstractions;

namespace DistributedLeasing.ChaosEngineering.DependencyInjection
{
    /// <summary>
    /// Extension methods for registering chaos engineering services with dependency injection.
    /// </summary>
    public static class ChaosServiceCollectionExtensions
    {
        /// <summary>
        /// Adds chaos engineering lease provider to the service collection.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configure">Action to configure chaos options.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddChaosLeaseProvider(
            this IServiceCollection services,
            Action<ChaosOptionsBuilder> configure)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            // Build chaos options
            var builder = new ChaosOptionsBuilder();
            configure(builder);
            var options = builder.Build();

            // Register options as singleton
            services.AddSingleton(options);

            // Register default observer if not already registered
            services.TryAddSingleton<IChaosObserver, ConsoleChaosObserver>();

            // NOTE: Decorator pattern implementation
            // Since IServiceCollection.Decorate is not available without Scrutor package,
            // users should manually decorate their ILeaseProvider registration like:
            // services.AddSingleton<ILeaseProvider>(sp => 
            //     new ChaosLeaseProviderV2(sp.GetRequiredService<ActualProvider>(), 
            //                               sp.GetRequiredService<ChaosOptions>(),
            //                               sp.GetService<IChaosObserver>()));
            // Or install Scrutor package: dotnet add package Scrutor

            return services;
        }

        /// <summary>
        /// Adds a custom chaos observer to the service collection.
        /// </summary>
        /// <typeparam name="TObserver">The observer type.</typeparam>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddChaosObserver<TObserver>(this IServiceCollection services)
            where TObserver : class, IChaosObserver
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            services.AddSingleton<IChaosObserver, TObserver>();
            return services;
        }

        /// <summary>
        /// Adds a fault strategy to the service collection.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="strategy">The fault strategy to add.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddFaultStrategy(
            this IServiceCollection services,
            IFaultStrategy strategy)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (strategy == null)
            {
                throw new ArgumentNullException(nameof(strategy));
            }

            services.AddSingleton(strategy);
            return services;
        }

        /// <summary>
        /// Adds a fault decision policy to the service collection.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="policy">The policy to add.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddFaultPolicy(
            this IServiceCollection services,
            IFaultDecisionPolicy policy)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (policy == null)
            {
                throw new ArgumentNullException(nameof(policy));
            }

            services.AddSingleton(policy);
            return services;
        }
    }
}
