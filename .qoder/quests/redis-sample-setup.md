# Redis Sample Setup and Enhanced Resource Provisioning Script

## Overview

This design defines the creation of a complete Redis-based distributed leasing sample application and an enhanced Azure resource provisioning script that supports multiple backend providers (Blob Storage, Cosmos DB, and Redis). The solution enables users to select which provider to set up, and automatically generates provider-specific configuration files with proper authentication settings.

## Objectives

1. Create a RedisSample application mirroring the structure and functionality of existing BlobLeaseSample and CosmosLeaseSample
2. Enhance the setup-resources.sh script to accept provider selection arguments
3. Generate provider-specific appsettings.Local.json files with appropriate connection details
4. Create comprehensive README documentation for the Redis sample
5. Ensure end-to-end testing capability across all three providers
6. Maintain idempotent resource creation patterns

## Project Structure

### New Redis Sample Directory

Location: `/samples/RedisLeaseSample/`

Files to create:
- Program.cs - Main application entry point
- DistributedLockWorker.cs - Lock competition and execution logic
- ConfigurationHelper.cs - Interactive configuration wizard
- ColoredConsoleLogger.cs - Enhanced console output
- RedisMetadataInspector.cs - Redis state inspection utility
- appsettings.json - Configuration template with defaults
- appsettings.Development.json - Development-specific settings
- RedisLeaseSample.csproj - Project file with dependencies
- README.md - Comprehensive documentation
- run-competition-demo.sh - Interactive demo script
- run-demo.sh - Automated dual-instance launcher

### Enhanced Setup Script

Location: `/scripts/setup-resources.sh`

Modified to support:
- Provider selection via command-line arguments
- Redis Cache provisioning in Azure
- Conditional resource creation based on selected provider
- Provider-specific configuration file generation

## Component Design

### Redis Sample Application

#### Application Architecture

The Redis sample follows the established pattern from Blob and Cosmos samples with provider-specific adaptations:

```
Application Entry (Program.cs)
    ↓
Configuration Loading & Validation
    ↓
Dependency Injection Setup
    ├─ RedisLeaseManager (via AddRedisLeaseManager)
    ├─ DistributedLockWorker
    ├─ RedisMetadataInspector
    └─ ColoredConsoleLogger
    ↓
Lock Acquisition Competition
    ↓
Critical Work Execution (Winner)
    ↓
Clean Release & Shutdown
```

#### Program.cs Structure

Key responsibilities:
- Parse command-line arguments for instance identification (--instance, --region)
- Check for appsettings.Local.json existence
- Trigger interactive configuration setup if needed
- Configure host with proper configuration sources
- Register Redis lease manager with connection settings
- Set up metadata inspector for Redis state inspection
- Configure colored console logging
- Execute distributed lock worker
- Handle graceful shutdown on Ctrl+C

Configuration binding approach:
- Bind entire "RedisLeasing" configuration section to RedisLeaseProviderOptions
- Include instance metadata (instanceId, region, hostname, startTime)
- Support both ConnectionString and Endpoint authentication modes

#### DistributedLockWorker.cs Logic

Core functionality:
- Attempt non-blocking lock acquisition via TryAcquireAsync
- Log success or failure with color-coded output
- Execute simulated critical work for 15 seconds if lock acquired
- Monitor lock renewal events and status
- Display progress updates every 3 seconds
- Check lock validity during execution
- Release lock gracefully on completion or cancellation
- Inspect and display current lock holder information on failure

Work execution characteristics:
- Duration: 15 seconds
- Progress interval: 3 seconds
- Auto-renewal: Enabled via configuration
- Renewal tracking: Count displayed in completion message

#### ConfigurationHelper.cs Functionality

Interactive setup wizard flow:
1. Display welcome banner
2. Prompt for Redis cache name (hostname validation)
3. Prompt for key prefix (default: "lease:")
4. Prompt for database number (0-15, default: 0)
5. Prompt for authentication mode:
   - Option 1: Connection String (with access key)
   - Option 2: DefaultAzureCredential (requires Azure CLI login)
6. Validate Azure CLI authentication if Option 2 selected
7. Generate appsettings.Local.json with appropriate settings
8. Confirm successful configuration

Validation logic:
- Redis hostname format: lowercase letters, numbers, hyphens, dots
- Database number: 0-15 range
- Connection string format: Contains required parameters
- Azure CLI authentication: Check logged-in status

#### RedisMetadataInspector.cs Design

Purpose: Inspect Redis key state and metadata for debugging and visibility

Capabilities:
- Connect to Redis database using provided connection
- Retrieve key value and TTL (time-to-live)
- Decode stored lease information (owner, expiration, metadata)
- Display formatted lease state information
- Handle connection failures gracefully

Key inspection operations:
- GET operation: Retrieve lease data
- TTL operation: Check remaining lease duration
- Parse JSON: Extract owner and metadata fields
- Format output: Present readable lease state

#### appsettings.json Configuration Template

Configuration structure:

```
{
  "RedisLeasing": {
    "KeyPrefix": "lease:",
    "Database": 0,
    "UseSsl": true,
    "Port": 6380,
    "DefaultLeaseDuration": "00:00:30",
    "AutoRenew": true,
    "AutoRenewInterval": "00:00:20",
    "ConnectTimeout": 5000,
    "SyncTimeout": 5000,
    "CreateContainerIfNotExists": false
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "DistributedLeasing": "Debug",
      "RedisLeaseSample": "Debug"
    }
  }
}
```

Placeholder indicators: No placeholders in template (relies on appsettings.Local.json)

#### appsettings.Local.json Generation Patterns

**Connection String Mode:**
```
{
  "RedisLeasing": {
    "ConnectionString": "mycache.redis.cache.windows.net:6380,password=<access-key>,ssl=True"
  }
}
```

**Managed Identity Mode:**
```
{
  "RedisLeasing": {
    "Endpoint": "mycache.redis.cache.windows.net:6380"
  }
}
```

Authentication resolution order:
1. ConnectionString (if provided)
2. Endpoint with DefaultAzureCredential (if ConnectionString absent)
3. Configuration error if neither provided

### Enhanced Setup Script Design

#### Script Arguments

New argument structure:

| Argument | Values | Description | Default |
|----------|--------|-------------|---------|
| --project | blob, cosmos, redis, all | Provider to set up | all |
| --storage-account | string | Blob storage account name | pranshublobdist |
| --cosmos-account | string | Cosmos DB account name | pranshucosmosdist |
| --redis-cache | string | Redis cache name | pranshuredisdist |
| --location | string | Azure region | eastus |
| --resource-group | string | Resource group name | pranshu-rg |

Backward compatibility: Script maintains existing --resource-type argument as alias for --project

#### Provider-Specific Resource Creation

**Blob Storage Resources (when --project blob or all):**
- Resource group (if not exists)
- Storage account with Standard_LRS SKU
- Blob container named "leases"
- Generate BlobLeaseSample/appsettings.Local.json with ConnectionString

**Cosmos DB Resources (when --project cosmos or all):**
- Resource group (if not exists)
- Cosmos DB account with Session consistency
- SQL database named "DistributedLeasing"
- Container with partition key "/id" and 400 RU/s throughput
- Container configured with 300 second TTL for automatic cleanup
- Generate CosmosLeaseSample/appsettings.Local.json with ConnectionString

**Redis Cache Resources (when --project redis or all):**
- Resource group (if not exists)
- Azure Cache for Redis (Basic tier, C0 size for development)
- Enable non-SSL port for development flexibility (optional)
- Configure maxmemory-policy: allkeys-lru
- Retrieve primary access key
- Generate RedisLeaseSample/appsettings.Local.json with ConnectionString

#### Redis Cache Creation Specification

Azure CLI command structure:
```
az redis create
  --name <redis-cache-name>
  --resource-group <resource-group>
  --location <location>
  --sku Basic
  --vm-size C0
  --enable-non-ssl-port false
```

Configuration settings:
- SKU: Basic tier (C0 - 250 MB cache for development)
- SSL: Enabled by default (port 6380)
- Non-SSL port: Disabled for security
- Maxmemory policy: allkeys-lru (evict any key using LRU)
- Minimum TLS version: 1.2

Key retrieval:
```
az redis list-keys
  --name <redis-cache-name>
  --resource-group <resource-group>
  --query primaryKey
  --output tsv
```

Endpoint construction:
```
<cache-name>.redis.cache.windows.net:6380
```

Connection string format:
```
<cache-name>.redis.cache.windows.net:6380,password=<primary-key>,ssl=True,abortConnect=False
```

#### Idempotent Resource Checks

Before creating each resource:
1. Check if resource exists using appropriate `az` show command
2. If exists: Display warning, skip creation, use existing resource
3. If not exists: Create resource with specified configuration
4. Always generate/update configuration file regardless of resource creation

Error handling:
- Capture creation errors but continue with other resources
- Validate resource availability before retrieving connection strings
- Provide clear error messages for authentication or permission issues

#### Configuration File Generation Logic

For each provider, determine sample directory path:
- Blob: `/samples/BlobLeaseSample/`
- Cosmos: `/samples/CosmosLeaseSample/`
- Redis: `/samples/RedisLeaseSample/`

File generation strategy:
- Retrieve connection details from Azure
- Construct provider-specific JSON structure
- Write to appsettings.Local.json in sample directory
- Set appropriate file permissions (readable only by owner)
- Display confirmation message with file path

Connection detail retrieval:
- Blob: Storage account connection string via `az storage account show-connection-string`
- Cosmos: Connection string via `az cosmosdb keys list --type connection-strings`
- Redis: Hostname and primary key via `az redis show` and `az redis list-keys`

### Redis Sample README Documentation

#### Documentation Structure

The README follows the established pattern with Redis-specific content:

1. Overview section describing Redis-based distributed locking
2. What This Demo Shows section with key features
3. Quick Start options (automated, interactive, manual)
4. Demo output examples (winner and loser instances)
5. Configuration modes (Connection String vs DefaultAzureCredential)
6. How It Works section explaining Redis locking mechanism
7. Redis lease structure and storage format
8. Inspecting lease state in Redis
9. Demo scenarios (simultaneous startup, takeover, multi-region)
10. Configuration files explanation
11. Troubleshooting guide
12. Clean up instructions
13. Architecture diagram
14. Performance characteristics

#### Redis-Specific Content

**Locking Mechanism Explanation:**

Redis uses SET command with NX (Not eXists) and PX (millisecond expiration) options:
- Atomic operation: SET NX PX ensures only one instance can acquire
- Expiration: Automatic cleanup if holder crashes
- Renewal: SET XX PX (update existing) for renewal
- Release: DEL command to explicitly release

**Lease Storage Format:**

Redis stores lease as a string key-value pair:
- Key: `<KeyPrefix><leaseName>` (e.g., "lease:critical-section-lock")
- Value: JSON string containing lease information
- TTL: Remaining lease duration in milliseconds

Value structure:
```
{
  "leaseId": "guid",
  "ownerId": "instance-id",
  "acquiredAt": "timestamp",
  "expiresAt": "timestamp",
  "metadata": {
    "instanceId": "...",
    "region": "...",
    "hostname": "...",
    "startTime": "..."
  }
}
```

**Inspection Methods:**

Using redis-cli:
```
redis-cli -h <cache-name>.redis.cache.windows.net -p 6380 -a <access-key> --tls
GET lease:critical-section-lock
TTL lease:critical-section-lock
```

Using Azure Portal:
- Navigate to Azure Cache for Redis
- Select Console blade
- Execute Redis commands directly

Using RedisMetadataInspector in sample:
- Automatically inspects state before and after acquisition
- Displays current lock holder on failure

#### Performance Characteristics

Latency expectations:
- Acquire (success): 5-15ms (single region)
- Acquire (conflict): 5-15ms (fast failure)
- Renew: 5-10ms (SET XX operation)
- Release: 3-8ms (DEL operation)

Throughput considerations:
- Basic C0: ~1000 operations per second
- Renewal frequency impacts load
- Network latency affects overall performance

Redis advantages:
- Faster than Cosmos DB for simple key operations
- Lower latency than Blob Storage
- Native expiration support
- Atomic operations guarantee consistency

#### Troubleshooting Section

Common issues and resolutions:

**"Authentication failed" or "NOAUTH":**
- Cause: Incorrect access key or managed identity not configured
- Fix: Verify access key in appsettings.Local.json or run `az login`

**"Connection timeout" or "Unable to connect":**
- Cause: Firewall rules blocking access or incorrect hostname
- Fix: Add client IP to Azure Redis firewall rules or verify endpoint

**Both instances acquire the lock:**
- Cause: Different Redis caches or different key prefixes
- Fix: Verify both instances use same configuration file

**High renewal failures:**
- Cause: Network instability or Redis throttling
- Fix: Increase lease duration to 60 seconds, reduce renewal frequency

**"SSL connection error":**
- Cause: TLS/SSL configuration mismatch
- Fix: Ensure UseSsl=true and port is 6380 (not 6379)

### Demo Scripts Design

#### run-competition-demo.sh

Interactive guided demo script:

Flow:
1. Display welcome banner
2. Check for appsettings.Local.json, prompt to run setup if missing
3. Present scenario menu:
   - Scenario 1: Simultaneous startup
   - Scenario 2: Takeover on failure
   - Scenario 3: Multi-region competition
4. Display commands to execute in separate terminals
5. Explain what to observe during each scenario
6. Provide inspection commands for Redis state

Output:
- Color-coded terminal instructions
- Expected behavior descriptions
- Troubleshooting tips

#### run-demo.sh

Automated dual-instance launcher:

Functionality:
- Validate prerequisites (dotnet SDK, configuration)
- Build project silently
- Launch Instance 1 in background with color prefix
- Wait 2 seconds for lock acquisition
- Launch Instance 2 in background with different color
- Merge outputs with instance identification
- Handle Ctrl+C to stop both instances
- Clean shutdown and resource cleanup

Output format:
```
[us-east-1] Log message from instance 1
[eu-west-1] Log message from instance 2
```

Color scheme:
- Instance 1: Green text
- Instance 2: Cyan text
- Success markers: Bright green
- Failure markers: Red

## Testing Strategy

### Unit Testing

Test projects structure follows existing pattern:
- Location: `/tests/DistributedLeasing.Azure.Redis.Tests/`
- Already exists with RedisLeaseProviderTests.cs

Validation approach:
- RedisLeaseProviderTests validates core provider functionality
- No new unit tests required for sample application
- Configuration validation tested through actual execution

### Integration Testing

End-to-end validation for each provider:

**Blob Storage Integration:**
1. Run setup script: `./setup-resources.sh --project blob`
2. Verify appsettings.Local.json created in BlobLeaseSample
3. Launch two instances simultaneously
4. Verify one acquires lock, other fails
5. Stop winner, verify loser can now acquire
6. Verify blob metadata in Azure Portal

**Cosmos DB Integration:**
1. Run setup script: `./setup-resources.sh --project cosmos`
2. Verify appsettings.Local.json created in CosmosLeaseSample
3. Launch two instances simultaneously
4. Verify one acquires lock, other sees ETag conflict
5. Stop winner, verify loser can acquire after expiration
6. Query lease document in Cosmos DB Data Explorer

**Redis Integration:**
1. Run setup script: `./setup-resources.sh --project redis`
2. Verify appsettings.Local.json created in RedisLeaseSample
3. Launch two instances simultaneously
4. Verify one acquires lock (SET NX succeeds), other fails (SET NX returns null)
5. Stop winner, verify loser can acquire after expiration
6. Inspect Redis key using redis-cli or Console blade

**All Providers Integration:**
1. Run setup script: `./setup-resources.sh --project all`
2. Verify all three appsettings.Local.json files created
3. Execute tests for each provider as above
4. Verify no cross-interference between providers

### Manual Testing Checklist

Configuration scenarios:
- [ ] Interactive setup with Connection String mode
- [ ] Interactive setup with DefaultAzureCredential mode
- [ ] Setup script with --project blob
- [ ] Setup script with --project cosmos
- [ ] Setup script with --project redis
- [ ] Setup script with --project all
- [ ] Setup script idempotency (run twice, verify no errors)

Competition scenarios:
- [ ] Two instances started simultaneously
- [ ] Three instances started simultaneously
- [ ] Winner stopped, loser acquires lock
- [ ] Multiple takeover cycles
- [ ] Network interruption during lock hold

Output validation:
- [ ] Color-coded console output displays correctly
- [ ] Success markers (✓) appear in green
- [ ] Failure markers (✗) appear in red
- [ ] Instance identification clear in logs
- [ ] Renewal count accurate
- [ ] Lock holder information displayed on failure

State inspection:
- [ ] Azure Portal shows correct blob metadata (Blob)
- [ ] Data Explorer shows correct lease document (Cosmos)
- [ ] Redis Console shows correct key and TTL (Redis)
- [ ] Metadata inspection displays current holder
- [ ] TTL decreases over time as expected

Cleanup:
- [ ] Lock released on normal shutdown (Ctrl+C)
- [ ] Lock released on application crash
- [ ] Expired locks become available
- [ ] No resource leaks after multiple runs

## Implementation Dependencies

### Azure Resources

Required Azure services:
- Azure Subscription (Visual Studio Enterprise Subscription)
- Resource Group (pranshu-rg)
- Azure Storage Account (for Blob sample)
- Azure Cosmos DB Account (for Cosmos sample)
- Azure Cache for Redis (for Redis sample)

Azure CLI requirements:
- Version: 2.0 or higher
- Authenticated session: `az login` completed
- Subscription set: `az account set` to target subscription

### NuGet Packages

Redis sample project dependencies (RedisLeaseSample.csproj):
- DistributedLeasing.Azure.Redis (project reference or NuGet)
- Microsoft.Extensions.Hosting (for host builder)
- Microsoft.Extensions.Configuration.Json (for appsettings)
- Microsoft.Extensions.DependencyInjection (for DI)
- Microsoft.Extensions.Logging (for logging)
- StackExchange.Redis (transitive via DistributedLeasing.Azure.Redis)

Target framework:
- net8.0 (consistent with other samples)

### File Dependencies

Files required from existing samples (to be copied/adapted):
- ColoredConsoleLogger.cs (copy from BlobLeaseSample, no changes needed)
- ConfigurationHelper.cs (adapt from BlobLeaseSample with Redis-specific prompts)
- DistributedLockWorker.cs (adapt from BlobLeaseSample with RedisMetadataInspector)
- run-demo.sh (copy from BlobLeaseSample, update project path)
- run-competition-demo.sh (copy from BlobLeaseSample, update instructions)

New files to create:
- RedisMetadataInspector.cs (new implementation for Redis inspection)

## Configuration Management

### Environment Variables

Script execution environment:
- AZURE_STORAGE_CONNECTION_STRING (used internally by Azure CLI)
- No custom environment variables required by samples

Configuration file precedence:
1. appsettings.Local.json (highest priority, user-specific)
2. appsettings.Development.json (if ASPNETCORE_ENVIRONMENT=Development)
3. appsettings.json (lowest priority, defaults)

### Git Ignore Configuration

Files excluded from version control:
- **/appsettings.Local.json (contains secrets)
- **/appsettings.*.Local.json (any environment-specific local files)

Already configured in existing .gitignore:
- Project validates these patterns remain in place

### Secrets Management

Development approach:
- Connection strings in appsettings.Local.json (local only)
- Access keys in appsettings.Local.json (local only)
- Files marked with chmod 600 by setup script

Production approach:
- DefaultAzureCredential with Managed Identity
- Azure Key Vault integration (future enhancement)
- No secrets in configuration files

## Error Handling Strategy

### Script Error Handling

Setup script error handling:
- Exit on first error: `set -e` enabled
- Validate Azure CLI installed before execution
- Check authentication status before resource operations
- Graceful handling of existing resources (idempotency)
- Display color-coded error messages with troubleshooting hints

Error scenarios:
- Azure CLI not installed: Display installation URL, exit with code 1
- Not authenticated: Display `az login` instruction, exit with code 1
- Subscription not found: Display available subscriptions, exit with code 1
- Resource creation failure: Display error, continue with remaining resources
- Configuration file write failure: Display error, exit with code 1

### Application Error Handling

Configuration validation:
- Missing appsettings.Local.json: Trigger interactive setup wizard
- Invalid configuration format: Display detailed error message with fix options
- Authentication failure: Provide troubleshooting steps for Azure CLI

Runtime error handling:
- Lock acquisition failure: Log gracefully, exit with code 0 (expected behavior)
- Network timeout: Log error, exit with code 1
- Lease renewal failure: Log warning, continue operation
- Lock lost unexpectedly: Stop work immediately, log error

Graceful shutdown:
- Ctrl+C: Release lock, dispose resources, exit cleanly
- Unhandled exception: Release lock in finally block
- Application crash: Lock expires automatically via TTL

## Success Criteria

### Functional Requirements

- [ ] Redis sample application compiles without errors
- [ ] Redis sample follows established patterns from Blob and Cosmos samples
- [ ] Setup script accepts --project argument with blob, cosmos, redis, all values
- [ ] Setup script creates Azure Redis Cache when --project redis or all
- [ ] Setup script generates correct appsettings.Local.json for each provider
- [ ] Script idempotency: Running twice produces same result without errors
- [ ] Interactive configuration wizard works for Redis sample
- [ ] Two Redis sample instances compete correctly for same lock
- [ ] Winner executes work, loser fails gracefully
- [ ] Lock takeover works after winner stops
- [ ] Redis metadata inspection displays current lock holder
- [ ] README documentation complete and accurate

### Non-Functional Requirements

- [ ] Configuration files match established JSON structure patterns
- [ ] Console output uses color-coding consistently
- [ ] Error messages provide actionable troubleshooting guidance
- [ ] Setup script completes in under 3 minutes (excluding Redis creation)
- [ ] Redis cache creation completes in under 10 minutes
- [ ] Sample application starts in under 5 seconds
- [ ] Lock acquisition latency under 50ms
- [ ] Documentation clarity: New user can run demo in under 10 minutes

### Quality Requirements

- [ ] No secrets committed to version control
- [ ] All resources cleaned up properly
- [ ] Code follows C# coding conventions
- [ ] Script follows Bash best practices
- [ ] README formatting consistent with existing samples
- [ ] No hardcoded paths or values (use variables)
- [ ] Cross-platform compatibility (Windows/Linux/macOS for sample)

## Security Considerations

### Authentication

Development environment:
- Connection strings acceptable for local testing
- Access keys stored only in git-ignored local files
- Azure CLI authentication for DefaultAzureCredential

Production environment:
- Managed Identity required
- No connection strings or access keys in configuration
- Azure RBAC roles for least privilege access

Role assignments required:
- Redis: Redis Cache Contributor (for management) or Data Contributor (for operations)
- Blob: Storage Blob Data Contributor
- Cosmos: Cosmos DB Data Contributor

### Network Security

Azure Redis firewall:
- Default: Deny all inbound traffic
- Setup script does NOT modify firewall rules
- User must manually add IP address in Azure Portal
- Alternative: Use Azure Virtual Network integration

SSL/TLS:
- Redis: SSL enabled by default (port 6380)
- Blob: HTTPS only
- Cosmos: HTTPS only

### Data Protection

Secrets handling:
- Never log connection strings or access keys
- Use secure string formatting (mask keys in output)
- Clean up temporary files containing secrets

Metadata privacy:
- Instance metadata (hostname, region) stored in leases
- No personally identifiable information (PII) in metadata
- Metadata cleared when lease released or expired

## Future Enhancements

### Potential Improvements

1. Redis Cluster support for high availability
2. Multiple Redis instances for distributed lock quorum
3. Azure Key Vault integration for secrets management
4. Prometheus metrics export for monitoring
5. Health check endpoints for Kubernetes deployments
6. Distributed tracing with OpenTelemetry
7. Automatic IP address whitelisting in setup script
8. Redis Sentinel support for failover
9. Connection pooling optimization
10. Retry policies with exponential backoff

### Extensibility Points

- Custom metadata providers for additional tracking
- Pluggable lock algorithms (Redlock, Single Redis)
- Configurable retry strategies
- Custom logging sinks
- Alternative storage backends via provider pattern

## Dependencies

This design depends on:
1. Existing DistributedLeasing.Azure.Redis library implementation
2. Existing BlobLeaseSample and CosmosLeaseSample as reference implementations
3. Azure CLI authentication session
4. Visual Studio Enterprise Subscription with appropriate permissions
5. Network access to Azure services

## Deliverables

### Code Deliverables

1. RedisLeaseSample/Program.cs
2. RedisLeaseSample/DistributedLockWorker.cs
3. RedisLeaseSample/ConfigurationHelper.cs
4. RedisLeaseSample/RedisMetadataInspector.cs
5. RedisLeaseSample/ColoredConsoleLogger.cs
6. RedisLeaseSample/appsettings.json
7. RedisLeaseSample/appsettings.Development.json
8. RedisLeaseSample/RedisLeaseSample.csproj
9. Enhanced scripts/setup-resources.sh

### Documentation Deliverables

1. RedisLeaseSample/README.md (comprehensive documentation)

### Script Deliverables

1. RedisLeaseSample/run-competition-demo.sh
2. RedisLeaseSample/run-demo.sh

### Testing Deliverables

1. End-to-end test validation results
2. Manual testing checklist completion
3. Screenshot/recording of successful demo execution

All deliverables should maintain consistency with existing samples in structure, naming conventions, and quality standards.
