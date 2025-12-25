# Azure Blob Lease Sample

This sample demonstrates how to use the **DistributedLeasing.Azure.Blob** provider to implement distributed locking and leader election using Azure Blob Storage leases.

## Overview

The sample showcases:

- ✅ **Dependency Injection** setup using Microsoft.Extensions.Hosting
- ✅ **Configuration binding** from appsettings.json
- ✅ **Automatic lease renewal** with configurable intervals
- ✅ **Event handling** for lease lifecycle (renewed, renewal failed, lost)
- ✅ **Graceful shutdown** with explicit lease release
- ✅ **Structured logging** for observability
- ✅ **Error handling** best practices

## Prerequisites

Before running this sample, you need:

1. **Azure Storage Account**
   - An active Azure subscription
   - A storage account with Blob Storage enabled
   - Note your storage account name (e.g., `mystorageaccount`)

2. **Authentication Setup** (choose one):
   - **Azure CLI**: Run `az login` for local development
   - **Managed Identity**: When running in Azure (VM, App Service, Container Apps, etc.)
   - **Service Principal**: Set environment variables for client credentials
   - **Connection String**: For development only (not recommended for production)

3. **Permissions**
   - Your identity needs the following Azure RBAC roles:
     - `Storage Blob Data Contributor` (to create containers and acquire leases)
     - Or `Storage Blob Data Owner` (for full control)

4. **.NET 8.0 SDK**
   - Download from https://dotnet.microsoft.com/download/dotnet/8.0

## Configuration

### Step 1: Update appsettings.json

Open `appsettings.json` and replace the placeholder with your actual storage account name:

```json
{
  "BlobLeasing": {
    "StorageAccountUri": "https://[YOUR_STORAGE_ACCOUNT].blob.core.windows.net",
    // ... other settings
  }
}
```

**Example:**
```json
"StorageAccountUri": "https://mystorageaccount.blob.core.windows.net"
```

### Step 2: Configure Authentication

The default configuration uses **DefaultAzureCredential**, which automatically tries multiple authentication methods in this order:

1. Environment variables (service principal)
2. Managed Identity
3. Azure CLI credentials
4. Visual Studio credentials
5. Azure PowerShell credentials

#### Option A: Azure CLI (Recommended for Local Development)

```bash
# Login to Azure
az login

# Set your subscription (if you have multiple)
az account set --subscription "Your Subscription Name"
```

#### Option B: Connection String (Development Only)

For local development, you can use a connection string instead:

1. Get your connection string from the Azure Portal:
   - Navigate to your Storage Account
   - Go to "Access keys"
   - Copy "Connection string" from key1 or key2

2. Update `appsettings.Development.json`:

```json
{
  "BlobLeasing": {
    "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=...;AccountKey=...;EndpointSuffix=core.windows.net",
    "StorageAccountUri": null
  }
}
```

**⚠️ Warning**: Never commit connection strings to source control. Use Azure Key Vault or user secrets for sensitive data.

#### Option C: Service Principal

Set environment variables:

```bash
export AZURE_TENANT_ID="your-tenant-id"
export AZURE_CLIENT_ID="your-client-id"
export AZURE_CLIENT_SECRET="your-client-secret"
```

#### Option D: Managed Identity

No configuration needed! When running in Azure (VM, App Service, etc.), Managed Identity is automatically used.

### Configuration Options Explained

| Setting | Default | Description |
|---------|---------|-------------|
| `StorageAccountUri` | - | Your Azure Storage account endpoint (required if not using connection string) |
| `ContainerName` | `"leases"` | Container where lease blobs are stored (created automatically if `CreateContainerIfNotExists` is true) |
| `CreateContainerIfNotExists` | `true` | Automatically create the container if it doesn't exist |
| `KeyPrefix` | `"sample-"` | Prefix for lease blob names (final name: `{prefix}{leaseName}`) |
| `DefaultLeaseDuration` | `"00:00:30"` | Initial lease duration (30 seconds). Azure Blob supports 15-60 seconds |
| `AutoRenew` | `true` | Enable automatic background renewal |
| `AutoRenewInterval` | `"00:00:20"` | How often to renew (should be ~2/3 of lease duration) |
| `AutoRenewRetryInterval` | `"00:00:05"` | Delay between retry attempts when renewal fails |
| `AutoRenewMaxRetries` | `3` | Maximum retry attempts before marking lease as lost |
| `AutoRenewSafetyThreshold` | `0.9` | Don't renew if past 90% of lease duration (safety buffer) |

## Running the Sample

### Option 1: Using .NET CLI

```bash
# Navigate to the sample directory
cd samples/BlobLeaseSample

# Run in Development mode
dotnet run --environment Development

# Or run in Production mode
dotnet run --environment Production
```

### Option 2: Using Visual Studio

1. Open `DistributedLeasing.sln` in Visual Studio
2. Set `BlobLeaseSample` as the startup project
3. Press F5 to run

### Option 3: Using VS Code

1. Open the sample folder in VS Code
2. Install the C# extension
3. Press F5 to run and debug

## What to Expect

When you run the sample, you should see output like this:

```
info: BlobLeaseSample.LeaseWorkerService[0]
      Blob Lease Sample Application starting...
info: BlobLeaseSample.LeaseWorkerService[0]
      Attempting to acquire lease for resource 'sample-resource'...
info: BlobLeaseSample.LeaseWorkerService[0]
      Successfully acquired lease! LeaseId: abc-123-def-456, AcquiredAt: 2024-01-15 10:30:00, ExpiresAt: 2024-01-15 10:30:30
info: BlobLeaseSample.LeaseWorkerService[0]
      Performing work while holding the lease...
info: BlobLeaseSample.LeaseWorkerService[0]
      The lease will be automatically renewed in the background.
info: BlobLeaseSample.LeaseWorkerService[0]
      Press Ctrl+C to stop and release the lease.
info: BlobLeaseSample.LeaseWorkerService[0]
      Lease renewed successfully! LeaseId: abc-123-def-456, NewExpiresAt: 2024-01-15 10:30:50, RenewalCount: 1
info: BlobLeaseSample.LeaseWorkerService[0]
      Still holding lease after 00:00:05. Renewal count: 1
...
```

### Testing Multiple Instances

To see distributed locking in action, run multiple instances simultaneously:

```bash
# Terminal 1
dotnet run

# Terminal 2 (in a new terminal)
dotnet run
```

**Expected behavior:**
- Instance 1 acquires the lease immediately
- Instance 2 waits for the lease to become available (or times out)
- When Instance 1 stops, Instance 2 can acquire the lease

## Understanding the Code

### Program.cs Structure

The sample follows a clean architecture with:

1. **Host Setup**: Uses Generic Host for dependency injection and lifecycle management
2. **Configuration**: Binds settings from appsettings.json
3. **Service Registration**: Registers `ILeaseManager` using `AddBlobLeaseManager()`
4. **Background Service**: `LeaseWorkerService` demonstrates lease usage

### Key Patterns Demonstrated

#### Acquiring a Lease

```csharp
// Non-blocking attempt (returns null if unavailable)
var lease = await _leaseManager.TryAcquireAsync("resource-name");

// Blocking attempt (waits until available or timeout)
var lease = await _leaseManager.AcquireAsync(
    "resource-name", 
    timeout: TimeSpan.FromSeconds(60));
```

#### Event Handling

```csharp
lease.LeaseRenewed += (sender, e) => 
{
    _logger.LogInformation("Renewed! Count: {Count}", e.RenewalCount);
};

lease.LeaseRenewalFailed += (sender, e) => 
{
    _logger.LogWarning("Renewal failed: {Error}", e.Exception?.Message);
};

lease.LeaseLost += (sender, e) => 
{
    _logger.LogError("Lease lost! Reason: {Reason}", e.Reason);
    // Stop any work that depends on the lease
};
```

#### Graceful Shutdown

```csharp
try
{
    // Do work while holding the lease
    await DoWorkAsync(lease);
}
finally
{
    // Always release the lease
    await lease.ReleaseAsync();
    await lease.DisposeAsync();
}
```

## Common Scenarios

### Leader Election

Use leases to elect a leader among multiple instances:

```csharp
var lease = await _leaseManager.TryAcquireAsync("leader-election");
if (lease != null)
{
    // This instance is the leader
    await PerformLeaderTasks(lease);
}
else
{
    // This instance is a follower
    await PerformFollowerTasks();
}
```

### Exclusive Resource Access

Ensure only one instance processes a resource at a time:

```csharp
var lease = await _leaseManager.AcquireAsync($"process-{resourceId}");
try
{
    await ProcessResourceExclusively(resourceId);
}
finally
{
    await lease.ReleaseAsync();
}
```

### Singleton Job Execution

Run a background job on only one instance:

```csharp
var lease = await _leaseManager.TryAcquireAsync("scheduled-job");
if (lease != null)
{
    try
    {
        await ExecuteScheduledJob();
    }
    finally
    {
        await lease.ReleaseAsync();
    }
}
```

## Troubleshooting

### "Authentication failed" or "Unauthorized"

**Cause**: Your identity doesn't have permission to access the storage account.

**Solutions**:
1. Verify you're logged in: `az login` and `az account show`
2. Check RBAC permissions in Azure Portal (Storage Account → Access Control → Role assignments)
3. Grant yourself `Storage Blob Data Contributor` role
4. Wait a few minutes for permission propagation

### "Container not found"

**Cause**: The container doesn't exist and `CreateContainerIfNotExists` is false.

**Solutions**:
1. Set `CreateContainerIfNotExists: true` in appsettings.json
2. Manually create the container in Azure Portal
3. Ensure your identity has permission to create containers

### "Lease could not be acquired"

**Cause**: Another instance is holding the lease.

**Solutions**:
1. This is expected behavior for distributed locking
2. Wait for the current lease to expire (check `DefaultLeaseDuration`)
3. Use `AcquireAsync()` with a timeout to wait automatically
4. Manually release the lease from Azure Storage Explorer (for debugging only)

### "Configuration binding failed"

**Cause**: Missing or invalid configuration in appsettings.json.

**Solutions**:
1. Verify JSON syntax is correct
2. Check that `StorageAccountUri` is set
3. Ensure section name matches: `"BlobLeasing"`
4. Review logs for specific validation errors

### High renewal failures

**Cause**: Network issues or renewal interval too close to lease expiration.

**Solutions**:
1. Increase `DefaultLeaseDuration` (e.g., to 60 seconds)
2. Adjust `AutoRenewInterval` to be ~2/3 of duration
3. Check network connectivity to Azure
4. Review `AutoRenewSafetyThreshold` setting

## Best Practices

1. **Always release leases**: Use `finally` blocks or `using` statements
2. **Handle lease loss**: Subscribe to `LeaseLost` event and stop work immediately
3. **Use appropriate durations**: 30-60 seconds for most scenarios
4. **Set renewal intervals**: Configure to ~2/3 of lease duration
5. **Monitor lease health**: Subscribe to all lifecycle events
6. **Test failure scenarios**: Simulate network issues, crashes, etc.
7. **Use Managed Identity**: Avoid connection strings in production
8. **Configure retries**: Set appropriate `AutoRenewMaxRetries` for your scenario

## Production Considerations

When deploying to production:

- ✅ Use **Managed Identity** or **Service Principal** authentication
- ✅ Store sensitive configuration in **Azure Key Vault**
- ✅ Enable **Application Insights** for telemetry and monitoring
- ✅ Set up **health checks** using the built-in `LeaseHealthCheck`
- ✅ Configure **retry policies** appropriate for your workload
- ✅ Use **separate containers** for different environments (dev/staging/prod)
- ✅ Monitor lease renewal metrics and set up alerts
- ✅ Test failover scenarios thoroughly

## Additional Resources

- [DistributedLeasing Documentation](../../README.md)
- [Azure Blob Storage Leases](https://docs.microsoft.com/azure/storage/blobs/storage-blob-lease)
- [DefaultAzureCredential](https://docs.microsoft.com/dotnet/api/azure.identity.defaultazurecredential)
- [Azure RBAC for Storage](https://docs.microsoft.com/azure/storage/blobs/authorize-access-azure-active-directory)

## Support

For issues or questions:

- File an issue on GitHub
- Check existing documentation
- Review Azure Storage logs in the portal

## License

This sample is part of the DistributedLeasing project and is licensed under the same terms.
