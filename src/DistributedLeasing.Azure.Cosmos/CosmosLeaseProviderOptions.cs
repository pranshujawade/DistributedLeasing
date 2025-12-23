using Azure.Core;
using DistributedLeasing.Abstractions;
using DistributedLeasing.Abstractions.Authentication;
using DistributedLeasing.Abstractions.Configuration;

namespace DistributedLeasing.Azure.Cosmos;

/// <summary>
/// Configuration options for the Cosmos DB lease provider.
/// </summary>
/// <remarks>
/// <para>
/// This class extends <see cref="LeaseOptions"/> with Cosmos DB-specific settings.
/// Supports authentication via connection string, managed identity, or explicit credentials.
/// </para>
/// <para>
/// <strong>AppSettings.json Example:</strong>
/// </para>
/// <code>
/// {
///   "Leasing": {
///     "Endpoint": "https://myaccount.documents.azure.com:443/",
///     "DatabaseName": "DistributedLeasing",
///     "ContainerName": "Leases",
///     "UseManagedIdentity": true,
///     "CreateDatabaseIfNotExists": true,
///     "CreateContainerIfNotExists": true,
///     "DefaultLeaseDuration": "00:01:00",
///     "AutoRenew": true,
///     "AutoRenewInterval": "00:00:40"
///   }
/// }
/// </code>
/// </remarks>
public class CosmosLeaseProviderOptions : LeaseOptions
{
    /// <summary>
    /// Gets or sets the Cosmos DB account endpoint URI.
    /// </summary>
    /// <remarks>
    /// Required when using managed identity or explicit credentials.
    /// Example: https://myaccount.documents.azure.com:443/
    /// </remarks>
    public Uri? AccountEndpoint { get; set; }

    /// <summary>
    /// Gets or sets the service endpoint URI (standardized alias for <see cref="AccountEndpoint"/>).
    /// </summary>
    /// <value>
    /// The service endpoint URI. This is an alias for <see cref="AccountEndpoint"/> for consistency across providers.
    /// </value>
    /// <remarks>
    /// This property provides a consistent naming convention across all providers.
    /// Setting this property also sets <see cref="AccountEndpoint"/>.
    /// </remarks>
    public Uri? Endpoint
    {
        get => AccountEndpoint;
        set => AccountEndpoint = value;
    }

    /// <summary>
    /// Gets or sets the Cosmos DB connection string.
    /// </summary>
    /// <remarks>
    /// Use only for local development. In production, prefer managed identity.
    /// </remarks>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the account key for authentication.
    /// </summary>
    /// <remarks>
    /// Use only for local development. In production, prefer managed identity.
    /// </remarks>
    public string? AccountKey { get; set; }

    /// <summary>
    /// Gets or sets the name of the database containing the lease collection.
    /// </summary>
    public string DatabaseName { get; set; } = "DistributedLeasing";

    /// <summary>
    /// Gets or sets the name of the container (collection) storing lease documents.
    /// </summary>
    public string ContainerName { get; set; } = "Leases";

    /// <summary>
    /// Gets or sets a value indicating whether to create the database if it doesn't exist.
    /// </summary>
    public bool CreateDatabaseIfNotExists { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to create the container if it doesn't exist.
    /// </summary>
    public bool CreateContainerIfNotExists { get; set; } = true;

    /// <summary>
    /// Gets or sets the partition key path for the lease container.
    /// </summary>
    /// <remarks>
    /// Default is "/id" for single-partition scenarios.
    /// </remarks>
    public string PartitionKeyPath { get; set; } = "/id";

    /// <summary>
    /// Gets or sets the throughput (RU/s) to provision when creating the container.
    /// </summary>
    /// <remarks>
    /// Only used if CreateContainerIfNotExists is true.
    /// Set to null to use serverless or autoscale (configured separately).
    /// </remarks>
    public int? ProvisionedThroughput { get; set; } = 400;

    /// <summary>
    /// Gets or sets the Time-to-Live (TTL) for lease documents in seconds.
    /// </summary>
    /// <remarks>
    /// Enables automatic cleanup of expired lease documents.
    /// Set to -1 to disable TTL, or a positive value for automatic expiration.
    /// Recommended: Set to 2-3x the maximum lease duration to allow for renewal failures.
    /// </remarks>
    public int? DefaultTimeToLive { get; set; } = 300; // 5 minutes

    /// <summary>
    /// Gets or sets the consistency level for Cosmos DB operations.
    /// </summary>
    /// <remarks>
    /// Session consistency (default) provides strong consistency for the session.
    /// Use Strong for maximum consistency at the cost of performance.
    /// </remarks>
    public string ConsistencyLevel { get; set; } = "Session";

    /// <summary>
    /// Gets or sets the authentication configuration.
    /// </summary>
    /// <value>
    /// Authentication options, or <c>null</c> to use connection string or account key.
    /// </value>
    /// <remarks>
    /// <para>
    /// When using managed identity or other token-based authentication, configure this property
    /// and provide <see cref="AccountEndpoint"/> instead of <see cref="ConnectionString"/> or <see cref="AccountKey"/>.
    /// </para>
    /// <para>
    /// For development, you can omit this and provide ConnectionString or AccountKey instead.
    /// </para>
    /// </remarks>
    public AuthenticationOptions? Authentication { get; set; }

    /// <summary>
    /// Gets or sets the credential to use for authentication.
    /// </summary>
    /// <value>
    /// The token credential, or <c>null</c> to use connection string, account key, or authentication configuration.
    /// </value>
    /// <remarks>
    /// <para>
    /// This property allows direct injection of a credential, bypassing the authentication configuration.
    /// </para>
    /// <para>
    /// If set, this takes precedence over <see cref="Authentication"/>, <see cref="ConnectionString"/>, and <see cref="AccountKey"/>.
    /// </para>
    /// </remarks>
    public TokenCredential? Credential { get; set; }

    /// <summary>
    /// Validates the configuration options.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the configuration is invalid.</exception>
    /// <exception cref="ArgumentException">Thrown when required properties are missing or invalid.</exception>
    public override void Validate()
    {
        base.Validate();

        // Validate authentication configuration
        ValidateAuthenticationConfigured(
            hasConnectionString: !string.IsNullOrWhiteSpace(ConnectionString) || !string.IsNullOrWhiteSpace(AccountKey),
            hasAlternativeAuth: AccountEndpoint != null || Credential != null || Authentication != null,
            providerName: "Azure Cosmos DB");

        // If using credential or authentication, require AccountEndpoint
        if ((Credential != null || Authentication != null) && AccountEndpoint == null)
        {
            throw new InvalidOperationException(
                "AccountEndpoint is required when using Credential or Authentication configuration.");
        }

        // Validate authentication options if provided
        if (Authentication != null)
        {
            Authentication.Validate();
        }

        // Validate database name
        if (string.IsNullOrWhiteSpace(DatabaseName))
        {
            throw new ArgumentException("DatabaseName cannot be null or whitespace.", nameof(DatabaseName));
        }

        // Validate container name
        if (string.IsNullOrWhiteSpace(ContainerName))
        {
            throw new ArgumentException("ContainerName cannot be null or whitespace.", nameof(ContainerName));
        }

        // Validate partition key path
        if (string.IsNullOrWhiteSpace(PartitionKeyPath))
        {
            throw new ArgumentException("PartitionKeyPath cannot be null or whitespace.", nameof(PartitionKeyPath));
        }

        // Validate throughput
        if (ProvisionedThroughput.HasValue && ProvisionedThroughput.Value < 400)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ProvisionedThroughput),
                "Provisioned throughput must be at least 400 RU/s or null.");
        }

        // Validate TTL
        if (DefaultTimeToLive.HasValue && DefaultTimeToLive.Value != -1 && DefaultTimeToLive.Value < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(DefaultTimeToLive),
                "DefaultTimeToLive must be -1 (disabled) or a positive number of seconds.");
        }
    }

    /// <summary>
    /// Validates that authentication is properly configured.
    /// </summary>
    private void ValidateAuthenticationConfigured(bool hasConnectionString, bool hasAlternativeAuth, string providerName)
    {
        if (!hasConnectionString && !hasAlternativeAuth)
        {
            throw new InvalidOperationException(
                $"Authentication must be configured for {providerName}. " +
                "Either provide ConnectionString/AccountKey, or configure Authentication/Credential with the required AccountEndpoint.");
        }
    }
}
