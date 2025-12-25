using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;

namespace BlobLeaseSample;

/// <summary>
/// Provides inspection and formatting capabilities for Azure Blob Storage lease metadata and state.
/// </summary>
public class AzureMetadataInspector
{
    private readonly BlobContainerClient _containerClient;
    private readonly ILogger<AzureMetadataInspector> _logger;
    private readonly string _containerName;
    private readonly string _storageAccountName;

    public AzureMetadataInspector(
        BlobContainerClient containerClient,
        ILogger<AzureMetadataInspector> logger,
        string containerName,
        string storageAccountName)
    {
        _containerClient = containerClient ?? throw new ArgumentNullException(nameof(containerClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _containerName = containerName;
        _storageAccountName = storageAccountName;
    }

    /// <summary>
    /// Inspects the current state of a blob and returns detailed information.
    /// </summary>
    public async Task<BlobInspectionResult?> InspectBlobStateAsync(
        string blobName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var blobClient = _containerClient.GetBlobClient(blobName);

            // Check if blob exists
            var exists = await blobClient.ExistsAsync(cancellationToken);
            if (!exists.Value)
            {
                return new BlobInspectionResult
                {
                    BlobName = blobName,
                    Exists = false
                };
            }

            // Get blob properties
            var properties = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);

            return new BlobInspectionResult
            {
                BlobName = blobName,
                Exists = true,
                LeaseState = properties.Value.LeaseState,
                LeaseStatus = properties.Value.LeaseStatus,
                LeaseDuration = properties.Value.LeaseDuration,
                Metadata = new Dictionary<string, string>(properties.Value.Metadata),
                CreatedOn = properties.Value.CreatedOn,
                LastModified = properties.Value.LastModified,
                ContentLength = properties.Value.ContentLength,
                ETag = properties.Value.ETag.ToString()
            };
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return new BlobInspectionResult
            {
                BlobName = blobName,
                Exists = false
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to inspect blob state for '{BlobName}'", blobName);
            return null;
        }
    }

    /// <summary>
    /// Formats a blob inspection result for console display.
    /// </summary>
    public string FormatBlobStateForDisplay(
        BlobInspectionResult result,
        string title,
        DateTimeOffset timestamp)
    {
        var sb = new StringBuilder();
        var separator = new string('═', 63);

        sb.AppendLine(separator);
        sb.AppendLine(title);
        sb.AppendLine($"Time: {timestamp:yyyy-MM-dd HH:mm:ss.fff} UTC");
        sb.AppendLine(separator);
        sb.AppendLine($"Blob: {result.BlobName}");
        sb.AppendLine($"Container: {_containerName}");
        sb.AppendLine($"Storage Account: {_storageAccountName}");
        sb.AppendLine();

        if (!result.Exists)
        {
            sb.AppendLine("Status: Blob does not exist (will be created on acquisition)");
            sb.AppendLine(separator);
            return sb.ToString();
        }

        sb.AppendLine($"Lease State: {result.LeaseState}");
        sb.AppendLine($"Lease Status: {result.LeaseStatus}");
        sb.AppendLine($"Lease Duration: {GetLeaseDurationDescription(result.LeaseDuration)}");
        sb.AppendLine();

        // Show metadata
        sb.AppendLine("Metadata:");
        if (result.Metadata.Count == 0)
        {
            sb.AppendLine("  (none)");
        }
        else
        {
            foreach (var kvp in result.Metadata.OrderBy(x => x.Key))
            {
                sb.AppendLine($"  • {kvp.Key}: {kvp.Value}");
            }
        }
        sb.AppendLine();

        // Show timestamps
        sb.AppendLine("Timestamps:");
        sb.AppendLine($"  • Created: {result.CreatedOn:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"  • Last Modified: {result.LastModified:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine();

        // Show properties
        sb.AppendLine("Properties:");
        sb.AppendLine($"  • Content Length: {result.ContentLength} bytes");
        sb.AppendLine($"  • ETag: {result.ETag}");
        
        sb.AppendLine(separator);

        return sb.ToString();
    }

    /// <summary>
    /// Formats lease holder information from metadata for display.
    /// </summary>
    public string FormatLeaseHolderInfo(BlobInspectionResult result)
    {
        var sb = new StringBuilder();

        sb.AppendLine("Current Lease Holder Metadata:");

        // Extract lease holder information from metadata
        var instanceId = result.Metadata.TryGetValue("lease_instanceId", out var id) ? id : "(unknown)";
        var region = result.Metadata.TryGetValue("lease_region", out var r) ? r : "(unknown)";
        var hostname = result.Metadata.TryGetValue("lease_hostname", out var h) ? h : "(unknown)";

        sb.AppendLine($"  • lease_instanceId: {instanceId}");
        sb.AppendLine($"  • lease_region: {region}");
        sb.AppendLine($"  • lease_hostname: {hostname}");

        // Show acquisition time if available (from createdAt or lastModified)
        if (result.Metadata.TryGetValue("createdAt", out var createdAt))
        {
            if (DateTimeOffset.TryParse(createdAt, out var createdTime))
            {
                var timeSinceAcquisition = DateTimeOffset.UtcNow - createdTime;
                sb.AppendLine($"  • Acquired At: {createdTime:yyyy-MM-dd HH:mm:ss.fff} UTC ({timeSinceAcquisition.TotalSeconds:F3} seconds ago)");
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Shows a summary of the container and its lease blobs.
    /// </summary>
    public async Task ShowContainerSummaryAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("═══════════════════════════════════════════════════════════");
            _logger.LogInformation("CONTAINER SUMMARY");
            _logger.LogInformation("═══════════════════════════════════════════════════════════");
            _logger.LogInformation("Container: {ContainerName}", _containerName);
            _logger.LogInformation("Storage Account: {StorageAccount}", _storageAccountName);
            _logger.LogInformation("");

            var blobs = new List<string>();
            var leasedBlobs = 0;
            var availableBlobs = 0;

            await foreach (var blobItem in _containerClient.GetBlobsAsync(cancellationToken: cancellationToken))
            {
                blobs.Add(blobItem.Name);
                
                if (blobItem.Properties.LeaseState == LeaseState.Leased)
                {
                    leasedBlobs++;
                }
                else if (blobItem.Properties.LeaseState == LeaseState.Available)
                {
                    availableBlobs++;
                }
            }

            _logger.LogInformation("Total Blobs: {TotalBlobs}", blobs.Count);
            _logger.LogInformation("  • Leased: {LeasedBlobs}", leasedBlobs);
            _logger.LogInformation("  • Available: {AvailableBlobs}", availableBlobs);
            
            if (blobs.Count > 0)
            {
                _logger.LogInformation("");
                _logger.LogInformation("Blobs:");
                foreach (var blob in blobs)
                {
                    _logger.LogInformation("  • {BlobName}", blob);
                }
            }

            _logger.LogInformation("═══════════════════════════════════════════════════════════");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve container summary");
        }
    }

    private string GetLeaseDurationDescription(LeaseDurationType durationType)
    {
        return durationType switch
        {
            LeaseDurationType.Infinite => "Infinite",
            LeaseDurationType.Fixed => "Fixed (15-60 seconds)",
            _ => durationType.ToString()
        };
    }
}

/// <summary>
/// Result of a blob inspection operation.
/// </summary>
public class BlobInspectionResult
{
    public string BlobName { get; set; } = string.Empty;
    public bool Exists { get; set; }
    public LeaseState LeaseState { get; set; }
    public LeaseStatus LeaseStatus { get; set; }
    public LeaseDurationType LeaseDuration { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
    public DateTimeOffset CreatedOn { get; set; }
    public DateTimeOffset LastModified { get; set; }
    public long ContentLength { get; set; }
    public string ETag { get; set; } = string.Empty;
}
