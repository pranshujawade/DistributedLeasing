using DistributedLeasing.Abstractions;
using DistributedLeasing.Abstractions.Contracts;

namespace DistributedLeasing.Azure.Blob
{
    /// <summary>
    /// Factory for creating Blob lease manager instances.
    /// </summary>
    /// <remarks>
    /// This factory provides a public API for creating lease managers while keeping
    /// the implementation details internal.
    /// </remarks>
    public static class BlobLeaseManagerFactory
    {
        /// <summary>
        /// Creates a new instance of ILeaseManager configured for Azure Blob Storage.
        /// </summary>
        /// <param name="options">The configuration options for the Blob lease provider.</param>
        /// <returns>An ILeaseManager instance backed by Azure Blob Storage.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when options is null.</exception>
        /// <exception cref="System.InvalidOperationException">Thrown when options validation fails.</exception>
        public static ILeaseManager Create(BlobLeaseProviderOptions options)
        {
            return new BlobLeaseManager(options);
        }
    }
}
