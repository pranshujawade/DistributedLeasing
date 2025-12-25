# DistributedLeasing.Abstractions

[![NuGet](https://img.shields.io/nuget/v/DistributedLeasing.Abstractions.svg)](https://www.nuget.org/packages/DistributedLeasing.Abstractions/)
[![Downloads](https://img.shields.io/nuget/dt/DistributedLeasing.Abstractions.svg)](https://www.nuget.org/packages/DistributedLeasing.Abstractions/)

**The foundation package for building distributed leasing systems in .NET**

This package provides the core framework for distributed leasing with comprehensive authentication, observability, and extensibility support. It includes contracts, base implementations, Azure authentication, configuration, event system, exceptions, and observability components.

## When to Use This Package

**Use this package directly when:**
- Building a custom lease provider for a different backend (e.g., PostgreSQL, MongoDB, etcd)
- Extending the framework with custom authentication mechanisms
- Integrating advanced observability features into your application

**Use a provider package instead when:**
- You need Azure Blob Storage leasing → [DistributedLeasing.Azure.Blob](https://www.nuget.org/packages/DistributedLeasing.Azure.Blob/)
- You need Azure Cosmos DB leasing → [DistributedLeasing.Azure.Cosmos](https://www.nuget.org/packages/DistributedLeasing.Azure.Cosmos/)
- You need Azure Redis leasing → [DistributedLeasing.Azure.Redis](https://www.nuget.org/packages/DistributedLeasing.Azure.Redis/)

**Provider packages automatically include this package as a dependency** - you don't need to install it separately.

## What's Included

### Core Contracts

**ILeaseProvider** - Factory for creating lease managers
```csharp
public interface ILeaseProvider
{
    Task<ILeaseManager> CreateLeaseManagerAsync(string leaseKey);
}
```

**ILeaseManager** - Manages lease acquisition and release
```csharp
public interface ILeaseManager
{
    Task<ILease?> AcquireAsync(TimeSpan? duration = null);
    Task<ILease?> TryAcquireAsync(TimeSpan? duration = null);
}
```

**ILease** - Represents an active lease with auto-renewal
```csharp
public interface ILease : IAsyncDisposable
{
    string LeaseId { get; }
    bool IsActive { get; }
    Task ReleaseAsync();
    event EventHandler<LeaseRenewedEventArgs>? LeaseRenewed;
    event EventHandler<LeaseLostEventArgs>? LeaseLost;
}
```

### Base Implementations

- **LeaseBase** - Abstract base class for implementing lease instances
- **LeaseManagerBase** - Abstract base class for implementing lease managers
- Handles automatic renewal, event dispatching, and lifecycle management

### Azure Authentication

Comprehensive Azure authentication support with multiple modes:

**Supported Authentication Modes:**
- **Auto** - Automatic credential chain (recommended for most scenarios)
- **ManagedIdentity** - System-assigned or user-assigned managed identity
- **WorkloadIdentity** - Kubernetes workload identity or GitHub OIDC
- **ServicePrincipal** - Certificate-based or client secret authentication
- **FederatedCredential** - External OIDC token exchange
- **Development** - Azure CLI, Visual Studio, VS Code (dev-only)

See the [Authentication Configuration](#authentication-configuration) section below for detailed setup.

### Configuration

**LeaseOptions** - Centralized lease configuration
- Lease duration settings
- Auto-renewal intervals
- Retry policies
- Custom metadata

### Event System

**Lifecycle Events:**
- `LeaseRenewed` - Fired when a lease is successfully renewed
- `LeaseLost` - Fired when a lease is lost (expiration, exception)
- `LeaseRenewalFailed` - Fired when renewal fails but lease still active

**Event Args:**
- `LeaseRenewedEventArgs` - Contains new expiration time
- `LeaseLostEventArgs` - Contains reason and exception details
- `LeaseRenewalFailedEventArgs` - Contains exception and retry information

### Exceptions

**LeaseException** - Base exception for all lease-related errors
- `LeaseAcquisitionException` - Failed to acquire lease
- `LeaseRenewalException` - Failed to renew lease
- `LeaseReleaseException` - Failed to release lease

### Observability

**Health Checks:**
```csharp
services.AddHealthChecks()
    .AddCheck<LeaseHealthCheck>("lease-health");
```

**Metrics (OpenTelemetry):**
- `leasing.acquire.duration` - Lease acquisition time
- `leasing.acquire.success` - Successful acquisitions counter
- `leasing.acquire.failure` - Failed acquisitions counter
- `leasing.renewal.success` - Successful renewals counter
- `leasing.renewal.failure` - Failed renewals counter
- `leasing.active.count` - Active leases gauge

**Distributed Tracing (OpenTelemetry):**
- Activity source: `DistributedLeasing`
- Spans for acquire, renew, release operations
- Automatic correlation with parent activities

## Installation

```bash
dotnet add package DistributedLeasing.Abstractions
```

**Note:** You typically don't install this package directly. Install a provider package instead, which will include this automatically.

## Authentication Configuration

All Azure providers support unified authentication through configuration. No code changes required to switch authentication modes.

### Configuration Structure

```json
{
  "Authentication": {
    "Mode": "Auto"
  }
}
```

### Mode 1: Auto (Recommended)

Automatically tries credentials in order: Environment → Managed Identity → Workload Identity → Development tools.

```json
{
  "Authentication": {
    "Mode": "Auto"
  }
}
```

**Best for:** Production and development environments with minimal configuration.

### Mode 2: Managed Identity

**System-Assigned Managed Identity:**
```json
{
  "Authentication": {
    "Mode": "ManagedIdentity"
  }
}
```

**User-Assigned Managed Identity:**
```json
{
  "Authentication": {
    "Mode": "ManagedIdentity",
    "ManagedIdentity": {
      "ClientId": "12345678-1234-1234-1234-123456789012"
    }
  }
}
```

**Best for:** Azure VMs, App Services, Container Apps, AKS with managed identity.

### Mode 3: Workload Identity

For Kubernetes or GitHub Actions with OIDC:

```json
{
  "Authentication": {
    "Mode": "WorkloadIdentity"
  }
}
```

**Best for:** AKS with workload identity, GitHub Actions with OIDC federation.

**Requires environment variables:**
- `AZURE_TENANT_ID`
- `AZURE_CLIENT_ID`
- `AZURE_FEDERATED_TOKEN_FILE`

### Mode 4: Service Principal

**Certificate-Based (Recommended):**
```json
{
  "Authentication": {
    "Mode": "ServicePrincipal",
    "ServicePrincipal": {
      "TenantId": "87654321-4321-4321-4321-210987654321",
      "ClientId": "abcdef12-abcd-abcd-abcd-abcdef123456",
      "CertificatePath": "/path/to/certificate.pfx"
    }
  }
}
```

**Client Secret (Not Recommended for Production):**
```json
{
  "Authentication": {
    "Mode": "ServicePrincipal",
    "ServicePrincipal": {
      "TenantId": "87654321-4321-4321-4321-210987654321",
      "ClientId": "abcdef12-abcd-abcd-abcd-abcdef123456",
      "ClientSecret": "your-client-secret"
    }
  }
}
```

**Best for:** CI/CD pipelines, non-Azure environments.

### Mode 5: Development

Uses local development tools (Azure CLI, Visual Studio, VS Code):

```json
{
  "Authentication": {
    "Mode": "Development"
  }
}
```

**Best for:** Local development only. Automatically blocked in Production/Staging environments.

**Requires:** `az login` or signed in to Visual Studio/VS Code.

## Event System Usage

### Monitoring Lease Lifecycle

```csharp
var lease = await leaseManager.AcquireAsync();

lease.LeaseRenewed += (sender, e) =>
{
    Console.WriteLine($"Lease renewed. New expiration: {e.NewExpiration}");
};

lease.LeaseLost += (sender, e) =>
{
    Console.WriteLine($"Lease lost! Reason: {e.Reason}");
    if (e.Exception != null)
    {
        Console.WriteLine($"Exception: {e.Exception.Message}");
    }
};

lease.LeaseRenewalFailed += (sender, e) =>
{
    Console.WriteLine($"Renewal failed but lease still active: {e.Exception.Message}");
};
```

### Graceful Shutdown on Lease Loss

```csharp
var cancellationTokenSource = new CancellationTokenSource();

lease.LeaseLost += (sender, e) =>
{
    Console.WriteLine("Lease lost - initiating graceful shutdown");
    cancellationTokenSource.Cancel();
};

try
{
    await ProcessWorkAsync(cancellationTokenSource.Token);
}
finally
{
    await lease.ReleaseAsync();
}
```

## Observability Integration

### Health Checks

Register the lease health check to monitor lease system health:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

services.AddHealthChecks()
    .AddCheck<LeaseHealthCheck>(
        "lease-health",
        failureStatus: HealthStatus.Degraded,
        tags: new[] { "leasing", "distributed" });
```

Access health status:
```bash
curl http://localhost:5000/health
```

### Metrics with OpenTelemetry

Configure OpenTelemetry to collect lease metrics:

```csharp
using OpenTelemetry.Metrics;
using DistributedLeasing.Abstractions.Observability;

services.AddOpenTelemetry()
    .WithMetrics(builder =>
    {
        builder.AddMeter(LeasingMetrics.MeterName);
    });
```

**Available Metrics:**
- `leasing.acquire.duration` (Histogram) - Time to acquire lease
- `leasing.acquire.success` (Counter) - Successful acquisitions
- `leasing.acquire.failure` (Counter) - Failed acquisitions
- `leasing.renewal.success` (Counter) - Successful renewals
- `leasing.renewal.failure` (Counter) - Failed renewals
- `leasing.active.count` (Gauge) - Currently active leases

### Distributed Tracing

Enable distributed tracing with OpenTelemetry:

```csharp
using OpenTelemetry.Trace;
using DistributedLeasing.Abstractions.Observability;

services.AddOpenTelemetry()
    .WithTracing(builder =>
    {
        builder.AddSource(LeasingActivitySource.SourceName);
    });
```

**Traced Operations:**
- `lease.acquire` - Lease acquisition
- `lease.renew` - Lease renewal
- `lease.release` - Lease release

Each span includes tags: `lease.key`, `lease.id`, `lease.duration`

## Building Custom Providers

Extend the framework to support additional backends:

### 1. Implement ILease

```csharp
using DistributedLeasing.Abstractions.Core;

public class CustomLease : LeaseBase
{
    public CustomLease(
        string leaseId,
        string leaseKey,
        TimeSpan leaseDuration,
        ILogger logger)
        : base(leaseId, leaseKey, leaseDuration, logger)
    {
    }

    protected override async Task<bool> RenewLeaseAsync(CancellationToken cancellationToken)
    {
        // Implement backend-specific renewal logic
        return await YourBackend.RenewAsync(LeaseId, cancellationToken);
    }

    protected override async Task ReleaseLeaseInternalAsync(CancellationToken cancellationToken)
    {
        // Implement backend-specific release logic
        await YourBackend.ReleaseAsync(LeaseId, cancellationToken);
    }
}
```

### 2. Implement ILeaseManager

```csharp
using DistributedLeasing.Abstractions.Core;

public class CustomLeaseManager : LeaseManagerBase
{
    public CustomLeaseManager(
        string leaseKey,
        LeaseOptions options,
        ILogger logger)
        : base(leaseKey, options, logger)
    {
    }

    protected override async Task<ILease?> AcquireLeaseInternalAsync(
        TimeSpan leaseDuration,
        CancellationToken cancellationToken)
    {
        // Implement backend-specific acquisition logic
        var leaseId = await YourBackend.TryAcquireAsync(LeaseKey, leaseDuration);
        
        if (leaseId == null)
            return null;

        return new CustomLease(leaseId, LeaseKey, leaseDuration, Logger);
    }
}
```

### 3. Implement ILeaseProvider

```csharp
using DistributedLeasing.Abstractions.Contracts;

public class CustomLeaseProvider : ILeaseProvider
{
    private readonly CustomLeaseProviderOptions _options;
    private readonly ILogger<CustomLeaseProvider> _logger;

    public CustomLeaseProvider(
        CustomLeaseProviderOptions options,
        ILogger<CustomLeaseProvider> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task<ILeaseManager> CreateLeaseManagerAsync(string leaseKey)
    {
        // Validate and create manager
        return new CustomLeaseManager(leaseKey, _options.LeaseOptions, _logger);
    }
}
```

## Framework Compatibility

- **.NET Standard 2.0** - Compatible with .NET Framework 4.6.1+, .NET Core 2.0+
- **.NET 8.0** - Long-term support release
- **.NET 10.0** - Latest release

## Package Dependencies

- **Azure.Core** - Azure SDK core functionality
- **Azure.Identity** - Azure authentication
- **Microsoft.Extensions.*** - Configuration, DI, logging, health checks
- **Microsoft.SourceLink.GitHub** - Source debugging support

## Related Packages

- [DistributedLeasing.Azure.Blob](https://www.nuget.org/packages/DistributedLeasing.Azure.Blob/) - Azure Blob Storage provider
- [DistributedLeasing.Azure.Cosmos](https://www.nuget.org/packages/DistributedLeasing.Azure.Cosmos/) - Azure Cosmos DB provider
- [DistributedLeasing.Azure.Redis](https://www.nuget.org/packages/DistributedLeasing.Azure.Redis/) - Azure Redis provider
- [DistributedLeasing.ChaosEngineering](https://www.nuget.org/packages/DistributedLeasing.ChaosEngineering/) - Testing utilities

## Samples and Documentation

- [BlobLeaseSample](../../samples/BlobLeaseSample/README.md) - Comprehensive Azure Blob leasing examples
- [CosmosLeaseSample](../../samples/CosmosLeaseSample/README.md) - Azure Cosmos DB leasing examples
- [GitHub Repository](https://github.com/pranshujawade/DistributedLeasing)

## License

MIT License - see [LICENSE](../../LICENSE) for details.

## Contributing

Contributions are welcome! Please see [CONTRIBUTING.md](../../CONTRIBUTING.md) for guidelines.
