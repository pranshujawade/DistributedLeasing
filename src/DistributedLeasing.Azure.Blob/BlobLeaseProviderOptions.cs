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
    /// For security, use <see cref="UseManagedIdentity"/> set to <c>true</c> and provide
    /// the <see cref="StorageAccountUri"/>. Alternatively, provide a <see cref="TokenCredential"/>
    /// for service principal or other credential-based authentication.
    /// </para>
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
        /// Gets or sets the connection string for the storage account.
        /// </summary>
        /// <value>
        /// The storage account connection string.
        /// </value>
        /// <remarks>
        /// <para>
        /// Use this for development or when managed identity is not available.
        /// For production, prefer <see cref="UseManagedIdentity"/> or <see cref="Credential"/>.
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
        /// Gets or sets a value indicating whether to use managed identity for authentication.
        /// </summary>
        /// <value>
        /// <c>true</c> to use DefaultAzureCredential for managed identity authentication;
        /// otherwise, <c>false</c>. Default is <c>false</c>.
        /// </value>
        /// <remarks>
        /// <para>
        /// When set to <c>true</c>, the provider will use DefaultAzureCredential which supports:
        /// <list type="bullet">
        /// <item>Environment variables (service principal)</item>
        /// <item>Workload identity (AKS)</item>
        /// <item>Managed identity (Azure VMs, App Service, Functions)</item>
        /// <item>Azure CLI (local development)</item>
        /// <item>Visual Studio / VS Code (local development)</item>
        /// </list>
        /// </para>
        /// <para>
        /// Requires <see cref="StorageAccountUri"/> to be set.
        /// </para>
        /// </remarks>
        public bool UseManagedIdentity { get; set; }

        /// <summary>
        /// Gets or sets the Azure credential to use for authentication.
        /// </summary>
        /// <value>
        /// A <see cref="TokenCredential"/> for authentication, or <c>null</c> to use other methods.
        /// </value>
        /// <remarks>
        /// Use this to provide a specific credential (e.g., ClientSecretCredential, ManagedIdentityCredential).
        /// If set, this takes precedence over <see cref="UseManagedIdentity"/> and <see cref="ConnectionString"/>.
        /// </remarks>
        public TokenCredential? Credential { get; set; }

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

            // Validate authentication configuration
            if (Credential == null && !UseManagedIdentity && string.IsNullOrEmpty(ConnectionString))
            {
                throw new InvalidOperationException(
                    "At least one authentication method must be configured: " +
                    "UseManagedIdentity, Credential, or ConnectionString.");
            }

            if ((Credential != null || UseManagedIdentity) && StorageAccountUri == null)
            {
                throw new InvalidOperationException(
                    "StorageAccountUri is required when using Credential or UseManagedIdentity.");
            }

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
