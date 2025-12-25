using DistributedLeasing.Abstractions.Contracts;
using DistributedLeasing.Abstractions.Events;
using Microsoft.Extensions.Logging;

namespace RedisLeaseSample;

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
    private readonly RedisMetadataInspector? _metadataInspector;

    public DistributedLockWorker(
        ILeaseManager leaseManager,
        ILogger<DistributedLockWorker> logger,
        string instanceId,
        string region,
        RedisMetadataInspector? metadataInspector = null)
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
        _logger.LogInformation("[{Instance}] Attempting lock | Region: {Region}", _instanceId, _region);

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
                await LogFailureAndHolderInfoAsync(cancellationToken);
                return false;
            }

            // Successfully acquired the lock!
            _logger.LogInformation("[{Instance}] ✓ Lock acquired | Lease: {LeaseId} | Duration: 15s", 
                _instanceId, lease.LeaseId.Substring(0, 8));

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
        var startTime = DateTimeOffset.UtcNow;
        var executionDuration = TimeSpan.FromSeconds(15);
        var progressInterval = TimeSpan.FromSeconds(3);
        var nextProgressTime = startTime + progressInterval;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(executionDuration);

        try
        {
            while (!cts.Token.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100), cts.Token);

                var elapsed = DateTimeOffset.UtcNow - startTime;
                
                // Log progress every 3 seconds
                if (DateTimeOffset.UtcNow >= nextProgressTime)
                {
                    _logger.LogInformation("[{Instance}] Working... [{ElapsedSeconds}s]", _instanceId, (int)elapsed.TotalSeconds);
                    nextProgressTime = DateTimeOffset.UtcNow + progressInterval;
                }

                // Check if we still hold the lock
                if (!lease.IsAcquired)
                {
                    _logger.LogError("Lock was lost! Stopping work immediately.");
                    return;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when 15 seconds elapsed
        }

        var finalDuration = DateTimeOffset.UtcNow - startTime;
        _logger.LogInformation("[{Instance}] Completed | Duration: {Duration}s | Renewals: {Renewals}", 
            _instanceId, (int)finalDuration.TotalSeconds, lease.RenewalCount);
    }

    /// <summary>
    /// Subscribes to lease lifecycle events for monitoring.
    /// </summary>
    private void SubscribeToLeaseEvents(ILease lease)
    {
        lease.LeaseRenewed += (sender, e) =>
        {
            // Suppress renewal logs for cleaner output (debug level only)
            _logger.LogDebug("Lock renewed | Expiration: {Expiration}", e.NewExpiration);
        };

        lease.LeaseRenewalFailed += (sender, e) =>
        {
            _logger.LogWarning("⚠ Lock renewal failed | Attempt {Attempt}", e.AttemptNumber);
        };

        lease.LeaseLost += (sender, e) =>
        {
            _logger.LogError("✗ Lock lost | Reason: {Reason}", e.Reason);
        };
    }

    /// <summary>
    /// Releases the distributed lock, making it available for other instances.
    /// </summary>
    private async Task ReleaseLockAsync(ILease lease)
    {
        try
        {
            await lease.ReleaseAsync();
            _logger.LogInformation("[{Instance}] Lock released", _instanceId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[{Instance}] Failed to release lock: {Message}", _instanceId, ex.Message);
        }
        finally
        {
            await lease.DisposeAsync();
        }
    }

    /// <summary>
    /// Logs failure information and attempts to show current lock holder.
    /// </summary>
    private async Task LogFailureAndHolderInfoAsync(CancellationToken cancellationToken)
    {
        if (_metadataInspector == null)
        {
            _logger.LogWarning("[{Instance}] ✗ Lock unavailable | Held by another instance", _instanceId);
            return;
        }

        try
        {
            var keyName = $"lease:{_lockName}";
            var result = await _metadataInspector.InspectKeyStateAsync(keyName, cancellationToken);
            
            if (result != null && result.Exists && result.LeaseData != null)
            {
                var holderId = result.LeaseData.OwnerId ?? "unknown";
                var holderRegion = result.LeaseData.Metadata?.TryGetValue("region", out var region) == true 
                    ? region 
                    : "unknown";
                _logger.LogWarning("[{Instance}] ✗ Lock unavailable | Held by: {HolderId} ({HolderRegion})", 
                    _instanceId, holderId, holderRegion);
            }
            else
            {
                _logger.LogWarning("[{Instance}] ✗ Lock unavailable | Held by another instance", _instanceId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[{Instance}] ✗ Lock unavailable | Held by another instance", _instanceId);
            _logger.LogDebug(ex, "Failed to inspect holder information");
        }
    }
}
