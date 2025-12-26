# Migration Guide: Chaos Engineering v4.x to v5.x

This guide helps you migrate from the legacy chaos engineering API (v4.x) to the new SOLID-compliant architecture (v5.x).

## Overview of Changes

### What's New in v5.x

- ✅ **SOLID Architecture** - Strategy, Policy, and Observer patterns
- ✅ **Full Lifecycle Coverage** - Fault injection for Renew and Release operations
- ✅ **Thread Safety** - Fixed Random generation issues
- ✅ **Configuration Validation** - Fail-fast on invalid configs
- ✅ **Observability** - Multiple observer implementations
- ✅ **Per-Operation Config** - Different chaos settings per operation
- ✅ **Deterministic Testing** - Reproducible test scenarios
- ✅ **Threshold Policies** - Count and time-based fault injection

### Breaking Changes

⚠️ **API Changes:**
- `ChaosPolicy` properties renamed and restructured into `ChaosOptions`
- Constructor signatures changed
- New required dependencies (strategies and policies)

⚠️ **Behavioral Changes:**
- Random number generation is now thread-safe
- Configuration validation runs by default
- Fault injection now covers all lease operations (including Renew/Release)

## Migration Checklist

- [ ] Update package reference to v5.x
- [ ] Replace `ChaosPolicy` with `ChaosOptionsBuilder`
- [ ] Convert probability settings to `ProbabilisticPolicy`
- [ ] Create fault strategies (Delay, Exception)
- [ ] Add observer for visibility (optional)
- [ ] Test with deterministic policies first
- [ ] Update to per-operation configuration (optional)
- [ ] Validate configuration builds successfully

## Step-by-Step Migration

### Step 1: Update Package Reference

```xml
<!-- Before (v4.x) -->
<PackageReference Include="DistributedLeasing.ChaosEngineering" Version="4.0.0" />

<!-- After (v5.x) -->
<PackageReference Include="DistributedLeasing.ChaosEngineering" Version="5.0.0" />
```

### Step 2: Add Using Statements

```csharp
// Add these new namespaces
using DistributedLeasing.ChaosEngineering.Configuration;
using DistributedLeasing.ChaosEngineering.Faults.Strategies;
using DistributedLeasing.ChaosEngineering.Lifecycle;
using DistributedLeasing.ChaosEngineering.Observability;
using DistributedLeasing.ChaosEngineering.Policies.Implementations;
```

### Step 3: Migrate Configuration

#### Before (v4.x):

```csharp
var chaosProvider = new ChaosLeaseProvider(actualProvider, new ChaosPolicy
{
    FailureRate = 0.2,                              // 20% failure rate
    MinDelay = TimeSpan.FromMilliseconds(100),      // Min latency
    MaxDelay = TimeSpan.FromSeconds(1),             // Max latency
    FaultTypes = ChaosFaultType.Delay | ChaosFaultType.Exception
});
```

#### After (v5.x):

```csharp
// 1. Create fault strategies
var delayStrategy = new DelayFaultStrategy(
    TimeSpan.FromMilliseconds(100),
    TimeSpan.FromSeconds(1));

var exceptionStrategy = ExceptionFaultStrategy.Create<ProviderUnavailableException>(
    "Chaos fault injection");

// 2. Create a policy
var policy = new ProbabilisticPolicy(0.2, delayStrategy, exceptionStrategy);

// 3. Build configuration
var options = new ChaosOptionsBuilder()
    .Enable()
    .WithDefaultPolicy(policy)
    .Build();

// 4. Create chaos provider
var chaosProvider = new ChaosLeaseProviderV2(actualProvider, options);
```

## Common Migration Scenarios

### Scenario 1: Simple Probabilistic Chaos

#### v4.x Code:
```csharp
var chaosProvider = new ChaosLeaseProvider(actualProvider, new ChaosPolicy
{
    FailureRate = 0.3,
    FaultTypes = ChaosFaultType.Exception
});
```

#### v5.x Equivalent:
```csharp
var strategy = ExceptionFaultStrategy.Create<ProviderUnavailableException>(
    "Chaos failure");
var policy = new ProbabilisticPolicy(0.3, strategy);
var options = new ChaosOptionsBuilder()
    .Enable()
    .WithDefaultPolicy(policy)
    .Build();
var chaosProvider = new ChaosLeaseProviderV2(actualProvider, options);
```

### Scenario 2: Latency Injection Only

#### v4.x Code:
```csharp
var chaosProvider = new ChaosLeaseProvider(actualProvider, new ChaosPolicy
{
    FailureRate = 0.5,
    MinDelay = TimeSpan.FromMilliseconds(200),
    MaxDelay = TimeSpan.FromMilliseconds(800),
    FaultTypes = ChaosFaultType.Delay
});
```

#### v5.x Equivalent:
```csharp
var delayStrategy = new DelayFaultStrategy(
    TimeSpan.FromMilliseconds(200),
    TimeSpan.FromMilliseconds(800));
var policy = new ProbabilisticPolicy(0.5, delayStrategy);
var options = new ChaosOptionsBuilder()
    .Enable()
    .WithDefaultPolicy(policy)
    .Build();
var chaosProvider = new ChaosLeaseProviderV2(actualProvider, options);
```

### Scenario 3: Deterministic Pattern

#### v4.x Code:
```csharp
var chaosProvider = new ChaosLeaseProvider(actualProvider, new ChaosPolicy
{
    FailurePattern = new[] { false, true, false, true } // Alternate
});
```

#### v5.x Equivalent:
```csharp
var strategy = ExceptionFaultStrategy.Create<LeaseException>("Pattern failure");
var policy = DeterministicPolicy.Alternate(strategy);
var options = new ChaosOptionsBuilder()
    .Enable()
    .WithDefaultPolicy(policy)
    .Build();
var chaosProvider = new ChaosLeaseProviderV2(actualProvider, options);
```

### Scenario 4: Combined Delay and Exception

#### v4.x Code:
```csharp
var chaosProvider = new ChaosLeaseProvider(actualProvider, new ChaosPolicy
{
    FailureRate = 0.2,
    MinDelay = TimeSpan.FromMilliseconds(100),
    MaxDelay = TimeSpan.FromSeconds(2),
    FaultTypes = ChaosFaultType.All
});
```

#### v5.x Equivalent:
```csharp
var delayStrategy = new DelayFaultStrategy(
    TimeSpan.FromMilliseconds(100),
    TimeSpan.FromSeconds(2));
var exceptionStrategy = ExceptionFaultStrategy.Create<ProviderUnavailableException>(
    "Chaos fault");
var timeoutStrategy = new TimeoutFaultStrategy(TimeSpan.FromSeconds(5));

var policy = new ProbabilisticPolicy(0.2, 
    delayStrategy, exceptionStrategy, timeoutStrategy);

var options = new ChaosOptionsBuilder()
    .Enable()
    .WithDefaultPolicy(policy)
    .Build();

var chaosProvider = new ChaosLeaseProviderV2(actualProvider, options);
```

## New Capabilities in v5.x

### Feature 1: Per-Operation Configuration

Now you can configure different chaos for each operation:

```csharp
var options = new ChaosOptionsBuilder()
    .Enable()
    // Acquire: 20% exception failures
    .ConfigureOperation("AcquireAsync", op => op
        .Enable()
        .WithPolicy(new ProbabilisticPolicy(0.2, exceptionStrategy)))
    // Renew: Delay on first 5 renewals (NEW in v5.x!)
    .ConfigureOperation("RenewAsync", op => op
        .Enable()
        .WithPolicy(ThresholdPolicy.FirstN(5, delayStrategy)))
    // Release: No chaos
    .ConfigureOperation("ReleaseAsync", op => op
        .Disable())
    .Build();
```

### Feature 2: Observability

Add visibility into chaos events:

```csharp
var observer = new ConsoleChaosObserver(useColors: true);
var chaosProvider = new ChaosLeaseProviderV2(actualProvider, options, observer);

// Now you'll see colorful output:
// [INJECT] Decision by 'ProbabilisticPolicy' for AcquireAsync
// [EXECUTING] Fault 'DelayFaultStrategy' (Severity: Low)
// [EXECUTED] Fault completed in 502ms
```

### Feature 3: Deterministic Testing

Create reproducible test scenarios:

```csharp
// Fail first 3 attempts, then succeed
var policy = DeterministicPolicy.FailFirstN(3, exceptionStrategy);

// Fail every 5th attempt
var policy = DeterministicPolicy.FailEveryN(5, exceptionStrategy);

// Custom sequence
var sequence = new List<bool> { true, true, false, false };
var policy = new DeterministicPolicy(sequence, exceptionStrategy);
```

### Feature 4: Threshold Policies

Limit chaos by count or time:

```csharp
// Only first 10 operations
var policy = ThresholdPolicy.FirstN(10, delayStrategy);

// Only after 100 operations
var policy = ThresholdPolicy.AfterN(100, exceptionStrategy);

// Only for 30 seconds
var policy = ThresholdPolicy.ForDuration(TimeSpan.FromSeconds(30), delayStrategy);

// Only during business hours (UTC)
var policy = ThresholdPolicy.BetweenTimes(
    new TimeSpan(9, 0, 0),   // 9 AM
    new TimeSpan(17, 0, 0),  // 5 PM
    delayStrategy);
```

### Feature 5: Renewal and Release Testing

Now you can test auto-renewal failures:

```csharp
var renewPolicy = new ProbabilisticPolicy(1.0, exceptionStrategy); // Always fail

var options = new ChaosOptionsBuilder()
    .Enable()
    .ConfigureOperation("RenewAsync", op => op
        .Enable()
        .WithPolicy(renewPolicy))
    .Build();

var chaosProvider = new ChaosLeaseProviderV2(actualProvider, options);
var lease = await chaosProvider.AcquireLeaseAsync("test", TimeSpan.FromSeconds(5));

// Listen for renewal failure
lease.LeaseRenewalFailed += (sender, e) => {
    Console.WriteLine("Renewal failed as expected!");
};
```

## Property Mapping Reference

| v4.x ChaosPolicy Property | v5.x Equivalent |
|---------------------------|-----------------|
| `FailureRate` | `ProbabilisticPolicy(rate, strategies)` |
| `MinDelay` | `DelayFaultStrategy(minDelay, maxDelay)` first parameter |
| `MaxDelay` | `DelayFaultStrategy(minDelay, maxDelay)` second parameter |
| `FaultTypes` | Create multiple `IFaultStrategy` instances |
| `FailurePattern` | `DeterministicPolicy(sequence, strategy)` |
| `ExceptionType` | `ExceptionFaultStrategy.Create<TException>()` |
| `FailureMessage` | `ExceptionFaultStrategy.Create<T>(message)` |
| N/A | `ChaosOptions.Enabled` - new global enable/disable |
| N/A | `ChaosOptions.ProviderName` - new telemetry tagging |
| N/A | `ChaosOptions.OperationOptions` - new per-operation config |

## Troubleshooting Migration Issues

### Issue: "ChaosPolicy does not exist"

**Solution**: You're referencing the old API. Update to v5.x syntax:

```csharp
// Remove this
using ChaosPolicy;

// Use this instead
using DistributedLeasing.ChaosEngineering.Configuration;
```

### Issue: "No implicit conversion from ChaosPolicy to ChaosOptions"

**Solution**: Use `ChaosOptionsBuilder`:

```csharp
// Old (won't compile)
var options = new ChaosPolicy { ... };

// New
var options = new ChaosOptionsBuilder()
    .Enable()
    .WithDefaultPolicy(policy)
    .Build();
```

### Issue: "Cannot resolve type IFaultStrategy"

**Solution**: Add using statement:

```csharp
using DistributedLeasing.ChaosEngineering.Faults.Strategies;
```

### Issue: "Configuration validation fails"

**Solution**: Check validation errors:

```csharp
try
{
    var options = builder.Build(); // Validation happens here
}
catch (ChaosConfigurationException ex)
{
    Console.WriteLine(ex.Message);
    // Fix issues mentioned in the message
}
```

### Issue: "Random behavior different from v4.x"

**Explanation**: v5.x uses thread-safe Random generation, which may produce different sequences. This is a **fix**, not a bug. The v4.x behavior was incorrect for multi-threaded scenarios.

**Solution**: If you need reproducible tests, use deterministic policies:

```csharp
// Instead of probabilistic
var policy = new ProbabilisticPolicy(0.5, strategy);

// Use deterministic
var policy = DeterministicPolicy.FailFirstN(5, strategy);
```

## Testing Your Migration

### Test 1: Verify Basic Functionality

```csharp
[Fact]
public async Task Migration_Test_Basic_Chaos_Works()
{
    var strategy = ExceptionFaultStrategy.Create<LeaseException>("Test");
    var policy = new ProbabilisticPolicy(1.0, strategy); // 100% failure
    var options = new ChaosOptionsBuilder()
        .Enable()
        .WithDefaultPolicy(policy)
        .Build();
    
    var chaosProvider = new ChaosLeaseProviderV2(actualProvider, options);
    
    await Assert.ThrowsAsync<LeaseException>(async () =>
    {
        await chaosProvider.AcquireLeaseAsync("test", TimeSpan.FromMinutes(1));
    });
}
```

### Test 2: Verify Configuration Validation

```csharp
[Fact]
public void Migration_Test_Validation_Works()
{
    var builder = new ChaosOptionsBuilder()
        .Enable()
        .WithMaxFaultRate(-1.0); // Invalid
    
    Assert.Throws<ChaosConfigurationException>(() => builder.Build());
}
```

### Test 3: Verify Observability

```csharp
[Fact]
public async Task Migration_Test_Observer_Works()
{
    var observer = new ConsoleChaosObserver();
    var strategy = new DelayFaultStrategy(TimeSpan.FromMilliseconds(100));
    var policy = new ProbabilisticPolicy(1.0, strategy);
    var options = new ChaosOptionsBuilder()
        .Enable()
        .WithDefaultPolicy(policy)
        .Build();
    
    var chaosProvider = new ChaosLeaseProviderV2(actualProvider, options, observer);
    
    // Should see console output
    await chaosProvider.AcquireLeaseAsync("test", TimeSpan.FromMinutes(1));
}
```

## Gradual Migration Strategy

If you have many tests using the old API, migrate gradually:

### Phase 1: Install v5.x (Week 1)
- Update package reference
- Verify old code still compiles (legacy API retained)
- Run existing tests to ensure compatibility

### Phase 2: Migrate Critical Tests (Week 2)
- Migrate highest-priority integration tests
- Add observability to see what's happening
- Use deterministic policies for stability

### Phase 3: Migrate Remaining Tests (Week 3)
- Convert remaining tests to new API
- Take advantage of per-operation configuration
- Add threshold policies where appropriate

### Phase 4: Remove Legacy Usage (Week 4)
- Deprecate old `ChaosLeaseProvider` usage in your codebase
- Update documentation
- Remove old using statements

## Benefits of Migrating

✅ **Better Architecture** - SOLID principles throughout  
✅ **More Testing Capabilities** - Deterministic and threshold policies  
✅ **Full Coverage** - Test renewal and release operations  
✅ **Thread Safety** - Proper random generation  
✅ **Observability** - See what chaos is doing  
✅ **Validation** - Fail fast on config errors  
✅ **Flexibility** - Per-operation configuration  
✅ **Future Proof** - Extensible architecture for new features  

## Getting Help

- **README**: See updated [README.md](README.md) for detailed examples
- **Sample**: Run the [ChaosSample](../../samples/ChaosSample) project
- **Design Doc**: Read [chaos-engineering-review.md](../../.qoder/quests/chaos-engineering-review.md)
- **Issues**: Report problems on GitHub

## Summary

The v5.x API is more verbose but provides significantly more power and flexibility. The migration requires updating configuration code but the benefits (thread safety, full lifecycle coverage, observability, deterministic testing) make it worthwhile.

**Recommended migration path**: Start with simple probabilistic policies, add observability, then explore advanced features like deterministic testing and per-operation configuration.
