# CriticalSectionFunction Sample - Quick Start Guide

## What You Just Created

A **production-ready Azure Function** that demonstrates:
- ? Distributed leasing for critical section protection
- ? **Auto-renewal** enabled (lease renews automatically every 20s)
- ? Per-product locking (concurrent operations on different products)
- ? Global job coordination (only one instance runs reconciliation)
- ? Event monitoring (tracks renewal, failures, and lease loss)

## File Structure

```
samples/CriticalSectionFunction/
??? Program.cs                     # DI setup with auto-renewal config
??? InventoryFunctions.cs          # HTTP + Timer triggered functions
??? IInventoryService.cs           # Service interface
??? InventoryService.cs            # Service with lease logic
??? host.json                      # Function host config
??? local.settings.json            # Local configuration
??? README.md                      # Full documentation
```

## Key Configuration (in `Program.cs`)

```csharp
services.AddBlobLeaseManager(options =>
{
    options.AutoRenew = true;                          // ? Enable auto-renewal
    options.DefaultLeaseDuration = TimeSpan.FromSeconds(30);
    options.AutoRenewInterval = TimeSpan.FromSeconds(20);  // Renew at 20s
    options.AutoRenewMaxRetries = 3;                   // Retry 3 times on failure
});
```

## Consumer Usage Pattern

### From `appsettings.json`:
```json
{
  "DistributedLeasing": {
    "StorageAccountUri": "https://mystorageaccount.blob.core.windows.net",
    "UseManagedIdentity": true,
    "AutoRenew": true,
    "DefaultLeaseDuration": "00:00:30",
    "AutoRenewInterval": "00:00:20"
  }
}
```

### In Code:
```csharp
// Acquire lease with auto-renewal
await using var lease = await _leaseManager.TryAcquireAsync(
    $"inventory-{productId}",
    duration: TimeSpan.FromSeconds(30));

if (lease != null)
{
    // Subscribe to events
    lease.LeaseRenewed += (s, e) => _logger.LogDebug("Renewed at {Time}", e.Timestamp);
    lease.LeaseLost += (s, e) => _logger.LogError("Lost: {Reason}", e.Reason);

    // Do work - lease auto-renews every 20 seconds
    await ProcessLongRunningOperation();
    
    // Renewal count available for monitoring
    _logger.LogInformation("Completed with {Count} renewals", lease.RenewalCount);
}
// Lease auto-released on dispose
```

## Functions Included

| Function | Trigger | Lease Type | Purpose |
|----------|---------|------------|---------|
| **ReserveInventory** | HTTP | Per-product | Reserve inventory atomically |
| **ReleaseInventory** | HTTP | Per-product | Release reservations |
| **GetInventory** | HTTP | None | Read inventory status |
| **ReconcileInventory** | Timer | Global | Clean expired reservations (only 1 instance) |

## Testing Locally

```bash
# 1. Start Azurite
azurite --silent

# 2. Navigate to sample
cd samples/CriticalSectionFunction

# 3. Run function
func start

# 4. Test reservation
curl -X POST http://localhost:7071/api/inventory/PROD-001/reserve \
  -H "Content-Type: application/json" \
  -d '{"quantity": 5, "customerId": "CUST-123"}'
```

## Expected Behavior

### Single Instance:
```
? Acquired lease abc-123 for product PROD-001
Performing work...
Lease abc-123 renewed. New expiration: 2025-01-19T14:45:30Z
? Successfully reserved 5 units. Renewal count: 2
Releasing lease abc-123
```

### Multiple Instances Competing:
```
[Instance 1] ? Acquired lease abc-123
[Instance 2] ? Could not acquire lease. Another operation in progress.
[Instance 1] Lease renewed at 14:45:10
[Instance 1] ? Work complete. Renewals: 2
[Instance 1] Releasing lease
[Instance 2] ? Acquired lease def-456
```

## What Auto-Renewal Does

1. **Starts automatically** when lease is acquired (if `AutoRenew = true`)
2. **Renews periodically** at configured interval (default: 2/3 of duration)
3. **Retries on failure** with exponential backoff
4. **Fires events** for monitoring:
   - `LeaseRenewed` - successful renewal
   - `LeaseRenewalFailed` - retry attempt
   - `LeaseLost` - lease definitively lost
5. **Stops automatically** on dispose/release

## Deployment to Azure

```bash
# Create function app
az functionapp create \
  --name MyInventoryFunction \
  --resource-group MyResourceGroup \
  --storage-account mystorageaccount \
  --runtime dotnet-isolated \
  --functions-version 4

# Enable managed identity
az functionapp identity assign \
  --name MyInventoryFunction \
  --resource-group MyResourceGroup

# Grant Storage Blob Data Contributor role
az role assignment create \
  --assignee <principal-id> \
  --role "Storage Blob Data Contributor" \
  --scope /subscriptions/{sub-id}/resourceGroups/{rg}/providers/Microsoft.Storage/storageAccounts/{account}

# Configure app settings
az functionapp config appsettings set \
  --name MyInventoryFunction \
  --resource-group MyResourceGroup \
  --settings \
  "DistributedLeasing:StorageAccountUri=https://mystorageaccount.blob.core.windows.net" \
  "DistributedLeasing:UseManagedIdentity=true" \
  "DistributedLeasing:AutoRenew=true"
```

## Next Steps

1. Review the [full README](./README.md) for detailed documentation
2. Check the [BasicLeaseAcquisition](../BasicLeaseAcquisition/) sample for fundamentals
3. See the [LeaderElection](../LeaderElection/) sample for leadership patterns
4. Customize the inventory logic for your use case
5. Replace in-memory storage with Azure Table Storage or Cosmos DB

## Architecture Highlights

- **Per-Resource Leasing**: Each product has its own lease for concurrency
- **Auto-Renewal**: Background task keeps lease alive during long operations
- **Event-Driven**: Monitor lease health with events
- **Idempotent**: Safe concurrent operations across multiple instances
- **Production-Ready**: Retry logic, error handling, logging

Happy coding! ??
