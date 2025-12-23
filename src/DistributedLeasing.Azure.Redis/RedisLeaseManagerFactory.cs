using DistributedLeasing.Abstractions;
using DistributedLeasing.Abstractions.Contracts;

namespace DistributedLeasing.Azure.Redis
{
    /// <summary>
    /// Factory for creating Redis lease manager instances.
    /// </summary>
    /// <remarks>
    /// This factory provides a public API for creating lease managers while keeping
    /// the implementation details internal.
    /// </remarks>
    public static class RedisLeaseManagerFactory
    {
        /// <summary>
        /// Creates a new instance of ILeaseManager configured for Azure Redis.
        /// </summary>
        /// <param name="options">The configuration options for the Redis lease provider.</param>
        /// <returns>An ILeaseManager instance backed by Azure Redis.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when options is null.</exception>
        /// <exception cref="System.InvalidOperationException">Thrown when options validation fails.</exception>
        public static async Task<ILeaseManager> CreateAsync(RedisLeaseProviderOptions options)
        {
            var provider = await RedisLeaseProviderFactory.CreateAsync(options);
            return new RedisLeaseManager(provider, options);
        }
    }
}
