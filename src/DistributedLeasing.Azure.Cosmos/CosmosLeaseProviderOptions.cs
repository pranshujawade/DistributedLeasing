using Azure.Core;
using DistributedLeasing.Core.Configuration;

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
    /// Validates the configuration options.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the configuration is invalid.</exception>
    /// <exception cref="ArgumentException">Thrown when required properties are missing or invalid.</exception>
    public override void Validate()
    {
        base.Validate();

        // Validate authentication configuration using base class helper
        ValidateAuthenticationConfigured(
            hasConnectionString: !string.IsNullOrWhiteSpace(ConnectionString),
            hasAlternativeAuth: !string.IsNullOrWhiteSpace(AccountKey),
            providerName: "Azure Cosmos DB");

        // Validate endpoint for credential-based authentication using base class helper
        ValidateEndpointForCredential(
            hasEndpoint: AccountEndpoint != null,
            endpointPropertyName: nameof(AccountEndpoint));

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
}
