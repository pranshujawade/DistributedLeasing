using Newtonsoft.Json;

namespace DistributedLeasing.Azure.Cosmos.Models;

/// <summary>
/// Represents a lease document stored in Cosmos DB.
/// </summary>
/// <remarks>
/// Uses optimistic concurrency control via ETag for conflict resolution.
/// The document includes TTL for automatic cleanup of expired leases.
/// </remarks>
internal class LeaseDocument
{
    /// <summary>
    /// Gets or sets the unique identifier for the lease document.
    /// </summary>
    /// <remarks>
    /// This serves as both the document ID and the partition key.
    /// </remarks>
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the lease.
    /// </summary>
    [JsonProperty("leaseName")]
    public string LeaseName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the unique identifier of the current lease holder.
    /// </summary>
    [JsonProperty("leaseId")]
    public string LeaseId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the UTC timestamp when the lease was acquired.
    /// </summary>
    [JsonProperty("acquiredAt")]
    public DateTimeOffset AcquiredAt { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the lease expires.
    /// </summary>
    [JsonProperty("expiresAt")]
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>
    /// Gets or sets the duration of the lease in seconds.
    /// </summary>
    [JsonProperty("durationSeconds")]
    public int DurationSeconds { get; set; }

    /// <summary>
    /// Gets or sets the ETag for optimistic concurrency control.
    /// </summary>
    [JsonProperty("_etag")]
    public string? ETag { get; set; }

    /// <summary>
    /// Gets or sets the time-to-live (TTL) in seconds for automatic cleanup.
    /// </summary>
    /// <remarks>
    /// Cosmos DB will automatically delete the document after this many seconds.
    /// Set to -1 to disable TTL for this document.
    /// </remarks>
    [JsonProperty("ttl")]
    public int? TimeToLive { get; set; }

    /// <summary>
    /// Gets or sets the owner identifier (hostname, instance ID, etc.).
    /// </summary>
    [JsonProperty("owner")]
    public string? Owner { get; set; }

    /// <summary>
    /// Gets or sets metadata about the lease holder.
    /// </summary>
    [JsonProperty("metadata")]
    public Dictionary<string, string>? Metadata { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the last renewal.
    /// </summary>
    [JsonProperty("lastRenewedAt")]
    public DateTimeOffset? LastRenewedAt { get; set; }

    /// <summary>
    /// Gets or sets the renewal count.
    /// </summary>
    [JsonProperty("renewalCount")]
    public int RenewalCount { get; set; }
}
