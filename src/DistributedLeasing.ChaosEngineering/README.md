# DistributedLeasing.ChaosEngineering

[![NuGet](https://img.shields.io/nuget/v/DistributedLeasing.ChaosEngineering.svg)](https://www.nuget.org/packages/DistributedLeasing.ChaosEngineering/)
[![Downloads](https://img.shields.io/nuget/dt/DistributedLeasing.ChaosEngineering.svg)](https://www.nuget.org/packages/DistributedLeasing.ChaosEngineering/)

**Chaos engineering toolkit for testing distributed leasing resilience**

This package provides controlled failure injection for testing the resilience and fault tolerance of distributed leasing systems. Use it to validate your application's behavior under various failure scenarios.

⚠️ **FOR TESTING ONLY - NOT FOR PRODUCTION USE**

## Features

✅ **Controlled Failure Injection** - Simulate specific failure scenarios  
✅ **Configurable Probability** - Set failure rates for chaos testing  
✅ **Latency Injection** - Add artificial delays to test timeout handling  
✅ **Intermittent Failures** - Simulate network hiccups and transient errors  
✅ **Integration Testing** - Validate error handling and retry logic  
✅ **Decorator Pattern** - Wraps any lease provider for easy testing

## When to Use This Package

**Use This Package When:**
- Writing integration tests for distributed leasing logic
- Validating error handling and retry mechanisms
- Testing lease timeout and expiration scenarios
- Simulating network failures and latency
- Chaos engineering experiments
- Load testing with controlled failures

**Do NOT Use This Package:**
- ❌ In production environments
- ❌ As primary lease provider implementation
- ❌ Without explicit test isolation

## Installation

```bash
dotnet add package DistributedLeasing.ChaosEngineering
```

Install only in test projects, not in production code.

## Quick Start

### Basic Chaos Testing

```csharp
using DistributedLeasing.ChaosEngineering;
using DistributedLeasing.Azure.Blob;

// Create your actual lease provider
var actualProvider = new BlobLeaseProvider(new BlobLeaseProviderOptions
{
    ContainerUri = testContainerUri,
    Credential = credential
});

// Wrap with chaos provider
var chaosProvider = new ChaosLeaseProvider(actualProvider, new ChaosOptions
{
    AcquireFailureProbability = 0.3,  // 30% of acquisitions fail
    RenewFailureProbability = 0.2,     // 20% of renewals fail
    ReleaseFailureProbability = 0.1    // 10% of releases fail
});

// Use in tests
var leaseManager = await chaosProvider.CreateLeaseManagerAsync("test-lock");
var lease = await leaseManager.TryAcquireAsync();

// Test your error handling
if (lease == null)
{
    // Your code should handle this gracefully
    Assert.NotNull(fallbackMechanism);
}
```

### Latency Injection

```csharp
var chaosProvider = new ChaosLeaseProvider(actualProvider, new ChaosOptions
{
    MinLatency = TimeSpan.FromMilliseconds(100),  // Minimum 100ms delay
    MaxLatency = TimeSpan.FromMilliseconds(500),  // Maximum 500ms delay
    LatencyProbability = 0.5                       // 50% of operations delayed
});

// Test timeout handling
var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
try
{
    var lease = await leaseManager.AcquireAsync(cancellationToken: cts.Token);
}
catch (OperationCanceledException)
{
    // Verify your code handles timeouts correctly
    Assert.True(true);
}
```

### Intermittent Failures

```csharp
var chaosProvider = new ChaosLeaseProvider(actualProvider, new ChaosOptions
{
    // Fail 2 out of every 5 operations
    FailurePattern = new[] { false, true, false, true, false }
});

// Test retry logic
int attempts = 0;
ILease? lease = null;

while (lease == null && attempts < 5)
{
    lease = await leaseManager.TryAcquireAsync();
    attempts++;
}

Assert.NotNull(lease);  // Should succeed after retries
Assert.True(attempts > 1);  // Verify retries happened
```

## Chaos Options

### Failure Probabilities

```csharp
public class ChaosOptions
{
    // Probability of acquire operation failing (0.0 - 1.0)
    public double AcquireFailureProbability { get; set; } = 0.0;

    // Probability of renew operation failing (0.0 - 1.0)
    public double RenewFailureProbability { get; set; } = 0.0;

    // Probability of release operation failing (0.0 - 1.0)
    public double ReleaseFailureProbability { get; set; } = 0.0;

    // Probability of any operation being delayed (0.0 - 1.0)
    public double LatencyProbability { get; set; } = 0.0;

    // Minimum latency to inject
    public TimeSpan MinLatency { get; set; } = TimeSpan.Zero;

    // Maximum latency to inject
    public TimeSpan MaxLatency { get; set; } = TimeSpan.Zero;

    // Custom failure pattern (overrides probabilities)
    public bool[]? FailurePattern { get; set; } = null;

    // Exception type to throw on failure
    public Type ExceptionType { get; set; } = typeof(LeaseException);

    // Custom exception message
    public string FailureMessage { get; set; } = "Chaos engineering failure";
}
```

## Testing Scenarios

### Scenario 1: Test Acquisition Retry Logic

```csharp
[Fact]
public async Task Should_Retry_On_Acquisition_Failure()
{
    var chaosProvider = new ChaosLeaseProvider(actualProvider, new ChaosOptions
    {
        // Fail first 2 attempts, succeed on 3rd
        FailurePattern = new[] { true, true, false }
    });

    var leaseManager = await chaosProvider.CreateLeaseManagerAsync("test");
    
    // Retry logic
    ILease? lease = null;
    for (int i = 0; i < 5; i++)
    {
        lease = await leaseManager.TryAcquireAsync();
        if (lease != null) break;
        await Task.Delay(100);
    }

    Assert.NotNull(lease);
}
```

### Scenario 2: Test Renewal Failure Handling

```csharp
[Fact]
public async Task Should_Detect_Renewal_Failure()
{
    var chaosProvider = new ChaosLeaseProvider(actualProvider, new ChaosOptions
    {
        RenewFailureProbability = 1.0  // Always fail renewal
    });

    var leaseManager = await chaosProvider.CreateLeaseManagerAsync("test");
    var lease = await leaseManager.AcquireAsync(TimeSpan.FromSeconds(5));

    bool renewalFailed = false;
    lease.LeaseRenewalFailed += (sender, e) =>
    {
        renewalFailed = true;
    };

    // Wait for auto-renewal attempt
    await Task.Delay(TimeSpan.FromSeconds(4));

    Assert.True(renewalFailed);
}
```

### Scenario 3: Test Lease Loss on Expiration

```csharp
[Fact]
public async Task Should_Handle_Lease_Loss()
{
    var chaosProvider = new ChaosLeaseProvider(actualProvider, new ChaosOptions
    {
        RenewFailureProbability = 1.0
    });

    var leaseManager = await chaosProvider.CreateLeaseManagerAsync("test");
    var lease = await leaseManager.AcquireAsync(TimeSpan.FromSeconds(5));

    bool leaseLost = false;
    lease.LeaseLost += (sender, e) =>
    {
        leaseLost = true;
    };

    // Wait for expiration
    await Task.Delay(TimeSpan.FromSeconds(6));

    Assert.True(leaseLost);
}
```

### Scenario 4: Test Timeout Handling

```csharp
[Fact]
public async Task Should_Timeout_On_High_Latency()
{
    var chaosProvider = new ChaosLeaseProvider(actualProvider, new ChaosOptions
    {
        LatencyProbability = 1.0,
        MinLatency = TimeSpan.FromSeconds(5),
        MaxLatency = TimeSpan.FromSeconds(10)
    });

    var leaseManager = await chaosProvider.CreateLeaseManagerAsync("test");
    
    var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
    
    await Assert.ThrowsAsync<OperationCanceledException>(async () =>
    {
        await leaseManager.AcquireAsync(cancellationToken: cts.Token);
    });
}
```

### Scenario 5: Test Concurrent Acquisition

```csharp
[Fact]
public async Task Should_Handle_Concurrent_Acquisition_With_Failures()
{
    var chaosProvider = new ChaosLeaseProvider(actualProvider, new ChaosOptions
    {
        AcquireFailureProbability = 0.5
    });

    var tasks = Enumerable.Range(0, 10).Select(async i =>
    {
        var manager = await chaosProvider.CreateLeaseManagerAsync("shared-lock");
        return await manager.TryAcquireAsync();
    });

    var results = await Task.WhenAll(tasks);
    
    // At most one should succeed (due to exclusivity)
    var successCount = results.Count(l => l != null);
    Assert.True(successCount <= 1);
}
```

## Advanced Usage

### Custom Exception Types

```csharp
var chaosProvider = new ChaosLeaseProvider(actualProvider, new ChaosOptions
{
    AcquireFailureProbability = 0.5,
    ExceptionType = typeof(TimeoutException),
    FailureMessage = "Simulated timeout"
});
```

### Deterministic Failure Patterns

```csharp
// Fail every other operation
var chaosProvider = new ChaosLeaseProvider(actualProvider, new ChaosOptions
{
    FailurePattern = new[] { false, true, false, true, false, true }
});

// Pattern repeats: succeed, fail, succeed, fail, ...
```

### Combining Multiple Chaos Types

```csharp
var chaosProvider = new ChaosLeaseProvider(actualProvider, new ChaosOptions
{
    AcquireFailureProbability = 0.2,   // 20% acquisition failures
    RenewFailureProbability = 0.1,      // 10% renewal failures
    LatencyProbability = 0.3,           // 30% operations delayed
    MinLatency = TimeSpan.FromMilliseconds(50),
    MaxLatency = TimeSpan.FromMilliseconds(200)
});

// Simulates realistic production chaos
```

## Integration with Test Frameworks

### xUnit Example

```csharp
public class LeasingIntegrationTests : IAsyncLifetime
{
    private ILeaseProvider _chaosProvider;
    private ILeaseProvider _actualProvider;

    public async Task InitializeAsync()
    {
        _actualProvider = new BlobLeaseProvider(testOptions);
        _chaosProvider = new ChaosLeaseProvider(_actualProvider, new ChaosOptions
        {
            AcquireFailureProbability = 0.3
        });
    }

    [Fact]
    public async Task TestLeaseResilience()
    {
        var manager = await _chaosProvider.CreateLeaseManagerAsync("test");
        // Test logic here
    }

    public async Task DisposeAsync()
    {
        // Cleanup
    }
}
```

### NUnit Example

```csharp
[TestFixture]
public class LeasingChaosTests
{
    private ILeaseProvider _chaosProvider;

    [SetUp]
    public async Task Setup()
    {
        var actualProvider = new CosmosLeaseProvider(testOptions);
        _chaosProvider = new ChaosLeaseProvider(actualProvider, new ChaosOptions
        {
            RenewFailureProbability = 0.5
        });
    }

    [Test]
    public async Task TestRenewalFailure()
    {
        // Test logic
    }
}
```

## Best Practices

### 1. Use Only in Test Projects

```xml
<!-- ✅ Good - Only in test project -->
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <PackageReference Include="DistributedLeasing.ChaosEngineering" Version="5.0.0" />
  </ItemGroup>
</Project>
```

```xml
<!-- ❌ Bad - Don't reference in production projects -->
```

### 2. Start with Low Probabilities

```csharp
// ✅ Start conservative
var chaosOptions = new ChaosOptions
{
    AcquireFailureProbability = 0.1  // 10%
};

// ❌ Avoid extreme probabilities initially
var chaosOptions = new ChaosOptions
{
    AcquireFailureProbability = 0.9  // 90% - too high for initial testing
};
```

### 3. Combine with Retry Logic

```csharp
async Task<ILease?> AcquireWithRetry(ILeaseManager manager, int maxAttempts)
{
    for (int i = 0; i < maxAttempts; i++)
    {
        var lease = await manager.TryAcquireAsync();
        if (lease != null) return lease;
        await Task.Delay(TimeSpan.FromMilliseconds(100 * (i + 1)));
    }
    return null;
}

// Test the retry logic
var lease = await AcquireWithRetry(leaseManager, 5);
Assert.NotNull(lease);
```

### 4. Test All Failure Modes

```csharp
[Theory]
[InlineData(1.0, 0.0, 0.0)]  // Acquire failures
[InlineData(0.0, 1.0, 0.0)]  // Renew failures
[InlineData(0.0, 0.0, 1.0)]  // Release failures
public async Task Should_Handle_All_Failure_Types(
    double acquireProb,
    double renewProb,
    double releaseProb)
{
    var chaosProvider = new ChaosLeaseProvider(actualProvider, new ChaosOptions
    {
        AcquireFailureProbability = acquireProb,
        RenewFailureProbability = renewProb,
        ReleaseFailureProbability = releaseProb
    });

    // Test logic for each failure type
}
```

### 5. Document Chaos Parameters

```csharp
// ✅ Good - Clear documentation
var chaosProvider = new ChaosLeaseProvider(actualProvider, new ChaosOptions
{
    // Simulate 20% network failures during acquisition
    AcquireFailureProbability = 0.2,
    
    // Simulate occasional renewal delays (100-500ms)
    LatencyProbability = 0.3,
    MinLatency = TimeSpan.FromMilliseconds(100),
    MaxLatency = TimeSpan.FromMilliseconds(500)
});
```

## Limitations

1. **Not for Production**: Never deploy chaos provider to production
2. **Decorator Only**: Requires wrapping a real provider
3. **Randomness**: Probability-based failures are non-deterministic
4. **Single Instance**: Chaos applies only to local provider instance

## Troubleshooting

### "Chaos provider not injecting failures"

**Problem:** Probability set to 0.0 or failure pattern incorrect.

**Solution:** Verify chaos options are configured:
```csharp
Assert.True(chaosOptions.AcquireFailureProbability > 0.0);
```

### "Too many failures in tests"

**Problem:** Probability too high for test stability.

**Solution:** Reduce failure probabilities:
```csharp
var chaosOptions = new ChaosOptions
{
    AcquireFailureProbability = 0.1  // Lower from 0.5
};
```

### "Nondeterministic test failures"

**Problem:** Random failures cause flaky tests.

**Solution:** Use deterministic patterns:
```csharp
var chaosOptions = new ChaosOptions
{
    FailurePattern = new[] { false, true, false }  // Deterministic
};
```

## Architecture

```
┌─────────────────────────────────────────────────────┐
│ Test Code                                            │
│ ┌──────────────────────────────────────────┐        │
│ │ ChaosLeaseProvider (Decorator)           │        │
│ │ ┌────────────────────────────────────┐   │        │
│ │ │ Chaos Logic:                        │   │        │
│ │ │ • Failure injection                 │   │        │
│ │ │ • Latency simulation                │   │        │
│ │ │ • Pattern-based failures            │   │        │
│ │ └────────────────────────────────────┘   │        │
│ │           │                                │        │
│ │           │ Delegates to                   │        │
│ │           ▼                                │        │
│ │ ┌────────────────────────────────────┐   │        │
│ │ │ Actual Provider                     │   │        │
│ │ │ (Blob, Cosmos, Redis)               │   │        │
│ │ └────────────────────────────────────┘   │        │
│ └──────────────────────────────────────────┘        │
└─────────────────────────────────────────────────────┘
```

## Framework Compatibility

- **.NET Standard 2.0** - Compatible with .NET Framework 4.6.1+, .NET Core 2.0+
- **.NET 8.0** - Long-term support release
- **.NET 10.0** - Latest release

## Package Dependencies

- [DistributedLeasing.Abstractions](https://www.nuget.org/packages/DistributedLeasing.Abstractions/) - Core framework

## Related Packages

- [DistributedLeasing.Azure.Blob](https://www.nuget.org/packages/DistributedLeasing.Azure.Blob/) - Blob Storage provider
- [DistributedLeasing.Azure.Cosmos](https://www.nuget.org/packages/DistributedLeasing.Azure.Cosmos/) - Cosmos DB provider
- [DistributedLeasing.Azure.Redis](https://www.nuget.org/packages/DistributedLeasing.Azure.Redis/) - Redis provider

## Documentation

- [GitHub Repository](https://github.com/pranshujawade/DistributedLeasing)
- [Chaos Engineering Principles](https://principlesofchaos.org/)

## License

MIT License - see [LICENSE](../../LICENSE) for details.
