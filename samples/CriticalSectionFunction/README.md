# Critical Section Function Sample

This Azure Function demonstrates using the DistributedLeasing library for **critical section protection** with **automatic lease renewal** using Azure Blob Storage.

## Overview

This sample implements an inventory management system where multiple function instances need to coordinate access to shared inventory data. It demonstrates:

? **Critical Section Protection** - Only one instance can modify inventory at a time  
? **Auto-Renewal** - Leases automatically renew during long operations  
? **Per-Resource Leasing** - Each product has its own lease for concurrency  
? **Global Job Coordination** - Only one instance runs reconciliation  
? **Event Monitoring** - Tracks lease renewal, failures, and loss  

## Architecture

```
???????????????????????????????????????????????????????????????????
?                     Azure Function Instances                     ?
?  ????????????  ????????????  ????????????  ????????????       ?
?  ?Instance 1?  ?Instance 2?  ?Instance 3?  ?Instance 4?       ?
?  ????????????  ????????????  ????????????  ????????????       ?
??????????????????????????????????????????????????????????????????
         ?             ?             ?             ?
         ???????????????????????????????????????????
                        ?
                        ?
         ????????????????????????????????????
         ?    Distributed Lease Manager      ?
         ?    (Auto-Renewal Enabled)         ?
         ????????????????????????????????????
                        ?
                        ?
         ????????????????????????????????????
         ?     Azure Blob Storage            ?
         ?   Container: "leases"             ?
         ?  • lease-inventory-PROD-001       ?
         ?  • lease-inventory-PROD-002       ?
         ?  • lease-inventory-reconciliation ?
         ????????????????????????????????????
```

## Functions

### 1. **ReserveInventory** (HTTP Trigger)
- **Endpoint**: `POST /api/inventory/{productId}/reserve`
- **Lease**: Per-product (e.g., `inventory-PROD-001`)
- **Duration**: 30 seconds with auto-renewal
- **Purpose**: Atomically reserve inventory for a customer

**Request**:
```json
{
  "quantity": 5,
  "customerId": "CUST-123"
}
```

**Response (Success)**:
```json
{
  "success": true,
  "productId": "PROD-001",
  "requestedQuantity": 5,
  "availableQuantity": 95,
  "reservationId": "a1b2c3d4-...",
  "message": "Successfully reserved 5 units",
  "acquiredLease": true
}
```

### 2. **ReleaseInventory** (HTTP Trigger)
- **Endpoint**: `POST /api/inventory/{productId}/release`
- **Lease**: Per-product
- **Duration**: 15 seconds
- **Purpose**: Release previously reserved inventory

### 3. **GetInventory** (HTTP Trigger)
- **Endpoint**: `GET /api/inventory/{productId}`
- **Lease**: None (read-only)
- **Purpose**: Get current inventory status

### 4. **ReconcileInventory** (Timer Trigger)
- **Schedule**: Every 5 minutes
- **Lease**: Global (`inventory-reconciliation-job`)
- **Duration**: 5 minutes with auto-renewal
- **Purpose**: Clean up expired reservations (only one instance runs)

## Configuration

### Local Development (`local.settings.json`)

```json
{
  "Values": {
    "DistributedLeasing:ConnectionString": "UseDevelopmentStorage=true",
    "DistributedLeasing:AutoRenew": "true",
    "DistributedLeasing:DefaultLeaseDuration": "00:00:30",
    "DistributedLeasing:AutoRenewInterval": "00:00:20"
  }
}
```

### Azure Production (App Settings)

```bash
# Using Managed Identity (Recommended)
az functionapp config appsettings set --name MyFunctionApp \
  --resource-group MyResourceGroup \
  --settings \
  "DistributedLeasing:StorageAccountUri=https://mystorageaccount.blob.core.windows.net" \
  "DistributedLeasing:UseManagedIdentity=true" \
  "DistributedLeasing:ContainerName=leases" \
  "DistributedLeasing:AutoRenew=true" \
  "DistributedLeasing:DefaultLeaseDuration=00:00:30" \
  "DistributedLeasing:AutoRenewInterval=00:00:20"

# Assign Storage Blob Data Contributor role to function app
az role assignment create \
  --assignee $(az functionapp identity show --name MyFunctionApp --resource-group MyResourceGroup --query principalId -o tsv) \
  --role "Storage Blob Data Contributor" \
  --scope /subscriptions/{subscription-id}/resourceGroups/{resource-group}/providers/Microsoft.Storage/storageAccounts/{storage-account}
```

## Running Locally

### Prerequisites
1. [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
2. [Azure Functions Core Tools](https://docs.microsoft.com/azure/azure-functions/functions-run-local)
3. [Azurite](https://docs.microsoft.com/azure/storage/common/storage-use-azurite) (local storage emulator)

### Steps

```bash
# 1. Start Azurite (in separate terminal)
azurite --silent --location c:\azurite --debug c:\azurite\debug.log

# 2. Navigate to the sample
cd samples/CriticalSectionFunction

# 3. Run the function
func start
```

### Test with Multiple Instances

```bash
# Terminal 1
func start --port 7071

# Terminal 2
func start --port 7072

# Terminal 3
func start --port 7073
```

## Testing the Functions

### Reserve Inventory (Terminal/PowerShell)

```bash
# Reserve 5 units of PROD-001
curl -X POST http://localhost:7071/api/inventory/PROD-001/reserve \
  -H "Content-Type: application/json" \
  -d '{"quantity": 5, "customerId": "CUST-123"}'
```

### Multiple Concurrent Requests

```bash
# Simulate concurrent requests from different instances
for i in {1..5}; do
  curl -X POST http://localhost:7071/api/inventory/PROD-001/reserve \
    -H "Content-Type: application/json" \
    -d "{\"quantity\": 10, \"customerId\": \"CUST-$i\"}" &
done
```

### Check Inventory

```bash
curl http://localhost:7071/api/inventory/PROD-001
```

## Key Implementation Details

### 1. Auto-Renewal Configuration

```csharp
services.AddBlobLeaseManager(options =>
{
    options.AutoRenew = true;  // Enable auto-renewal
    options.DefaultLeaseDuration = TimeSpan.FromSeconds(30);
    options.AutoRenewInterval = TimeSpan.FromSeconds(20);  // Renew at 20s (2/3 of duration)
    options.AutoRenewMaxRetries = 3;  // Retry up to 3 times
});
```

### 2. Critical Section Pattern

```csharp
// Acquire lease for exclusive access
await using var lease = await _leaseManager.TryAcquireAsync(
    $"inventory-{productId}",
    duration: TimeSpan.FromSeconds(30));

if (lease != null)
{
    // Critical section - only one instance can execute this
    // The lease auto-renews every 20 seconds
    var inventory = GetInventory(productId);
    inventory.Reserve(quantity);
    SaveInventory(inventory);
}
// Lease auto-released on dispose
```

### 3. Event Monitoring

```csharp
lease.LeaseRenewed += (sender, args) =>
    _logger.LogDebug("Lease renewed at {Time}", args.Timestamp);

lease.LeaseRenewalFailed += (sender, args) =>
    _logger.LogWarning("Renewal failed (attempt {Attempt})", args.AttemptNumber);

lease.LeaseLost += (sender, args) =>
    _logger.LogError("Lease lost: {Reason}", args.Reason);
```

## Expected Behavior

### When Multiple Instances Run:

1. **First instance** acquires lease for PROD-001
2. **Second instance** attempts to acquire same lease ? returns `null`
3. **First instance** processes reservation (auto-renewing every 20s)
4. **First instance** completes and releases lease
5. **Second instance** can now acquire lease and process

### Logs Show:

```
[Instance 1] ? Acquired lease abc-123 for product PROD-001
[Instance 2] ? Could not acquire lease for product PROD-001. Another operation in progress.
[Instance 1] Lease abc-123 renewed. New expiration: 2025-01-19T14:45:30Z
[Instance 1] ? Successfully reserved 5 units. Renewal count: 2
[Instance 1] Releasing lease abc-123
[Instance 2] ? Acquired lease def-456 for product PROD-001
```

## Production Considerations

### 1. **Persistent Storage**
Replace in-memory `ConcurrentDictionary` with:
- Azure Table Storage
- Azure Cosmos DB
- Azure SQL Database

### 2. **Idempotency**
Add idempotency keys to prevent duplicate reservations

### 3. **Monitoring**
- Use Application Insights to track lease metrics
- Set up alerts for frequent lease losses
- Monitor renewal failure rates

### 4. **Lease Duration Tuning**
- Short operations (< 5s): 15-second lease
- Medium operations (5-30s): 30-second lease
- Long operations (> 30s): 60-second lease with auto-renewal

### 5. **Scaling**
- Each product gets its own lease (parallel processing)
- Use durable functions for complex workflows
- Consider partitioning by product category

## Troubleshooting

### Issue: "Could not acquire lease"
**Cause**: Another instance holds the lease  
**Solution**: This is normal - wait and retry, or use `AcquireAsync()` with timeout

### Issue: "Lease lost during operation"
**Cause**: Auto-renewal failed multiple times  
**Solution**: 
- Increase `AutoRenewMaxRetries`
- Check Azure Storage connectivity
- Increase `AutoRenewInterval` to renew sooner

### Issue: "Operation taking too long"
**Cause**: Operation exceeds lease duration  
**Solution**: 
- Enable `AutoRenew = true`
- Increase `DefaultLeaseDuration`
- Break operation into smaller chunks

## Learn More

- [Main README](../../README.md)
- [Basic Lease Acquisition Sample](../BasicLeaseAcquisition/)
- [Leader Election Sample](../LeaderElection/)
- [Azure Functions Documentation](https://docs.microsoft.com/azure/azure-functions/)
