using DistributedLeasing.Abstractions;

namespace DistributedLeasing.Azure.Blob
{
    /// <summary>
    /// Azure Blob Storage implementation of the lease manager.
    /// </summary>
    /// <remarks>
    /// This class combines the <see cref="BlobLeaseProvider"/> with the base lease manager
    /// functionality to provide a complete blob-based lease management solution.
    /// </remarks>
    public class BlobLeaseManager : LeaseManagerBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BlobLeaseManager"/> class.
        /// </summary>
        /// <param name="options">The configuration options for the blob lease provider.</param>
        public BlobLeaseManager(BlobLeaseProviderOptions options)
            : base(new BlobLeaseProvider(options), options)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BlobLeaseManager"/> class with a custom provider.
        /// </summary>
        /// <param name="provider">The blob lease provider.</param>
        /// <param name="options">The configuration options.</param>
        internal BlobLeaseManager(BlobLeaseProvider provider, BlobLeaseProviderOptions options)
            : base(provider, options)
        {
        }
    }
}
