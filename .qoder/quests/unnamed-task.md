# Enhanced Distributed Lock Competition Demo - Implementation Instructions

## Objective

Enhance the existing BlobLeaseSample to demonstrate and prove distributed lock competition behavior between multiple regions/instances through comprehensive logging, Azure visibility, and metadata inspection capabilities.

## Background

The current BlobLeaseSample demonstrates basic lock competition but lacks detailed visibility into:
- Blob metadata storage and structure
- Azure Storage state during competition
- Real-time lease status monitoring
- Detailed competition timeline
- Metadata evolution during lease lifecycle

## Requirements

### Functional Requirements

1. **Enhanced Terminal Logging**
   - Display blob metadata before and after lease acquisition
   - Show Azure Storage container state
   - Log lease competition timeline with timestamps
   - Display metadata changes during auto-renewal
   - Show blob properties including lease state and lease duration

2. **Azure Visibility**
   - Provide Azure CLI commands to inspect blob state
   - Show how to query blob lease metadata via Azure Portal
   - Display container-level lease statistics
   - Enable real-time monitoring of blob properties during demo

3. **Metadata Tracking**
   - Capture and display instance identification metadata
   - Show creation and modification timestamps
   - Track lease acquisition history in blob metadata
   - Display user-provided metadata from LeaseOptions

4. **Competition Proof**
   - Log exact timing of acquisition attempts from multiple instances
   - Show HTTP 409 conflict responses when lock is held
   - Display winner's lease ID and expiration time
   - Show loser's failed acquisition with reason

### Non-Functional Requirements

1. **Clarity**: Logs must be easy to read with clear visual separation
2. **Completeness**: All relevant metadata and state information must be visible
3. **Real-time**: Updates should be shown as they occur during execution
4. **Instructional**: Output should educate users about Azure Blob lease mechanics

## Design

### Architecture Overview

The enhancement will add observability layers around the existing lock competition flow without modifying core leasing logic.

```mermaid
graph TB
    subgraph "Instance 1 - us-east-1"
        I1[Program.cs] --> W1[DistributedLockWorker]
        W1 --> ML1[MetadataLogger]
        W1 --> LM1[ILeaseManager]
    end
    
    subgraph "Instance 2 - eu-west-1"
        I2[Program.cs] --> W2[DistributedLockWorker]
        W2 --> ML2[MetadataLogger]
        W2 --> LM2[ILeaseManager]
    end
    
    subgraph "Observability Layer"
        ML1 --> AZM[Azure Metadata Inspector]
        ML2 --> AZM
        AZM --> CLI[Azure CLI Commands]
    end
    
    subgraph "Azure Blob Storage"
        LM1 --> BC[Blob Container: leases]
        LM2 --> BC
        BC --> B1[Blob: critical-section-lock]
        B1 --> M[Metadata]
        B1 --> LS[Lease State]
    end
    
    CLI -.-> BC
</mermaid>

### Component Design

#### 1. Azure Metadata Inspector

A new component responsible for querying and displaying Azure Blob metadata.

**Purpose**: Provide visibility into blob state, lease properties, and metadata during demo execution.

**Responsibilities**:
- Query blob properties using Azure SDK
- Format and display metadata in readable format
- Show lease state transitions
- Display blob creation and modification times

**Interface**:

| Method | Purpose | Parameters | Returns |
|--------|---------|------------|---------|
| InspectBlobStateAsync | Retrieve and display current blob state | blobClient, cancellationToken | BlobInspectionResult |
| DisplayMetadata | Format metadata for console output | metadata dictionary | formatted string |
| QueryLeaseStatus | Get current lease status | blobClient, cancellationToken | lease state enum |
| ShowContainerInfo | Display container-level information | containerClient, cancellationToken | container summary |

**Data Structures**:

| Field | Type | Description |
|-------|------|-------------|
| BlobName | string | Name of the blob representing the lease |
| LeaseState | LeaseState enum | Current lease state (Available, Leased, Expired, Breaking, Broken) |
| LeaseStatus | LeaseStatus enum | Lease status (Locked, Unlocked) |
| LeaseDuration | LeaseDurationType enum | Fixed or Infinite |
| Metadata | Dictionary&lt;string, string&gt; | Key-value pairs stored with blob |
| CreatedOn | DateTimeOffset | Blob creation timestamp |
| LastModified | DateTimeOffset | Last modification timestamp |
| ContentLength | long | Blob size in bytes (should be 0 for lease blobs) |

#### 2. Enhanced DistributedLockWorker

Extend the existing worker to integrate metadata inspection and detailed logging.

**Enhancements**:

1. **Pre-Acquisition Inspection**
   - Query blob state before attempting lock acquisition
   - Display existing metadata if blob exists
   - Show current lease holder information if available

2. **Post-Acquisition Inspection**
   - Display acquired lease metadata
   - Show updated blob properties with lease information
   - Log metadata changes

3. **Renewal Logging Enhancement**
   - Log metadata state before renewal
   - Show expiration time updates
   - Display renewal count in metadata

4. **Competition Timeline**
   - Log precise timestamps for all operations
   - Show time elapsed between operations
   - Display relative timing between competing instances

**Logging Format**:

```
═══════════════════════════════════════════════════════════
BLOB STATE INSPECTION
═══════════════════════════════════════════════════════════
Blob Name: critical-section-lock
Container: leases
Storage Account: pranshuleasestore123456

Lease State: Available / Leased / Expired
Lease Status: Unlocked / Locked
Lease Duration: 30 seconds
Current Lease ID: [lease-id-guid] (if leased)

Metadata:
  • leaseName: critical-section-lock
  • createdAt: 2025-12-25T12:00:00.000Z
  • lastModified: 2025-12-25T12:05:30.000Z
  • lease_instanceId: us-east-1
  • lease_region: us-east
  • lease_hostname: machine-name
  • acquisitionCount: 5

Timestamps:
  • Created: 2025-12-25 12:00:00 UTC
  • Last Modified: 2025-12-25 12:05:30 UTC
  
Properties:
  • Content Length: 0 bytes
  • ETag: "0x8DCBA123456789A"
═══════════════════════════════════════════════════════════
```

#### 3. Azure CLI Integration Guide

Provide executable commands for manual inspection during demo.

**Commands to Include**:

| Command Purpose | Azure CLI Command |
|----------------|-------------------|
| List all blobs in container | `az storage blob list --container-name leases --connection-string "[conn-string]" --output table` |
| Show blob metadata | `az storage blob metadata show --container-name leases --name critical-section-lock --connection-string "[conn-string]"` |
| Show blob properties | `az storage blob show --container-name leases --name critical-section-lock --connection-string "[conn-string]" --query "{LeaseState:properties.lease.state, LeaseStatus:properties.lease.status, Duration:properties.lease.duration}"` |
| List blobs with lease state | `az storage blob list --container-name leases --connection-string "[conn-string]" --query "[].{Name:name, LeaseState:properties.lease.state, LeaseStatus:properties.lease.status}"` |

#### 4. Configuration Enhancement

Extend configuration to support metadata injection for tracking.

**appsettings.Local.json additions**:

| Configuration Key | Purpose | Example Value |
|------------------|---------|---------------|
| BlobLeasing:Metadata:instanceId | Instance identifier | "us-east-1" |
| BlobLeasing:Metadata:region | Region name | "us-east" |
| BlobLeasing:Metadata:hostname | Machine hostname | Environment.MachineName |
| BlobLeasing:Metadata:startTime | Instance start time | DateTimeOffset.UtcNow |
| BlobLeasing:EnableMetadataInspection | Enable inspection logging | true |
| BlobLeasing:InspectionInterval | How often to log state | "00:00:05" |

### Execution Flow

```mermaid
sequenceDiagram
    participant I1 as Instance 1<br/>(us-east-1)
    participant I2 as Instance 2<br/>(eu-west-1)
    participant AMI as Azure Metadata<br/>Inspector
    participant BS as Blob Storage<br/>(critical-section-lock)
    
    Note over I1,BS: Phase 1: Pre-Competition Inspection
    
    I1->>AMI: Inspect blob state
    AMI->>BS: Query properties & metadata
    BS-->>AMI: State: Available, No lease
    AMI-->>I1: Display: Blob available for acquisition
    
    I2->>AMI: Inspect blob state
    AMI->>BS: Query properties & metadata
    BS-->>AMI: State: Available, No lease
    AMI-->>I2: Display: Blob available for acquisition
    
    Note over I1,BS: Phase 2: Competition
    
    I1->>BS: TryAcquireAsync("critical-section-lock")
    activate BS
    BS-->>I1: Success - Lease ID: abc123
    deactivate BS
    
    I1->>AMI: Inspect acquired lease
    AMI->>BS: Query updated state
    BS-->>AMI: State: Leased, Holder: us-east-1
    AMI-->>I1: Display: Winner metadata
    
    I2->>BS: TryAcquireAsync("critical-section-lock")
    activate BS
    BS-->>I2: HTTP 409 - Conflict
    deactivate BS
    
    I2->>AMI: Inspect blob state
    AMI->>BS: Query current state
    BS-->>AMI: State: Leased, Holder: us-east-1
    AMI-->>I2: Display: Loser - Held by us-east-1
    
    Note over I1,BS: Phase 3: Active Hold with Monitoring
    
    loop Every 5 seconds
        I1->>AMI: Inspect current state
        AMI->>BS: Query metadata
        BS-->>AMI: Lease active, metadata updated
        AMI-->>I1: Display: Renewal count, expiration
    end
    
    Note over I1,BS: Phase 4: Release
    
    I1->>BS: ReleaseAsync()
    BS-->>I1: Success
    I1->>AMI: Inspect released state
    AMI->>BS: Query state
    BS-->>AMI: State: Available
    AMI-->>I1: Display: Lock released, available
</sequenceDiagram>

### Metadata Storage Design

Based on the BlobLeaseProvider implementation, metadata is stored as blob metadata key-value pairs.

**Metadata Structure**:

| Metadata Key | Source | Purpose | Example Value |
|--------------|--------|---------|---------------|
| leaseName | BlobLeaseProvider (automatic) | Original lease name | "critical-section-lock" |
| createdAt | BlobLeaseProvider (automatic) | Blob creation timestamp | "2025-12-25T12:00:00.000Z" |
| lastModified | BlobLeaseProvider (automatic) | Last metadata update | "2025-12-25T12:05:30.000Z" |
| lease_instanceId | LeaseOptions.Metadata (user) | Instance identifier | "us-east-1" |
| lease_region | LeaseOptions.Metadata (user) | Region name | "us-east" |
| lease_hostname | LeaseOptions.Metadata (user) | Machine hostname | "MACHINE-01" |
| lease_startTime | LeaseOptions.Metadata (user) | Instance start time | "2025-12-25T12:00:00Z" |

**Important Notes**:
- User-provided metadata from LeaseOptions is prefixed with "lease_" to avoid conflicts
- Metadata is updated via UpdateBlobMetadataAsync before lease acquisition
- Metadata updates can fail with HTTP 409 during concurrent access (non-critical)
- Metadata persists even after lease is released

### Demo Scenarios

#### Scenario 1: Simultaneous Competition with Full Visibility

**Objective**: Demonstrate two instances competing simultaneously with complete state visibility.

**Steps**:

1. Terminal 1 - Start Instance 1 with inspection enabled
2. Terminal 2 - Start Instance 2 immediately after
3. Terminal 3 - Run Azure CLI commands to inspect blob state
4. Observe logs showing:
   - Pre-acquisition blob state (Available)
   - Instance 1 wins, acquires lease
   - Instance 1 metadata is written to blob
   - Instance 2 sees HTTP 409 conflict
   - Instance 2 queries blob and sees Instance 1 as holder
   - Auto-renewal updates metadata periodically

**Expected Logs** (Instance 1 - Winner):

```
═══════════════════════════════════════════════════════════
PRE-ACQUISITION BLOB INSPECTION
Time: 2025-12-25 12:00:00.123 UTC
═══════════════════════════════════════════════════════════
Blob: critical-section-lock
Container: leases
State: Available
Status: Unlocked

Metadata: (empty - new blob will be created)

Attempting acquisition...

═══════════════════════════════════════════════════════════
POST-ACQUISITION BLOB INSPECTION
Time: 2025-12-25 12:00:00.456 UTC
═══════════════════════════════════════════════════════════
✓ LOCK ACQUIRED SUCCESSFULLY

Lease Details:
  • Lease ID: 954d91dc-67fd-409a-b78a-90a78e36c928
  • Acquired At: 2025-12-25 12:00:00.456 UTC
  • Expires At: 2025-12-25 12:00:30.456 UTC
  • Duration: 30 seconds

Blob State:
  • Lease State: Leased
  • Lease Status: Locked
  • Lease Duration: Fixed (30 seconds)

Metadata:
  • leaseName: critical-section-lock
  • createdAt: 2025-12-25T12:00:00.200Z
  • lastModified: 2025-12-25T12:00:00.456Z
  • lease_instanceId: us-east-1
  • lease_region: us-east
  • lease_hostname: DESKTOP-ABC123
  • lease_startTime: 2025-12-25T12:00:00Z

This instance is now the ACTIVE lock holder.
═══════════════════════════════════════════════════════════
```

**Expected Logs** (Instance 2 - Loser):

```
═══════════════════════════════════════════════════════════
PRE-ACQUISITION BLOB INSPECTION
Time: 2025-12-25 12:00:00.234 UTC
═══════════════════════════════════════════════════════════
Blob: critical-section-lock
Container: leases
State: Available
Status: Unlocked

Metadata: (checking...)

Attempting acquisition...

═══════════════════════════════════════════════════════════
ACQUISITION FAILED - BLOB INSPECTION
Time: 2025-12-25 12:00:00.789 UTC
═══════════════════════════════════════════════════════════
✗ LOCK ACQUISITION FAILED

Reason: HTTP 409 Conflict - Lease already held by another instance

Current Blob State:
  • Lease State: Leased
  • Lease Status: Locked
  • Lease Duration: Fixed (30 seconds)
  • Current Lease ID: 954d91dc-67fd-409a-b78a-90a78e36c928

Current Lease Holder Metadata:
  • lease_instanceId: us-east-1
  • lease_region: us-east
  • lease_hostname: DESKTOP-ABC123
  • Acquired At: 2025-12-25 12:00:00.456 UTC (0.333 seconds ago)
  • Expires At: 2025-12-25 12:00:30.456 UTC (in 29.667 seconds)

This instance CANNOT acquire the lock.
The lock is held by instance 'us-east-1' in region 'us-east'.
═══════════════════════════════════════════════════════════
```

#### Scenario 2: Azure Portal/CLI Inspection

**Objective**: Demonstrate manual inspection of blob state using Azure tools.

**Terminal 3 Commands** (while demo is running):

1. List all lease blobs:
   ```
   az storage blob list \
     --container-name leases \
     --connection-string "[from appsettings.Local.json]" \
     --output table
   ```

2. Show lease blob properties:
   ```
   az storage blob show \
     --container-name leases \
     --name critical-section-lock \
     --connection-string "[connection-string]" \
     --query "{Name:name, LeaseState:properties.lease.state, \
               LeaseStatus:properties.lease.status, \
               LastModified:properties.lastModified}"
   ```

3. Show blob metadata:
   ```
   az storage blob metadata show \
     --container-name leases \
     --name critical-section-lock \
     --connection-string "[connection-string]"
   ```

**Expected Output**:

```json
{
  "createdAt": "2025-12-25T12:00:00.200Z",
  "lastModified": "2025-12-25T12:05:30.123Z",
  "leaseName": "critical-section-lock",
  "lease_hostname": "DESKTOP-ABC123",
  "lease_instanceId": "us-east-1",
  "lease_region": "us-east",
  "lease_startTime": "2025-12-25T12:00:00Z"
}
```

#### Scenario 3: Auto-Renewal Metadata Tracking

**Objective**: Show how metadata is updated during auto-renewal cycles.

**Enhancement**: Add renewal count to metadata during each renewal.

**Expected Logs** (every 20 seconds during renewal):

```
───────────────────────────────────────────────────────────
AUTO-RENEWAL EVENT
Time: 2025-12-25 12:00:20.500 UTC
───────────────────────────────────────────────────────────
↻ Lock renewed successfully

Lease Details:
  • Lease ID: 954d91dc-67fd-409a-b78a-90a78e36c928
  • Previous Expiration: 2025-12-25 12:00:30.456 UTC
  • New Expiration: 2025-12-25 12:00:50.500 UTC
  • Renewal Count: 1

Updated Metadata:
  • lastModified: 2025-12-25T12:00:20.500Z (updated)
  • renewalCount: 1 (new)
  
Elapsed Time: 00:00:20 | Next renewal in: 00:00:10
───────────────────────────────────────────────────────────
```

### Azure Portal Inspection

**Steps for Manual Verification**:

1. Navigate to Azure Portal
2. Go to Storage Account (e.g., pranshuleasestore123456)
3. Select "Containers" under "Data storage"
4. Click on "leases" container
5. Click on "critical-section-lock" blob
6. View "Overview" tab for lease state
7. View "Metadata" section for instance information

**What to Observe**:

| Property | Location in Portal | Expected Value During Active Lease |
|----------|-------------------|-------------------------------------|
| Lease State | Overview > Lease state | Leased |
| Lease Status | Overview > Lease status | Locked |
| Last Modified | Overview > Properties | Updates with renewals |
| Metadata - leaseName | Metadata tab | critical-section-lock |
| Metadata - lease_instanceId | Metadata tab | us-east-1 (or current holder) |
| Metadata - lease_region | Metadata tab | us-east (or current holder) |

## Implementation Tasks

### Task 1: Create Azure Metadata Inspector Component

**File**: `samples/BlobLeaseSample/AzureMetadataInspector.cs`

**Responsibilities**:
- Create a class that wraps Azure Blob SDK for metadata inspection
- Implement methods to query blob properties and lease state
- Implement methods to query and format blob metadata
- Provide formatted string output for console display

**Key Methods**:
- `InspectBlobStateAsync` - Query and return complete blob state
- `FormatBlobStateForDisplay` - Format inspection result for console
- `QueryLeaseStatusAsync` - Get current lease state and status
- `ShowContainerSummaryAsync` - Display container-level information

**Data Model**:
- Create `BlobInspectionResult` class with all relevant properties
- Include lease state, status, duration, metadata, timestamps

### Task 2: Enhance DistributedLockWorker with Inspection

**File**: `samples/BlobLeaseSample/DistributedLockWorker.cs`

**Modifications**:
1. Inject `AzureMetadataInspector` into constructor
2. Add pre-acquisition inspection before `TryAcquireAsync`
3. Add post-acquisition inspection after successful acquisition
4. Add post-failure inspection when acquisition fails (show current holder)
5. Add periodic inspection during work execution (every 5 seconds)
6. Add metadata update tracking during renewals

**New Methods**:
- `InspectAndLogPreAcquisitionAsync` - Log state before attempting acquisition
- `InspectAndLogPostAcquisitionAsync` - Log state after successful acquisition
- `InspectAndLogFailureAsync` - Log current holder information on failure
- `InspectAndLogRenewalAsync` - Log metadata changes during renewal

### Task 3: Integrate Instance Metadata into Configuration

**File**: `samples/BlobLeaseSample/Program.cs`

**Modifications**:
1. Build metadata dictionary from command-line arguments and environment
2. Pass metadata to BlobLeaseManager configuration
3. Ensure metadata includes: instanceId, region, hostname, startTime

**Implementation**:
- Create metadata dictionary with instance information
- Configure `BlobLeaseProviderOptions.Metadata` with this dictionary
- Metadata will be automatically written to blob during acquisition

### Task 4: Create Azure CLI Inspection Guide

**File**: `samples/BlobLeaseSample/AZURE_INSPECTION_GUIDE.md`

**Content**:
1. Introduction to blob lease inspection
2. Prerequisites (Azure CLI, connection string)
3. Step-by-step commands with examples
4. Expected output explanations
5. Azure Portal inspection guide
6. Troubleshooting common issues

**Sections**:
- Getting Started
- Real-time Blob Inspection Commands
- Container-level Queries
- Metadata Inspection
- Azure Portal Navigation
- Understanding Lease States

### Task 5: Update README with Enhanced Demo Instructions

**File**: `samples/BlobLeaseSample/README.md`

**Additions**:
1. New section: "Blob Metadata and Azure Visibility"
2. New section: "Inspecting Lease State in Real-Time"
3. Update "Understanding the Output" with metadata examples
4. Add screenshots or ASCII diagrams of Azure Portal views
5. Add troubleshooting section for metadata issues

**Content to Add**:
- How metadata is stored in Azure Blob Storage
- What metadata is automatically vs user-provided
- How to inspect metadata during demo
- Interpretation of metadata values

### Task 6: Enhance Logging Configuration

**File**: `samples/BlobLeaseSample/appsettings.json`

**Additions**:
1. Set DistributedLeasing log level to Debug by default
2. Add configuration for inspection intervals
3. Add configuration to enable/disable metadata inspection

**Changes**:
```
"Logging": {
  "LogLevel": {
    "Default": "Information",
    "DistributedLeasing": "Debug",
    "BlobLeaseSample": "Debug"
  }
}
```

### Task 7: Create Demo Execution Script

**File**: `samples/BlobLeaseSample/run-competition-demo.sh`

**Purpose**: Automate the multi-instance competition demo execution.

**Features**:
1. Accept instance and region parameters
2. Set up terminal windows/tabs with appropriate labels
3. Display ASCII art banner for each instance
4. Show color-coded output (red for loser, green for winner)
5. Provide instructions for manual inspection

**Script Structure**:
- Parse arguments for instance IDs and regions
- Display setup instructions
- Run dotnet commands with appropriate parameters
- Show real-time Azure CLI inspection commands

### Task 8: Create Azure CLI Helper Script

**File**: `samples/BlobLeaseSample/inspect-lease-state.sh`

**Purpose**: Quick inspection of current lease state.

**Features**:
1. Read connection string from appsettings.Local.json
2. Execute common inspection commands
3. Format output for readability
4. Show lease holder information if available

**Commands to Include**:
- List all blobs with lease state
- Show metadata for specific blob
- Show properties including lease information
- Display last modified timestamp

## Testing Approach

### Test Scenario 1: Metadata Visibility

**Objective**: Verify metadata is correctly written and visible.

**Steps**:
1. Run single instance and acquire lock
2. Inspect blob metadata via Azure CLI
3. Verify all expected metadata keys are present
4. Verify metadata values match instance configuration

**Expected Results**:
- Metadata includes leaseName, createdAt, lastModified
- Metadata includes lease_instanceId, lease_region, lease_hostname
- Values match command-line arguments and environment

### Test Scenario 2: Competition Logging

**Objective**: Verify detailed logs show competition correctly.

**Steps**:
1. Start two instances simultaneously
2. Capture logs from both instances
3. Verify winner shows acquisition success with metadata
4. Verify loser shows HTTP 409 and current holder information

**Expected Results**:
- Winner logs show "LOCK ACQUIRED SUCCESSFULLY"
- Winner logs show complete metadata
- Loser logs show "LOCK ACQUISITION FAILED"
- Loser logs show holder instance information from metadata

### Test Scenario 3: Azure Inspection During Active Lease

**Objective**: Verify Azure CLI/Portal shows correct lease state.

**Steps**:
1. Run instance and acquire lock
2. While holding lock, execute Azure CLI inspection commands
3. Verify lease state shows "Leased"
4. Verify metadata shows current holder information

**Expected Results**:
- `az storage blob show` returns LeaseState: Leased
- `az storage blob metadata show` returns instance metadata
- Portal shows lock icon on blob
- Metadata tab shows all custom fields

### Test Scenario 4: Renewal Metadata Tracking

**Objective**: Verify metadata updates during renewal cycles.

**Steps**:
1. Run instance with auto-renewal enabled
2. Monitor logs for renewal events
3. Query metadata after each renewal
4. Verify lastModified timestamp updates

**Expected Results**:
- Renewal logs show metadata inspection
- lastModified updates with each renewal
- Renewal count increments correctly

## Acceptance Criteria

1. **Metadata Visibility**
   - All instance metadata is stored in blob metadata
   - Metadata is visible via Azure CLI commands
   - Metadata is visible in Azure Portal
   - Metadata includes: instanceId, region, hostname, timestamps

2. **Competition Proof**
   - Logs clearly show winner vs loser
   - Loser logs display holder information from metadata
   - Timing information proves competition
   - HTTP 409 responses are logged and explained

3. **Azure Inspection**
   - Azure CLI commands work and show current state
   - Documentation explains how to inspect via Portal
   - Lease state is correctly displayed
   - Helper scripts simplify inspection process

4. **Log Quality**
   - Logs are visually clear with proper formatting
   - Timestamps are precise and consistent
   - Metadata is formatted in readable tables
   - Events are properly sequenced and labeled

5. **Documentation**
   - README includes complete inspection guide
   - Azure CLI commands are documented with examples
   - Portal navigation is explained
   - Metadata structure is documented

## Deliverables

1. **Code Components**
   - AzureMetadataInspector.cs
   - Enhanced DistributedLockWorker.cs
   - Updated Program.cs with metadata configuration

2. **Scripts**
   - run-competition-demo.sh
   - inspect-lease-state.sh

3. **Documentation**
   - AZURE_INSPECTION_GUIDE.md
   - Updated README.md
   - Inline code comments explaining metadata handling

4. **Configuration**
   - Updated appsettings.json with debug logging
   - Example metadata configuration

## Technical Considerations

### Azure Blob Storage Lease Mechanics

**Lease States**:
- Available: No lease exists, blob can be leased
- Leased: Blob has an active lease
- Expired: Lease duration has elapsed but not yet released
- Breaking: Break period is in progress
- Broken: Lease was broken and is now available

**Lease Status**:
- Locked: Lease is active and blob is locked
- Unlocked: No active lease, blob is available

**Important Constraints**:
- Lease duration: 15-60 seconds or infinite
- Metadata size limit: 8 KB total
- Metadata key restrictions: alphanumeric, underscore, dash
- User metadata must be prefixed with "lease_" to avoid conflicts with system metadata

### Connection String Security

**Important Notes**:
- Connection strings in appsettings.Local.json contain sensitive credentials
- File is excluded from git via .gitignore
- For production, use Managed Identity or Azure Key Vault
- Demo uses connection string for simplicity

### Performance Considerations

**Metadata Inspection Impact**:
- Each inspection requires an Azure API call
- Inspections should be rate-limited to avoid throttling
- Recommended inspection interval: 5 seconds or more
- Use conditional requests (ETag) when possible

**Logging Volume**:
- Debug logging will produce significant output
- Consider log levels for production scenarios
- Inspection logs should be optional via configuration

### Error Handling

**Common Scenarios**:
- HTTP 409 during metadata update (concurrent access) - non-critical, continue
- HTTP 404 when querying non-existent blob - handle gracefully
- Network timeouts during inspection - retry with backoff
- Invalid metadata values - validate before setting

## Glossary

| Term | Definition |
|------|------------|
| Blob Lease | Azure Storage mechanism for distributed locking using blob leases |
| Lease State | Current state of the lease (Available, Leased, Expired, etc.) |
| Lease Status | Whether blob is locked or unlocked |
| Lease ID | Unique identifier (GUID) for an active lease |
| Metadata | Key-value pairs stored with a blob |
| User Metadata | Custom metadata provided via LeaseOptions |
| System Metadata | Automatically managed metadata (leaseName, createdAt, etc.) |
| Lease Duration | Time period for which lease is held (15-60 seconds) |
| Auto-Renewal | Automatic extension of lease before expiration |
| HTTP 409 Conflict | Azure response when lease is already held |
| ETag | Entity tag for blob version control |
