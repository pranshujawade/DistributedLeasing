using System;
using Azure.Core;
using DistributedLeasing.Core.Configuration;

namespace DistributedLeasing.Azure.Blob
{
    /// <summary>
    /// Configuration options for the Azure Blob Storage lease provider.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class extends <see cref="LeaseOptions"/> with Blob Storage-specific configuration.
    /// Azure Blob Storage supports lease durations between 15 and 60 seconds, or infinite leases.
    /// </para>
    /// <para>
    /// For security, use <see cref="LeaseOptions.UseManagedIdentity"/> set to <c>true</c> and provide
    /// the <see cref="Endpoint"/>. Alternatively, provide a <see cref="LeaseOptions.Credential"/>
    /// for service principal or other credential-based authentication.
    /// </para>
    /// <para>
    /// <strong>AppSettings.json Example:</strong>
    /// </para>
    /// <code>
    /// {
    ///   "Leasing": {
    ///     "Endpoint": "https://mystorageaccount.blob.core.windows.net",
    ///     "ContainerName": "leases",
    ///     "KeyPrefix": "myapp-",
    ///     "UseManagedIdentity": true,
    ///     "CreateContainerIfNotExists": true,
    ///     "DefaultLeaseDuration": "00:00:30",
    ///     "AutoRenew": true,
    ///     "AutoRenewInterval": "00:00:20"
    ///   }
    /// }
    /// </code>
    /// </remarks>
    public class BlobLeaseProviderOptions : LeaseOptions
    {
        private TimeSpan _minLeaseDuration = TimeSpan.FromSeconds(15);
        private TimeSpan _maxLeaseDuration = TimeSpan.FromSeconds(60);

        /// <summary>
        /// Gets or sets the URI of the Azure Storage account.
        /// </summary>
        /// <value>
        /// The storage account URI (e.g., https://mystorageaccount.blob.core.windows.net).
        /// </value>
        /// <remarks>
        /// This is required when using managed identity or token credential authentication.
        /// </remarks>
        public Uri? StorageAccountUri { get; set; }
        
        /// <summary>
        /// Gets or sets the service endpoint URI (standardized alias for <see cref="StorageAccountUri"/>).
        /// </summary>
        /// <value>
        /// The service endpoint URI. This is an alias for <see cref="StorageAccountUri"/> for consistency across providers.
        /// </value>
        /// <remarks>
        /// This property provides a consistent naming convention across all providers.
        /// Setting this property also sets <see cref="StorageAccountUri"/>.
        /// </remarks>
        public Uri? Endpoint
        {
            get => StorageAccountUri;
            set => StorageAccountUri = value;
        }

        /// <summary>
        /// Gets or sets the connection string for the storage account.
        /// </summary>
        /// <value>
        /// The storage account connection string.
        /// </value>
        /// <remarks>
        /// <para>
        /// Use this for development or when managed identity is not available.
        /// For production, prefer managed identity or explicit credentials.
        /// </para>
        /// <para>
        /// If both connection string and credential are provided, the credential takes precedence.
        /// </para>
        /// </remarks>
        public string? ConnectionString { get; set; }

        /// <summary>
        /// Gets or sets the name of the container that will store lease blobs.
        /// </summary>
        /// <value>
        /// The container name. Default is "leases".
        /// </value>
        public string ContainerName { get; set; } = "leases";

        /// <summary>
        /// Gets or sets a value indicating whether to create the container if it does not exist.
        /// </summary>
        /// <value>
        /// <c>true</c> to automatically create the container; otherwise, <c>false</c>. Default is <c>false</c>.
        /// </value>
        /// <remarks>
        /// When set to <c>true</c>, requires the identity to have appropriate permissions
        /// (e.g., Storage Blob Data Contributor role at the account level).
        /// </remarks>
        public bool CreateContainerIfNotExists { get; set; }

        /// <summary>
        /// Gets or sets the prefix to use for lease blob names.
        /// </summary>
        /// <value>
        /// The blob name prefix. Default is "lease-".
        /// </value>
        /// <remarks>
        /// The final blob name will be {BlobPrefix}{leaseName}.
        /// This allows for organizing lease blobs within the container.
        /// </remarks>
        public string BlobPrefix { get; set; } = "lease-";
        
        /// <summary>
        /// Gets or sets the key prefix for lease names (standardized alias for <see cref="BlobPrefix"/>).
        /// </summary>
        /// <value>
        /// The key prefix. This is an alias for <see cref="BlobPrefix"/> for consistency across providers.
        /// </value>
        /// <remarks>
        /// This property provides a consistent naming convention across all providers.
        /// Setting this property also sets <see cref="BlobPrefix"/>.
        /// </remarks>
        public string KeyPrefix
        {
            get => BlobPrefix;
            set => BlobPrefix = value;
        }

        /// <summary>
        /// Gets the minimum allowed lease duration for Azure Blob Storage.
        /// </summary>
        /// <value>
        /// 15 seconds (Azure Blob Storage minimum).
        /// </value>
        public TimeSpan MinLeaseDuration => _minLeaseDuration;

        /// <summary>
        /// Gets the maximum allowed lease duration for Azure Blob Storage.
        /// </summary>
        /// <value>
        /// 60 seconds (Azure Blob Storage maximum for non-infinite leases).
        /// </value>
        public TimeSpan MaxLeaseDuration => _maxLeaseDuration;

        /// <summary>
        /// Validates the configuration options.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the configuration is invalid.
        /// </exception>
        public override void Validate()
        {
            base.Validate();

            // Validate authentication configuration using base class helper
            ValidateAuthenticationConfigured(
                hasConnectionString: !string.IsNullOrEmpty(ConnectionString),
                hasAlternativeAuth: false,
                providerName: "Azure Blob Storage");

            // Validate endpoint for credential-based authentication using base class helper
            ValidateEndpointForCredential(
                hasEndpoint: StorageAccountUri != null,
                endpointPropertyName: nameof(StorageAccountUri));

            if (string.IsNullOrWhiteSpace(ContainerName))
            {
                throw new InvalidOperationException("ContainerName cannot be null or empty.");
            }

            // Validate lease duration against Azure Blob Storage limits
            if (DefaultLeaseDuration != Timeout.InfiniteTimeSpan)
            {
                if (DefaultLeaseDuration < MinLeaseDuration)
                {
                    throw new InvalidOperationException(
                        $"DefaultLeaseDuration must be at least {MinLeaseDuration.TotalSeconds} seconds for Azure Blob Storage.");
                }

                if (DefaultLeaseDuration > MaxLeaseDuration)
                {
                    throw new InvalidOperationException(
                        $"DefaultLeaseDuration must be at most {MaxLeaseDuration.TotalSeconds} seconds for Azure Blob Storage (use Timeout.InfiniteTimeSpan for infinite leases).");
                }
            }
        }
    }
}
