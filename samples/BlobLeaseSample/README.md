# Distributed Lock Demo - Azure Blob Lease Sample

This sample demonstrates **distributed lock competition** using Azure Blob Storage leases. It shows how multiple instances compete for the same lock, where only one winner can execute critical work while others fail gracefully.

## What This Demo Shows

✅ **Lock Competition**: Two instances simultaneously trying to acquire the same lock  
✅ **Winner/Loser Pattern**: Only one instance wins and executes work  
✅ **Graceful Failure**: Losing instances fail without blocking  
✅ **Automatic Renewal**: Winner maintains lock with auto-renewal  
✅ **Clean Takeover**: Lock becomes available when winner releases it

## Quick Start

### Option 1: Automatic Setup (Recommended)

Run the combined demo script that handles setup and execution:

```bash
cd samples/BlobLeaseSample
./run-demo.sh
```

This script will:
- Validate prerequisites (.NET SDK, Azure CLI)
- Run Azure resource setup if needed
- Launch two competing instances automatically
- Display color-coded output in one terminal

**What you'll see:**
- Green text `[us-east-1]` for Instance 1
- Cyan text `[eu-west-1]` for Instance 2
- One instance acquires the lock, the other fails gracefully
- Press Ctrl+C to stop both instances

### Option 2: Manual Setup and Execution

For more control, set up and run instances manually:

```bash
# Step 1: Setup Azure resources (one-time)
cd samples/BlobLeaseSample
./setup-azure-resources.sh
```

This creates:
- Resource group: `pranshu-rg`
- Storage account with unique name
- Blob container: `leases`
- Configuration file: `appsettings.Local.json`

```bash
# Step 2: Run instances in separate terminals
# Terminal 1:
dotnet run --instance us-east-1 --region us-east

# Terminal 2:
dotnet run --instance eu-west-1 --region eu-west
```

### Option 3: Interactive First-Run

Simply run the application - it will guide you through setup:

```bash
cd samples/BlobLeaseSample
dotnet run --instance demo-1 --region demo
```

If `appsettings.Local.json` is missing, you'll be prompted for:
- Azure Storage Account name
- Container name (default: "leases")
- Authentication mode (Connection String or DefaultAzureCredential)

The application will generate the configuration file and start automatically.

### 4. Observe the Competition

**Instance 1 Output (Winner):**
```
================================================================================
DISTRIBUTED LOCK DEMO
Instance ID: us-east-1
Region: us-east
================================================================================

╔════════════════════════════════════════════════════════╗
║  ✓ LOCK ACQUIRED SUCCESSFULLY                          ║
║  This region is now the ACTIVE processor               ║
╚════════════════════════════════════════════════════════╝

Lock Details:
  • Lease ID: 954d91dc-67fd-409a-b78a-90a78e36c928
  • Instance: us-east-1
  • Region: us-east

▶ Starting critical work execution...
  (Auto-renewal is active - lock will be maintained)

[us-east-1] Processing work item #1 | Elapsed: 00:03 | Renewals: 0
[us-east-1] Processing work item #2 | Elapsed: 00:06 | Renewals: 0
...
```

**Instance 2 Output (Loser):**
```
================================================================================
DISTRIBUTED LOCK DEMO
Instance ID: eu-west-1
Region: eu-west
================================================================================

╔════════════════════════════════════════════════════════╗
║  LOCK ACQUISITION FAILED                               ║
║  Another region is currently holding the lock         ║
╚════════════════════════════════════════════════════════╝

This instance cannot execute critical work at this time.
The lock is held by another instance in a different region.
Exiting gracefully...
```

### 5. Test Takeover

1. Stop Instance 1 (Ctrl+C)
2. Run Instance 2 again - it will now acquire the lock!

## Configuration Modes

The sample supports two authentication modes:

### Mode 1: Connection String (Simple)
- **Best for**: Local development, quick testing
- **Requires**: Azure Storage connection string
- **Security**: Lower (credentials stored in file)
- **Setup**: Automatically configured by `setup-azure-resources.sh`

### Mode 2: DefaultAzureCredential (Recommended)
- **Best for**: Production, CI/CD, team environments  
- **Requires**: Azure CLI login (`az login`)
- **Security**: Higher (no credentials stored)
- **Falls back through**: Managed Identity → Azure CLI → Environment variables
- **Setup**: Choose option 2 in interactive configuration

The application automatically detects which mode to use based on your configuration.

## Configuration

### Command-Line Arguments

```bash
dotnet run --instance <instance-id> --region <region-name>
```

Examples:
```bash
# Simulate US East region
dotnet run --instance us-east-1 --region us-east

# Simulate EU West region
dotnet run --instance eu-west-1 --region eu-west

# Simulate Asia Pacific region
dotnet run --instance ap-south-1 --region ap-south
```

If not specified, random values are generated.

### Configuration Files

| File | Purpose | Auto-Generated | Version Controlled |
|------|---------|----------------|--------------------|
| `appsettings.json` | Template with placeholders | No | Yes (safe to commit) |
| `appsettings.Local.json` | Your Azure credentials | Yes | No (git-ignored) |

**How configuration works:**

1. When you run `dotnet run` for the first time:
   - Application checks for `appsettings.Local.json`
   - If missing, interactive wizard prompts for Azure details
   - Configuration file is generated automatically
   - Application proceeds with demo

2. Alternatively, run `./setup-azure-resources.sh` to:
   - Create Azure resources (storage account, container)
   - Generate `appsettings.Local.json` with connection string
   - Verify setup completed successfully

**Note**: Never commit `appsettings.Local.json` - it contains secrets and is automatically excluded by `.gitignore`.

### appsettings.json (Template - Do Not Modify)

This file contains placeholder values for reference:

```json
{
  "BlobLeasing": {
    "StorageAccountUri": "https://[YOUR_STORAGE_ACCOUNT].blob.core.windows.net",
    "ContainerName": "leases",
    "DefaultLeaseDuration": "00:00:30",
    "AutoRenew": true,
    "AutoRenewInterval": "00:00:20"
  }
}
```

**Do not replace the placeholder values here.** Instead, let the setup process generate `appsettings.Local.json` with actual values.

### appsettings.Local.json (Auto-Generated)

Generated by setup script or interactive configuration:

**Connection String Mode:**
```json
{
  "BlobLeasing": {
    "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=...;",
    "ContainerName": "leases"
  }
}
```

**DefaultAzureCredential Mode:**
```json
{
  "BlobLeasing": {
    "StorageAccountUri": "https://youraccount.blob.core.windows.net",
    "ContainerName": "leases"
  }
}
```

## How It Works

### The Distributed Lock Pattern

1. **Instance Startup**: Each instance identifies itself with instance ID and region
2. **Lock Acquisition**: Uses `TryAcquireAsync()` for non-blocking attempt
3. **Winner Path**: If successful, executes critical work with auto-renewal
4. **Loser Path**: If failed, logs failure and exits gracefully
5. **Lock Release**: Winner releases lock on shutdown (Ctrl+C)
6. **Takeover**: Next instance can now acquire the lock

### Key Code Components

**Non-Blocking Acquisition:**
```csharp
var lease = await _leaseManager.TryAcquireAsync(
    leaseName: "critical-section-lock",
    duration: null,  // Use default 30 seconds
    cancellationToken: cancellationToken);

if (lease == null)
{
    // Loser - another instance holds the lock
    return false;
}

// Winner - proceed with critical work
await ExecuteCriticalWorkAsync(lease, cancellationToken);
```

**Automatic Renewal:**
```csharp
lease.LeaseRenewed += (sender, e) =>
{
    _logger.LogDebug("Lock renewed | New expiration: {Expiration}",
        e.NewExpiration.ToString("HH:mm:ss"));
};
```

**Clean Release:**
```csharp
finally
{
    if (lease != null)
    {
        await lease.ReleaseAsync();
        await lease.DisposeAsync();
    }
}
```

## Blob Metadata and Azure Visibility

### What is Blob Metadata?

Each lease is represented by a blob in Azure Storage. This demo automatically stores **instance identification metadata** with each blob, allowing you to:

- **Track which instance holds the lock** - See instance ID, region, and hostname
- **Monitor lease lifecycle** - View creation, acquisition, and modification timestamps
- **Inspect state in real-time** - Use Azure CLI or Portal to see current holder
- **Prove competition behavior** - Verify that only one instance holds the lock at a time

### Metadata Structure

The following metadata is automatically stored with each lease blob:

| Metadata Key | Source | Description | Example Value |
|--------------|--------|-------------|---------------|
| `leaseName` | Automatic | Original lease name | `critical-section-lock` |
| `createdAt` | Automatic | Blob creation timestamp | `2025-12-25T12:00:00.000Z` |
| `lastModified` | Automatic | Last metadata update | `2025-12-25T12:05:30.000Z` |
| `lease_instanceId` | User | Instance identifier | `us-east-1` |
| `lease_region` | User | Region name | `us-east` |
| `lease_hostname` | User | Machine hostname | `MACHINE-NAME` |
| `lease_startTime` | User | Instance start time | `2025-12-25T12:00:00Z` |

**Important**: User-provided metadata is automatically prefixed with `lease_` to avoid conflicts with system metadata.

### Enhanced Demo Output

The demo now shows blob state inspection before and after lease acquisition. See **[AZURE_INSPECTION_GUIDE.md](AZURE_INSPECTION_GUIDE.md)** for complete examples and detailed inspection commands.

## Inspecting Lease State in Real-Time

### Quick Inspection Script

Use the provided helper script for easy real-time inspection:

```bash
# One-time inspection
./inspect-lease-state.sh

# Continuous monitoring (refreshes every 3 seconds)
./inspect-lease-state.sh --watch
```

### Manual Azure CLI Inspection

For more control, use Azure CLI directly:

```bash
# Set connection string
export AZURE_STORAGE_CONNECTION_STRING=$(cat appsettings.Local.json | grep "ConnectionString" | cut -d '"' -f 4)

# Show lease state
az storage blob show \
  --container-name leases \
  --name lease-critical-section-lock \
  --connection-string "$AZURE_STORAGE_CONNECTION_STRING" \
  --query "{LeaseState:properties.lease.state, Holder:metadata.lease_instanceId}"

# Show full metadata
az storage blob metadata show \
  --container-name leases \
  --name lease-critical-section-lock \
  --connection-string "$AZURE_STORAGE_CONNECTION_STRING"
```

For comprehensive inspection commands and Azure Portal navigation, see **[AZURE_INSPECTION_GUIDE.md](AZURE_INSPECTION_GUIDE.md)**.

## Running the Enhanced Demo

### Interactive Demo Script

Use the demo runner script for guided execution:

```bash
./run-competition-demo.sh
```

This interactive script will:
- Check prerequisites (Azure setup, .NET SDK)
- Let you choose a demo scenario
- Display commands for each terminal
- Show what to observe during the demo

### Manual Execution with Inspection

**Terminal 1 - Instance 1:**
```bash
dotnet run --instance us-east-1 --region us-east
```

**Terminal 2 - Instance 2:**
```bash
dotnet run --instance eu-west-1 --region eu-west
```

**Terminal 3 - Inspection (Optional):**
```bash
./inspect-lease-state.sh --watch
```


## Demo Scenarios

### Scenario 1: Simultaneous Startup

Start both instances at the same time to see competition:

```bash
# Terminal 1
dotnet run --instance us-east-1 --region us-east &

# Terminal 2 (immediately)
dotnet run --instance eu-west-1 --region eu-west
```

**Result**: First instance to reach Azure wins, second fails immediately.

### Scenario 2: Takeover on Failure

1. Start Instance 1
2. Verify it's processing work
3. Stop Instance 1 (simulates crash)
4. Start Instance 2 within 30 seconds
5. Instance 2 acquires the lock and continues work

### Scenario 3: Multiple Regions Competing

Run 3+ instances to simulate multi-region deployment:

```bash
# Terminal 1 - US East
dotnet run --instance us-east-1 --region us-east

# Terminal 2 - EU West  
dotnet run --instance eu-west-1 --region eu-west

# Terminal 3 - AP South
dotnet run --instance ap-south-1 --region ap-south
```

**Result**: Only ONE instance wins, all others fail gracefully.

## Using the Automated Demo Script

The `run-demo.sh` script provides the easiest way to run the complete demo in a single terminal.

### Features

✅ **Prerequisites Validation**: Checks for .NET SDK and Azure CLI  
✅ **Automatic Setup**: Prompts to run setup if configuration is missing  
✅ **Dual Instance Launch**: Starts both instances automatically  
✅ **Color-Coded Output**: Green for Instance 1, Cyan for Instance 2  
✅ **Single Terminal**: No need to juggle multiple windows  
✅ **Clean Shutdown**: Ctrl+C stops both instances gracefully

### Usage

```bash
cd samples/BlobLeaseSample
./run-demo.sh
```

### What You'll See

```
═══════════════════════════════════════════════════════════════
  DISTRIBUTED LOCK DEMO - DUAL INSTANCE MODE
═══════════════════════════════════════════════════════════════

Checking prerequisites...

✓ .NET SDK installed (version: 10.0.0)
✓ Azure CLI installed

Checking configuration...

✓ Configuration found: appsettings.Local.json

Launching instances...

✓ Instance 1 started (PID: 12345) - us-east-1
✓ Instance 2 started (PID: 12346) - eu-west-1

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

[us-east-1] ════════════════════════════════════════════════
[us-east-1] DISTRIBUTED LOCK DEMO
[us-east-1] Instance ID: us-east-1
[us-east-1] Region: us-east
[us-east-1] ════════════════════════════════════════════════

[eu-west-1] ════════════════════════════════════════════════
[eu-west-1] DISTRIBUTED LOCK DEMO
[eu-west-1] Instance ID: eu-west-1
[eu-west-1] Region: eu-west
[eu-west-1] ════════════════════════════════════════════════

[us-east-1] ✓ LOCK ACQUIRED SUCCESSFULLY
[us-east-1] Lease ID: 954d91dc-67fd-409a-b78a...

[eu-west-1] ✗ LOCK ACQUISITION FAILED
[eu-west-1] Current holder: us-east-1 (us-east region)
[eu-west-1] Exiting gracefully...

[us-east-1] Processing work item #1 | Elapsed: 00:03
[us-east-1] Processing work item #2 | Elapsed: 00:06

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Press Ctrl+C to stop the demo
```

### Script Behavior

1. **Prerequisites Check**: Validates that .NET SDK is installed
2. **Configuration Check**: 
   - If `appsettings.Local.json` exists: Proceeds to launch
   - If missing: Prompts "Would you like to run the Azure setup script now? (y/n)"
3. **Instance Launch**: 
   - Starts Instance 1 (us-east-1) first
   - Waits 2 seconds to let it acquire the lock
   - Starts Instance 2 (eu-west-1)
4. **Output Display**: Merges both outputs with color-coded prefixes
5. **Cleanup**: On Ctrl+C, gracefully stops both instances and removes temp files

### Advantages Over Manual Execution

| Feature | Manual (Multiple Terminals) | run-demo.sh |
|---------|----------------------------|-------------|
| Terminal Windows Required | 2-3 | 1 |
| Setup Verification | Manual | Automatic |
| Color Coding | No | Yes |
| Instance Identification | Harder (switch windows) | Easy (color + prefix) |
| Cleanup | Manual (close each) | Automatic (one Ctrl+C) |
| Time to Start | ~2-3 minutes | ~30 seconds |

### Alternative: Interactive Demo Guide

For a guided manual approach, the `run-competition-demo.sh` script provides step-by-step instructions without executing commands:

```bash
./run-competition-demo.sh
```

This displays commands for you to run manually in separate terminals, useful for learning or presentations.

## Understanding the Output

### Winner Output Breakdown

```
╔════════════════════════════════════════════════════════╗
║  ✓ LOCK ACQUIRED SUCCESSFULLY                          ║  ← Lock acquired
╚════════════════════════════════════════════════════════╝

Lock Details:
  • Lease ID: 954d91dc-67fd-409a-b78a-90a78e36c928        ← Unique lease identifier
  • Acquired At: 12/25/2025 07:50:30 +00:00                ← When lock was acquired
  • Expires At: 12/25/2025 07:51:00 +00:00                 ← Initial expiration (30s)
  • Instance: us-east-1                                     ← This instance
  • Region: us-east                                         ← This region

▶ Starting critical work execution...
  (Auto-renewal is active - lock will be maintained)       ← Renewal running

[us-east-1] Processing work item #1 | Elapsed: 00:03 | Renewals: 0
        └── Instance ID ──┘                           └── How many times renewed
```

### Loser Output Breakdown

```
╔════════════════════════════════════════════════════════╗
║  LOCK ACQUISITION FAILED                               ║  ← Failed to acquire
║  Another region is currently holding the lock         ║  ← Reason
╚════════════════════════════════════════════════════════╝

This instance cannot execute critical work at this time.   ← Cannot proceed
The lock is held by another instance in a different region.
Exiting gracefully...                                      ← Clean exit
```

## Troubleshooting

### "Invalid URI: The hostname could not be parsed" or "Failed to convert configuration value"

**Cause**: The `appsettings.Local.json` file is missing or was not generated, causing the application to use placeholder values from `appsettings.json`.

**Symptoms**:
- Error occurs during application startup
- Stack trace references `System.Uri..ctor` and `ConfigurationBinder.BindInstance`
- Error message mentions `[YOUR_STORAGE_ACCOUNT]`

**Fix**:

The application will automatically display a helpful error message with three options:

**Option 1 - Run automatic setup:**
```bash
./setup-azure-resources.sh
```

**Option 2 - Use interactive configuration:**
```bash
dotnet run --configure
```

**Option 3 - Manually create appsettings.Local.json:**
Create the file with either:
- Connection String mode: Include `ConnectionString` property
- DefaultAzureCredential mode: Include `StorageAccountUri` property

See [Configuration Files](#configuration-files) section for examples.

### Both instances acquire the lock

**Cause**: Running against different storage accounts or containers.

**Fix**: Verify both instances use the same `appsettings.Local.json`

### Instance 1 never releases the lock

**Cause**: Not stopping gracefully (kill -9, crash, etc.)

**Fix**: 
- Always use Ctrl+C for clean shutdown
- Wait 30 seconds for lease to expire automatically
- Or manually release from Azure Storage Explorer

### "Authentication failed"

**Cause**: Not logged into Azure CLI or missing permissions.

**Fix**:
```bash
az login
az account set --subscription "Visual Studio Enterprise Subscription"
```

### High renewal failures

**Cause**: Network latency or Azure throttling.

**Fix**: Increase `DefaultLeaseDuration` to 60 seconds in appsettings.json

### Application hangs during startup

**Cause**: Waiting for user input during interactive configuration.

**Fix**: 
- Answer the prompts for Azure Storage Account name and authentication mode
- Or run setup script first: `./setup-azure-resources.sh`
- Or create `appsettings.Local.json` manually

### run-demo.sh script not executable

**Cause**: Script permissions not set.

**Fix**:
```bash
chmod +x run-demo.sh
./run-demo.sh
```

### Color-coded output not working

**Cause**: Terminal doesn't support ANSI color codes.

**Fix**: Use a modern terminal emulator (iTerm2, Windows Terminal, GNOME Terminal) or run instances manually in separate terminals.

## Clean Up

Delete all Azure resources:

```bash
az group delete --name pranshu-rg --yes --no-wait
```

Delete local configuration:

```bash
rm appsettings.Local.json
```

## Architecture

```
┌─────────────────┐         ┌─────────────────┐         ┌─────────────────┐
│  Instance 1     │         │  Instance 2     │         │  Instance 3     │
│  (us-east-1)    │         │  (eu-west-1)    │         │  (ap-south-1)   │
└────────┬────────┘         └────────┬────────┘         └────────┬────────┘
         │                           │                           │
         │   TryAcquireAsync()       │   TryAcquireAsync()       │
         └───────────┬───────────────┴───────────┬───────────────┘
                     │                           │
                     ▼                           ▼
         ┌─────────────────────────────────────────────────┐
         │   Azure Blob Storage - Lease Container          │
         │   ┌───────────────────────────────────┐         │
         │   │  critical-section-lock (blob)     │         │
         │   │  • Lease ID: 954d91dc...          │         │
         │   │  • Held by: us-east-1             │  ✓ Winner
         │   │  • Expires: 07:51:00 UTC          │         │
         │   └───────────────────────────────────┘         │
         └─────────────────────────────────────────────────┘
                     │                           │
                     │                           │
                   SUCCESS                     FAILURE
                     │                           │
                     ▼                           ▼
         ┌─────────────────────┐     ┌─────────────────────┐
         │  Execute Work       │     │  Exit Gracefully    │
         │  • Auto-renewal ON  │     │  • Log failure      │
         │  • Process items    │     │  • Return false     │
         └─────────────────────┘     └─────────────────────┘
```

## Additional Resources

- [Azure Blob Storage Leases](https://docs.microsoft.com/azure/storage/blobs/storage-blob-lease)
- [Distributed Lock Pattern](https://docs.microsoft.com/azure/architecture/patterns/distributed-lock)
- [DistributedLeasing Library Documentation](../../README.md)

## License

This sample is part of the DistributedLeasing project and is licensed under the MIT License.
