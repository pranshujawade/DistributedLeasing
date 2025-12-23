using DistributedLeasing.Core;

namespace DistributedLeasing.Azure.Cosmos
{
    /// <summary>
    /// Factory for creating Cosmos DB lease manager instances.
    /// </summary>
    /// <remarks>
    /// This factory provides a public API for creating lease managers while keeping
    /// the implementation details internal.
    /// </remarks>
    public static class CosmosLeaseManagerFactory
    {
        /// <summary>
        /// Creates a new instance of ILeaseManager configured for Azure Cosmos DB.
        /// </summary>
        /// <param name="options">The configuration options for the Cosmos lease provider.</param>
        /// <returns>An ILeaseManager instance backed by Azure Cosmos DB.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when options is null.</exception>
        /// <exception cref="System.InvalidOperationException">Thrown when options validation fails.</exception>
        public static ILeaseManager Create(CosmosLeaseProviderOptions options)
        {
            return new CosmosLeaseManager(options);
        }
    }
}
