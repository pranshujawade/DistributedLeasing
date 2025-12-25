using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace CosmosLeaseSample;

/// <summary>
/// Provides inspection and formatting capabilities for Cosmos DB lease document metadata and state.
/// </summary>
public class CosmosMetadataInspector
{
    private readonly Container _container;
    private readonly ILogger<CosmosMetadataInspector> _logger;
    private readonly string _containerName;
    private readonly string _databaseName;
    private readonly string _accountName;

    public CosmosMetadataInspector(
        Container container,
        ILogger<CosmosMetadataInspector> logger,
        string containerName,
        string databaseName,
        string accountName)
    {
        _container = container ?? throw new ArgumentNullException(nameof(container));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _containerName = containerName;
        _databaseName = databaseName;
        _accountName = accountName;
    }

    /// <summary>
    /// Inspects the current state of a lease document and returns detailed information.
    /// </summary>
    public async Task<DocumentInspectionResult?> InspectDocumentStateAsync(
        string leaseName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var documentId = GetDocumentId(leaseName);
            
            // Read as dynamic to avoid internal class access issues
            var response = await _container.ReadItemStreamAsync(
                documentId,
                new PartitionKey(documentId),
                cancellationToken: cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return new DocumentInspectionResult
                {
                    DocumentId = documentId,
                    Exists = false
                };
            }

            using var streamReader = new StreamReader(response.Content);
            var content = await streamReader.ReadToEndAsync();
            var doc = JsonSerializer.Deserialize<JsonDocument>(content);
            
            if (doc == null)
            {
                return null;
            }

            var root = doc.RootElement;
            var acquiredAt = root.TryGetProperty("acquiredAt", out var acq) 
                ? DateTimeOffset.Parse(acq.GetString() ?? string.Empty) 
                : (DateTimeOffset?)null;
            var expiresAt = root.TryGetProperty("expiresAt", out var exp) 
                ? DateTimeOffset.Parse(exp.GetString() ?? string.Empty) 
                : (DateTimeOffset?)null;

            var metadata = new Dictionary<string, string>();
            if (root.TryGetProperty("metadata", out var metaProp) && metaProp.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in metaProp.EnumerateObject())
                {
                    metadata[prop.Name] = prop.Value.GetString() ?? string.Empty;
                }
            }

            return new DocumentInspectionResult
            {
                DocumentId = documentId,
                Exists = true,
                LeaseName = root.TryGetProperty("leaseName", out var ln) ? ln.GetString() ?? string.Empty : string.Empty,
                LeaseId = root.TryGetProperty("leaseId", out var li) ? li.GetString() ?? string.Empty : string.Empty,
                AcquiredAt = acquiredAt,
                ExpiresAt = expiresAt,
                Owner = root.TryGetProperty("owner", out var own) ? own.GetString() ?? string.Empty : string.Empty,
                IsExpired = expiresAt.HasValue && expiresAt.Value <= DateTimeOffset.UtcNow,
                Metadata = metadata,
                ETag = response.Headers.ETag ?? string.Empty,
                RenewalCount = root.TryGetProperty("renewalCount", out var rc) ? rc.GetInt32() : 0
            };
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return new DocumentInspectionResult
            {
                DocumentId = GetDocumentId(leaseName),
                Exists = false
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to inspect document state for '{LeaseName}'", leaseName);
            return null;
        }
    }

    /// <summary>
    /// Formats lease holder information from document metadata for display.
    /// </summary>
    public string FormatLeaseHolderInfo(DocumentInspectionResult result)
    {
        var sb = new StringBuilder();

        sb.AppendLine("Current Lease Holder:");

        // Extract lease holder information from metadata
        var instanceId = result.Metadata.TryGetValue("instanceId", out var id) ? id : "(unknown)";
        var region = result.Metadata.TryGetValue("region", out var r) ? r : "(unknown)";
        var hostname = result.Metadata.TryGetValue("hostname", out var h) ? h : result.Owner;

        sb.AppendLine($"  • Instance ID: {instanceId}");
        sb.AppendLine($"  • Region: {region}");
        sb.AppendLine($"  • Owner: {hostname}");

        // Show acquisition time
        if (result.AcquiredAt.HasValue)
        {
            var timeSinceAcquisition = DateTimeOffset.UtcNow - result.AcquiredAt.Value;
            sb.AppendLine($"  • Acquired: {result.AcquiredAt.Value:yyyy-MM-dd HH:mm:ss} UTC ({timeSinceAcquisition.TotalSeconds:F1}s ago)");
        }

        return sb.ToString();
    }

    private static string GetDocumentId(string leaseName)
    {
        // Use same normalization as CosmosLeaseProvider
        return leaseName.ToLowerInvariant().Replace(" ", "-");
    }
}

/// <summary>
/// Result of a document inspection operation.
/// </summary>
public class DocumentInspectionResult
{
    public string DocumentId { get; set; } = string.Empty;
    public bool Exists { get; set; }
    public string LeaseName { get; set; } = string.Empty;
    public string LeaseId { get; set; } = string.Empty;
    public DateTimeOffset? AcquiredAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public string Owner { get; set; } = string.Empty;
    public bool IsExpired { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
    public string ETag { get; set; } = string.Empty;
    public int RenewalCount { get; set; }
}
