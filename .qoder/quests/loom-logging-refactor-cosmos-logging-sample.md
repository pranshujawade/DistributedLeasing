# Logging Refactoring and Cosmos Sample Design

## Overview

Refactor the Blob lease sample to implement professional, minimal logging with color-coded output, create a similar Cosmos DB sample, and develop Azure resource management scripts for the pranshu-rg resource group.

## Objectives

1. Refactor BlobLeaseSample logging to be professional and minimal
2. Implement color-coded console logging for better user experience
3. Simplify demo to show lock competition ending with 15-second execution and graceful shutdown
4. Create parallel CosmosLeaseSample with identical behavior
5. Build resource cleanup script for pranshu-rg
6. Build idempotent resource creation script
7. Test both samples to ensure proper execution

## Goals

### Primary Goals
- Remove excessive debug logging while retaining essential operational visibility
- Use color-coded output to distinguish between instances and operation states
- Create professional demo experience showing distributed lock behavior clearly
- Establish Cosmos DB as an alternative leasing backend with identical sample behavior
- Provide clean resource management for Azure testing environment

### Success Criteria
- Users understand lock competition without log overload
- Both samples run reliably and demonstrate distributed locking
- Resource scripts work idempotently and reliably
- Output is professional and informative without verbosity

## Design Constraints

- Both samples must demonstrate the same distributed lock competition behavior
- Logging must use structured logging patterns, not direct Console.WriteLine for application logic
- Color coding must work cross-platform (Windows, Linux, macOS)
- Scripts must be idempotent and safe to run multiple times
- Configuration follows existing patterns (connection string vs DefaultAzureCredential)
- Must maintain compatibility with existing DistributedLeasing library

## Requirements Analysis

### Functional Requirements

#### FR-1: Blob Sample Logging Refactoring
- Remove verbose metadata inspection logs from normal flow
- Reduce renewal logging to minimal notifications
- Use color-coded output to distinguish instance identity
- Show only critical events: startup, lock attempt, acquisition result, work progress, shutdown
- Limit execution to 15 seconds after lock acquisition
- Display clean shutdown message

#### FR-2: Color-Coded Logging System
- Green: Successful operations (lock acquired, work completed)
- Red: Failures (lock acquisition failed, errors)
- Yellow: Warnings (renewal failures, important notices)
- Cyan: Informational (instance identity, progress updates)
- Gray: System messages (startup, shutdown)
- Apply colors consistently across both samples

#### FR-3: Simplified Demo Flow
- Instance startup with identity
- Single lock acquisition attempt
- Winner executes work for 15 seconds
- Loser shows graceful failure and exits
- Clean shutdown for both
- No continuous loop or "Press any key" prompts

#### FR-4: Cosmos DB Sample Creation
- Mirror BlobLeaseSample structure exactly
- Replace Blob-specific components with Cosmos equivalents
- Implement CosmosMetadataInspector for document state inspection
- Use identical logging patterns and color scheme
- Support same authentication modes (connection string and DefaultAzureCredential)
- Share ConfigurationHelper for setup wizard

#### FR-5: Resource Management Scripts
- Cleanup script: Delete all resources in pranshu-rg
- Creation script: Idempotently create/update resources
- Support both Blob and Cosmos resources
- Validate prerequisites before execution
- Provide clear progress and error messages
- Safe error handling for non-existent resources

### Non-Functional Requirements

#### NFR-1: Performance
- Minimal logging overhead
- Fast startup and shutdown
- Efficient resource cleanup

#### NFR-2: Usability
- Clear, concise output
- Intuitive color coding
- Helpful error messages
- Automated setup wizard

#### NFR-3: Maintainability
- Consistent code structure between samples
- Reusable components (ConfigurationHelper)
- Well-documented scripts
- Clear separation of concerns

#### NFR-4: Reliability
- Idempotent resource operations
- Graceful error handling
- Proper resource cleanup
- Connection resilience

## Architecture Design

### Component Structure

#### Blob Sample Architecture
```
BlobLeaseSample/
├── Program.cs (refactored)
├── DistributedLockWorker.cs (refactored)
├── ConfigurationHelper.cs (enhanced)
├── ColoredConsoleLogger.cs (new)
├── appsettings.json
└── Scripts/
    ├── cleanup-resources.sh (new)
    └── setup-resources.sh (refactored)
```

#### Cosmos Sample Architecture
```
CosmosLeaseSample/ (new)
├── Program.cs
├── DistributedLockWorker.cs
├── CosmosMetadataInspector.cs
├── ConfigurationHelper.cs (shared)
├── ColoredConsoleLogger.cs (shared)
├── appsettings.json
└── Scripts/
    ├── cleanup-resources.sh (shared)
    └── setup-resources.sh (shared)
```

### Logging Architecture

#### Log Level Mapping
| Event Type | Log Level | Color | Example |
|------------|-----------|-------|---------|
| Instance startup | Information | Cyan | "Instance: us-east-1 attempting lock acquisition" |
| Lock acquired | Information | Green | "Lock acquired successfully" |
| Lock failed | Warning | Red | "Lock acquisition failed - held by another instance" |
| Work progress | Information | Cyan | "Processing work [3s elapsed]" |
| Renewal | Debug | Gray | "Lock renewed (suppressed unless verbose)" |
| Shutdown | Information | Gray | "Shutting down gracefully" |
| Errors | Error | Red | "Unexpected error occurred" |

#### Structured Logging Pattern
- Use ILogger with structured properties
- Avoid string interpolation in log messages
- Use named parameters for context
- Suppress Debug-level logs by default
- Color applied via custom console formatter

### Data Flow

#### Lock Competition Flow
```
Instance 1 Startup → Attempt Lock → SUCCESS → Execute 15s → Release → Shutdown
Instance 2 Startup → Attempt Lock → FAILURE → Log holder → Shutdown
```

#### Configuration Flow
```
Check appsettings.Local.json
  ├─ Exists → Load config → Start demo
  └─ Missing → Interactive wizard → Generate config → Start demo
```

#### Resource Management Flow
```
Cleanup Script:
  List all resources in pranshu-rg → Confirm deletion → Delete each → Verify

Setup Script:
  Check if resource exists
    ├─ Exists → Verify configuration → Skip or update
    └─ Missing → Create with defaults → Configure
```

## Detailed Design

### Refactored Logging Strategy

#### What to Remove
- Pre-acquisition blob inspection logs
- Post-acquisition blob inspection logs
- Detailed renewal event logs (move to Debug level)
- Metadata display boxes
- Azure state inspection during normal flow
- Verbose connection details

#### What to Keep
- Instance identity at startup
- Lock acquisition attempt notification
- Success/failure result with clear messaging
- Work execution progress (every 3-5 seconds)
- Final statistics at shutdown
- Critical errors only

#### Logging Examples

**Instance Startup (Cyan)**
```
Instance: us-east-1 | Region: us-east | Attempting lock acquisition...
```

**Lock Acquired (Green)**
```
✓ Lock acquired | Lease: a1b2c3d4... | Executing work for 15 seconds
```

**Lock Failed (Red)**
```
✗ Lock unavailable | Held by: eu-west-1 | Exiting gracefully
```

**Work Progress (Cyan)**
```
Processing work... [3s] [6s] [9s] [12s] [15s]
```

**Shutdown (Gray)**
```
Completed successfully | Duration: 15s | Renewals: 1
```

### Color-Coded Console Logger Design

#### Purpose
Custom console logger formatter that applies ANSI color codes based on log level and message content.

#### Implementation Approach
- Create custom ILoggerProvider and ILogger implementation
- Apply colors based on log level
- Support special markers for instance identity coloring
- Cross-platform ANSI color code support
- Fallback for terminals without color support

#### Color Mapping Table
| Log Level | Foreground Color | Use Case |
|-----------|------------------|----------|
| Trace | DarkGray | Suppressed by default |
| Debug | Gray | Renewal events (verbose mode only) |
| Information | Cyan | Progress, instance identity |
| Warning | Yellow | Renewal failures, non-critical issues |
| Error | Red | Acquisition failures, errors |
| Critical | Magenta | System failures |

#### Special Formatting
- Instance ID always in bold
- Success markers (✓) in green
- Failure markers (✗) in red
- Timestamps suppressed for cleaner output
- Single-line format preferred

### Simplified Demo Execution Flow

#### Execution Timeline
```
0s    Instance starts, displays identity
1s    Attempts lock acquisition
2s    Result displayed (success or failure)
      Winner: Starts work loop
      Loser: Displays holder info, exits
3s    Winner: First progress update
6s    Winner: Second progress update
9s    Winner: Third progress update
12s   Winner: Fourth progress update
15s   Winner: Fifth progress update
16s   Winner: Releases lock
17s   Winner: Displays summary, exits
```

#### Termination Logic
- Winner: After 15 seconds of work, release lock and exit cleanly
- Loser: After displaying failure, exit immediately
- No interactive prompts
- No continuous loops
- Graceful shutdown on Ctrl+C

### Cosmos Sample Design

#### Cosmos-Specific Components

**CosmosMetadataInspector**
Similar to BlobLeaseSample's AzureMetadataInspector but adapted for Cosmos DB:
- Inspect lease document state
- Format current holder information
- Query document metadata
- Display partition and ETag information

**Configuration Structure**
```
CosmosLeasing:
  AccountEndpoint: https://[account].documents.azure.com
  ConnectionString: (optional, for dev)
  DatabaseName: DistributedLeasing
  ContainerName: Leases
  DefaultLeaseDuration: 00:00:30
  AutoRenew: true
  AutoRenewInterval: 00:00:20
```

**Document Structure**
The Cosmos sample leverages existing LeaseDocument model:
- id: lease name (normalized)
- leaseName: original name
- leaseId: current holder GUID
- acquiredAt: acquisition timestamp
- expiresAt: expiration timestamp
- metadata: instance identity (instanceId, region, hostname)
- ttl: auto-cleanup configuration

#### Cosmos vs Blob Differences

| Aspect | Blob | Cosmos |
|--------|------|--------|
| Lock mechanism | Blob lease | Optimistic concurrency (ETag) |
| State inspection | Blob properties | Document query |
| Metadata storage | Blob metadata | Document properties |
| Auto-cleanup | Manual | TTL-based |
| Cost model | Storage + operations | RU consumption |
| Setup complexity | Container only | Database + Container |

### Resource Management Scripts

#### cleanup-resources.sh Design

**Purpose**: Remove all resources from pranshu-rg resource group

**Flow**:
1. Verify Azure CLI authentication
2. List all resources in pranshu-rg
3. Display resources to be deleted
4. Confirm deletion (with --yes flag to skip)
5. Delete Cosmos accounts
6. Delete Storage accounts
7. Optionally delete resource group itself
8. Verify cleanup completion

**Parameters**:
- `--yes` or `-y`: Skip confirmation
- `--delete-group`: Also delete resource group
- `--resource-type <blob|cosmos|all>`: Filter by resource type

**Safety Features**:
- Confirmation prompt by default
- List resources before deletion
- Handle non-existent resources gracefully
- Verify successful deletion
- Support dry-run mode

#### setup-resources.sh Design

**Purpose**: Idempotently create Azure resources for samples

**Flow**:
1. Verify Azure CLI authentication
2. Create resource group if missing
3. For Blob resources:
   - Check if storage account exists
   - Create if missing with standardized name
   - Create container if missing
4. For Cosmos resources:
   - Check if Cosmos account exists
   - Create if missing with standardized name
   - Create database if missing
   - Create container with partition key and TTL
5. Generate appsettings.Local.json for both samples
6. Display summary

**Parameters**:
- `--resource-type <blob|cosmos|all>`: Select resources to create
- `--storage-account <name>`: Custom storage account name
- `--cosmos-account <name>`: Custom Cosmos account name
- `--location <region>`: Azure region (default: eastus)

**Idempotency Strategy**:
- Check existence before creation
- Use "CreateIfNotExists" patterns
- Skip with message if exists
- Update configuration files regardless
- Validate final state

**Resource Naming**:
- Storage account: `pranshublobdist` (blob + dist for distributed)
- Cosmos account: `pranshucosmosdist`
- Container: `leases` (both Blob and Cosmos)
- Database: `DistributedLeasing` (Cosmos only)

### Testing Strategy

#### Manual Testing Checklist

**Blob Sample Testing**:
1. Run cleanup script
2. Run setup script for blob
3. Start Instance 1 (us-east-1)
4. Verify lock acquisition and work execution
5. Verify shutdown after 15 seconds
6. Start Instance 2 (eu-west-1)
7. Verify lock failure message
8. Run both instances simultaneously
9. Verify one succeeds, one fails
10. Verify color-coded output

**Cosmos Sample Testing**:
1. Run setup script for Cosmos
2. Repeat all Blob sample tests
3. Verify document creation in Cosmos
4. Inspect document metadata via Azure Portal
5. Verify TTL auto-cleanup after expiration

**Resource Script Testing**:
1. Test cleanup with no resources
2. Test cleanup with resources
3. Test setup when resources exist
4. Test setup from clean state
5. Verify idempotency (run setup twice)

#### Validation Criteria

**Logging Validation**:
- No excessive logs during normal operation
- Colors display correctly on different terminals
- Instance identity clearly visible
- Success/failure immediately apparent
- No confusing or duplicate messages

**Behavior Validation**:
- Winner executes exactly 15 seconds of work
- Loser exits immediately after failure
- No hang or infinite loops
- Clean shutdown on Ctrl+C
- Proper lease release

**Resource Validation**:
- Scripts complete without errors
- Resources created with correct configuration
- Cleanup removes all intended resources
- Configuration files generated correctly
- Both samples can run independently

## Implementation Phases

### Phase 1: Blob Sample Logging Refactoring
1. Create ColoredConsoleLogger infrastructure
2. Refactor DistributedLockWorker logging
3. Remove metadata inspection from normal flow
4. Implement 15-second execution limit
5. Remove interactive prompts
6. Test and validate output

### Phase 2: Cosmos Sample Creation
1. Create CosmosLeaseSample project structure
2. Implement Program.cs with Cosmos configuration
3. Create CosmosMetadataInspector
4. Port DistributedLockWorker with Cosmos-specific changes
5. Configure appsettings.json
6. Test against Cosmos DB

### Phase 3: Resource Management Scripts
1. Implement cleanup-resources.sh
2. Implement setup-resources.sh with idempotency
3. Update existing azure-onetime-setup.sh to use new setup script
4. Add support for both Blob and Cosmos resources
5. Test script combinations

### Phase 4: Integration and Testing
1. Test both samples end-to-end
2. Validate color output on multiple terminals
3. Test resource scripts in various states
4. Update README files
5. Create demo video or GIF

## Edge Cases and Error Handling

### Logging Edge Cases
- Terminal without color support: Fallback to plain text
- Log message with null parameters: Safe handling with placeholders
- Concurrent log messages: Thread-safe logger implementation

### Execution Edge Cases
- Network interruption during work: Lease lost event triggers immediate shutdown
- Multiple instances start simultaneously: One wins via optimistic concurrency
- Ctrl+C during execution: Graceful shutdown and lease release

### Resource Script Edge Cases
- Resource group doesn't exist: Create it
- Partial resource state: Complete the setup
- Authentication expired: Clear error message and re-login prompt
- Resource creation conflict: Detect and handle gracefully
- Deletion of non-existent resource: Skip with informational message

## Security Considerations

### Authentication
- Prefer DefaultAzureCredential over connection strings
- Never log connection strings or keys
- Store secrets in appsettings.Local.json (git-ignored)
- Support managed identity for production scenarios

### Resource Management
- Require confirmation before deletion
- Validate resource group name
- Prevent accidental cross-subscription operations
- Audit trail via script logging

## Configuration Management

### Blob Sample Configuration
```
appsettings.json (template):
  BlobLeasing:
    ContainerName: leases
    DefaultLeaseDuration: 00:00:30
    AutoRenew: true
    AutoRenewInterval: 00:00:20

appsettings.Local.json (generated):
  BlobLeasing:
    ConnectionString: [from setup]
    (or StorageAccountUri for DefaultAzureCredential)
```

### Cosmos Sample Configuration
```
appsettings.json (template):
  CosmosLeasing:
    DatabaseName: DistributedLeasing
    ContainerName: Leases
    DefaultLeaseDuration: 00:00:30
    AutoRenew: true
    AutoRenewInterval: 00:00:20

appsettings.Local.json (generated):
  CosmosLeasing:
    ConnectionString: [from setup]
    (or AccountEndpoint for DefaultAzureCredential)
```

## Documentation Updates

### README Updates Required

**BlobLeaseSample/README.md**:
- Update output examples to reflect new logging
- Document 15-second execution flow
- Show color-coded output examples
- Update troubleshooting for new behavior

**CosmosLeaseSample/README.md** (new):
- Mirror Blob sample README structure
- Highlight Cosmos-specific setup steps
- Document database and container creation
- Explain ETag-based locking vs Blob leasing
- Include cost considerations

**Root README.md**:
- Add link to Cosmos sample
- Document resource management scripts
- Update quick start for both samples

## Monitoring and Observability

### Key Metrics to Log
- Lock acquisition latency
- Lock hold duration
- Renewal count
- Failure count
- Execution duration

### Log Format
Structured logging with named parameters for easy parsing:
```
[{Timestamp:HH:mm:ss}] {Level}: {Message} | Instance={InstanceId} | Region={Region}
```

### Sample Log Output

**Winner Instance**:
```
[12:00:01] Instance: us-east-1 | Region: us-east | Attempting lock acquisition...
[12:00:02] ✓ Lock acquired | Lease: a1b2c3d4 | Executing work for 15 seconds
[12:00:05] Processing work... [3s]
[12:00:08] Processing work... [6s]
[12:00:11] Processing work... [9s]
[12:00:14] Processing work... [12s]
[12:00:17] Processing work... [15s]
[12:00:18] Completed successfully | Duration: 15s | Renewals: 1
```

**Loser Instance**:
```
[12:00:01] Instance: eu-west-1 | Region: eu-west | Attempting lock acquisition...
[12:00:02] ✗ Lock unavailable | Held by: us-east-1 (us-east) | Exiting gracefully
```

## Acceptance Criteria

### Logging Refactoring
- [ ] No verbose metadata logs during normal execution
- [ ] Color-coded output working on Windows, Linux, macOS
- [ ] Instance identity clearly visible
- [ ] Success and failure states immediately obvious
- [ ] Execution limited to 15 seconds for winner
- [ ] Loser exits gracefully after displaying holder

### Cosmos Sample
- [ ] Identical behavior to Blob sample
- [ ] Successfully acquires and releases Cosmos-based leases
- [ ] Handles lock competition correctly
- [ ] Configuration wizard works
- [ ] Metadata stored and queryable in Cosmos documents

### Resource Scripts
- [ ] Cleanup script removes all resources in pranshu-rg
- [ ] Setup script creates resources idempotently
- [ ] Scripts handle errors gracefully
- [ ] Configuration files generated correctly
- [ ] Scripts work with both Blob and Cosmos resources

### Overall
- [ ] Both samples tested and working
- [ ] Documentation updated
- [ ] No excessive logging or noise
- [ ] Professional output quality
- [ ] Clean shutdown behavior
