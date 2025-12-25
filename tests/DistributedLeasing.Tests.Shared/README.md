# DistributedLeasing.Tests.Shared

## Overview

This is the **shared test infrastructure library** for the DistributedLeasing project. It provides reusable test fixtures, builders, helpers, and constants that enable consistent, production-grade testing across all test projects.

## Architecture

The library follows the **Test Fixture Pattern** and implements several design patterns to ensure DRY, SOLID, and maintainable test code.

### Key Components

#### 1. Test Fixtures (`Fixtures/`)

**Purpose**: Share expensive resources across multiple tests while ensuring proper isolation and cleanup.

- **`LoggingFixture`**: Captures and verifies log messages
  - Implements custom `TestLoggerProvider` for log capture
  - Supports filtering by log level and category
  - Thread-safe for concurrent tests
  
  ```csharp
  public class MyTests : IClassFixture<LoggingFixture>
  {
      private readonly LoggingFixture _loggingFixture;
      
      public MyTests(LoggingFixture loggingFixture)
      {
          _loggingFixture = loggingFixture;
      }
      
      [Fact]
      public void Test_LogsExpectedMessage()
      {
          var logger = _loggingFixture.CreateLogger<MyClass>();
          // ... test code ...
          _loggingFixture.AssertLogged(LogLevel.Information, "expected message");
      }
  }
  ```

- **`MockProviderFixture`**: Provides pre-configured `ILeaseProvider` mocks
  - Fluent API for common mock scenarios
  - Eliminates boilerplate mock setup
  
  ```csharp
  var fixture = new MockProviderFixture()
      .WithSuccessfulAcquisition(mockLease)
      .WithRenewalBehavior(times: 3, thenFail: true);
  ```

- **`TimeProviderFixture`**: Enables deterministic time-based testing
  - Controls time progression in tests
  - Prevents flaky tests due to timing issues
  
  ```csharp
  var fixture = new TimeProviderFixture();
  var now = fixture.UtcNow; // Frozen time
  fixture.Advance(TimeSpan.FromMinutes(5));
  ```

#### 2. Builders (`Builders/`)

**Purpose**: Fluent APIs for creating complex test objects with sensible defaults.

- **`LeaseOptionsBuilder`**: Creates `LeaseOptions` with preset configurations
  
  ```csharp
  // Using presets
  var options = LeaseOptionsBuilder.Default()
      .AsHighPerformance()
      .Build();
  
  // Custom configuration
  var options = LeaseOptionsBuilder.Default()
      .WithDuration(TimeSpan.FromSeconds(30))
      .WithAutoRenewal(interval: TimeSpan.FromSeconds(20))
      .Build();
  
  // Implicit conversion
  LeaseOptions options = LeaseOptionsBuilder.Default().AsHighPerformance();
  ```

**Preset Configurations**:
- `Default()`: Standard 60-second lease, no auto-renewal
- `AsHighPerformance()`: 15s duration, aggressive renewal (10s interval)
- `AsLongRunning()`: 120s duration, conservative renewal (90s interval)
- `AsMinimal()`: Shortest viable configuration for fast tests

#### 3. Test Helpers (`TestHelpers.cs`)

**Purpose**: Common utility methods for test operations.

```csharp
// Async assertion helpers
await TestHelpers.AssertThrowsAsync<LeaseException>(() => provider.AcquireLeaseAsync(...));

// Retry logic for flaky operations
var result = await TestHelpers.RetryAsync(() => networkOperation(), maxAttempts: 3);

// Deterministic delays
await TestHelpers.WaitForAsync(() => condition, timeout: TimeSpan.FromSeconds(5));
```

#### 4. Test Constants (`TestConstants.cs`)

**Purpose**: Centralized test values to eliminate magic numbers.

```csharp
public static class TestConstants
{
    public static class LeaseDurations
    {
        public static readonly TimeSpan Short = TimeSpan.FromSeconds(15);
        public static readonly TimeSpan Medium = TimeSpan.FromSeconds(60);
        public static readonly TimeSpan Long = TimeSpan.FromSeconds(120);
    }
    
    public static class Timeouts
    {
        public static readonly TimeSpan Fast = TimeSpan.FromSeconds(1);
        public static readonly TimeSpan Standard = TimeSpan.FromSeconds(5);
        public static readonly TimeSpan Extended = TimeSpan.FromSeconds(30);
    }
    
    public static class ConnectionStrings
    {
        public const string AzuriteDevStorage = "UseDevelopmentStorage=true";
        public const string LocalRedis = "localhost:6379";
    }
    
    public static class SafetyThresholds
    {
        public static readonly TimeSpan Aggressive = TimeSpan.FromSeconds(3);
        public static readonly TimeSpan Balanced = TimeSpan.FromSeconds(5);
        public static readonly TimeSpan Conservative = TimeSpan.FromSeconds(10);
    }
}
```

## Usage Examples

### Example 1: Testing with Logging Verification

```csharp
public class LeaseManagerTests : IClassFixture<LoggingFixture>
{
    private readonly LoggingFixture _loggingFixture;
    
    public LeaseManagerTests(LoggingFixture loggingFixture)
    {
        _loggingFixture = loggingFixture;
    }
    
    [Fact]
    public async Task AcquireLease_LogsSuccessfulAcquisition()
    {
        // Arrange
        var logger = _loggingFixture.CreateLogger<LeaseManager>();
        var provider = new MockProviderFixture()
            .WithSuccessfulAcquisition(mockLease)
            .Provider;
        var manager = new LeaseManager(provider, logger);
        
        // Act
        await manager.AcquireLeaseAsync("test-lease", TestConstants.LeaseDurations.Medium);
        
        // Assert
        _loggingFixture.AssertLogged(LogLevel.Information, "Lease acquired");
        _loggingFixture.AssertNoErrors();
    }
}
```

### Example 2: Testing with Builder Pattern

```csharp
[Fact]
public async Task AutoRenewal_WithHighPerformanceConfig_RenewsAggressively()
{
    // Arrange
    LeaseOptions options = LeaseOptionsBuilder.Default()
        .AsHighPerformance();
    
    var manager = new LeaseManager(provider, options);
    
    // Act
    var lease = await manager.AcquireLeaseAsync("high-perf-lease");
    
    // Assert
    options.AutoRenewInterval.Should().Be(TimeSpan.FromSeconds(10));
    options.AutoRenewSafetyThreshold.Should().Be(TestConstants.SafetyThresholds.Aggressive);
}
```

### Example 3: Testing with Mock Provider

```csharp
[Fact]
public async Task AcquireLease_WhenProviderFails_ThrowsException()
{
    // Arrange
    var mockFixture = new MockProviderFixture()
        .WithFailedAcquisition(new LeaseException("Provider unavailable"));
    
    var manager = new LeaseManager(mockFixture.Provider);
    
    // Act & Assert
    await Assert.ThrowsAsync<LeaseException>(
        () => manager.AcquireLeaseAsync("test-lease", TestConstants.LeaseDurations.Short));
    
    mockFixture.VerifyAcquisitionAttempted(Times.Once());
}
```

## Design Principles

### 1. **DRY (Don't Repeat Yourself)**
- Centralized test constants
- Reusable fixtures and builders
- Common assertion helpers

### 2. **SOLID Principles**
- **Single Responsibility**: Each fixture has one clear purpose
- **Open/Closed**: Builders are extensible through fluent methods
- **Liskov Substitution**: Mock fixtures implement real interfaces
- **Interface Segregation**: Fixtures expose minimal, focused APIs
- **Dependency Inversion**: Tests depend on abstractions (ILeaseProvider)

### 3. **Test Isolation**
- Fixtures use xUnit's lifecycle management
- Each test gets clean state
- Thread-safe for parallel execution

### 4. **Readability**
- Fluent APIs read like natural language
- Descriptive method and constant names
- Clear separation of Arrange-Act-Assert

## Best Practices

### ✅ DO

- Use `TestConstants` for all magic values
- Use builders for complex object creation
- Use fixtures for expensive resources (loggers, mocks)
- Clear logs between tests: `_loggingFixture.ClearLogs()`
- Verify mock interactions explicitly

### ❌ DON'T

- Hard-code durations or timeouts
- Create new loggers without `LoggingFixture`
- Manually create complex mock setups
- Share mutable state between tests
- Ignore disposal (use `IAsyncDisposable`)

## Coverage Strategy

The shared library supports the project's **90% code coverage target** through:

1. **Deterministic Testing**: `TimeProviderFixture` eliminates timing-based flakiness
2. **Comprehensive Logging**: `LoggingFixture` verifies all code paths emit expected logs
3. **Mock Behaviors**: `MockProviderFixture` enables testing error conditions
4. **Builder Presets**: Ensure consistent test coverage across scenarios

## Integration with CI/CD

The library is designed for use with:
- **Coverlet**: Code coverage collection
- **ReportGenerator**: HTML coverage reports
- **xUnit**: Parallel test execution
- **FluentAssertions**: Readable assertions

Coverage is configured in `/coverlet.runsettings` to:
- Exclude test projects from coverage
- Generate deterministic results
- Track both line and branch coverage
- Output XML and JSON for tooling integration

## Dependencies

```xml
<PackageReference Include="xunit" />
<PackageReference Include="Moq" />
<PackageReference Include="FluentAssertions" />
<PackageReference Include="Microsoft.Extensions.Logging" />
<PackageReference Include="Microsoft.Extensions.Logging.Abstractions" />
```

## Project Structure

```
DistributedLeasing.Tests.Shared/
├── Builders/
│   └── LeaseOptionsBuilder.cs          # Fluent builder for LeaseOptions
├── Fixtures/
│   ├── LoggingFixture.cs               # Log capture and verification
│   ├── MockProviderFixture.cs          # ILeaseProvider mocks
│   └── TimeProviderFixture.cs          # Deterministic time control
├── TestConstants.cs                     # Centralized test values
├── TestHelpers.cs                       # Common utility methods
└── README.md                            # This file
```

## Version Compatibility

- **.NET**: net10.0
- **C# Language Version**: latest
- **Nullable Reference Types**: Enabled

## Contributing

When adding new shared test infrastructure:

1. **Follow Naming Conventions**:
   - Fixtures: `*Fixture`
   - Builders: `*Builder`
   - Helpers: `*Helpers` or `*Helper`
   
2. **Document Public APIs**:
   - XML comments for all public members
   - Usage examples in this README
   
3. **Maintain Thread Safety**:
   - Use `ConcurrentBag`, `Interlocked`, etc.
   - Document thread-safety guarantees
   
4. **Write Tests**:
   - Even test infrastructure needs tests
   - Focus on contract verification

## Support

For questions or issues with the shared test library:
1. Check this README for usage patterns
2. Review existing test projects for examples
3. Refer to the design document: `/.qoder/quests/test-library-refactor.md`

---

**Last Updated**: 2025-12-25  
**Maintained By**: Platform Engineering Team
