# DistributedLeasing

A flexible, production-ready distributed leasing library for .NET that enables distributed coordination, leader election, and exclusive resource access across multiple instances.

## Features

- **Multiple Azure Providers**: Support for Azure Blob Storage, Azure Cosmos DB, and Azure Redis
- **Automatic Lease Renewal**: Built-in mechanisms to maintain leases automatically
- **Leader Election**: Simple, reliable leader election for distributed systems
- **Comprehensive Authentication**: Support for Managed Identity, Service Principal, Workload Identity, and development credentials
- **Observability Built-in**: OpenTelemetry metrics, distributed tracing, and health checks
- **Event-Driven**: Lease lifecycle events for monitoring and integration
- **Multi-Framework Support**: Compatible with .NET Framework 4.6.1+, .NET Core 2.0+, .NET 8.0, and .NET 10.0

## Quick Start

### Installation

Install the provider package you need:

```bash
# For Azure Blob Storage (cloud-native, reliable)
dotnet add package DistributedLeasing.Azure.Blob

# For Azure Cosmos DB (global distribution, multi-region)
dotnet add package DistributedLeasing.Azure.Cosmos

# For Azure Redis (lowest latency, high throughput)
dotnet add package DistributedLeasing.Azure.Redis

# For testing and chaos engineering
dotnet add package DistributedLeasing.ChaosEngineering
```

Each provider package automatically includes the core `DistributedLeasing.Abstractions` package with authentication and observability support.

### Basic Usage - Azure Blob Storage

```csharp
using DistributedLeasing.Azure.Blob;
using Azure.Identity;

// Create a blob lease provider
var provider = new BlobLeaseProvider(new BlobLeaseProviderOptions
{
    ContainerUri = new Uri("https://youraccount.blob.core.windows.net/leases"),
    Credential = new DefaultAzureCredential()
});

// Create a lease manager for a specific resource
var leaseManager = await provider.CreateLeaseManagerAsync("my-resource");

// Acquire a lease
var lease = await leaseManager.AcquireAsync(TimeSpan.FromSeconds(60));

if (lease != null)
{
    try
    {
        // Do work while holding the lease
        Console.WriteLine("Lease acquired! Doing critical work...");
        
        // Lease auto-renews in the background
        await Task.Delay(TimeSpan.FromMinutes(5));
    }
    finally
    {
        // Always release the lease when done
        await lease.ReleaseAsync();
    }
}
```

### With Managed Identity Authentication

```csharp
using DistributedLeasing.Azure.Blob;
using Azure.Identity;

// Configure authentication in appsettings.json
// {
//   "Authentication": {
//     "Mode": "Auto"  // or "ManagedIdentity", "WorkloadIdentity", etc.
//   }
// }

var provider = new BlobLeaseProvider(new BlobLeaseProviderOptions
{
    ContainerUri = new Uri("https://youraccount.blob.core.windows.net/leases"),
    Credential = new DefaultAzureCredential(),  // Automatic credential chain
    CreateContainerIfNotExists = true
});

var leaseManager = await provider.CreateLeaseManagerAsync("my-resource");
var lease = await leaseManager.AcquireAsync(TimeSpan.FromSeconds(60));

if (lease != null)
{
    try
    {
        // Your critical work here
        await DoExclusiveWorkAsync();
    }
    finally
    {
        await lease.ReleaseAsync();
    }
}
```

## Package Structure

This library follows a clean package structure:

- **[DistributedLeasing.Abstractions](https://www.nuget.org/packages/DistributedLeasing.Abstractions/)**: Core contracts, base implementations, authentication, events, observability
- **[DistributedLeasing.Azure.Blob](https://www.nuget.org/packages/DistributedLeasing.Azure.Blob/)**: Azure Blob Storage provider (native leases, reliable)
- **[DistributedLeasing.Azure.Cosmos](https://www.nuget.org/packages/DistributedLeasing.Azure.Cosmos/)**: Azure Cosmos DB provider (ETag-based, global distribution)  
- **[DistributedLeasing.Azure.Redis](https://www.nuget.org/packages/DistributedLeasing.Azure.Redis/)**: Azure Redis provider (Redlock algorithm, ultra-low latency)
- **[DistributedLeasing.ChaosEngineering](https://www.nuget.org/packages/DistributedLeasing.ChaosEngineering/)**: Testing utilities (chaos injection, resilience testing)

When you install a provider package, all required dependencies (including Abstractions) are automatically installed.

## Use Cases

### Leader Election

Ensure only one instance performs a task:

```csharp
var manager = await provider.CreateLeaseManagerAsync("leader");
var lease = await manager.AcquireAsync(TimeSpan.FromSeconds(60));

if (lease != null)
{
    // This instance is the leader
    await PerformLeaderTasksAsync();
}
```

### Critical Section Protection

Protect shared resources from concurrent access:

```csharp
var manager = await provider.CreateLeaseManagerAsync("database-migration");
var lease = await manager.AcquireAsync(TimeSpan.FromMinutes(10));

if (lease != null)
{
    try
    {
        await RunDatabaseMigrationAsync();
    }
    finally
    {
        await lease.ReleaseAsync();
    }
}
```

### Scheduled Job Coordination

Prevent multiple instances from running the same scheduled job:

```csharp
public async Task ExecuteScheduledJobAsync()
{
    var manager = await _provider.CreateLeaseManagerAsync("daily-report-job");
    var lease = await manager.AcquireAsync(TimeSpan.FromMinutes(30));
    
    if (lease != null)
    {
        try
        {
            await GenerateDailyReportAsync();
        }
        finally
        {
            await lease.ReleaseAsync();
        }
    }
}
```

## Authentication

All Azure providers support unified authentication through the Abstractions package. Configure once, use everywhere.

### Supported Authentication Modes

- **Auto** - Automatic credential chain (recommended)
- **ManagedIdentity** - System-assigned or user-assigned managed identity
- **WorkloadIdentity** - Kubernetes/GitHub Actions OIDC
- **ServicePrincipal** - Certificate or client secret
- **FederatedCredential** - External OIDC token exchange
- **Development** - Azure CLI, Visual Studio, VS Code

### Configuration Example

```json
{
  "Authentication": {
    "Mode": "Auto"
  }
}
```

For detailed authentication configuration, see the [Abstractions package documentation](https://www.nuget.org/packages/DistributedLeasing.Abstractions/).

## Observability

Built-in support for monitoring and observability:

### Health Checks

```csharp
services.AddHealthChecks()
    .AddCheck<LeaseHealthCheck>("lease-health");
```

### OpenTelemetry Metrics

```csharp
services.AddOpenTelemetry()
    .WithMetrics(builder => builder.AddMeter(LeasingMetrics.MeterName));
```

**Available Metrics**:
- `leasing.acquire.duration` - Lease acquisition time
- `leasing.acquire.success` - Successful acquisitions
- `leasing.renewal.success` - Successful renewals
- `leasing.active.count` - Active leases

### Distributed Tracing

```csharp
services.AddOpenTelemetry()
    .WithTracing(builder => builder.AddSource(LeasingActivitySource.SourceName));
```

## Event System

Monitor lease lifecycle with events:

```csharp
lease.LeaseRenewed += (sender, e) =>
{
    Console.WriteLine($"Lease renewed. New expiration: {e.NewExpiration}");
};

lease.LeaseLost += (sender, e) =>
{
    Console.WriteLine($"Lease lost! Reason: {e.Reason}");
    // Trigger graceful shutdown
};
```

## Provider Comparison

| Feature | Azure Blob | Azure Cosmos | Azure Redis |
|---------|------------|--------------|-------------|
| **Latency** | 50-150ms | 5-100ms | 1-5ms |
| **Mechanism** | Native leases | ETag optimistic | Redlock algorithm |
| **Global Distribution** | Single region | Multi-region | Single region |
| **Cost** | Low | Medium-High | Medium |
| **Best For** | Reliability | Global apps | Low latency |

## Documentation

For comprehensive documentation, samples, and advanced scenarios:

- [GitHub Repository](https://github.com/pranshujawade/DistributedLeasing)
- [Abstractions Package](https://www.nuget.org/packages/DistributedLeasing.Abstractions/) - Core framework and authentication
- [Blob Provider](https://www.nuget.org/packages/DistributedLeasing.Azure.Blob/) - Azure Blob Storage
- [Cosmos Provider](https://www.nuget.org/packages/DistributedLeasing.Azure.Cosmos/) - Azure Cosmos DB
- [Redis Provider](https://www.nuget.org/packages/DistributedLeasing.Azure.Redis/) - Azure Redis
- [BlobLeaseSample](samples/BlobLeaseSample/README.md) - Complete working example
- [CosmosLeaseSample](samples/CosmosLeaseSample/README.md) - Cosmos DB examples

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Contributing

Contributions are welcome! Please see our contributing guidelines for more information.
