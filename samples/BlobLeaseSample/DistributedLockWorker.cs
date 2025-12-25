using DistributedLeasing.Abstractions.Contracts;
using DistributedLeasing.Abstractions.Events;
using Microsoft.Extensions.Logging;
using Azure.Storage.Blobs;

namespace BlobLeaseSample;

/// <summary>
/// Demonstrates distributed lock acquisition where multiple instances compete for a single lock.
/// Only one instance (winner) can hold the lock and execute critical work.
/// </summary>
public class DistributedLockWorker
{
    private readonly ILeaseManager _leaseManager;
    private readonly ILogger<DistributedLockWorker> _logger;
    private readonly string _instanceId;
    private readonly string _region;
    private readonly string _lockName;
    private readonly AzureMetadataInspector? _metadataInspector;

    public DistributedLockWorker(
        ILeaseManager leaseManager,
        ILogger<DistributedLockWorker> logger,
        string instanceId,
        string region,
        AzureMetadataInspector? metadataInspector = null)
    {
        _leaseManager = leaseManager ?? throw new ArgumentNullException(nameof(leaseManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _instanceId = instanceId;
        _region = region;
        _lockName = "critical-section-lock"; // Single lock that all instances compete for
        _metadataInspector = metadataInspector;
    }

    /// <summary>
    /// Attempts to acquire the distributed lock and execute work if successful.
    /// </summary>
    public async Task<bool> TryExecuteWithLockAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("═══════════════════════════════════════════════════════════");
        _logger.LogInformation("Instance: {Instance} (Region: {Region})", _instanceId, _region);
        _logger.LogInformation("Attempting to acquire distributed lock: '{LockName}'", _lockName);
        _logger.LogInformation("═══════════════════════════════════════════════════════════");

        // Pre-acquisition inspection
        await InspectAndLogPreAcquisitionAsync(cancellationToken);

        ILease? lease = null;

        try
        {
            // Try to acquire the lock (non-blocking)
            lease = await _leaseManager.TryAcquireAsync(
                leaseName: _lockName,
                duration: null, // Use default from configuration
                cancellationToken: cancellationToken);

            if (lease == null)
            {
                // Failed to acquire - another instance holds the lock
                _logger.LogWarning("╔════════════════════════════════════════════════════════╗");
                _logger.LogWarning("║  LOCK ACQUISITION FAILED                               ║");
                _logger.LogWarning("║  Another region is currently holding the lock         ║");
                _logger.LogWarning("╚════════════════════════════════════════════════════════╝");
                _logger.LogInformation("");
                
                // Show who holds the lock
                await InspectAndLogFailureAsync(cancellationToken);
                
                _logger.LogInformation("This instance cannot execute critical work at this time.");
                _logger.LogInformation("The lock is held by another instance in a different region.");
                _logger.LogInformation("Exiting gracefully...");
                return false;
            }

            // Successfully acquired the lock!
            _logger.LogInformation("╔════════════════════════════════════════════════════════╗");
            _logger.LogInformation("║  ✓ LOCK ACQUIRED SUCCESSFULLY                          ║");
            _logger.LogInformation("║  This region is now the ACTIVE processor               ║");
            _logger.LogInformation("╚════════════════════════════════════════════════════════╝");
            _logger.LogInformation("");
            _logger.LogInformation("Lock Details:");
            _logger.LogInformation("  • Lease ID: {LeaseId}", lease.LeaseId);
            _logger.LogInformation("  • Acquired At: {AcquiredAt}", lease.AcquiredAt);
            _logger.LogInformation("  • Expires At: {ExpiresAt}", lease.ExpiresAt);
            _logger.LogInformation("  • Duration: {Duration} seconds", (lease.ExpiresAt - lease.AcquiredAt).TotalSeconds);
            _logger.LogInformation("  • Instance: {Instance}", _instanceId);
            _logger.LogInformation("  • Region: {Region}", _region);
            _logger.LogInformation("");
            
            // Post-acquisition inspection
            await InspectAndLogPostAcquisitionAsync(lease, cancellationToken);

            // Subscribe to lease events for monitoring
            SubscribeToLeaseEvents(lease);

            // Execute critical work while holding the lock
            await ExecuteCriticalWorkAsync(lease, cancellationToken);

            return true;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Operation cancelled. Shutting down...");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during lock acquisition or execution: {Message}", ex.Message);
            return false;
        }
        finally
        {
            // Always release the lock when done
            if (lease != null)
            {
                await ReleaseLockAsync(lease);
            }
        }
    }

    /// <summary>
    /// Executes critical work that requires exclusive access (the protected critical section).
    /// </summary>
    private async Task ExecuteCriticalWorkAsync(ILease lease, CancellationToken cancellationToken)
    {
        _logger.LogInformation("▶ Starting critical work execution...");
        _logger.LogInformation("  (Auto-renewal is active - lock will be maintained)");
        _logger.LogInformation("");
        _logger.LogInformation("Press Ctrl+C to stop and release the lock.");
        _logger.LogInformation("───────────────────────────────────────────────────────────");

        int workItemsProcessed = 0;
        var startTime = DateTimeOffset.UtcNow;

        while (!cancellationToken.IsCancellationRequested)
        {
            // Simulate processing work items
            await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
            workItemsProcessed++;

            var elapsed = DateTimeOffset.UtcNow - startTime;

            _logger.LogInformation(
                "[{Instance}] Processing work item #{Count} | Elapsed: {Elapsed:mm\\:ss} | Renewals: {Renewals}",
                _instanceId,
                workItemsProcessed,
                elapsed,
                lease.RenewalCount);

            // Check if we still hold the lock
            if (!lease.IsAcquired)
            {
                _logger.LogError("Lock was lost! Stopping work immediately.");
                break;
            }
        }

        _logger.LogInformation("");
        _logger.LogInformation("───────────────────────────────────────────────────────────");
        _logger.LogInformation("Critical work completed. Total items processed: {Count}", workItemsProcessed);
    }

    /// <summary>
    /// Subscribes to lease lifecycle events for monitoring.
    /// </summary>
    private void SubscribeToLeaseEvents(ILease lease)
    {
        lease.LeaseRenewed += async (sender, e) =>
        {
            _logger.LogDebug(
                "  ↻ Lock renewed | New expiration: {Expiration}",
                e.NewExpiration.ToString("HH:mm:ss"));
            
            // Log renewal metadata if inspector is available
            if (_metadataInspector != null && sender is ILease renewedLease)
            {
                await InspectAndLogRenewalAsync(renewedLease, e);
            }
        };

        lease.LeaseRenewalFailed += (sender, e) =>
        {
            _logger.LogWarning(
                "  ⚠ Lock renewal failed | Attempt {Attempt} | Will retry: {WillRetry}",
                e.AttemptNumber,
                e.WillRetry);
        };

        lease.LeaseLost += (sender, e) =>
        {
            _logger.LogError(
                "  ✗ LOCK LOST | Reason: {Reason} | Critical work must stop!",
                e.Reason);
        };
    }

    /// <summary>
    /// Releases the distributed lock, making it available for other instances.
    /// </summary>
    private async Task ReleaseLockAsync(ILease lease)
    {
        try
        {
            _logger.LogInformation("");
            _logger.LogInformation("───────────────────────────────────────────────────────────");
            _logger.LogInformation("Releasing lock: {LeaseId}", lease.LeaseId);
            
            await lease.ReleaseAsync();
            
            _logger.LogInformation("✓ Lock released successfully");
            _logger.LogInformation("  Other instances can now acquire this lock");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to release lock cleanly: {Message}", ex.Message);
        }
        finally
        {
            await lease.DisposeAsync();
        }
    }

    /// <summary>
    /// Inspects and logs blob state before acquisition attempt.
    /// </summary>
    private async Task InspectAndLogPreAcquisitionAsync(CancellationToken cancellationToken)
    {
        if (_metadataInspector == null)
            return;

        try
        {
            var blobName = $"lease-{_lockName}";
            var result = await _metadataInspector.InspectBlobStateAsync(blobName, cancellationToken);
            
            if (result != null)
            {
                var display = _metadataInspector.FormatBlobStateForDisplay(
                    result,
                    "PRE-ACQUISITION BLOB INSPECTION",
                    DateTimeOffset.UtcNow);
                _logger.LogInformation("{Display}", display);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to inspect blob state before acquisition");
        }
    }

    /// <summary>
    /// Inspects and logs blob state after successful acquisition.
    /// </summary>
    private async Task InspectAndLogPostAcquisitionAsync(ILease lease, CancellationToken cancellationToken)
    {
        if (_metadataInspector == null)
            return;

        try
        {
            var blobName = $"lease-{_lockName}";
            var result = await _metadataInspector.InspectBlobStateAsync(blobName, cancellationToken);
            
            if (result != null)
            {
                var display = _metadataInspector.FormatBlobStateForDisplay(
                    result,
                    "POST-ACQUISITION BLOB INSPECTION",
                    DateTimeOffset.UtcNow);
                _logger.LogInformation("{Display}", display);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to inspect blob state after acquisition");
        }
    }

    /// <summary>
    /// Inspects and logs current holder information when acquisition fails.
    /// </summary>
    private async Task InspectAndLogFailureAsync(CancellationToken cancellationToken)
    {
        if (_metadataInspector == null)
            return;

        try
        {
            var blobName = $"lease-{_lockName}";
            var result = await _metadataInspector.InspectBlobStateAsync(blobName, cancellationToken);
            
            if (result != null && result.Exists)
            {
                _logger.LogInformation("Current Blob State:");
                _logger.LogInformation("  • Lease State: {LeaseState}", result.LeaseState);
                _logger.LogInformation("  • Lease Status: {LeaseStatus}", result.LeaseStatus);
                _logger.LogInformation("");
                
                var holderInfo = _metadataInspector.FormatLeaseHolderInfo(result);
                _logger.LogInformation("{HolderInfo}", holderInfo);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to inspect lease holder information");
        }
    }

    /// <summary>
    /// Inspects and logs metadata during renewal.
    /// </summary>
    private async Task InspectAndLogRenewalAsync(ILease lease, LeaseRenewedEventArgs eventArgs)
    {
        if (_metadataInspector == null)
            return;

        try
        {
            _logger.LogDebug("───────────────────────────────────────────────────────────");
            _logger.LogDebug("AUTO-RENEWAL EVENT");
            _logger.LogDebug("Time: {Time:yyyy-MM-dd HH:mm:ss.fff} UTC", DateTimeOffset.UtcNow);
            _logger.LogDebug("───────────────────────────────────────────────────────────");
            _logger.LogDebug("Lease Details:");
            _logger.LogDebug("  • Lease ID: {LeaseId}", lease.LeaseId);
            _logger.LogDebug("  • Renewal Timestamp: {Timestamp:yyyy-MM-dd HH:mm:ss.fff} UTC", eventArgs.Timestamp);
            _logger.LogDebug("  • New Expiration: {NewExpiration:yyyy-MM-dd HH:mm:ss.fff} UTC", eventArgs.NewExpiration);
            _logger.LogDebug("  • Renewal Count: {RenewalCount}", lease.RenewalCount);
            _logger.LogDebug("───────────────────────────────────────────────────────────");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to log renewal metadata");
        }
    }
}
