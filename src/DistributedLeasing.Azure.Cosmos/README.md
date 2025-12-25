# DistributedLeasing.Azure.Cosmos

[![NuGet](https://img.shields.io/nuget/v/DistributedLeasing.Azure.Cosmos.svg)](https://www.nuget.org/packages/DistributedLeasing.Azure.Cosmos/)
[![Downloads](https://img.shields.io/nuget/dt/DistributedLeasing.Azure.Cosmos.svg)](https://www.nuget.org/packages/DistributedLeasing.Azure.Cosmos/)

**Azure Cosmos DB distributed leasing provider for .NET**

This package implements distributed leasing using Azure Cosmos DB's optimistic concurrency control with ETags. It provides globally-distributed lease coordination with strong consistency guarantees.

## Features

✅ **ETag-Based Optimistic Concurrency** - No pessimistic locks, high performance  
✅ **Global Distribution** - Multi-region active-active coordination  
✅ **Automatic Renewal** - Background renewal keeps leases alive  
✅ **Managed Identity Support** - First-class Azure authentication integration  
✅ **Strong Consistency** - Configurable consistency levels  
✅ **Flexible Storage** - Store leases alongside your application data

## When to Use Cosmos DB Leasing

**Best For:**
- Global multi-region deployments requiring coordination
- Applications already using Cosmos DB
- High availability scenarios with automatic failover
- Need for strong consistency guarantees
- Applications requiring lease metadata persistence

**Consider Alternatives When:**
- Need lowest possible latency (< 10ms) → Use [Azure Redis](https://www.nuget.org/packages/DistributedLeasing.Azure.Redis/)
- Single-region deployment with simple needs → Use [Azure Blob](https://www.nuget.org/packages/DistributedLeasing.Azure.Blob/)
- Cost is primary concern (Cosmos DB has higher cost per operation)

## Installation

```bash
dotnet add package DistributedLeasing.Azure.Cosmos
```

This automatically includes [DistributedLeasing.Abstractions](https://www.nuget.org/packages/DistributedLeasing.Abstractions/) with authentication and observability support.

## Quick Start

### Basic Usage with Managed Identity

```csharp
using DistributedLeasing.Azure.Cosmos;
using Azure.Identity;

// Create provider with managed identity
var provider = new CosmosLeaseProvider(new CosmosLeaseProviderOptions
{
    AccountEndpoint = "https://mycosmosaccount.documents.azure.com:443/",
    Credential = new DefaultAzureCredential(),
    DatabaseName = "LeaseDB",
    ContainerName = "Leases",
    CreateDatabaseIfNotExists = true,
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

### Using Existing Database and Container

```csharp
var provider = new CosmosLeaseProvider(new CosmosLeaseProviderOptions
{
    AccountEndpoint = "https://mycosmosaccount.documents.azure.com:443/",
    Credential = new DefaultAzureCredential(),
    DatabaseName = "MyApplicationDB",  // Existing database
    ContainerName = "Leases",           // Dedicated container for leases
    CreateContainerIfNotExists = true,  // Creates container if needed
    CreateDatabaseIfNotExists = false   // Don't create database
});
```

## Configuration

### Authentication Options

**Option 1: Managed Identity (Recommended for Azure)**

```csharp
using Azure.Identity;

var options = new CosmosLeaseProviderOptions
{
    AccountEndpoint = "https://mycosmosaccount.documents.azure.com:443/",
    Credential = new DefaultAzureCredential(),
    DatabaseName = "LeaseDB",
    ContainerName = "Leases"
};
```

**Option 2: Connection String (Development)**

```csharp
var options = new CosmosLeaseProviderOptions
{
    ConnectionString = "AccountEndpoint=https://...;AccountKey=...",
    DatabaseName = "LeaseDB",
    ContainerName = "Leases"
};
```

**Option 3: Account Key (Not Recommended for Production)**

```csharp
var options = new CosmosLeaseProviderOptions
{
    AccountEndpoint = "https://mycosmosaccount.documents.azure.com:443/",
    AccountKey = "your-account-key",
    DatabaseName = "LeaseDB",
    ContainerName = "Leases"
};
```

For comprehensive authentication configuration (including Service Principal, Workload Identity, etc.), see [Authentication Guide](https://github.com/pranshujawade/DistributedLeasing#authentication).

### Provider Options

```csharp
public class CosmosLeaseProviderOptions
{
    // Authentication (choose one approach)
    public string? AccountEndpoint { get; set; }
    public TokenCredential? Credential { get; set; }
    public string? AccountKey { get; set; }
    public string? ConnectionString { get; set; }

    // Container configuration
    public string? DatabaseName { get; set; }
    public string? ContainerName { get; set; }
    public bool CreateDatabaseIfNotExists { get; set; } = false;
    public bool CreateContainerIfNotExists { get; set; } = false;

    // Throughput (when creating container)
    public int? ThroughputRUs { get; set; } = 400;  // RU/s for new container

    // Consistency level
    public ConsistencyLevel ConsistencyLevel { get; set; } = ConsistencyLevel.Session;

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

var providerOptions = new CosmosLeaseProviderOptions
{
    AccountEndpoint = accountEndpoint,
    Credential = credential,
    DatabaseName = "LeaseDB",
    ContainerName = "Leases",
    LeaseOptions = leaseOptions
};
```

## How Cosmos DB Leasing Works

### Optimistic Concurrency with ETags

Unlike pessimistic locking (Azure Blob), Cosmos DB uses optimistic concurrency:

1. **Read**: Application reads lease document with current ETag
2. **Check**: Verify lease is available or expired
3. **Update**: Attempt to update with ETag constraint
4. **Conflict**: If ETag changed (someone else acquired), operation fails
5. **Retry**: Application can retry acquisition if desired

**Key Characteristics:**
- **No Server-Side Lock**: Cosmos DB doesn't hold locks
- **ETag Validation**: Ensures no concurrent modifications
- **High Performance**: No blocking, just conditional updates
- **Automatic Expiration**: Application manages expiration logic

### Lease Document Structure

Each lease is stored as a document in Cosmos DB:

```json
{
  "id": "lease-my-resource-lock",
  "leaseKey": "my-resource-lock",
  "leaseId": "abc123-def456-ghi789",
  "ownerId": "instance-01",
  "acquiredAt": "2025-12-25T12:00:00Z",
  "expiresAt": "2025-12-25T12:01:00Z",
  "metadata": {
    "instance": "server-01",
    "region": "us-east",
    "version": "1.0.0"
  },
  "_etag": "\"0000abc1-0000-0000-0000-000000000000\""
}
```

### Partition Key Strategy

By default, the lease provider uses `/leaseKey` as the partition key, allowing:
- Each lease to be independently distributed
- High throughput across many leases
- Efficient point reads and updates

## Performance Characteristics

### Latency

| Operation | Typical Latency | Notes |
|-----------|----------------|-------|
| Acquire (success) | 5-20ms | Single region, Session consistency |
| Acquire (success) | 20-100ms | Multi-region, Strong consistency |
| Acquire (failure) | 5-20ms | Fast conflict detection via ETag |
| Renew | 5-20ms | Point write operation |
| Release | 5-20ms | Document delete |

**Consistency Impact:** Strong consistency adds cross-region latency.

### Throughput

- **Request Units (RUs)**: Each operation consumes RUs
  - Acquire: ~5-10 RUs (read + conditional write)
  - Renew: ~5-10 RUs
  - Release: ~5 RUs
- **Provisioned Throughput**: Scale based on lease operations per second
- **Autoscale**: Consider autoscale for variable workloads

**Example Calculation:**
- 100 leases, each renewed every 40 seconds
- Renewals per second: 100 / 40 = 2.5
- RUs per second: 2.5 * 10 = 25 RU/s minimum

### Cost Considerations

Cosmos DB pricing is based on:
- **Provisioned RUs**: Pay per 100 RU/s per hour
- **Storage**: Pay per GB per month
- **Multi-region**: Multiplies cost by number of regions

**Cost Optimization:**
- Use shared throughput across containers
- Set appropriate TTL for expired leases
- Consider autoscale for variable workloads

## Best Practices

### 1. Use Dedicated Container for Leases

```csharp
// ✅ Good - separate container
var options = new CosmosLeaseProviderOptions
{
    DatabaseName = "MyApplicationDB",
    ContainerName = "Leases"  // Dedicated for leases
};

// ⚠️ Possible but not recommended - sharing with app data
var options = new CosmosLeaseProviderOptions
{
    DatabaseName = "MyApplicationDB",
    ContainerName = "ApplicationData"  // Mixed with app documents
};
```

### 2. Set Appropriate Consistency Level

```csharp
// ✅ For single-region or eventual consistency acceptable
var options = new CosmosLeaseProviderOptions
{
    ConsistencyLevel = ConsistencyLevel.Session  // Lower latency
};

// ✅ For multi-region with strong guarantees
var options = new CosmosLeaseProviderOptions
{
    ConsistencyLevel = ConsistencyLevel.Strong  // Higher latency, stronger guarantees
};
```

### 3. Provision Adequate RUs

```csharp
// ✅ Calculate based on expected load
var options = new CosmosLeaseProviderOptions
{
    ThroughputRUs = 400,  // Minimum for most scenarios
    CreateContainerIfNotExists = true
};

// For high-throughput scenarios, calculate:
// RUs = (leases * renewals_per_second * 10) + buffer
```

### 4. Handle ETag Conflicts Gracefully

```csharp
// ✅ Use TryAcquireAsync for non-blocking attempts
var lease = await leaseManager.TryAcquireAsync();

if (lease == null)
{
    // Expected - another instance holds it
    // No need to log as error
    return;
}
```

### 5. Always Release Leases

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
```

### 6. Set Time-to-Live (TTL) for Cleanup

Configure TTL on the container to automatically remove expired lease documents:

```csharp
// When creating container manually:
var containerProperties = new ContainerProperties
{
    Id = "Leases",
    PartitionKeyPath = "/leaseKey",
    DefaultTimeToLive = 3600  // 1 hour - cleanup old leases
};
```

## Troubleshooting

### "Database/Container does not exist"

**Problem:** Database or container not created.

**Solution:**
```csharp
var options = new CosmosLeaseProviderOptions
{
    DatabaseName = "LeaseDB",
    ContainerName = "Leases",
    CreateDatabaseIfNotExists = true,  // Enable auto-creation
    CreateContainerIfNotExists = true
};
```

### "Insufficient permissions"

**Problem:** Managed identity lacks permissions on Cosmos DB.

**Solution:**
1. Assign "Cosmos DB Built-in Data Contributor" role to the managed identity
2. Verify account endpoint is correct
3. Ensure firewall rules allow access

### Frequent ETag conflicts

**Problem:** Multiple instances competing for same lease (expected behavior).

**Solution:** This is normal for distributed locks. Use appropriate retry logic or `TryAcquireAsync()` for fail-fast behavior.

### High RU consumption

**Problem:** More RUs consumed than expected.

**Solution:**
- Increase lease duration to reduce renewal frequency
- Optimize auto-renewal interval
- Consider shared throughput across containers
- Use autoscale for variable workloads

```csharp
var leaseOptions = new LeaseOptions
{
    DefaultLeaseDuration = TimeSpan.FromMinutes(2),  // Longer duration
    AutoRenewInterval = TimeSpan.FromSeconds(80)     // Less frequent renewals
};
```

### Lease not released after application crash

**Problem:** Application terminated before releasing lease.

**Solution:** This is expected - the lease document remains but expires based on `expiresAt`. Next acquisition attempt will detect expiration and reacquire. Set appropriate lease duration to balance responsiveness and overhead.

## Container Setup

### Manual Container Creation

If you prefer to create the container manually:

```bash
# Using Azure CLI
az cosmosdb sql container create \
  --account-name mycosmosaccount \
  --database-name LeaseDB \
  --name Leases \
  --partition-key-path "/leaseKey" \
  --throughput 400 \
  --ttl 3600
```

### Partition Key Best Practices

The default `/leaseKey` partition key provides:
- Even distribution of leases across partitions
- Efficient point reads and writes
- Independent throughput per lease

**Alternative:** If you have related leases, consider grouping:
```csharp
// Partition by application or service
var leaseKey = $"{serviceName}/{resourceName}";
```

## Multi-Region Deployment

### Configuration for Multi-Region

```csharp
var options = new CosmosLeaseProviderOptions
{
    AccountEndpoint = "https://mycosmosaccount.documents.azure.com:443/",
    Credential = new DefaultAzureCredential(),
    DatabaseName = "LeaseDB",
    ContainerName = "Leases",
    ConsistencyLevel = ConsistencyLevel.Strong  // Strong consistency for multi-region
};
```

### Write Region Selection

Cosmos DB automatically routes writes to the nearest writable region. No additional configuration needed for basic scenarios.

### Conflict Resolution

With Strong consistency, conflicts are resolved by Cosmos DB automatically through last-write-wins based on timestamp.

## Monitoring and Observability

### Query Lease Documents

```csharp
// Using Cosmos DB SDK directly
var container = cosmosClient.GetContainer("LeaseDB", "Leases");

var query = container.GetItemQueryIterator<LeaseDocument>(
    "SELECT * FROM c WHERE c.expiresAt > GetCurrentDateTime()");

while (query.HasMoreResults)
{
    var response = await query.ReadNextAsync();
    foreach (var lease in response)
    {
        Console.WriteLine($"Active lease: {lease.LeaseKey} held by {lease.OwnerId}");
    }
}
```

### Azure Portal Inspection

1. Navigate to Azure Portal → Cosmos DB account
2. Select "Data Explorer"
3. Expand database → container → Items
4. View lease documents in JSON format
5. Check `_etag`, `expiresAt`, and `metadata` fields

### Metrics and Health Checks

See [DistributedLeasing.Abstractions README](../DistributedLeasing.Abstractions/README.md#observability-integration) for:
- OpenTelemetry metrics configuration
- Health check setup
- Distributed tracing integration

## Samples

For comprehensive examples with real-world scenarios, see:

- [CosmosLeaseSample](../../samples/CosmosLeaseSample/README.md) - Complete distributed lock competition demo
  - Multiple instance competition
  - ETag conflict handling
  - Multi-region scenarios
  - Troubleshooting guide

## Architecture

```
┌─────────────────────────────────────────────────────┐
│ Your Application                                     │
│ ┌──────────────┐         ┌──────────────────┐      │
│ │ILeaseProvider│────────▶│CosmosLeaseProvider│      │
│ └──────────────┘         └──────────────────┘      │
│         │                                            │
│         │ CreateLeaseManagerAsync("my-lock")        │
│         ▼                                            │
│ ┌──────────────┐         ┌───────────────────┐     │
│ │ILeaseManager │────────▶│CosmosLeaseManager │     │
│ └──────────────┘         └───────────────────┘     │
│         │                                            │
│         │ AcquireAsync()                            │
│         ▼                                            │
│ ┌──────────────┐         ┌─────────────┐           │
│ │   ILease     │────────▶│ CosmosLease │           │
│ └──────────────┘         └─────────────┘           │
└─────────────────────────────────────────────────────┘
                    │
                    │ Cosmos DB SQL API (ETag-based)
                    ▼
┌─────────────────────────────────────────────────────┐
│ Azure Cosmos DB                                     │
│ ┌─────────────────────────────────────────────┐    │
│ │ Database: LeaseDB                            │    │
│ │ ┌─────────────────────────────────────┐     │    │
│ │ │ Container: Leases (/leaseKey)        │     │    │
│ │ │ ┌─────────────────────────────────┐ │     │    │
│ │ │ │ Document: lease-my-lock          │ │     │    │
│ │ │ │ • leaseId: abc123...             │ │     │    │
│ │ │ │ • ownerId: instance-01           │ │     │    │
│ │ │ │ • expiresAt: 2025-12-25T12:01:00│ │     │    │
│ │ │ │ • _etag: "0000abc1..."           │ │     │    │
│ │ │ │ • metadata: {...}                │ │     │    │
│ │ │ └─────────────────────────────────┘ │     │    │
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
- **Microsoft.Azure.Cosmos** - Azure Cosmos DB SDK
- **Azure.Identity** - Azure authentication
- **Newtonsoft.Json** - JSON serialization

## Related Packages

- [DistributedLeasing.Azure.Blob](https://www.nuget.org/packages/DistributedLeasing.Azure.Blob/) - Blob Storage provider (pessimistic locking)
- [DistributedLeasing.Azure.Redis](https://www.nuget.org/packages/DistributedLeasing.Azure.Redis/) - Redis provider (low latency)
- [DistributedLeasing.ChaosEngineering](https://www.nuget.org/packages/DistributedLeasing.ChaosEngineering/) - Testing utilities

## Documentation

- [GitHub Repository](https://github.com/pranshujawade/DistributedLeasing)
- [Authentication Guide](https://github.com/pranshujawade/DistributedLeasing#authentication)
- [Observability Integration](../DistributedLeasing.Abstractions/README.md#observability-integration)

## License

MIT License - see [LICENSE](../../LICENSE) for details.
