# DistributedLeasing.Azure.Blob

[![NuGet](https://img.shields.io/nuget/v/DistributedLeasing.Azure.Blob.svg)](https://www.nuget.org/packages/DistributedLeasing.Azure.Blob/)
[![Downloads](https://img.shields.io/nuget/dt/DistributedLeasing.Azure.Blob.svg)](https://www.nuget.org/packages/DistributedLeasing.Azure.Blob/)

**Azure Blob Storage distributed leasing provider for .NET**

This package implements distributed leasing using native Azure Blob Storage lease capabilities. It leverages Azure's built-in pessimistic locking mechanism for reliable, cloud-native distributed coordination.

## Features

✅ **Native Azure Blob Leases** - Uses Azure's built-in lease mechanism (no polling)  
✅ **Automatic Renewal** - Background renewal keeps leases alive  
✅ **Managed Identity Support** - First-class Azure authentication integration  
✅ **Metadata Storage** - Store custom metadata with each lease  
✅ **High Reliability** - Azure-guaranteed consistency and durability  
✅ **Simple Setup** - Container auto-creation, minimal configuration

## When to Use Azure Blob Leasing

**Best For:**
- Leader election in Azure-hosted applications
- Distributed lock coordination with moderate throughput
- Long-running exclusive processes (minutes to hours)
- Applications already using Azure Storage
- Multi-region deployments with single-region coordination

**Consider Alternatives When:**
- Need sub-second latency → Use [Azure Redis](https://www.nuget.org/packages/DistributedLeasing.Azure.Redis/)
- Need global distribution → Use [Azure Cosmos DB](https://www.nuget.org/packages/DistributedLeasing.Azure.Cosmos/)
- Need extremely high throughput (>1000 ops/sec per lease)

## Installation

```bash
dotnet add package DistributedLeasing.Azure.Blob
```

This automatically includes [DistributedLeasing.Abstractions](https://www.nuget.org/packages/DistributedLeasing.Abstractions/) with authentication and observability support.

## Quick Start

### Basic Usage with Managed Identity

```csharp
using DistributedLeasing.Azure.Blob;
using Azure.Identity;

// Create provider with managed identity
var provider = new BlobLeaseProvider(new BlobLeaseProviderOptions
{
    ContainerUri = new Uri("https://mystorageaccount.blob.core.windows.net/leases"),
    Credential = new DefaultAzureCredential(),
    CreateContainerIfNotExists = true
});

// Create lease manager for a specific resource
var leaseManager = await provider.CreateLeaseManagerAsync("my-resource-lock");

// Acquire lease
var lease = await leaseManager.AcquireAsync(TimeSpan.FromSeconds(60));

if (lease != null)
{
    try
    {
        // Do work while holding the lease
        Console.WriteLine($"Lease acquired: {lease.LeaseId}");
        await DoExclusiveWorkAsync();
    }
    finally
    {
        // Always release when done
        await lease.ReleaseAsync();
    }
}
else
{
    Console.WriteLine("Could not acquire lease - another instance holds it");
}
```

### Non-Blocking Acquisition

```csharp
// Try to acquire without waiting
var lease = await leaseManager.TryAcquireAsync(TimeSpan.FromSeconds(60));

if (lease == null)
{
    // Lease is held by another instance - fail fast
    Console.WriteLine("Lease unavailable");
    return;
}

// Proceed with work
```

### With Custom Metadata

```csharp
var options = new BlobLeaseProviderOptions
{
    ContainerUri = new Uri("https://mystorageaccount.blob.core.windows.net/leases"),
    Credential = new DefaultAzureCredential(),
    Metadata = new Dictionary<string, string>
    {
        ["instance"] = Environment.MachineName,
        ["version"] = "1.2.3",
        ["region"] = "us-east-1"
    }
};

var provider = new BlobLeaseProvider(options);
```

Metadata is automatically prefixed with `lease_` and stored with the blob. Useful for debugging and monitoring.

## Configuration

### Authentication Options

**Option 1: Managed Identity (Recommended for Azure)**

```csharp
using Azure.Identity;

var options = new BlobLeaseProviderOptions
{
    ContainerUri = new Uri("https://mystorageaccount.blob.core.windows.net/leases"),
    Credential = new DefaultAzureCredential()
};
```

**Option 2: Connection String (Development)**

```csharp
var options = new BlobLeaseProviderOptions
{
    ConnectionString = "DefaultEndpointsProtocol=https;AccountName=...",
    ContainerName = "leases"
};
```

**Option 3: Specific Managed Identity**

```csharp
var options = new BlobLeaseProviderOptions
{
    ContainerUri = new Uri("https://mystorageaccount.blob.core.windows.net/leases"),
    Credential = new ManagedIdentityCredential("client-id-of-user-assigned-identity")
};
```

For comprehensive authentication configuration (including Service Principal, Workload Identity, etc.), see [Authentication Guide](https://github.com/pranshujawade/DistributedLeasing#authentication).

### Provider Options

```csharp
public class BlobLeaseProviderOptions
{
    // Authentication (choose one)
    public Uri? ContainerUri { get; set; }
    public TokenCredential? Credential { get; set; }
    public string? ConnectionString { get; set; }
    public string? ContainerName { get; set; }

    // Container behavior
    public bool CreateContainerIfNotExists { get; set; } = true;

    // Lease configuration
    public LeaseOptions? LeaseOptions { get; set; }

    // Custom metadata
    public IDictionary<string, string>? Metadata { get; set; }
}
```

### Lease Options

```csharp
var leaseOptions = new LeaseOptions
{
    DefaultLeaseDuration = TimeSpan.FromSeconds(60),
    AutoRenew = true,
    AutoRenewInterval = TimeSpan.FromSeconds(40),
    MaxRetryAttempts = 3,
    RetryDelay = TimeSpan.FromSeconds(2)
};

var providerOptions = new BlobLeaseProviderOptions
{
    ContainerUri = containerUri,
    Credential = credential,
    LeaseOptions = leaseOptions
};
```

## How Azure Blob Leasing Works

### Blob Lease Mechanism

Azure Blob Storage provides native pessimistic locking through the lease API:

1. **Acquire**: Application requests a lease on a blob for a specified duration (15-60 seconds)
2. **Lock**: Azure grants an exclusive lease ID to the requester
3. **Renew**: Lease holder periodically renews before expiration
4. **Release**: Lease holder explicitly releases, or Azure auto-expires

**Key Characteristics:**
- **Pessimistic Lock**: Only one lease holder at a time
- **Automatic Expiration**: Azure guarantees lease expires if not renewed
- **No Polling**: Lease state is authoritative server-side
- **High Reliability**: Built on Azure Storage's consistency guarantees

### Blob Naming Convention

Leases are stored as blobs with the naming pattern:
```
lease-{leaseKey}
```

Example:
- Lease key: `database-migration`
- Blob name: `lease-database-migration`

### Metadata Storage

Each lease blob stores metadata automatically:

| Metadata Key | Description | Example |
|--------------|-------------|---------|
| `leaseName` | Original lease key | `database-migration` |
| `createdAt` | Blob creation time | `2025-12-25T12:00:00Z` |
| `lastModified` | Last metadata update | `2025-12-25T12:05:30Z` |
| `lease_*` | Custom user metadata | `lease_instance: server-01` |

User-provided metadata is prefixed with `lease_` to avoid conflicts.

## Performance Characteristics

### Latency

| Operation | Typical Latency | Notes |
|-----------|----------------|-------|
| Acquire (success) | 50-150ms | Single HTTP request to Azure |
| Acquire (failure) | 50-150ms | Fast fail if unavailable |
| Renew | 50-150ms | Background operation |
| Release | 50-150ms | Single HTTP request |

**Network Dependency:** Latency varies by region and network conditions.

### Throughput

- **Single Lease**: ~100-200 operations/second (acquire/renew/release combined)
- **Multiple Leases**: Scales linearly (each lease is independent blob)
- **Concurrent Acquisitions**: Azure serializes requests per blob

**Best Practice:** For high-throughput scenarios (>1000 ops/sec), use Azure Redis instead.

### Lease Duration Limits

- **Minimum**: 15 seconds
- **Maximum**: 60 seconds
- **Recommended**: 30-60 seconds with renewal at 2/3 interval

## Best Practices

### 1. Use Managed Identity in Production

```csharp
// ✅ Good
var options = new BlobLeaseProviderOptions
{
    ContainerUri = new Uri("https://mystorageaccount.blob.core.windows.net/leases"),
    Credential = new DefaultAzureCredential()
};

// ❌ Avoid in production
var options = new BlobLeaseProviderOptions
{
    ConnectionString = "DefaultEndpointsProtocol=https;AccountName=..."
};
```

### 2. Always Release Leases

```csharp
// ✅ Good
var lease = await leaseManager.AcquireAsync();
try
{
    await DoWorkAsync();
}
finally
{
    await lease.ReleaseAsync(); // Always release
}

// ❌ Risky - lease may not be released
var lease = await leaseManager.AcquireAsync();
await DoWorkAsync();
```

### 3. Handle Lease Loss

```csharp
var cts = new CancellationTokenSource();

lease.LeaseLost += (sender, e) =>
{
    Console.WriteLine($"Lease lost: {e.Reason}");
    cts.Cancel(); // Stop work immediately
};

try
{
    await LongRunningWorkAsync(cts.Token);
}
finally
{
    await lease.ReleaseAsync();
}
```

### 4. Use Appropriate Lease Duration

```csharp
// ✅ For short tasks (seconds to minutes)
var lease = await leaseManager.AcquireAsync(TimeSpan.FromSeconds(30));

// ✅ For longer tasks (minutes to hours)
// Auto-renewal keeps it alive
var lease = await leaseManager.AcquireAsync(TimeSpan.FromSeconds(60));
```

### 5. Add Metadata for Debugging

```csharp
var options = new BlobLeaseProviderOptions
{
    ContainerUri = containerUri,
    Credential = credential,
    Metadata = new Dictionary<string, string>
    {
        ["instance"] = Environment.MachineName,
        ["process"] = Process.GetCurrentProcess().Id.ToString(),
        ["started"] = DateTime.UtcNow.ToString("o")
    }
};
```

## Troubleshooting

### "Container does not exist"

**Problem:** Container not created or wrong name.

**Solution:**
```csharp
var options = new BlobLeaseProviderOptions
{
    ContainerUri = containerUri,
    Credential = credential,
    CreateContainerIfNotExists = true // Enable auto-creation
};
```

### "Authentication failed"

**Problem:** Managed identity not configured or insufficient permissions.

**Solution:**
1. Ensure managed identity is enabled on the Azure resource
2. Assign "Storage Blob Data Contributor" role to the identity
3. Verify container URI is correct

### "Lease already exists" on acquire

**Problem:** Another instance holds the lease (expected behavior).

**Solution:** This is not an error - it's the distributed lock working correctly. Use `TryAcquireAsync()` for non-blocking behavior.

### Frequent renewal failures

**Problem:** Network latency or lease duration too short.

**Solution:**
```csharp
var leaseOptions = new LeaseOptions
{
    DefaultLeaseDuration = TimeSpan.FromSeconds(60), // Increase duration
    AutoRenewInterval = TimeSpan.FromSeconds(40)     // 2/3 of duration
};
```

### Lease not released after application crash

**Problem:** Application terminated before releasing lease.

**Solution:** This is expected - Azure automatically expires the lease after the duration. Wait for expiration (max 60 seconds) before next acquisition.

## Monitoring and Observability

### Inspect Lease State with Azure CLI

```bash
# List all lease blobs
az storage blob list \
  --account-name mystorageaccount \
  --container-name leases \
  --output table

# Show lease metadata
az storage blob metadata show \
  --account-name mystorageaccount \
  --container-name leases \
  --name lease-my-resource-lock

# Check lease status
az storage blob show \
  --account-name mystorageaccount \
  --container-name leases \
  --name lease-my-resource-lock \
  --query "properties.lease"
```

### Metrics and Health Checks

See [DistributedLeasing.Abstractions README](../DistributedLeasing.Abstractions/README.md#observability-integration) for:
- OpenTelemetry metrics configuration
- Health check setup
- Distributed tracing integration

## Samples

For comprehensive examples with real-world scenarios, see:

- [BlobLeaseSample](../../samples/BlobLeaseSample/README.md) - Complete distributed lock competition demo
  - Multiple instance competition
  - Automatic renewal examples
  - Metadata inspection
  - Troubleshooting scenarios

## Architecture

```
┌─────────────────────────────────────────────────────┐
│ Your Application                                     │
│ ┌──────────────┐         ┌─────────────────┐       │
│ │ ILeaseProvider│────────▶│ BlobLeaseProvider│       │
│ └──────────────┘         └─────────────────┘       │
│         │                                            │
│         │ CreateLeaseManagerAsync("my-lock")        │
│         ▼                                            │
│ ┌──────────────┐         ┌──────────────────┐      │
│ │ILeaseManager │────────▶│ BlobLeaseManager │      │
│ └──────────────┘         └──────────────────┘      │
│         │                                            │
│         │ AcquireAsync()                            │
│         ▼                                            │
│ ┌──────────────┐         ┌──────────────┐          │
│ │   ILease     │────────▶│  BlobLease   │          │
│ └──────────────┘         └──────────────┘          │
└─────────────────────────────────────────────────────┘
                    │
                    │ Azure Blob Storage API
                    ▼
┌─────────────────────────────────────────────────────┐
│ Azure Blob Storage                                  │
│ ┌─────────────────────────────────────────────┐    │
│ │ Container: leases                            │    │
│ │ ┌─────────────────────────────────────┐     │    │
│ │ │ Blob: lease-my-lock                  │     │    │
│ │ │ • Lease ID: abc123...                │     │    │
│ │ │ • State: Leased                       │     │    │
│ │ │ • Duration: 60s                       │     │    │
│ │ │ • Metadata:                           │     │    │
│ │ │   - lease_instance: server-01         │     │    │
│ │ │   - lease_region: us-east             │     │    │
│ │ └─────────────────────────────────────┘     │    │
│ └─────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────┘
```

## Framework Compatibility

- **.NET Standard 2.0** - Compatible with .NET Framework 4.6.1+, .NET Core 2.0+
- **.NET 8.0** - Long-term support release
- **.NET 10.0** - Latest release

## Package Dependencies

- [DistributedLeasing.Abstractions](https://www.nuget.org/packages/DistributedLeasing.Abstractions/) - Core framework
- **Azure.Storage.Blobs** - Azure Blob Storage SDK
- **Azure.Identity** - Azure authentication

## Related Packages

- [DistributedLeasing.Azure.Cosmos](https://www.nuget.org/packages/DistributedLeasing.Azure.Cosmos/) - Cosmos DB provider (global distribution)
- [DistributedLeasing.Azure.Redis](https://www.nuget.org/packages/DistributedLeasing.Azure.Redis/) - Redis provider (low latency)
- [DistributedLeasing.ChaosEngineering](https://www.nuget.org/packages/DistributedLeasing.ChaosEngineering/) - Testing utilities

## Documentation

- [GitHub Repository](https://github.com/pranshujawade/DistributedLeasing)
- [Authentication Guide](https://github.com/pranshujawade/DistributedLeasing#authentication)
- [Observability Integration](../DistributedLeasing.Abstractions/README.md#observability-integration)

## License

MIT License - see [LICENSE](../../LICENSE) for details.
