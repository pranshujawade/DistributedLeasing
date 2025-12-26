# DistributedLeasing.ChaosEngineering

[![NuGet](https://img.shields.io/nuget/v/DistributedLeasing.ChaosEngineering.svg)](https://www.nuget.org/packages/DistributedLeasing.ChaosEngineering/)
[![Downloads](https://img.shields.io/nuget/dt/DistributedLeasing.ChaosEngineering.svg)](https://www.nuget.org/packages/DistributedLeasing.ChaosEngineering/)

**Comprehensive chaos engineering toolkit for testing distributed leasing resilience**

This package provides a SOLID-compliant chaos engineering framework for testing the resilience and fault tolerance of distributed leasing systems. Use it to validate your application's behavior under various failure scenarios with controlled, observable, and configurable fault injection.

⚠️ **FOR TESTING ONLY - NOT FOR PRODUCTION USE**

## What's New in Version 5.x

🎉 **Major Architectural Improvements:**
- ✅ **SOLID Principles** - Clean architecture with Strategy, Policy, and Observer patterns
- ✅ **Full Lifecycle Coverage** - Fault injection for ALL operations (Acquire, **Renew**, **Release**, Break)
- ✅ **Thread-Safe** - Proper synchronization for multi-threaded scenarios
- ✅ **Configuration Validation** - Fail-fast with comprehensive validation
- ✅ **Observability** - Multiple observer types for debugging and monitoring
- ✅ **Extensible** - Easy to add custom fault strategies and policies

📖 **[Migration Guide](#migration-from-legacy-api)** available for users of the previous API.

## Features

### Core Capabilities
✅ **Multiple Fault Types** - Delay, Exception, Timeout, Intermittent patterns  
✅ **Decision Policies** - Probabilistic, Deterministic, Threshold-based  
✅ **Full Lifecycle** - Covers Acquire, Renew, Release, and Break operations  
✅ **Per-Operation Config** - Different chaos settings for each operation  
✅ **Thread-Safe** - Safe for concurrent usage across .NET versions  
✅ **Observable** - Console, Diagnostic, and Composite observers  
✅ **Configurable** - Fluent builder API with validation  

### Advanced Features
✅ **Deterministic Testing** - Sequence-based fault injection for reproducible tests  
✅ **Threshold Policies** - Count and time-based fault limits  
✅ **Pattern-Based Faults** - Intermittent failure patterns  
✅ **Auto-Renewal Testing** - Simulate renewal failures  
✅ **Metadata Tagging** - Environment and custom metadata support  

## Installation

```bash
dotnet add package DistributedLeasing.ChaosEngineering
```

Install only in test projects, not in production code.

## Quick Start

### Basic Chaos Testing (Simple API)

```csharp
using DistributedLeasing.ChaosEngineering;
using DistributedLeasing.ChaosEngineering.Faults.Strategies;
using DistributedLeasing.ChaosEngineering.Policies.Implementations;
using DistributedLeasing.ChaosEngineering.Configuration;
using DistributedLeasing.ChaosEngineering.Lifecycle;

// 1. Create fault strategies
var delayStrategy = new DelayFaultStrategy(
    TimeSpan.FromMilliseconds(100),
    TimeSpan.FromMilliseconds(500));

var exceptionStrategy = ExceptionFaultStrategy.Create<ProviderUnavailableException>(
    "Chaos fault injection");

// 2. Create a policy (10% failure rate)
var policy = new ProbabilisticPolicy(0.1, delayStrategy, exceptionStrategy);

// 3. Configure chaos options
var options = new ChaosOptionsBuilder()
    .Enable()
    .WithProviderName("TestChaosProvider")
    .WithDefaultPolicy(policy)
    .Build();

// 4. Wrap your actual provider
var chaosProvider = new ChaosLeaseProviderV2(actualProvider, options);

// 5. Use as normal ILeaseProvider
var lease = await chaosProvider.AcquireLeaseAsync("my-lease", TimeSpan.FromMinutes(5));
```

### With Observability

```csharp
using DistributedLeasing.ChaosEngineering.Observability;

// Create an observer to see chaos events
var observer = new ConsoleChaosObserver(useColors: true, includeTimestamps: true);

var options = new ChaosOptionsBuilder()
    .Enable()
    .WithDefaultPolicy(policy)
    .Build();

var chaosProvider = new ChaosLeaseProviderV2(actualProvider, options, observer);

// Console will show colorful output like:
// [2024-12-26 10:30:45.123] [INJECT] Decision by 'ProbabilisticPolicy' for AcquireAsync on 'my-lease': Random value 0.0543 < threshold 0.1000 (Strategy: DelayFaultStrategy)
// [2024-12-26 10:30:45.150] [EXECUTING] Fault 'DelayFaultStrategy' (Severity: Low) for AcquireAsync on 'my-lease'
// [2024-12-26 10:30:45.652] [EXECUTED] Fault 'DelayFaultStrategy' completed in 502.35ms for AcquireAsync on 'my-lease'
```

## Fault Strategies

### Delay Fault (Latency Injection)

```csharp
// Fixed delay
var fixedDelay = new DelayFaultStrategy(TimeSpan.FromMilliseconds(500));

// Variable delay (random between min and max)
var variableDelay = new DelayFaultStrategy(
    TimeSpan.FromMilliseconds(100),
    TimeSpan.FromSeconds(2));
```

### Exception Fault

```csharp
// Generic exception
var exceptionStrategy = ExceptionFaultStrategy.Create<ProviderUnavailableException>(
    "Simulated provider failure");

// Custom exception with constructor parameters
var timeoutStrategy = ExceptionFaultStrategy.Create<TimeoutException>(
    "Operation timed out due to chaos");
```

### Timeout Fault

```csharp
// Simulates operation cancellation after timeout
var timeoutFault = new TimeoutFaultStrategy(TimeSpan.FromSeconds(5));
// After 5 seconds, throws OperationCanceledException
```

### Intermittent Fault (Pattern-Based)

```csharp
// Fail first 3 attempts
var strategy = IntermittentFaultStrategy.FailFirstN(3, exceptionStrategy);

// Fail every 3rd attempt
var strategy = IntermittentFaultStrategy.FailEveryN(3, exceptionStrategy);

// Custom pattern: true = inject, false = skip
var pattern = new[] { false, false, true, false }; // Fail every 3rd
var strategy = new IntermittentFaultStrategy(pattern, exceptionStrategy);
```

## Decision Policies

### Probabilistic Policy (Random)

```csharp
// 20% chance of fault injection
var policy = new ProbabilisticPolicy(0.2, delayStrategy, exceptionStrategy);

// Randomly selects one of the provided strategies when injecting
```

### Deterministic Policy (Sequence-Based)

```csharp
// Fail first 3 operations, then succeed
var policy = DeterministicPolicy.FailFirstN(3, exceptionStrategy);

// Fail every 5th operation
var policy = DeterministicPolicy.FailEveryN(5, exceptionStrategy);

// Alternate: fail, succeed, fail, succeed...
var policy = DeterministicPolicy.Alternate(exceptionStrategy);

// Custom sequence
var sequence = new List<bool> { true, true, false, false }; // fail 2, succeed 2, repeat
var policy = new DeterministicPolicy(sequence, exceptionStrategy);
```

### Threshold Policy (Count/Time-Based)

```csharp
// Inject faults only for first 5 operations
var policy = ThresholdPolicy.FirstN(5, delayStrategy);

// Inject faults only after 10 operations
var policy = ThresholdPolicy.AfterN(10, exceptionStrategy);

// Inject faults between operation 5 and 15
var policy = ThresholdPolicy.BetweenCounts(5, 15, delayStrategy);

// Inject faults only for 30 seconds after start
var policy = ThresholdPolicy.ForDuration(TimeSpan.FromSeconds(30), delayStrategy);

// Inject faults only between specific times (UTC)
var policy = ThresholdPolicy.BetweenTimes(
    new TimeSpan(9, 0, 0),   // 9:00 AM UTC
    new TimeSpan(17, 0, 0),  // 5:00 PM UTC
    delayStrategy);
```

## Per-Operation Configuration

Configure different chaos settings for each operation:

```csharp
var options = new ChaosOptionsBuilder()
    .Enable()
    
    // Acquire: 20% exception failures
    .ConfigureOperation("AcquireAsync", op => op
        .Enable()
        .WithPolicy(new ProbabilisticPolicy(0.2, exceptionStrategy)))
    
    // Renew: Delay on first 5 renewals
    .ConfigureOperation("RenewAsync", op => op
        .Enable()
        .WithPolicy(ThresholdPolicy.FirstN(5, delayStrategy)))
    
    // Release: No chaos
    .ConfigureOperation("ReleaseAsync", op => op
        .Disable())
    
    // Break: Always succeed
    .Build();

var chaosProvider = new ChaosLeaseProviderV2(actualProvider, options);
```

## Configuration with Fluent Builder

```csharp
var options = new ChaosOptionsBuilder()
    .Enable()
    .WithProviderName("MyChaosProvider")
    .WithSeed(42) // Reproducible random for testing
    .WithDefaultPolicy(probabilisticPolicy)
    .AddFaultStrategies(delayStrategy, exceptionStrategy)
    .WithMaxFaultRate(10.0) // Max 10 faults per second
    .WithRateLimitWindow(60) // In 60-second windows
    .EnableObservability()
    .WithMinimumSeverity(FaultSeverity.Low)
    .AddGlobalMetadata("Environment", "Test")
    .AddEnvironmentTag("Region", "US-West")
    .WithFailFast(true) // Throw on config errors
    .ConfigureOperation("RenewAsync", op => op
        .Enable()
        .WithPolicy(deterministicPolicy)
        .WithMaxFaultRate(5.0)
        .WithMinimumSeverity(FaultSeverity.Medium)
        .ForLeasePattern("critical-*") // Only for leases matching pattern
        .AddMetadata("Critical", true)
        .WithConditions(cond => cond
            .OnlyOnRetry() // Only inject on retry attempts
            .FromAttempt(2) // Start from 2nd attempt
            .UntilAttempt(5) // Stop after 5th attempt
            .WithMetadata("SpecialFlag", "Value")
            .WithTimeConditions(time => time
                .BetweenTimes(new TimeSpan(9, 0, 0), new TimeSpan(17, 0, 0))
                .OnDaysOfWeek(DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday))))
    .Build();
```

## Observability

### Console Observer (Development)

```csharp
var observer = new ConsoleChaosObserver(
    useColors: true,
    includeTimestamps: true);

// Output appears in console with color coding:
// - Yellow: Fault injection decision
// - Magenta: Fault executing
// - Green: Fault executed successfully
// - Red: Fault execution failed
// - Gray: Fault skipped
```

### Diagnostic Observer (Integration with System.Diagnostics)

```csharp
var observer = new DiagnosticChaosObserver(
    sourceName: "MyChaosEngine",
    traceSwitch: new TraceSwitch("Chaos", "Chaos events"));

// Writes to System.Diagnostics trace listeners
// Integrates with existing diagnostic infrastructure
```

### Composite Observer (Multiple Observers)

```csharp
var composite = new CompositeChaosObserver(
    new ConsoleChaosObserver(),
    new DiagnosticChaosObserver());

// Add more observers dynamically
composite.AddObserver(myCustomObserver);

// Events forwarded to all registered observers
```

## Testing Scenarios

### Scenario 1: Test Deterministic Retry Logic

```csharp
[Fact]
public async Task Should_Succeed_After_3_Retries()
{
    // Fail first 3 attempts, succeed on 4th
    var policy = DeterministicPolicy.FailFirstN(3, 
        ExceptionFaultStrategy.Create<ProviderUnavailableException>("Chaos"));
    
    var options = new ChaosOptionsBuilder()
        .Enable()
        .WithDefaultPolicy(policy)
        .Build();
    
    var chaosProvider = new ChaosLeaseProviderV2(actualProvider, options);
    
    ILease? lease = null;
    int attempts = 0;
    
    while (lease == null && attempts < 5)
    {
        try
        {
            lease = await chaosProvider.AcquireLeaseAsync("test-lease", TimeSpan.FromMinutes(5));
        }
        catch (ProviderUnavailableException)
        {
            attempts++;
            await Task.Delay(100);
        }
    }
    
    Assert.NotNull(lease);
    Assert.Equal(3, attempts); // Exactly 3 retries
}
```

### Scenario 2: Test Renewal Failure Handling

```csharp
[Fact]
public async Task Should_Detect_Renewal_Failure()
{
    var renewPolicy = new ProbabilisticPolicy(1.0, // Always fail
        ExceptionFaultStrategy.Create<LeaseException>("Renewal failed"));
    
    var options = new ChaosOptionsBuilder()
        .Enable()
        .ConfigureOperation("RenewAsync", op => op
            .Enable()
            .WithPolicy(renewPolicy))
        .Build();
    
    var observer = new ConsoleChaosObserver();
    var chaosProvider = new ChaosLeaseProviderV2(actualProvider, options, observer);
    
    var lease = await chaosProvider.AcquireLeaseAsync("test", TimeSpan.FromSeconds(5));
    
    bool renewalFailed = false;
    lease.LeaseRenewalFailed += (sender, e) => renewalFailed = true;
    
    // Wait for auto-renewal attempt
    await Task.Delay(TimeSpan.FromSeconds(4));
    
    Assert.True(renewalFailed);
}
```

### Scenario 3: Test Timeout Handling with Latency

```csharp
[Fact]
public async Task Should_Timeout_On_High_Latency()
{
    var delayStrategy = new DelayFaultStrategy(
        TimeSpan.FromSeconds(5), 
        TimeSpan.FromSeconds(10));
    
    var policy = new ProbabilisticPolicy(1.0, delayStrategy);
    
    var options = new ChaosOptionsBuilder()
        .Enable()
        .WithDefaultPolicy(policy)
        .Build();
    
    var chaosProvider = new ChaosLeaseProviderV2(actualProvider, options);
    
    var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
    
    await Assert.ThrowsAsync<OperationCanceledException>(async () =>
    {
        await chaosProvider.AcquireLeaseAsync("test", TimeSpan.FromMinutes(5), cts.Token);
    });
}
```

### Scenario 4: Test First N Failures

```csharp
[Fact]
public async Task Should_Fail_First_5_Then_Succeed()
{
    var policy = ThresholdPolicy.FirstN(5, 
        ExceptionFaultStrategy.Create<LeaseException>("First 5 fail"));
    
    var options = new ChaosOptionsBuilder()
        .Enable()
        .WithDefaultPolicy(policy)
        .Build();
    
    var chaosProvider = new ChaosLeaseProviderV2(actualProvider, options);
    
    // First 5 should fail
    for (int i = 0; i < 5; i++)
    {
        await Assert.ThrowsAsync<LeaseException>(async () =>
        {
            await chaosProvider.AcquireLeaseAsync($"lease-{i}", TimeSpan.FromMinutes(1));
        });
    }
    
    // 6th should succeed
    var lease = await chaosProvider.AcquireLeaseAsync("lease-6", TimeSpan.FromMinutes(1));
    Assert.NotNull(lease);
}
```

## Configuration Validation

The framework includes comprehensive validation:

```csharp
var options = new ChaosOptionsBuilder()
    .Enable()
    .WithMaxFaultRate(-1.0) // INVALID: negative rate
    .Build(); // Throws ChaosConfigurationException

// Validation errors include:
// - Negative seeds
// - Invalid probability ranges (not 0.0-1.0)
// - Negative fault rates
// - Invalid time windows
// - Duplicate strategy names
// - Null policies/strategies
// - Invalid threshold ranges
```

Manual validation:

```csharp
var validator = new ChaosOptionsValidator();
var result = validator.Validate(options);

if (!result.IsValid)
{
    Console.WriteLine(result.GetValidationSummary());
    // Errors (2):
    //   - MaxFaultRate must be positive. Current value: -1
    //   - No default policy configured and no operation-specific policies defined
}
```

## Thread Safety

All components are thread-safe:
- **.NET 6.0+**: Uses `Random.Shared` (built-in thread-safe)
- **.NET Standard 2.0**: Uses `ThreadLocal<Random>` (thread-local instances)
- **Policy state**: Lock-based synchronization for deterministic/threshold policies
- **Observers**: Thread-safe collection management in composite observer

## Legacy API (Backward Compatibility)

The original `ChaosLeaseProvider` with `ChaosPolicy` is still available for backward compatibility:

```csharp
// Legacy API (still works, but limited features)
var chaosProvider = new ChaosLeaseProvider(actualProvider, new ChaosPolicy
{
    FailureRate = 0.1,
    MinDelay = TimeSpan.FromMilliseconds(100),
    MaxDelay = TimeSpan.FromSeconds(2),
    FaultTypes = ChaosFaultType.Delay | ChaosFaultType.Exception
});

// ⚠️ Limitations:
// - No per-operation configuration
// - No renew/release fault injection
// - No observability
// - No advanced policies (deterministic, threshold)
// - Thread safety improved but limited configurability
```

## Migration from Legacy API

### Old Code (Version 4.x)
```csharp
var chaosProvider = new ChaosLeaseProvider(actualProvider, new ChaosPolicy
{
    FailureRate = 0.2,
    MinDelay = TimeSpan.FromMilliseconds(100),
    MaxDelay = TimeSpan.FromSeconds(1),
    FaultTypes = ChaosFaultType.Delay | ChaosFaultType.Exception
});
```

### New Code (Version 5.x)
```csharp
var delayStrategy = new DelayFaultStrategy(
    TimeSpan.FromMilliseconds(100),
    TimeSpan.FromSeconds(1));

var exceptionStrategy = ExceptionFaultStrategy.Create<ProviderUnavailableException>(
    "Chaos fault injection");

var policy = new ProbabilisticPolicy(0.2, delayStrategy, exceptionStrategy);

var options = new ChaosOptionsBuilder()
    .Enable()
    .WithDefaultPolicy(policy)
    .Build();

var chaosProvider = new ChaosLeaseProviderV2(actualProvider, options);
```

### Benefits of Migration
- ✅ **Full lifecycle coverage** (Renew, Release)
- ✅ **Deterministic testing** (repeatable scenarios)
- ✅ **Observability** (see what's happening)
- ✅ **Per-operation config** (different chaos per operation)
- ✅ **Validation** (fail fast on config errors)
- ✅ **Thread safety** (proper random generation)

## Architecture

```
┌────────────────────────────────────────────────────────────┐
│                  ChaosLeaseProviderV2                       │
│  ┌──────────────────────────────────────────────────────┐  │
│  │ Configuration (ChaosOptions)                          │  │
│  │ • Global Policy                                       │  │
│  │ • Per-Operation Policies (Acquire, Renew, Release)   │  │
│  │ • Fault Strategies                                    │  │
│  │ • Observers                                           │  │
│  └──────────────────────────────────────────────────────┘  │
│                           │                                 │
│         ┌─────────────────┼─────────────────┐              │
│         ▼                 ▼                 ▼              │
│  ┌───────────┐     ┌──────────┐     ┌──────────────┐      │
│  │  Policy   │────▶│ Strategy │────▶│   Observer   │      │
│  │ Decision  │     │Execution │     │Notification  │      │
│  └───────────┘     └──────────┘     └──────────────┘      │
│         │                 │                                 │
│         └─────────────────┴────────────────┐               │
│                                             ▼               │
│                           ┌────────────────────────┐        │
│                           │   ChaosLease Wrapper   │        │
│                           │  (Renew/Release Chaos) │        │
│                           └────────────────────────┘        │
│                                       │                     │
│                                       ▼                     │
│                           ┌────────────────────────┐        │
│                           │   Actual ILease        │        │
│                           └────────────────────────┘        │
└────────────────────────────────────────────────────────────┘
```

## Best Practices

### 1. Use Only in Test Projects
```xml
<!-- ✅ Good - Only in test project -->
<ItemGroup>
  <PackageReference Include="DistributedLeasing.ChaosEngineering" Version="5.0.0" />
</ItemGroup>
```

### 2. Start with Deterministic Policies for Tests
```csharp
// ✅ Deterministic - reproducible
var policy = DeterministicPolicy.FailFirstN(3, exceptionStrategy);

// ❌ Probabilistic - flaky tests
var policy = new ProbabilisticPolicy(0.5, exceptionStrategy);
```

### 3. Use Threshold Policies for Time-Limited Chaos
```csharp
// Chaos only for first 30 seconds
var policy = ThresholdPolicy.ForDuration(TimeSpan.FromSeconds(30), delayStrategy);
```

### 4. Enable Observability for Debugging
```csharp
var observer = new ConsoleChaosObserver();
var chaosProvider = new ChaosLeaseProviderV2(actualProvider, options, observer);
```

### 5. Validate Configuration Early
```csharp
var options = new ChaosOptionsBuilder()
    .Enable()
    .WithFailFast(true) // Throws on invalid config
    .Build();
```

## Framework Compatibility

- **.NET Standard 2.0** - Compatible with .NET Framework 4.6.1+, .NET Core 2.0+
- **.NET 6.0** - Long-term support release
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
- [Design Document](../../.qoder/quests/chaos-engineering-review.md)
- [Implementation Progress](../../.qoder/quests/chaos-engineering-implementation-progress.md)

## License

MIT License - see [LICENSE](../../LICENSE) for details.
