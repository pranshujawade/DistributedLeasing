# Test Library Refactoring Design

## Executive Overview

This design outlines a comprehensive refactoring of the DistributedLeasing test library to achieve production-grade quality, maintainability, and coverage. The current test suite exhibits basic unit testing but lacks the architectural patterns, reusability constructs, and comprehensive coverage expected in enterprise-grade distributed systems.

### Core Objectives

- Establish a robust test infrastructure following industry-standard design patterns
- Achieve comprehensive test coverage with proper measurement and enforcement
- Eliminate code duplication through reusable test utilities and builders
- Implement integration testing for provider implementations
- Ensure observability components are thoroughly tested
- Apply SOLID, DRY, KISS, and YAGNI principles throughout

## Current State Analysis

### Existing Test Coverage

#### Abstractions Tests
- **LeaseBaseTests**: 14 tests covering basic lease lifecycle, validation, thread safety
- **LeaseManagerBaseTests**: 15 tests covering acquisition, retries, exponential backoff
- **AutoRenewalTests**: 9 tests covering auto-renewal events and lifecycle
- **LeaseOptionsTests**: 19 tests covering configuration validation
- **LeaseExceptionTests**: 13 tests covering exception hierarchy

#### Provider Tests
- **BlobLeaseProviderTests**: Minimal placeholder tests, mostly argument validation
- **BlobLeaseProviderOptionsTests**: 26 tests covering options validation and authentication
- **CosmosLeaseProviderTests**: 12 tests focusing on options validation
- **RedisLeaseProviderTests**: 15 tests covering argument validation and options

### Identified Gaps

#### Missing Test Coverage
1. **Authentication Components** - No tests for AuthenticationFactory, AuthenticationOptions validation, or credential creation flows
2. **Observability Layer** - No tests for LeaseHealthCheck, LeasingMetrics, LeasingActivitySource
3. **Provider Implementations** - Only validation tests exist; no behavior or integration tests
4. **Event System** - Limited event testing; missing edge cases and thread safety
5. **Extension Methods** - No tests for DI registration extensions
6. **Factory Classes** - No tests for LeaseManagerFactory implementations

#### Architectural Deficiencies
1. **No Test Fixtures** - Repeated object creation in every test method
2. **No Test Builders** - Difficult to create complex test scenarios
3. **No Test Base Classes** - Common setup code duplicated across test files
4. **No Integration Tests** - Provider implementations not tested against real backends
5. **Insufficient Mocking Strategy** - Mix of direct mocking and incomplete abstractions
6. **No Performance Tests** - No measurement of concurrent scenarios or throughput
7. **Limited Edge Case Coverage** - Missing boundary conditions, race conditions, failure scenarios

#### Code Quality Issues
1. **Magic Values** - Hardcoded timeouts, durations, and configuration values
2. **Test Interdependence** - Some tests rely on timing which may be flaky
3. **Inconsistent Patterns** - Mix of xUnit Assert and FluentAssertions
4. **Missing Test Categories** - No trait-based test organization for selective execution
5. **Code Duplication** - Similar test setup repeated across multiple test classes

## Target Architecture

### Test Project Structure

#### Shared Test Infrastructure
Create a new shared test project to centralize reusable components across all test projects.

**Project**: DistributedLeasing.Tests.Shared

Purpose: Provide common test utilities, builders, fixtures, and base classes to eliminate duplication and promote consistency.

Components:
- Test Builders using the Builder Pattern
- Test Fixtures using the Object Mother Pattern
- Base Test Classes for common setup and teardown
- Custom xUnit Attributes for test categorization
- Mock Factories for consistent mock object creation
- Test Data Generators for realistic scenarios
- Custom Assertions for domain-specific validations

#### Integration Test Projects
Create dedicated integration test projects for each provider implementation.

**Projects**:
- DistributedLeasing.Azure.Blob.IntegrationTests
- DistributedLeasing.Azure.Cosmos.IntegrationTests
- DistributedLeasing.Azure.Redis.IntegrationTests

Purpose: Test provider implementations against real or emulated backend services to validate end-to-end functionality.

Requirements:
- Use Docker containers or test containers for isolated test environments
- Implement setup and teardown for test infrastructure
- Mark with Integration trait for selective test execution
- Support both local development and CI pipeline execution
- Include performance benchmarks and stress tests

### Design Patterns for Test Library

#### Builder Pattern
Implement fluent builders for complex object creation to improve test readability and maintainability.

**Use Cases**:
- LeaseOptionsBuilder: Configure LeaseOptions with sensible defaults and fluent overrides
- LeaseBuilder: Create test ILease instances with configurable properties
- AuthenticationOptionsBuilder: Construct authentication configurations for various scenarios
- ProviderOptionsBuilder: Base builder for provider-specific options

**Benefits**:
- Reduces code duplication in test setup
- Improves test readability by focusing on relevant properties
- Enables method chaining for expressive test arrangement
- Provides immutable test objects to prevent test pollution

Example usage pattern:
Test creates lease with specific properties while using defaults for others, builder handles complexity of construction and validation.

#### Object Mother Pattern
Create factory methods that return commonly used test objects with predefined configurations.

**Use Cases**:
- TestLeases: Preconfigured lease instances for common scenarios (acquired, expired, auto-renewing)
- TestProviders: Mock provider instances with predefined behaviors
- TestOptions: Common configuration scenarios (default, high-performance, aggressive-retry)
- TestEvents: Event argument instances for testing event handlers

**Benefits**:
- Centralized test data management
- Consistent test objects across test suites
- Self-documenting test scenarios through named factory methods
- Easy updates when domain models evolve

Example usage pattern:
Test uses factory method to get a standard acquired lease, or expired lease, or lease about to expire, simplifying test arrangement.

#### Test Fixture Pattern (Primary Pattern)
Implement xUnit class fixtures and collection fixtures for expensive resource initialization. This is the foundational pattern for the test infrastructure.

**xUnit Fixture Types**:

**Class Fixture (IClassFixture<T>)**
Purpose: Share fixture instance across all tests in a single test class.
Lifecycle: Created before first test, disposed after last test in class.
Usage: When tests in a class need the same expensive resource.

**Collection Fixture (ICollectionFixture<T>)**
Purpose: Share fixture instance across multiple test classes.
Lifecycle: Created before first test in collection, disposed after last test.
Usage: When multiple test classes need shared infrastructure (database, container).

**Constructor Injection**
Purpose: Receive fresh instance per test method.
Lifecycle: Created before each test, disposed after each test.
Usage: When tests need isolated state.

**Fixture Implementations**:

**MockProviderFixture**
Shared mock provider instances across test classes with configurable behaviors.

Responsibilities:
- Create and configure Mock<ILeaseProvider> with standard behaviors
- Provide fluent API to reconfigure mock for specific test scenarios
- Reset mock state between tests while reusing mock instance
- Capture provider interactions for verification

Setup:
- Creates Mock<ILeaseProvider> once per test class
- Configures default successful acquisition behavior (returns valid lease)
- Configures default successful renewal behavior
- Configures default successful release behavior
- Sets up tracking of all provider calls

Configuration Methods:
- WithSuccessfulAcquisition: Configure to return specific lease
- WithFailedAcquisition: Configure to return null (lease held)
- WithAcquisitionException: Configure to throw specific exception
- WithSuccessfulRenewal: Configure successful renewal
- WithFailedRenewal: Configure renewal failure
- ResetToDefaults: Reset all configurations to initial state

Verification Methods:
- VerifyAcquisitionCalled: Assert acquisition was called with expected parameters
- VerifyRenewalCalled: Assert renewal was called expected number of times
- VerifyReleaseCalled: Assert release was called
- GetAcquisitionCalls: Retrieve all acquisition call details

Cleanup:
- Verify no unexpected calls were made
- Clear call history for next test
- Dispose of any created resources

Usage Example:
Test class implements IClassFixture<MockProviderFixture>, receives fixture in constructor, configures specific behavior in test method, executes test, verifies interactions through fixture methods.

**InMemoryStorageFixture**
Shared in-memory storage for fast provider tests without external dependencies.

Responsibilities:
- Provide thread-safe in-memory key-value store
- Simulate lease storage behavior (acquire, renew, release)
- Support concurrent access for testing race conditions
- Track all storage operations for verification

Capabilities:
- TryAcquire: Atomically acquire lease if not held
- Renew: Extend lease expiration if held by caller
- Release: Release lease if held by caller
- IsHeld: Check if lease is currently held
- GetHolder: Get current lease holder identifier
- Clear: Clear all leases for test isolation
- GetOperationHistory: Retrieve all operations for verification

Thread Safety:
- Uses ConcurrentDictionary for thread-safe storage
- Atomic compare-and-swap for lease acquisition
- Lock-free reads for query operations

Cleanup:
- Clear all leases between tests
- Verify no leaked resources

Usage Example:
Create FakeLeaseProvider backed by fixture storage, multiple tests can run in parallel against same fixture, each test uses unique lease names for isolation.

**LoggingFixture**
Shared test logger with assertion capabilities and structured log capture.

Responsibilities:
- Capture all log messages with level, category, timestamp, and structured data
- Provide query API for finding specific log entries
- Support assertion methods for log verification
- Thread-safe capture for parallel test execution

Setup:
- Creates ILoggerFactory with TestLoggerProvider
- Configures minimum log level (default: Debug)
- Registers structured log formatter
- Initializes thread-safe log collection

Capture:
- LogEntry structure: Level, Category, Message, Timestamp, Exception, State (structured data)
- Captures original message template and parameters
- Preserves exception details including stack traces
- Tags entries with test context for parallel test isolation

Query Methods:
- GetLogs: Retrieve all captured logs
- GetLogs(level): Filter by log level
- GetLogs(category): Filter by logger category
- GetLogs(predicate): Custom filtering
- ContainsLog(message): Check for log containing text
- GetStructuredData: Extract structured logging state

Assertion Methods:
- AssertLogged(level, message): Assert specific log was written
- AssertLoggedContaining(message): Assert log containing text exists
- AssertLogCount(count): Assert exact number of logs
- AssertNoErrors: Assert no error or critical logs
- AssertWarningLogged(message): Assert specific warning exists

Cleanup:
- Clear captured logs before each test
- Dispose logger factory after all tests

Usage Example:
Test class receives fixture via constructor, system under test receives ILogger from fixture factory, test executes operation, test asserts expected logs were written using fixture assertion methods.

**MetricsCollectorFixture**
Shared metrics collector for observability tests validating OpenTelemetry instrumentation.

Responsibilities:
- Subscribe to DistributedLeasing meter
- Capture all metric measurements (counters, histograms, gauges)
- Organize measurements by instrument name and tags
- Provide query and assertion API for metric verification

Setup:
- Creates MeterListener subscribed to "DistributedLeasing" meter
- Configures callbacks for each instrument type
- Initializes thread-safe measurement collections
- Starts listening before first test

Capture:
- Counter values: Instrument name, value, tags, timestamp
- Histogram measurements: Instrument name, value, tags, timestamp
- Gauge observations: Instrument name, value, tags, timestamp
- Tag normalization for consistent querying

Query Methods:
- GetCounterValue(name, tags): Get current counter value
- GetHistogramMeasurements(name, tags): Get all histogram values
- GetGaugeValue(name, tags): Get latest gauge observation
- GetAllMeasurements(name): Get all measurements for instrument
- GetMetricsByTag(tagKey, tagValue): Filter measurements by tag

Assertion Methods:
- AssertCounterIncremented(name, expectedValue, tags): Verify counter value
- AssertHistogramRecorded(name, tags): Verify histogram has measurements
- AssertGaugeObserved(name, expectedValue, tags): Verify gauge value
- AssertMetricHasTag(name, tagKey, tagValue): Verify tag presence
- AssertNoMetricsRecorded(name): Verify instrument was not used

Calculation Helpers:
- GetHistogramPercentile(name, percentile, tags): Calculate P50, P95, P99
- GetCounterDelta(name, tags): Get change since last query
- GetAverageHistogramValue(name, tags): Calculate average

Cleanup:
- Clear measurements between tests
- Stop listener after all tests

Usage Example:
Test receives fixture, executes lease operations, asserts specific metrics were recorded with correct values and tags.

**ActivityCollectorFixture**
Shared activity collector for distributed tracing tests validating OpenTelemetry activities.

Responsibilities:
- Subscribe to DistributedLeasing activity source
- Capture all started and stopped activities
- Maintain parent-child activity relationships
- Provide query and assertion API for activity verification

Setup:
- Creates ActivityListener subscribed to "DistributedLeasing" source
- Configures Sample callback to return AllDataAndRecorded
- Initializes activity collection with hierarchy tracking
- Starts listening before first test

Capture:
- Activity details: Operation name, span ID, trace ID, parent ID
- Tags: All tag key-value pairs
- Status: Status code and description
- Events: All activity events with timestamps
- Exceptions: Exception details if activity failed
- Duration: Activity start and stop times

Query Methods:
- GetActivities(operationName): Get all activities for operation
- GetActivityById(spanId): Get specific activity
- GetChildActivities(parentId): Get child activities
- GetRootActivities: Get activities without parents
- GetActivitiesWithTag(key, value): Filter by tag

Assertion Methods:
- AssertActivityStarted(operationName): Verify activity was created
- AssertActivityStopped(operationName): Verify activity completed
- AssertActivityStatus(operationName, status): Verify activity status
- AssertActivityHasTag(operationName, key, value): Verify tag presence
- AssertActivityHasException(operationName): Verify exception recorded
- AssertActivityParent(childName, parentName): Verify parent-child relationship

Hierarchy Analysis:
- GetActivityTree: Build hierarchical structure of activities
- VerifyActivityDepth(operationName, depth): Verify nesting level
- GetActivityPath(leafName): Get path from root to leaf

Cleanup:
- Clear captured activities between tests
- Stop listener after all tests

Usage Example:
Test receives fixture, executes lease operations that create activities, asserts activities were created with correct operation names, tags, and status.

**ContainerFixture (Integration Tests)**
Shared Docker container for integration tests with lifecycle management.

Responsibilities:
- Start Docker container before tests
- Wait for container health/readiness
- Provide connection details to tests
- Stop and clean up container after tests

Container Types:
- AzuriteFixture: Azure Storage emulator
- RedisFixture: Redis server
- CosmosDbFixture: Cosmos DB emulator or container

Setup:
- Create container with Testcontainers library
- Configure port mapping (random host port)
- Set environment variables
- Start container
- Wait for health check or readiness
- Expose connection string/endpoint to tests

Health Checks:
- Poll container until service responds
- Timeout after configured duration (default 60 seconds)
- Retry with exponential backoff

Connection Details:
- ConnectionString: Formatted connection string for provider
- Endpoint: URI endpoint for client creation
- Port: Host port mapped to container port
- ContainerId: Docker container ID for debugging

Cleanup:
- Stop container gracefully
- Remove container and volumes
- Clean up port mappings
- Log container output on failure for debugging

Usage Example:
Test collection uses collection fixture for container, all tests in collection share same container instance, each test uses unique resource names within container for isolation.

**TimeProviderFixture**
Shared controllable time provider for deterministic time-dependent testing.

Responsibilities:
- Provide controllable current time
- Allow programmatic time advancement
- Track time-based callbacks and timers
- Enable deterministic testing of auto-renewal and expiration

Capabilities:
- GetUtcNow: Return configured current time
- Advance(timespan): Move time forward by duration
- SetTime(datetime): Set absolute current time
- CreateTimer: Create virtual timer that fires based on virtual time
- GetPendingTimers: Retrieve all active timers

Timer Management:
- Track all created timers and callbacks
- Fire callbacks when virtual time advances past due time
- Support one-shot and periodic timers
- Maintain timer order for deterministic execution

Cleanup:
- Reset time to default
- Cancel all pending timers
- Clear timer callback history

Usage Example:
Test injects fixture as time provider, system under test uses fixture for time queries, test advances time to trigger expiration or renewal, test asserts expected behavior occurred.

**Fixture Composition and Organization**:

**Single Fixture per Test Class**
Use IClassFixture<T> when test class needs one expensive resource.
Fixture created once, shared across all tests in class.
Example: LeaseBaseTests using MockProviderFixture.

**Multiple Fixtures per Test Class**
Implement IClassFixture<T1>, IClassFixture<T2> for multiple resources.
Each fixture created independently.
Example: LeaseManagerTests using MockProviderFixture and LoggingFixture.

**Collection Fixtures for Shared Infrastructure**
Define collection with [CollectionDefinition] attribute.
Multiple test classes join collection with [Collection] attribute.
Fixture shared across all classes in collection.
Example: Integration test collection sharing ContainerFixture.

**Fixture Dependency Chain**
Fixtures can depend on other fixtures through constructor parameters.
xUnit creates dependency graph and instantiates in order.
Example: ProviderFixture depending on ContainerFixture for connection string.

**Benefits of Test Fixture Pattern**:
- Reduces test execution time by eliminating repeated expensive setup
- Ensures proper cleanup through IDisposable contract
- Provides isolation between test classes via collection fixtures
- Supports parallel test execution with proper resource management
- Centralizes complex setup logic in reusable components
- Enables realistic testing with shared infrastructure
- Improves test readability by hiding infrastructure details

### Practical Fixture Implementation Examples

#### Example 1: MockProviderFixture Implementation

Class Structure:
- Implements IDisposable for cleanup
- Contains Mock<ILeaseProvider> instance
- Provides fluent configuration API
- Tracks all provider interactions

Constructor Logic:
- Initialize Mock<ILeaseProvider>
- Setup default behaviors for all ILeaseProvider methods
- Initialize call tracking collections
- Configure mock to be lenient (no strict behavior enforcement)

Default Behaviors:
- AcquireLeaseAsync: Returns new mock lease with standard properties
- RenewLeaseAsync: Returns Task.CompletedTask
- ReleaseLeaseAsync: Returns Task.CompletedTask
- BreakLeaseAsync: Returns Task.CompletedTask

Fluent Configuration:
- Each configuration method returns this for chaining
- Configuration methods use mock.Setup() to override defaults
- Support for multiple scenarios per test method

Dispose Implementation:
- Verify all setups were called if configured
- Clear all setups for next usage
- Dispose of any created resources

Test Usage Pattern:
Test class declares IClassFixture<MockProviderFixture>, constructor receives fixture instance, test method calls fixture.WithFailedAcquisition(), creates LeaseManager with fixture.MockProvider, executes test, asserts expected behavior.

#### Example 2: LoggingFixture Implementation

Class Structure:
- Implements IDisposable
- Contains ILoggerFactory instance
- Contains TestLoggerProvider for capturing logs
- Thread-safe log storage using ConcurrentBag

Constructor Logic:
- Create LoggerFactory instance
- Create and register TestLoggerProvider
- Configure minimum log level from configuration or default to Debug
- Initialize thread-safe log collection

TestLoggerProvider Implementation:
- Implements ILoggerProvider
- Creates TestLogger instances
- Aggregates logs from all loggers
- Thread-safe log capture

TestLogger Implementation:
- Implements ILogger
- Captures log entries with level, message, exception, state
- Formats message with parameters
- Stores original message template for pattern matching

Log Entry Structure:
- LogLevel: The severity level
- Category: Logger category name
- Message: Formatted message string
- MessageTemplate: Original message template
- Timestamp: When log was written
- Exception: Exception object if present
- State: Structured logging state object
- Scopes: Active logging scopes

Assertion Methods Implementation:
- AssertLogged: Search captured logs for exact match
- AssertLoggedContaining: Search for partial message match
- AssertLogCount: Compare captured log count
- AssertNoErrors: Filter logs by level and assert count is zero

Query Methods Implementation:
- GetLogs: Return all or filtered logs
- Use LINQ for filtering by level, category, message pattern
- Return IReadOnlyList to prevent modification

Cleanup:
- Clear captured logs using thread-safe clear operation
- Dispose logger factory
- Dispose all created loggers

Test Usage Pattern:
Test class uses IClassFixture<LoggingFixture>, constructor receives fixture, system under test receives logger from fixture.CreateLogger<T>(), test executes operation, test calls fixture.AssertLogged(LogLevel.Information, expected message).

#### Example 3: ContainerFixture for Integration Tests

Class Structure:
- Implements IAsyncLifetime for async initialization
- Contains IContainer instance from Testcontainers
- Exposes connection details as properties
- Handles container lifecycle

InitializeAsync Logic:
- Create container builder with image name and version
- Configure port mappings (map container port to random host port)
- Set environment variables for container configuration
- Configure wait strategy for readiness (HTTP, port, log message)
- Build container instance
- Start container (async operation)
- Wait for container to be ready with timeout
- Retrieve connection details (host, port)
- Format connection string based on provider type
- Test connectivity before returning

Connection Details:
- For Azurite: UseDevelopmentStorage=true or custom connection string
- For Redis: host:port format
- For Cosmos: endpoint URL and master key

Wait Strategies:
- HTTP: Poll HTTP endpoint until 200 response
- Port: Wait until port is listening
- Log: Wait for specific log message in container output
- Timeout: Fail if not ready within configured duration

DisposeAsync Logic:
- Stop container gracefully
- Remove container and volumes
- Clean up network resources
- Log container output if test failed for debugging

Collection Definition:
- Define collection with [CollectionDefinition("Integration")]
- Implement ICollectionFixture<ContainerFixture>
- All integration test classes use [Collection("Integration")]

Test Usage Pattern:
Test class decorated with [Collection("Integration")], constructor receives ContainerFixture, test creates provider using fixture.ConnectionString, test executes operations against real container, test cleanup removes test data but leaves container running.

#### Example 4: Fixture Composition Pattern

Scenario: Integration test needs container, logging, and metrics.

Composite Fixture Structure:
- Contains ContainerFixture, LoggingFixture, MetricsCollectorFixture
- Implements IAsyncLifetime
- Delegates lifecycle to contained fixtures
- Exposes unified API combining all fixture capabilities

InitializeAsync:
- Initialize ContainerFixture first (async)
- Initialize LoggingFixture (sync)
- Initialize MetricsCollectorFixture (sync)
- Verify all fixtures ready

Properties:
- ConnectionString: Delegates to ContainerFixture
- LoggerFactory: Delegates to LoggingFixture  
- MetricsCollector: Delegates to MetricsCollectorFixture

Helper Methods:
- CreateProviderWithLogging: Creates provider with connection string and logger
- CreateProviderWithObservability: Creates provider with logging and metrics
- AssertOperationLogged: Combines logging and metrics assertions

DisposeAsync:
- Dispose fixtures in reverse order
- Ensure all cleanup completes even if one fails
- Aggregate cleanup exceptions

Test Usage Pattern:
Test class uses single composite fixture, receives all capabilities through one constructor parameter, accesses connection string, logger, and metrics through fixture properties, assertions combine logging and metrics validation.

### Fixture Testing Patterns

#### Pattern 1: Shared Fixture with Test Isolation

Problem: Multiple tests need same expensive fixture but must not interfere.

Solution:
- Use class or collection fixture for shared resource
- Each test uses unique identifiers (lease names, keys, container names)
- Fixture provides helper to generate unique identifiers
- Cleanup removes test-specific data, not entire fixture

Implementation:
- Fixture has GetUniqueLeaseId() method returning Guid-based string
- Fixture tracks created resources for cleanup verification
- Tests use unique identifiers for all operations
- Fixture.Dispose verifies no leaked resources

Example:
Multiple tests share RedisContainerFixture, each test uses uniqueId = fixture.GetUniqueId(), test creates lease with name = $"test-lease-{uniqueId}", test cleanup deletes key with that exact name, container remains running for next test.

#### Pattern 2: Fixture with Test Context

Problem: Fixture needs to know which test is executing for better diagnostics.

Solution:
- Fixture provides BeginTest(testName) method
- Returns IDisposable scope that ends on dispose
- Scopes tag captured logs, metrics, activities with test name
- Enables filtering fixture data by test

Implementation:
- Fixture maintains current test name in AsyncLocal<string>
- BeginTest creates TestScope that sets and clears test name
- Captured data includes test name in metadata
- Query methods filter by test name

Example:
Test begins with using var scope = fixture.BeginTest(nameof(TestMethod)), all logs captured during scope tagged with test name, test asserts logs using fixture.GetLogsForTest(nameof(TestMethod)), scope disposal clears test context.

#### Pattern 3: Fixture with State Reset

Problem: Fixture maintains state that must be reset between tests for isolation.

Solution:
- Fixture implements Reset() method
- Test constructor calls fixture.Reset() to clear state
- Reset is idempotent and fast (no teardown/setup)
- Fixture initialization remains shared

Implementation:
- Reset clears collections (logs, metrics, calls)
- Reset does not recreate expensive resources
- Reset validates fixture is in expected state
- Reset is thread-safe for parallel tests

Example:
MockProviderFixture.Reset() clears call history and mock setups, resets to default behaviors, verifies no outstanding expectations, test proceeds with clean state while keeping mock instance.

#### Pattern 4: Lazy Fixture Initialization

Problem: Fixture initialization is expensive but not all tests use all fixture capabilities.

Solution:
- Use Lazy<T> for expensive fixture components
- Initialize on first access
- Dispose only if initialized

Implementation:
- Fixture has Lazy<IContainer> for container
- Property getter returns Lazy.Value (triggers initialization)
- Dispose checks Lazy.IsValueCreated before disposing
- Reduces initialization time for tests not needing all features

Example:
ContainerFixture with Container property using Lazy<IContainer>, tests that only need mock provider don't trigger container start, tests that access fixture.Container automatically start container on first access, dispose only stops container if it was started.

#### Pattern 5: Fixture Inheritance Hierarchy

Problem: Multiple fixture types share common setup logic.

Solution:
- Create base fixture class with shared logic
- Derive specific fixtures extending base
- Base handles common concerns (logging, cleanup tracking)
- Derived classes focus on specific infrastructure

Implementation:
- BaseFixture implements IAsyncLifetime
- BaseFixture provides logging, unique ID generation, cleanup tracking
- ContainerFixture : BaseFixture adds container management
- ProviderFixture : BaseFixture adds provider creation

Example:
BaseFixture handles ILogger creation and unique ID generation, AzuriteFixture : BaseFixture starts Azurite container using base.Logger for diagnostics, RedisFixture : BaseFixture starts Redis container, both inherit cleanup tracking from base.

### Fixture-First Test Architecture

The test library will be built with fixtures as the primary architectural pattern, with builders and other patterns supporting the fixture infrastructure.

#### Fixture Catalog

All fixtures organized by purpose and usage:

**Core Test Fixtures (Unit Testing)**
- MockProviderFixture: Mock ILeaseProvider for unit tests
- InMemoryStorageFixture: Fast in-memory lease storage
- LoggingFixture: Test logger with assertions
- TimeProviderFixture: Controllable time for deterministic tests

**Observability Fixtures (Component Testing)**
- MetricsCollectorFixture: OpenTelemetry metrics capture
- ActivityCollectorFixture: OpenTelemetry tracing capture  
- HealthCheckFixture: Health check testing infrastructure

**Integration Test Fixtures**
- AzuriteFixture: Azure Storage emulator container
- RedisFixture: Redis server container
- CosmosDbFixture: Cosmos DB emulator/container
- ContainerCollectionFixture: Shared container across test classes

**Composite Fixtures (Full Stack Testing)**
- ObservabilityFixture: Combines logging, metrics, and tracing
- IntegrationFixture: Combines container, logging, and metrics
- ProviderFixture: Combines container and provider creation

#### Fixture Usage Guidelines

**When to Use Class Fixture**:
- Expensive resource needed by all tests in a class
- State can be safely shared (read-only or properly isolated)
- Setup time is significant (>100ms)
- Resource is thread-safe for parallel test execution

Examples:
- Unit test class using MockProviderFixture
- Observability test class using MetricsCollectorFixture
- Test class using LoggingFixture

**When to Use Collection Fixture**:
- Very expensive resource needed by multiple test classes
- Integration infrastructure (containers, databases)
- Resource takes >1 second to initialize
- Multiple test classes test same component

Examples:
- Integration test classes sharing AzuriteFixture
- Multiple provider test classes sharing RedisFixture
- All observability tests sharing ObservabilityFixture collection

**When to Use Constructor Injection**:
- Test needs fresh state for every test method
- Resource is cheap to create (<10ms)
- State must be isolated between tests
- Resource is not thread-safe

Examples:
- Fresh LeaseOptions per test using builder in constructor
- Fresh mock setup per test
- Test-specific configuration objects

**When NOT to Use Fixtures**:
- Simple value objects (use builders instead)
- Test data (use object mothers instead)
- One-time initialization (use test method setup)
- Test-specific configuration (use inline setup)

#### Fixture Lifecycle Management

**Initialization Order**:
1. Collection fixtures created first (before any test class in collection)
2. Class fixtures created (before first test in class)
3. Test constructor called (before each test method)
4. Test method executes

**Cleanup Order**:
1. Test method completes
2. Test class disposed (if implements IDisposable)
3. Class fixtures disposed (after last test in class)
4. Collection fixtures disposed (after last test in collection)

**Async Lifecycle**:
- Fixtures implementing IAsyncLifetime use async initialization
- InitializeAsync called before first usage
- DisposeAsync called during cleanup
- Supports async operations (container start, connection establishment)

**Parallel Execution**:
- Tests in same collection run serially (share collection fixture)
- Tests in different collections run in parallel
- Class fixtures are NOT shared across parallel test classes
- Use collection fixtures to force serial execution when needed

#### Fixture Error Handling

**Initialization Failures**:
- Fixture initialization failure fails all tests using fixture
- xUnit reports fixture initialization error with stack trace
- No tests in affected class/collection execute
- Cleanup still runs for any successfully initialized fixtures

Strategy:
- Implement graceful fallbacks (skip tests if infrastructure unavailable)
- Provide clear error messages indicating what failed
- Log initialization steps for debugging
- Validate fixture state before returning from initialization

**Cleanup Failures**:
- Cleanup exceptions logged but don't fail tests
- Multiple fixture cleanup exceptions aggregated
- Test results not affected by cleanup failures

Strategy:
- Make cleanup idempotent (safe to call multiple times)
- Catch and log cleanup exceptions rather than throwing
- Use finally blocks to ensure all cleanup attempts
- Verify cleanup in fixture dispose method

**Test Failures with Fixtures**:
- Fixture state may be invalid after test failure
- Subsequent tests may fail due to fixture corruption
- Collection fixtures particularly vulnerable

Strategy:
- Reset fixture state in test constructor (defensive)
- Use unique identifiers to isolate test data
- Implement fixture health checks
- Provide fixture.Reset() method for recovery

#### Custom Assertion Pattern

**Use Cases**:
- LeaseAssertions: Fluent assertions for lease state verification
- EventAssertions: Assertions for event raising and argument validation
- MetricsAssertions: Assertions for metric values and tags
- ActivityAssertions: Assertions for distributed tracing activities

**Benefits**:
- Improves test readability through domain language
- Provides detailed failure messages specific to domain concepts
- Reduces assertion boilerplate in test methods
- Centralizes assertion logic for consistency

#### Fake Implementation Pattern
Create lightweight fake implementations of interfaces for testing without mocking frameworks.

**Use Cases**:
- FakeLeaseProvider: In-memory lease provider for fast, isolated tests
- FakeTimeProvider: Controllable time source for testing time-dependent logic
- FakeMetricsCollector: Captures metrics for verification without external dependencies
- FakeActivitySource: Captures tracing activities for verification

**Benefits**:
- Faster test execution compared to mocking frameworks
- More realistic behavior than simple mocks
- Better support for testing sequences of operations
- Easier debugging through visible state

### Test Categories and Organization

#### Test Classification
Organize tests using xUnit traits for selective execution in different contexts.

**Categories**:

**Unit**
- Purpose: Fast, isolated tests of individual components
- Characteristics: No external dependencies, in-memory only, milliseconds execution
- Execution: Every build, pre-commit hooks, developer workstation

**Integration**
- Purpose: Tests validating interaction with external systems
- Characteristics: Requires infrastructure (containers, emulators), seconds to minutes execution
- Execution: CI pipeline, pre-release validation

**Performance**
- Purpose: Validates performance characteristics and identifies regressions
- Characteristics: Measures throughput, latency, resource usage
- Execution: Nightly builds, performance baseline validation

**Acceptance**
- Purpose: End-to-end validation of complete scenarios
- Characteristics: Multiple components, realistic data, complete workflows
- Execution: Pre-release, major version validation

**Component**
- Purpose: Tests specific to a component (Authentication, Observability, Events)
- Characteristics: Focused on single subsystem, may use real or fake dependencies
- Execution: Component-specific CI validation

Implementation approach:
Use xUnit Trait attribute to mark tests with categories, allowing selective execution via dotnet test filter.

### Comprehensive Test Coverage Strategy

#### Components Requiring New Tests

**Authentication Layer**

AuthenticationFactory:
- Credential creation for each authentication mode (ManagedIdentity, WorkloadIdentity, ServicePrincipal, FederatedCredential, Development, Auto)
- Configuration validation for each mode
- Error handling for invalid configurations
- Environment variable detection for Auto mode
- Production environment protection for Development mode
- ChainedTokenCredential construction for Auto mode
- Certificate file existence validation
- Token file existence validation

AuthenticationOptions:
- Validation rules for each authentication mode
- Mode-specific configuration requirements
- Mutual exclusivity of certain options
- Default value initialization

AuthenticationServiceExtensions:
- Dependency injection registration
- Options pattern integration
- Factory lifetime management

**Observability Layer**

LeaseHealthCheck:
- Successful health check when provider is responsive
- Degraded health when lease is held
- Degraded health when release fails but acquisition succeeds
- Unhealthy when provider throws exceptions
- Timeout handling and degraded state
- Proper disposal of acquired leases
- Health check data dictionary contents

LeasingMetrics:
- Counter increments for acquisitions, renewals, failures, leases lost
- Histogram recording for durations and retry attempts
- Observable gauge for active lease count
- Tag attachment for provider, lease name, result
- Active lease tracker thread safety
- Metric naming conventions

LeasingActivitySource:
- Activity creation for each operation type
- Tag attachment with semantic conventions
- Activity status setting for success and failure
- Parent-child activity relationships
- Exception recording in activities
- Activity ID propagation

**Event System**

Event Arguments:
- Proper initialization of event properties
- Immutability of event data
- Timestamp accuracy

Event Raising:
- Thread-safe event invocation
- Multiple subscriber handling
- Exception isolation between subscribers
- Event ordering guarantees
- Null subscriber handling

**Provider Implementations**

BlobLeaseProvider:
- Lease acquisition against Azure Blob Storage
- Lease renewal with proper timing
- Lease release and idempotency
- Container creation when configured
- Blob metadata handling
- Conflict resolution when lease is held
- Connection string and credential authentication paths
- Retry logic for transient failures
- Cancellation token propagation

CosmosLeaseProvider:
- Document-based lease acquisition with optimistic concurrency
- TTL-based lease expiration
- Partition key handling
- Database and container creation
- Conflict handling with HTTP 412 responses
- Throughput provisioning
- Connection modes and consistency levels

RedisLeaseProvider:
- SET NX EX pattern for lease acquisition
- Key expiration for lease duration
- GETDEL pattern for lease release
- Multiple Redis endpoints with failover
- Connection multiplexer management
- Key prefix handling
- Clock drift factor application

**Extension Methods**

Service Collection Extensions:
- Provider registration with options pattern
- Factory registration and lifetime
- Health check registration
- Metrics and tracing registration
- Configuration binding from IConfiguration
- Multiple provider registration scenarios

**Factory Classes**

BlobLeaseManagerFactory:
- Manager creation with proper options
- Provider initialization
- Authentication credential creation
- Blob client initialization

CosmosLeaseManagerFactory:
- CosmosClient creation from options
- Database and container initialization
- Partition key configuration

RedisLeaseManagerFactory:
- ConnectionMultiplexer creation
- Connection string parsing
- Endpoint configuration

#### Code Coverage Standards

**Target Coverage Levels**

Core Abstractions: 95% minimum
- Rationale: Foundational components used by all providers; defects have wide impact
- Scope: LeaseBase, LeaseManagerBase, LeaseOptions, all exceptions

Provider Implementations: 85% minimum
- Rationale: Provider-specific code may have untestable platform-specific paths
- Scope: All provider classes, provider-specific options

Observability Components: 90% minimum
- Rationale: Critical for production monitoring; failures invisible without proper instrumentation
- Scope: Metrics, health checks, activity sources

Authentication Components: 95% minimum
- Rationale: Security-critical; misconfigurations can cause production outages
- Scope: AuthenticationFactory, all credential creation paths

Configuration and Options: 100%
- Rationale: Simple validation logic; full coverage is achievable and necessary
- Scope: All Options classes, validation methods

**Coverage Exclusions**

Classes and members to exclude from coverage requirements using ExcludeFromCodeCoverage attribute:

- Generated code (AssemblyInfo, designer files)
- Program.cs and Startup.cs in sample applications
- Obsolete methods marked with ObsoleteAttribute
- Exception constructors for serialization (obsolete in modern .NET)
- Private nested types used for implementation details only
- Explicit interface implementations that delegate to other tested methods

**Coverage Calculation and Reporting**

Tooling:
- Primary: Coverlet for cross-platform code coverage collection
- Reporting: ReportGenerator for HTML and badge generation
- CI Integration: Coverlet collector for Azure Pipelines, GitHub Actions
- Format: OpenCover XML for tool interoperability

Configuration:
- Use coverlet.runsettings for consistent collection parameters
- Exclude test projects from coverage calculation
- Exclude compiler-generated code and external dependencies
- Include all source code directories
- Generate both line and branch coverage metrics

Reporting Requirements:
- HTML report with drill-down capability by project, namespace, class, method
- Summary statistics (total coverage, lines covered, branches covered)
- Trend tracking across builds to detect coverage regressions
- Badge generation for repository README
- Automatic upload to coverage tracking services (Codecov, Coveralls)

Coverage Gates:
- Fail builds if total coverage drops below threshold
- Fail pull requests if coverage delta is negative
- Allow coverage exemptions for specific files with documented justification
- Track coverage by project and enforce project-specific minimums

### Test Infrastructure Components

#### Shared Test Utilities

**TestConstants**
Purpose: Centralize magic values used across tests to ensure consistency and enable easy updates.

Contents:
- Common lease durations (short, medium, long)
- Timeout values for async test operations
- Retry intervals and max retry counts
- Safety thresholds and clock drift factors
- Connection strings for development and testing
- Common lease names for different scenarios

**TestHelpers**
Purpose: Provide utility methods for common test operations.

Methods:
- WaitForConditionAsync: Polls a condition with timeout and interval
- CreateCancellationTokenWithTimeout: Creates a token that cancels after specified duration
- AssertEventRaisedAsync: Waits for event to be raised within timeout
- GenerateUniqueLeaseId: Creates unique lease identifiers for test isolation
- SimulateDelayAsync: Controllable delay that works with virtual time providers

**TestLoggerProvider**
Purpose: Capture log messages during tests for verification and debugging.

Capabilities:
- Capture all log messages with level, category, and message
- Query captured logs by level, category, or message content
- Assert that specific logs were written
- Clear logs between tests
- Configure minimum log level
- Thread-safe log capture for parallel tests

**MockTimeProvider**
Purpose: Provide controllable time source for deterministic testing of time-dependent logic.

Capabilities:
- Return configurable current time
- Advance time programmatically
- Create timers that fire based on virtual time
- Track time-based operations for verification

#### Test Builders

**LeaseOptionsBuilder**

Default Configuration:
- Standard 60-second duration
- Auto-renew disabled
- Standard retry intervals
- Infinite acquire timeout

Fluent Methods:
- WithDuration: Set lease duration
- WithAutoRenew: Enable auto-renewal with optional interval
- WithAcquireTimeout: Set acquisition timeout
- WithRetryPolicy: Configure retry intervals and max retries
- WithSafetyThreshold: Set safety threshold percentage
- AsHighPerformance: Preset for low-latency scenarios
- AsResilient: Preset for aggressive retry scenarios

**AuthenticationOptionsBuilder**

Default Configuration:
- Auto mode with development credentials enabled

Fluent Methods:
- WithManagedIdentity: Configure system or user-assigned managed identity
- WithWorkloadIdentity: Configure Kubernetes workload identity
- WithServicePrincipal: Configure certificate or secret-based service principal
- WithFederatedCredential: Configure federated token authentication
- WithDevelopmentMode: Use development credential chain
- WithAutoMode: Use automatic credential detection
- DisableDevelopmentCredentials: Exclude development credentials from Auto chain

**ProviderOptionsBuilder (Base)**

Common Configuration:
- Connection settings (string, URI, credential)
- Metadata dictionary
- Creation flags

Provider-Specific Builders:
- BlobLeaseProviderOptionsBuilder: Container name, blob prefix, create container flag
- CosmosLeaseProviderOptionsBuilder: Database name, container name, partition key, TTL, throughput
- RedisLeaseProviderOptionsBuilder: Key prefix, database number, clock drift factor, minimum validity

**LeaseBuilder**

Default Configuration:
- Unique lease ID
- Generic lease name
- Acquired now
- 60-second duration

Fluent Methods:
- WithLeaseId: Set specific lease ID
- WithLeaseName: Set specific lease name
- AcquiredAt: Set acquisition timestamp
- WithDuration: Set lease duration
- ThatExpiredAt: Set expiration timestamp directly
- ThatIsExpired: Create already-expired lease
- ThatExpiresIn: Set relative expiration time
- WithAutoRenewal: Enable auto-renewal
- Build: Returns ILease implementation

#### Test Fixtures

**MockProviderFixture**

Purpose: Provide configured mock ILeaseProvider with common behaviors.

Setup:
- Creates Mock of ILeaseProvider
- Configures default successful lease acquisition
- Configures default successful lease renewal
- Configures default successful lease release
- Provides methods to reconfigure behaviors for specific tests

Cleanup:
- Verifies expected calls were made
- Resets mock for next test

**LoggingFixture**

Purpose: Provide ILogger with captured output for test verification.

Setup:
- Creates in-memory logger factory
- Registers TestLoggerProvider
- Exposes ILogger instances for injection

Capabilities:
- Retrieve captured logs
- Assert specific log messages were written
- Filter logs by level or category

Cleanup:
- Clears captured logs

**MetricsFixture**

Purpose: Provide metric collection and verification for observability tests.

Setup:
- Creates MeterListener subscribed to DistributedLeasing meter
- Captures all metric measurements
- Organizes measurements by instrument name

Capabilities:
- Retrieve counter values with tags
- Retrieve histogram measurements
- Retrieve gauge observations
- Assert on metric values and tags

Cleanup:
- Stops listener
- Clears captured measurements

**ActivityFixture**

Purpose: Provide activity collection and verification for tracing tests.

Setup:
- Creates ActivityListener subscribed to DistributedLeasing activity source
- Captures all started and stopped activities
- Maintains activity hierarchy

Capabilities:
- Retrieve activities by operation name
- Assert on activity tags and status
- Verify parent-child relationships
- Retrieve exception data from activities

Cleanup:
- Stops listener
- Clears captured activities

### Integration Testing Strategy

#### Test Environment Setup

**Containerized Infrastructure**

Approach: Use Testcontainers library to spin up isolated infrastructure for integration tests.

Required Containers:
- Azurite: Azure Storage emulator for Blob lease provider tests
- Azure Cosmos DB Emulator: For Cosmos lease provider tests (Windows) or Cosmos DB container (Linux)
- Redis: Redis container for Redis lease provider tests

Container Configuration:
- Ephemeral containers destroyed after test run
- Random port allocation to enable parallel test execution
- Health checks to ensure container readiness before tests
- Network isolation between test runs

Alternatives:
- Local emulators when containers unavailable
- Conditional test execution based on infrastructure availability
- Skip integration tests with clear messaging when infrastructure missing

**Test Data Management**

Strategy: Each test creates and cleans up its own test data to ensure isolation.

Principles:
- Unique resource names per test run to enable parallel execution
- Cleanup in finally blocks or IDisposable to handle test failures
- Separate containers/databases/key prefixes per test class using collection fixtures
- No shared test data between tests

#### Integration Test Scenarios

**Blob Provider Integration Tests**

Lease Lifecycle:
- Acquire lease successfully creates blob and obtains lease
- Acquired lease prevents concurrent acquisition
- Release lease allows subsequent acquisition
- Expired lease allows acquisition
- Lease renewal extends expiration

Concurrency:
- Multiple processes competing for same lease (only one succeeds)
- Lease holder maintains lease while others wait
- Failover when lease holder releases or expires

Authentication:
- Connection string authentication
- Managed identity authentication (with emulated token)
- Explicit credential authentication

Container Management:
- Create container if not exists flag creates container
- Existing container is reused
- Proper permissions on created container

Metadata:
- Custom metadata is attached to blob
- Metadata survives lease operations
- Metadata is retrievable

**Cosmos Provider Integration Tests**

Document Operations:
- Lease acquisition creates document with ETags
- Optimistic concurrency prevents conflicts
- TTL automatically expires old leases
- Partition key routing works correctly

Concurrency:
- Concurrent acquisitions use optimistic concurrency
- Conflict detection and resolution
- Last-write-wins behavior for renewals

Database Management:
- Create database if not exists
- Create container with proper partition key
- Provisioned throughput configuration

Query Performance:
- Point reads for lease acquisition (not scans)
- Proper indexing for lease queries

**Redis Provider Integration Tests**

Key Operations:
- SET NX EX atomically acquires lease
- Expiration automatically releases lease
- GETDEL atomically releases and clears lease
- Key prefix isolation

Concurrency:
- Multiple clients competing for same key
- Distributed locking semantics
- Redlock algorithm validation (if implemented)

Connection Management:
- Connection multiplexer reuse
- Failover to replica endpoints
- Connection timeout handling

Clock Drift:
- Clock drift factor reduces effective duration
- Minimum validity threshold enforcement

#### Performance Testing

**Throughput Tests**

Objective: Measure maximum acquisition and renewal throughput.

Scenarios:
- Serial acquisitions per second
- Parallel acquisitions with N concurrent tasks
- Renewal rate under steady load
- Mixed workload (acquisitions, renewals, releases)

Metrics:
- Operations per second
- P50, P95, P99 latency
- Error rate under load
- Resource utilization (memory, CPU, connections)

**Stress Tests**

Objective: Identify breaking points and failure modes.

Scenarios:
- Sustained high concurrency (100+ parallel workers)
- Memory leak detection over extended duration
- Connection pool exhaustion
- Backend throttling and backpressure
- Cascading failure scenarios

Metrics:
- Time to failure
- Failure mode characteristics
- Recovery behavior
- Resource leak detection

**Baseline Performance Metrics**

Establish performance baselines for regression detection.

Benchmarks:
- Blob provider: 50+ acquisitions/second, P95 latency under 100ms
- Cosmos provider: 100+ acquisitions/second, P95 latency under 50ms
- Redis provider: 500+ acquisitions/second, P95 latency under 20ms
- Auto-renewal: Renews within 10% of target interval
- Memory: No growth over 1000 lease lifecycle operations

Regression Detection:
- Fail builds if performance degrades by more than 20%
- Track performance trends across versions
- Report performance metrics to monitoring systems

### Anti-Patterns to Avoid

#### Test Design Anti-Patterns

**Fragile Tests**
Problem: Tests that fail due to timing issues, environmental differences, or other non-deterministic factors.

Avoidance:
- Use controllable time providers instead of real time
- Configure appropriate timeouts with buffer for CI environments
- Use polling with timeout instead of fixed delays
- Avoid file system dependencies; use in-memory alternatives
- Isolate tests from environmental variables

**Test Interdependence**
Problem: Tests that depend on execution order or shared state.

Avoidance:
- Each test creates its own test data
- Use collection fixtures for shared expensive resources, not shared mutable state
- Clear any static state in constructor or dispose
- Run tests in random order locally to detect dependencies

**Obscure Tests**
Problem: Tests that are difficult to understand or maintain.

Avoidance:
- Follow Arrange-Act-Assert pattern consistently
- Use descriptive test method names that explain the scenario
- Limit one logical assertion per test
- Use builders and object mothers to hide irrelevant details
- Document complex scenarios with comments

**Slow Tests**
Problem: Tests that take too long, discouraging frequent execution.

Avoidance:
- Use fakes instead of real infrastructure for unit tests
- Minimize use of Task.Delay; use virtual time when possible
- Parallelize independent tests using xUnit features
- Move slow tests to integration or nightly categories
- Profile slow tests and optimize or split them

**Test Code Duplication**
Problem: Copy-pasted test code that becomes maintenance burden.

Avoidance:
- Extract common setup to base classes or fixtures
- Use builders for object creation
- Create helper methods for common assertions
- Share test utilities across test projects

#### Mocking Anti-Patterns

**Over-Mocking**
Problem: Mocking too many dependencies makes tests brittle and coupled to implementation.

Avoidance:
- Mock only external dependencies (file system, network, time)
- Use real objects for value objects and simple classes
- Consider fakes instead of mocks for complex behaviors
- Don't mock the system under test

**Verification Overload**
Problem: Verifying every method call makes tests fragile.

Avoidance:
- Verify only meaningful interactions
- Focus on observable behavior, not implementation details
- Use state-based verification when possible
- Group related verifications

**Mock Leakage**
Problem: Mock setup leaking implementation details into tests.

Avoidance:
- Encapsulate mock setup in factory methods or fixtures
- Use object mothers for common mock configurations
- Keep mock setup close to where it's used
- Reset mocks between tests

### Testing Best Practices

#### SOLID Principles in Tests

**Single Responsibility**
Each test method verifies one specific behavior or scenario. Test classes group related scenarios for a single component.

**Open-Closed**
Test base classes and fixtures are designed for extension through inheritance or composition without modification.

**Liskov Substitution**
Test doubles (mocks, stubs, fakes) correctly implement interface contracts and can be substituted for real implementations.

**Interface Segregation**
Test builders and fixtures expose only the configuration methods relevant to specific scenarios, not a monolithic interface.

**Dependency Inversion**
Tests depend on abstractions (ILeaseProvider, ILogger) not concrete implementations, allowing flexible test configurations.

#### DRY (Don't Repeat Yourself)

Apply DRY to test infrastructure:
- Shared builders eliminate repeated object creation code
- Object mothers provide reusable test data
- Custom assertions centralize verification logic
- Base classes share common setup and teardown
- Extension methods reduce assertion boilerplate

Do not apply DRY to test bodies:
- Each test should be independently readable
- Some duplication is acceptable for clarity
- Favor explicit over clever in test code

#### KISS (Keep It Simple, Stupid)

Simplicity in test code:
- Straightforward Arrange-Act-Assert flow
- Minimal logic in tests (no loops, conditionals)
- Direct assertions without complex calculations
- Self-explanatory variable names
- One concept per test

#### YAGNI (You Aren't Gonna Need It)

Avoid over-engineering test infrastructure:
- Don't build generic frameworks; solve specific problems
- Add builders only when duplication becomes painful
- Create fixtures only for expensive resources
- Start simple; refactor when complexity emerges

#### Separation of Concerns

Clear separation in test organization:
- Unit tests separate from integration tests (different projects)
- Test infrastructure separate from test code (Shared project)
- Test data builders separate from test fixtures
- Mock setup separate from test execution
- Assertions separated by concern (state, behavior, events)

### Test Execution Strategy

#### Local Development

Developer workflow:
- Run unit tests on every build (fast feedback)
- Run integration tests before pushing (quality gate)
- Use test filtering to run specific categories
- Leverage IDE test runner for debugging
- Watch mode for continuous test execution during development

Performance targets:
- Unit test suite under 30 seconds total
- Individual unit tests under 1 second
- Integration test suite under 5 minutes
- Individual integration tests under 30 seconds

#### Continuous Integration

CI pipeline stages:

**Build Validation**
- Execute all unit tests
- Calculate code coverage
- Fail if coverage below threshold
- Fail if any test fails
- Duration target: Under 5 minutes

**Integration Validation**
- Spin up test containers
- Execute integration tests
- Collect integration coverage
- Tear down test containers
- Duration target: Under 15 minutes

**Nightly Validation**
- Execute performance tests
- Execute stress tests
- Generate performance trend reports
- Execute acceptance tests
- Duration target: Under 2 hours

Parallelization:
- Run test projects in parallel
- Run test collections in parallel within projects
- Limit parallelism for resource-intensive integration tests
- Use xUnit collection attributes to control parallelization

#### Pull Request Validation

Required checks before merge:
- All unit tests pass
- Code coverage does not decrease
- No new SonarQube issues
- Integration tests pass (can be optional for draft PRs)
- Performance tests pass (for performance-impacting changes)

Fast feedback:
- Run unit tests first (fail fast)
- Run integration tests only after unit tests pass
- Cache dependencies and build artifacts
- Provide clear failure messages with logs

### Refactoring Roadmap

#### Phase 1: Foundation (Week 1-2)

Objectives:
- Create shared test infrastructure project
- Implement core test builders and object mothers
- Establish base test classes
- Set up code coverage collection and reporting

Deliverables:
- DistributedLeasing.Tests.Shared project with builders, fixtures, helpers
- Updated coverlet.runsettings with proper exclusions
- Coverage reporting script generating HTML reports
- CI pipeline integration for coverage tracking

#### Phase 2: Abstractions Tests (Week 2-3)

Objectives:
- Complete test coverage for Authentication components
- Complete test coverage for Observability components
- Enhance existing tests with builders and fixtures
- Add missing edge cases and error scenarios

Deliverables:
- AuthenticationFactoryTests: All credential creation paths
- LeaseHealthCheckTests: All health states and edge cases
- LeasingMetricsTests: All metrics instruments and operations
- LeasingActivitySourceTests: All activity operations and tags
- EventSystemTests: Thread safety and multiple subscribers
- Enhanced LeaseBaseTests and LeaseManagerBaseTests using shared infrastructure

#### Phase 3: Provider Unit Tests (Week 3-4)

Objectives:
- Implement comprehensive unit tests for provider implementations
- Add mock-based tests for provider behaviors
- Test provider-specific logic and error handling

Deliverables:
- BlobLeaseProviderTests: Comprehensive behavior tests
- CosmosLeaseProviderTests: Document operations and concurrency
- RedisLeaseProviderTests: Key operations and connection management
- ProviderOptionsTests: Enhanced validation and configuration tests

#### Phase 4: Integration Tests (Week 4-5)

Objectives:
- Create integration test projects with containerized infrastructure
- Implement end-to-end provider tests
- Validate concurrent scenarios and failure modes

Deliverables:
- DistributedLeasing.Azure.Blob.IntegrationTests with Azurite
- DistributedLeasing.Azure.Cosmos.IntegrationTests with emulator
- DistributedLeasing.Azure.Redis.IntegrationTests with Redis container
- Test container setup and teardown infrastructure
- CI pipeline integration with conditional execution

#### Phase 5: Performance and Stress Tests (Week 5-6)

Objectives:
- Implement performance benchmarks
- Create stress test scenarios
- Establish baseline metrics
- Integrate with performance tracking

Deliverables:
- Performance test suite with throughput and latency measurements
- Stress test scenarios for breaking point identification
- Performance baseline documentation
- CI integration with regression detection

#### Phase 6: Documentation and Training (Week 6)

Objectives:
- Document test architecture and patterns
- Create developer guide for writing tests
- Update contributing guidelines
- Knowledge transfer sessions

Deliverables:
- Test architecture documentation
- Test writing guide with examples
- Updated CONTRIBUTING.md with test requirements
- Team training on test patterns and infrastructure

### Success Metrics

#### Quantitative Metrics

**Code Coverage**
- Overall coverage: Target 90% (currently estimated 40-50%)
- Core abstractions: Target 95%
- Provider implementations: Target 85%
- Observability components: Target 90%
- Authentication components: Target 95%

**Test Execution**
- Unit test suite: Under 30 seconds (currently ~10 seconds)
- Integration test suite: Under 5 minutes (new)
- Test count: 200+ total tests (currently ~70)
- Flaky test rate: Under 1%

**Maintainability**
- Code duplication in tests: Under 5%
- Average test length: Under 30 lines
- Test setup complexity: Maximum 3 levels of abstraction

#### Qualitative Metrics

**Developer Experience**
- New tests written using shared infrastructure: Target 90%
- Developer satisfaction with test tooling: Target 4/5
- Time to write new tests: Reduced by 50%
- Time to debug failing tests: Reduced by 40%

**Defect Detection**
- Bugs caught by tests before production: Target 95%
- Regression rate: Under 2% per release
- Severity 1 bugs in production: Zero tolerance

**CI/CD Efficiency**
- Build time: No increase despite 3x test count
- PR validation time: Under 20 minutes
- False positive rate: Under 2%

### Risk Mitigation

#### Technical Risks

**Risk: Integration tests flaky due to container startup**
Mitigation:
- Implement robust health checks with retries
- Use testcontainers wait strategies
- Increase timeout allowances in CI environments
- Fall back to skipping tests if infrastructure unavailable

**Risk: Performance tests affected by CI resource contention**
Mitigation:
- Use dedicated performance test agents
- Run performance tests during off-peak hours
- Establish wide tolerance bands for pass/fail
- Track trends rather than absolute values

**Risk: Code coverage gates blocking legitimate changes**
Mitigation:
- Establish per-project coverage minimums with documented rationale
- Allow coverage exemptions with PR approval from maintainers
- Measure coverage delta instead of absolute values for new code
- Provide clear guidance on when and how to request exemptions

**Risk: Test suite maintenance overhead becomes burdensome**
Mitigation:
- Regular refactoring sprints to reduce test code duplication
- Automated detection of duplicate test patterns
- Clear ownership and responsibilities for test infrastructure
- Ongoing training and documentation updates

#### Process Risks

**Risk: Developers bypass tests to move faster**
Mitigation:
- Make tests fast and easy to write with shared infrastructure
- Provide clear examples and templates
- Enforce test requirements through PR reviews
- Celebrate teams with high test coverage

**Risk: Integration tests too slow for regular execution**
Mitigation:
- Optimize test parallelization
- Cache container images
- Allow developers to skip integration tests locally with clear documentation
- Run integration tests only on PR validation, not every commit

**Risk: Team lacks knowledge of new test patterns**
Mitigation:
- Comprehensive documentation with examples
- Pair programming sessions
- Code review feedback on test code
- Regular knowledge sharing sessions

### Microsoft Best Practices Alignment

#### .NET Testing Guidance

Aligned with Microsoft recommendations:
- Use xUnit for modern .NET projects (Microsoft's current standard)
- Leverage Coverlet for code coverage (Microsoft-supported open source)
- Follow AAA (Arrange-Act-Assert) pattern consistently
- Use async/await properly in tests (avoid sync-over-async)
- Dispose resources properly (IDisposable, IAsyncDisposable)
- Use meaningful test names describing scenario and expected outcome

#### ASP.NET Core Testing Patterns

Following ASP.NET Core testing practices:
- Health check testing patterns for LeaseHealthCheck
- IOptions pattern testing for configuration validation
- Dependency injection testing for service registrations
- Extension method testing for service collection configuration

#### Azure SDK Testing Patterns

Aligned with Azure SDK guidelines:
- Mock Azure SDK clients using Moq
- Test authentication credential creation flows
- Validate proper client initialization
- Test retry policies and error handling
- Verify proper resource disposal

#### OpenTelemetry Testing Patterns

Following OpenTelemetry best practices:
- Test metric collection using MeterListener
- Test activity creation using ActivityListener
- Verify semantic conventions for tags and attributes
- Test proper instrumentation scope naming

### Code Coverage Exemption Policy

#### Exemption Criteria

Code may be exempted from coverage requirements if it meets one of the following criteria:

**Generated Code**
- AssemblyInfo files
- Designer-generated code
- Scaffolded code that is not modified
- Protobuf or gRPC generated stubs

**Obsolete Code**
- Methods marked with ObsoleteAttribute that are maintained for backward compatibility
- Code scheduled for removal in future versions

**Platform-Specific Code**
- Platform-specific code paths that cannot be tested on CI environment
- Code that requires specific hardware or OS features

**Defensive Code**
- Null checks in constructors injected by DI (guaranteed non-null)
- Exception handling for impossible conditions (defensive programming)
- Trivial property getters and setters with no logic

**External Integration**
- Code that requires licenses or credentials not available in CI
- Code that interacts with third-party services in untestable ways

#### Exemption Process

**Application**:
- Add ExcludeFromCodeCoverageAttribute to class or member
- Add XML documentation comment explaining exemption reason
- Document in coverage configuration file
- Reference justification in PR description

**Approval**:
- Requires approval from at least one maintainer
- Documented in architecture decision record
- Reviewed quarterly for relevance

**Review**:
- All exemptions reviewed during major version planning
- Exemptions removed when underlying reason addressed
- Exemption list published in repository documentation

### Tooling and Infrastructure

#### Required NuGet Packages

**Test Framework**
- xunit: 2.9.2 or later
- xunit.runner.visualstudio: 2.8.2 or later
- Microsoft.NET.Test.Sdk: 17.12.0 or later

**Assertion Libraries**
- FluentAssertions: 7.0.0 or later (fluent assertion syntax)
- Shouldly: Alternative assertion library for specific scenarios

**Mocking Frameworks**
- Moq: 4.20.72 or later (primary mocking framework)
- NSubstitute: Alternative for specific scenarios

**Code Coverage**
- coverlet.collector: 6.0.2 or later
- coverlet.msbuild: 6.0.2 or later (optional for local collection)

**Test Utilities**
- Testcontainers: Latest version for Docker container management
- Bogus: For generating realistic test data
- AutoFixture: For auto-generating test objects

**Reporting**
- ReportGenerator: 5.3.7 or later (coverage report generation)
- dotnet-reportgenerator-globaltool: Global tool installation

#### Development Environment Setup

**Required Tools**
- .NET 10.0 SDK or later
- Docker Desktop (for integration tests)
- Visual Studio 2022 or JetBrains Rider or VS Code with C# extension

**Optional Tools**
- NCrunch or dotCover for live test coverage in IDE
- ReportGenerator global tool for local coverage reports
- Azure Storage Emulator or Azurite
- Cosmos DB Emulator (Windows) or Cosmos DB Docker container
- Redis Docker container

**Configuration**
- coverlet.runsettings in solution root
- .editorconfig with test code conventions
- Directory.Build.props with shared test package versions

#### CI/CD Integration

**Build Pipeline Steps**

Stage: Build
- Restore NuGet packages
- Build solution in Release configuration
- Run static analysis (Roslyn analyzers, SonarQube)

Stage: Unit Test
- Execute unit tests with coverage collection
- Generate coverage report
- Upload coverage to Codecov/Coveralls
- Enforce coverage thresholds

Stage: Integration Test
- Start test containers (Azurite, Redis, Cosmos)
- Execute integration tests
- Stop and clean up test containers
- Collect integration test results

Stage: Package
- Create NuGet packages
- Sign assemblies
- Publish artifacts

**Coverage Badge**
- Generate coverage badge from report
- Update README.md with current coverage percentage
- Link to detailed coverage report

### Appendix: Reference Implementations

#### Example Test Using Builders

Scenario: Test lease acquisition with custom duration using builder pattern.

Arrangement demonstrates:
- Use of LeaseOptionsBuilder for clean configuration
- Mock provider setup using fixture
- Expected lease creation

Action:
- Call manager TryAcquireAsync with custom duration

Assertion:
- Verify lease acquired successfully
- Verify provider called with correct parameters
- Verify lease properties match expectations

Benefits:
- Highly readable test code
- Minimal setup complexity
- Easy to modify for similar scenarios

#### Example Integration Test

Scenario: Test Redis lease provider acquires and releases lease against real Redis.

Setup demonstrates:
- Testcontainer Redis initialization
- Wait for container readiness
- Provider creation with container connection

Execution:
- Acquire lease
- Verify lease is held
- Release lease
- Verify lease is released

Cleanup:
- Stop and remove container
- Dispose provider

Benefits:
- Validates real-world behavior
- Catches integration issues early
- Provides confidence in deployment

#### Example Custom Assertion

Scenario: Fluent assertion extension for verifying lease state.

Implementation demonstrates:
- Extension method on ILease
- Fluent API returning assertion object
- Rich failure messages

Usage:
- lease.Should().BeAcquired()
- lease.Should().BeExpired()
- lease.Should().HaveRenewalCount(3)

Benefits:
- Domain-specific language in tests
- Clear failure messages
- Reusable across test suite

### Conclusion

This comprehensive refactoring will transform the DistributedLeasing test suite from basic unit tests to a production-grade testing infrastructure that:

**Ensures Quality**
- Comprehensive coverage of all components with measurable targets
- Both unit and integration tests validating behavior
- Performance and stress testing preventing regressions
- Continuous monitoring of test health and coverage

**Enables Velocity**
- Shared test infrastructure reduces time to write tests
- Fast unit test feedback loop for developers
- Reliable CI/CD pipeline with appropriate gates
- Clear patterns and examples for consistency

**Promotes Maintainability**
- SOLID principles applied to test code
- DRY through builders, fixtures, and shared utilities
- Clear separation of concerns in test organization
- Comprehensive documentation and training

**Aligns with Industry Standards**
- Microsoft .NET testing best practices
- OpenTelemetry instrumentation testing patterns
- Azure SDK testing guidelines
- Modern testing frameworks and tooling

Implementing this design will require approximately 6 weeks of focused effort but will pay dividends through improved code quality, reduced defects, faster development cycles, and greater confidence in releases.