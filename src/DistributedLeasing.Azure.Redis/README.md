# DistributedLeasing.Azure.Redis

[![NuGet](https://img.shields.io/nuget/v/DistributedLeasing.Azure.Redis.svg)](https://www.nuget.org/packages/DistributedLeasing.Azure.Redis/)
[![Downloads](https://img.shields.io/nuget/dt/DistributedLeasing.Azure.Redis.svg)](https://www.nuget.org/packages/DistributedLeasing.Azure.Redis/)

**Azure Redis distributed leasing provider for .NET using the Redlock algorithm**

This package implements distributed leasing using the Redlock algorithm on Azure Cache for Redis. It provides the lowest-latency lease coordination with high-performance in-memory operations.

## Features

✅ **Redlock Algorithm** - Industry-standard distributed locking  
✅ **Ultra-Low Latency** - Sub-millisecond operations (< 5ms typical)  
✅ **Automatic Renewal** - Background renewal keeps leases alive  
✅ **Managed Identity Support** - Azure Cache for Redis authentication  
✅ **High Throughput** - Thousands of operations per second  
✅ **Connection Resilience** - Automatic reconnection and failover

## When to Use Redis Leasing

**Best For:**
- Ultra-low latency requirements (< 10ms)
- High-throughput scenarios (>1000 ops/sec)
- Short-duration leases (seconds to minutes)
- Applications requiring immediate lease feedback
- Rate limiting and throttling use cases

**Consider Alternatives When:**
- Need global multi-region coordination → Use [Azure Cosmos DB](https://www.nuget.org/packages/DistributedLeasing.Azure.Cosmos/)
- Prefer cloud-native Azure integration → Use [Azure Blob](https://www.nuget.org/packages/DistributedLeasing.Azure.Blob/)
- Cost is primary concern (Redis has higher hosting cost)

## Installation

```bash
dotnet add package DistributedLeasing.Azure.Redis
```

This automatically includes [DistributedLeasing.Abstractions](https://www.nuget.org/packages/DistributedLeasing.Abstractions/) with authentication and observability support.

## Quick Start

### Basic Usage with Connection String

```csharp
using DistributedLeasing.Azure.Redis;

// Create provider with connection string
var provider = new RedisLeaseProvider(new RedisLeaseProviderOptions
{
    ConnectionString = "mycache.redis.cache.windows.net:6380,password=...,ssl=True"
});

// Create lease manager for a specific resource
var leaseManager = await provider.CreateLeaseManagerAsync("my-resource-lock");

// Acquire lease
var lease = await leaseManager.AcquireAsync(TimeSpan.FromSeconds(30));

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
var lease = await leaseManager.TryAcquireAsync(TimeSpan.FromSeconds(30));

if (lease == null)
{
    // Lease is held by another instance - fail fast
    Console.WriteLine("Lease unavailable");
    return;
}

// Proceed with work
```

### High-Performance Pattern

```csharp
// For short-duration, high-frequency leases
var leaseOptions = new LeaseOptions
{
    DefaultLeaseDuration = TimeSpan.FromSeconds(10),  // Short duration
    AutoRenew = true,
    AutoRenewInterval = TimeSpan.FromSeconds(6)       // Frequent renewal
};

var provider = new RedisLeaseProvider(new RedisLeaseProviderOptions
{
    ConnectionString = connectionString,
    LeaseOptions = leaseOptions
});

// Rapid acquisition and release
for (int i = 0; i < 1000; i++)
{
    var lease = await leaseManager.TryAcquireAsync();
    if (lease != null)
    {
        await ProcessItemAsync(i);
        await lease.ReleaseAsync();
    }
}
```

## Configuration

### Authentication Options

**Option 1: Connection String (Most Common)**

```csharp
var options = new RedisLeaseProviderOptions
{
    ConnectionString = "mycache.redis.cache.windows.net:6380,password=...,ssl=True,abortConnect=False"
};
```

**Option 2: Managed Identity (Azure Cache for Redis Premium)**

```csharp
using Azure.Identity;

var options = new RedisLeaseProviderOptions
{
    ConnectionString = "mycache.redis.cache.windows.net:6380,ssl=True,abortConnect=False",
    Credential = new DefaultAzureCredential()
};
```

**Option 3: Configuration String Options**

```csharp
var configurationOptions = new StackExchange.Redis.ConfigurationOptions
{
    EndPoints = { "mycache.redis.cache.windows.net:6380" },
    Password = "your-access-key",
    Ssl = true,
    AbortOnConnectFail = false,
    ConnectTimeout = 5000,
    SyncTimeout = 5000
};

var options = new RedisLeaseProviderOptions
{
    ConfigurationOptions = configurationOptions
};
```

For comprehensive authentication configuration, see [Authentication Guide](https://github.com/pranshujawade/DistributedLeasing#authentication).

### Provider Options

```csharp
public class RedisLeaseProviderOptions
{
    // Authentication (choose one)
    public string? ConnectionString { get; set; }
    public ConfigurationOptions? ConfigurationOptions { get; set; }
    public TokenCredential? Credential { get; set; }  // For managed identity

    // Key prefix for lease keys
    public string KeyPrefix { get; set; } = "lease:";

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
    DefaultLeaseDuration = TimeSpan.FromSeconds(30),  // Redis optimized for shorter leases
    AutoRenew = true,
    AutoRenewInterval = TimeSpan.FromSeconds(20),     // 2/3 of duration
    MaxRetryAttempts = 3,
    RetryDelay = TimeSpan.FromMilliseconds(100)       // Fast retry for Redis
};

var providerOptions = new RedisLeaseProviderOptions
{
    ConnectionString = connectionString,
    LeaseOptions = leaseOptions
};
```

## How Redis Leasing Works

### Redlock Algorithm

This implementation uses the Redlock algorithm created by Redis author Salvatore Sanfilippo:

1. **Acquire**: Set key with unique lease ID using `SET key value NX PX milliseconds`
   - `NX`: Only set if key doesn't exist
   - `PX`: Set expiration in milliseconds
2. **Validate**: Check operation success and compute elapsed time
3. **Renew**: Use Lua script to atomically check and extend expiration
4. **Release**: Use Lua script to atomically check lease ID and delete key

**Key Characteristics:**
- **Atomic Operations**: Lua scripts ensure atomicity
- **Time-Based Expiration**: Redis automatically expires keys
- **No Polling**: Direct get/set operations
- **High Performance**: In-memory operations

### Redis Key Structure

Leases are stored as Redis keys with the pattern:
```
lease:{leaseKey}
```

Example:
- Lease key: `database-migration`
- Redis key: `lease:database-migration`

Value stored:
```json
{
  "leaseId": "abc123-def456-ghi789",
  "ownerId": "instance-01",
  "acquiredAt": "2025-12-25T12:00:00Z",
  "metadata": {
    "instance": "server-01",
    "version": "1.0.0"
  }
}
```

## Performance Characteristics

### Latency

| Operation | Typical Latency | Notes |
|-----------|----------------|-------|
| Acquire (success) | 1-5ms | Same Azure region |
| Acquire (failure) | 1-5ms | Instant NX check |
| Renew | 1-5ms | Lua script execution |
| Release | 1-5ms | Lua script delete |

**Network Dependency:** Latency increases with distance from Redis instance.

### Throughput

- **Single Lease**: 10,000+ operations/second
- **Multiple Leases**: Scales linearly (independent keys)
- **Connection Multiplexing**: StackExchange.Redis handles connection pooling

**Best Practice:** Redis excels at high throughput - ideal for rate limiting and short-duration locks.

### Lease Duration Recommendations

- **Minimum**: 5 seconds (avoid excessive network overhead)
- **Typical**: 10-30 seconds (balanced responsiveness and overhead)
- **Maximum**: 300 seconds (5 minutes - though Redis better suited for shorter)

## Best Practices

### 1. Use Premium Tier for Production

```text
Azure Cache for Redis Tiers:
- Basic: Single node, no SLA (development only)
- Standard: Two nodes, 99.9% SLA (production)
- Premium: Clustering, VNet, geo-replication (high availability)
```

### 2. Configure Connection Resilience

```csharp
var configOptions = new ConfigurationOptions
{
    EndPoints = { "mycache.redis.cache.windows.net:6380" },
    Ssl = true,
    AbortOnConnectFail = false,      // ✅ Don't fail on initial connect failure
    ConnectRetry = 3,                 // ✅ Retry connection attempts
    ConnectTimeout = 5000,            // 5 seconds
    SyncTimeout = 5000,               // 5 seconds
    ReconnectRetryPolicy = new ExponentialRetry(1000)  // ✅ Exponential backoff
};
```

### 3. Use Appropriate Lease Duration

```csharp
// ✅ For high-frequency operations (rate limiting)
var lease = await leaseManager.AcquireAsync(TimeSpan.FromSeconds(5));

// ✅ For moderate-duration tasks
var lease = await leaseManager.AcquireAsync(TimeSpan.FromSeconds(30));

// ⚠️ For very long tasks, consider Blob or Cosmos
var lease = await leaseManager.AcquireAsync(TimeSpan.FromMinutes(5));
```

### 4. Always Release Leases

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

### 5. Handle Connection Failures

```csharp
var cts = new CancellationTokenSource();

lease.LeaseLost += (sender, e) =>
{
    Console.WriteLine($"Lease lost: {e.Reason}");
    if (e.Exception != null)
    {
        Console.WriteLine($"Connection issue: {e.Exception.Message}");
    }
    cts.Cancel(); // Stop work on connection loss
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

### 6. Use Key Prefixes for Organization

```csharp
var options = new RedisLeaseProviderOptions
{
    ConnectionString = connectionString,
    KeyPrefix = "myapp:leases:"  // Organize keys by application
};

// Results in keys like: myapp:leases:my-resource-lock
```

## Redlock Algorithm Details

### Safety Properties

The Redlock algorithm provides:

1. **Mutual Exclusion**: At most one client can hold the lock at any time
2. **Deadlock Free**: Eventually possible to acquire lock (via expiration)
3. **Fault Tolerance**: Lock survives as long as majority of Redis instances available

### Implementation Specifics

**Atomic Renewal (Lua Script):**
```lua
if redis.call("get", KEYS[1]) == ARGV[1] then
    return redis.call("pexpire", KEYS[1], ARGV[2])
else
    return 0
end
```

**Atomic Release (Lua Script):**
```lua
if redis.call("get", KEYS[1]) == ARGV[1] then
    return redis.call("del", KEYS[1])
else
    return 0
end
```

These ensure that only the lease holder can renew or release.

## Troubleshooting

### "Unable to connect to Redis server"

**Problem:** Connection string incorrect or firewall blocking.

**Solution:**
1. Verify connection string from Azure Portal
2. Check firewall rules allow your IP/VNet
3. Ensure SSL port (6380) is used for Azure Cache for Redis
4. Set `AbortOnConnectFail=False` in connection string

### "Timeout performing operation"

**Problem:** Network latency or Redis overloaded.

**Solution:**
```csharp
var configOptions = new ConfigurationOptions
{
    EndPoints = { "mycache.redis.cache.windows.net:6380" },
    SyncTimeout = 10000,  // Increase timeout to 10 seconds
    ConnectTimeout = 10000
};
```

### Frequent lease loss

**Problem:** Network instability or lease duration too short.

**Solution:**
```csharp
var leaseOptions = new LeaseOptions
{
    DefaultLeaseDuration = TimeSpan.FromSeconds(60),  // Increase duration
    AutoRenewInterval = TimeSpan.FromSeconds(40)      // Earlier renewal
};
```

### "WRONGTYPE Operation against a key holding the wrong kind of value"

**Problem:** Redis key used for different purpose previously.

**Solution:** Use unique key prefixes or delete the conflicting key:
```bash
redis-cli -h mycache.redis.cache.windows.net -p 6380 -a "password" DEL lease:my-resource-lock
```

## Monitoring and Observability

### Redis CLI Inspection

```bash
# Connect to Redis
redis-cli -h mycache.redis.cache.windows.net -p 6380 -a "your-access-key" --tls

# List all lease keys
KEYS lease:*

# Get lease value
GET lease:my-resource-lock

# Check TTL (time to live)
TTL lease:my-resource-lock

# Get all keys with pattern
SCAN 0 MATCH lease:* COUNT 100
```

### Azure Portal Metrics

Monitor in Azure Portal → Cache for Redis → Metrics:
- **Connected Clients**: Number of active connections
- **Operations/sec**: Total operations per second
- **Cache Hits/Misses**: Performance indicators
- **Server Load**: CPU usage
- **Network Bandwidth**: In/out traffic

### Metrics and Health Checks

See [DistributedLeasing.Abstractions README](../DistributedLeasing.Abstractions/README.md#observability-integration) for:
- OpenTelemetry metrics configuration
- Health check setup
- Distributed tracing integration

## Architecture

```
┌─────────────────────────────────────────────────────┐
│ Your Application                                     │
│ ┌──────────────┐         ┌─────────────────┐       │
│ │ILeaseProvider│────────▶│RedisLeaseProvider│       │
│ └──────────────┘         └─────────────────┘       │
│         │                                            │
│         │ CreateLeaseManagerAsync("my-lock")        │
│         ▼                                            │
│ ┌──────────────┐         ┌──────────────────┐      │
│ │ILeaseManager │────────▶│RedisLeaseManager │      │
│ └──────────────┘         └──────────────────┘      │
│         │                                            │
│         │ AcquireAsync()                            │
│         ▼                                            │
│ ┌──────────────┐         ┌────────────┐            │
│ │   ILease     │────────▶│ RedisLease │            │
│ └──────────────┘         └────────────┘            │
└─────────────────────────────────────────────────────┘
                    │
                    │ Redis Protocol (Redlock Algorithm)
                    ▼
┌─────────────────────────────────────────────────────┐
│ Azure Cache for Redis                               │
│ ┌─────────────────────────────────────────────┐    │
│ │ In-Memory Key-Value Store                    │    │
│ │ ┌─────────────────────────────────────┐     │    │
│ │ │ Key: lease:my-lock                   │     │    │
│ │ │ Value: {leaseId, ownerId, metadata}  │     │    │
│ │ │ TTL: 30 seconds                      │     │    │
│ │ └─────────────────────────────────────┘     │    │
│ └─────────────────────────────────────────────┘    │
│ Operations: SET NX PX, Lua scripts for renewal     │
└─────────────────────────────────────────────────────┘
```

## Redis Cluster Support

For Redis Cluster deployments:

```csharp
var configOptions = new ConfigurationOptions
{
    EndPoints = 
    {
        "node1.redis.cache.windows.net:6380",
        "node2.redis.cache.windows.net:6380",
        "node3.redis.cache.windows.net:6380"
    },
    Ssl = true,
    AbortOnConnectFail = false
};

var options = new RedisLeaseProviderOptions
{
    ConfigurationOptions = configOptions
};
```

**Note:** Redlock works best with single-instance Redis for simplicity. For multi-instance coordination, consider Cosmos DB.

## Framework Compatibility

- **.NET Standard 2.0** - Compatible with .NET Framework 4.6.1+, .NET Core 2.0+
- **.NET 8.0** - Long-term support release
- **.NET 10.0** - Latest release

## Package Dependencies

- [DistributedLeasing.Abstractions](https://www.nuget.org/packages/DistributedLeasing.Abstractions/) - Core framework
- **StackExchange.Redis** - Redis client library
- **Azure.Identity** - Azure authentication

## Related Packages

- [DistributedLeasing.Azure.Blob](https://www.nuget.org/packages/DistributedLeasing.Azure.Blob/) - Blob Storage provider (cloud-native)
- [DistributedLeasing.Azure.Cosmos](https://www.nuget.org/packages/DistributedLeasing.Azure.Cosmos/) - Cosmos DB provider (global distribution)
- [DistributedLeasing.ChaosEngineering](https://www.nuget.org/packages/DistributedLeasing.ChaosEngineering/) - Testing utilities

## Documentation

- [GitHub Repository](https://github.com/pranshujawade/DistributedLeasing)
- [Redlock Algorithm](https://redis.io/topics/distlock) - Official Redis documentation
- [StackExchange.Redis Documentation](https://stackexchange.github.io/StackExchange.Redis/)
- [Authentication Guide](https://github.com/pranshujawade/DistributedLeasing#authentication)

## License

MIT License - see [LICENSE](../../LICENSE) for details.
