# DistributedLeasing

A flexible, production-ready distributed leasing library for .NET that enables distributed coordination, leader election, and exclusive resource access across multiple instances.

## Features

- **Multiple Azure Providers**: Support for Azure Blob Storage, Azure Cosmos DB, and Azure Redis
- **Automatic Lease Renewal**: Built-in mechanisms to maintain leases automatically
- **Leader Election**: Simple, reliable leader election for distributed systems
- **Managed Identity Support**: First-class support for Azure Managed Identity authentication
- **Dependency Injection**: ASP.NET Core integration with familiar service registration patterns
- **Multi-Framework Support**: Compatible with .NET Framework 4.6.1+, .NET Core 2.0+, .NET 8.0, and .NET 10.0

## Quick Start

### Installation

Install the provider package you need. Dependencies are installed automatically:

```bash
# For Azure Blob Storage
dotnet add package DistributedLeasing.Azure.Blob

# For Azure Cosmos DB
dotnet add package DistributedLeasing.Azure.Cosmos

# For Azure Redis
dotnet add package DistributedLeasing.Azure.Redis

# For ASP.NET Core with Dependency Injection (includes all providers)
dotnet add package DistributedLeasing.Extensions.DependencyInjection
```

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

### ASP.NET Core with Dependency Injection

```csharp
using DistributedLeasing.Extensions.DependencyInjection;

// In Program.cs or Startup.cs
builder.Services.AddDistributedLeasing(options =>
{
    options.UseBlobStorage(blobOptions =>
    {
        blobOptions.ContainerUri = new Uri("https://youraccount.blob.core.windows.net/leases");
        blobOptions.Credential = new DefaultAzureCredential();
    });
});

// In your service
public class MyService
{
    private readonly ILeaseProvider _leaseProvider;
    
    public MyService(ILeaseProvider leaseProvider)
    {
        _leaseProvider = leaseProvider;
    }
    
    public async Task DoWorkAsync()
    {
        var manager = await _leaseProvider.CreateLeaseManagerAsync("my-resource");
        var lease = await manager.AcquireAsync(TimeSpan.FromSeconds(60));
        
        if (lease != null)
        {
            try
            {
                // Your critical work here
            }
            finally
            {
                await lease.ReleaseAsync();
            }
        }
    }
}
```

## Package Structure

This library follows a granular package structure with automatic dependency resolution:

- **DistributedLeasing.Core**: Core interfaces and contracts
- **DistributedLeasing.Abstractions**: Base provider abstractions (for building custom providers)
- **DistributedLeasing.Azure.Blob**: Azure Blob Storage implementation
- **DistributedLeasing.Azure.Cosmos**: Azure Cosmos DB implementation  
- **DistributedLeasing.Azure.Redis**: Azure Redis implementation
- **DistributedLeasing.Extensions.DependencyInjection**: ASP.NET Core integration

When you install a provider package (e.g., `DistributedLeasing.Azure.Blob`), all required dependencies are automatically installed.

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

## Documentation

For comprehensive documentation, API reference, and advanced scenarios, visit:
- [GitHub Repository](https://github.com/yourusername/DistributedLeasing)
- [API Documentation](https://github.com/yourusername/DistributedLeasing/docs)

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Contributing

Contributions are welcome! Please see our contributing guidelines for more information.
