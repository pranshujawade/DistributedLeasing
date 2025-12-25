# Distributed Lock Demo - Azure Cosmos DB Lease Sample

This sample demonstrates **distributed lock competition** using Azure Cosmos DB leases with ETag-based optimistic concurrency. It shows how multiple instances compete for the same lock using Cosmos DB's global distribution capabilities.

## What This Demo Shows

✅ **Lock Competition**: Two instances simultaneously trying to acquire the same lock  
✅ **ETag-Based Concurrency**: Optimistic concurrency with automatic conflict resolution  
✅ **Winner/Loser Pattern**: Only one instance wins and executes work  
✅ **Graceful Failure**: Losing instances fail without blocking  
✅ **Automatic Renewal**: Winner maintains lock with auto-renewal  
✅ **Lease Documents**: Inspect lease state in Cosmos DB

## Quick Start

### Option 1: Interactive Configuration (Recommended)

Simply run the application - it will guide you through setup:

```bash
cd samples/CosmosLeaseSample
dotnet run --instance demo-1 --region demo
```

If `appsettings.Local.json` is missing, you'll be prompted for:
- Azure Cosmos DB account endpoint
- Database name (default: "DistributedLeasing")
- Container name (default: "Leases")
- Authentication mode (Connection String or DefaultAzureCredential)

The application will generate the configuration file and start automatically.

### Option 2: Manual Setup

**Step 1: Create Azure Cosmos DB Resources**

```bash
# Using Azure CLI
COSMOS_ACCOUNT="mycosmosaccount"
RESOURCE_GROUP="pranshu-rg"
LOCATION="eastus"

az cosmosdb create \
  --name $COSMOS_ACCOUNT \
  --resource-group $RESOURCE_GROUP \
  --locations regionName=$LOCATION failoverPriority=0

az cosmosdb sql database create \
  --account-name $COSMOS_ACCOUNT \
  --resource-group $RESOURCE_GROUP \
  --name DistributedLeasing

az cosmosdb sql container create \
  --account-name $COSMOS_ACCOUNT \
  --resource-group $RESOURCE_GROUP \
  --database-name DistributedLeasing \
  --name Leases \
  --partition-key-path "/leaseKey" \
  --throughput 400
```

**Step 2: Create Configuration File**

Create `appsettings.Local.json`:

**Connection String Mode:**
```json
{
  "CosmosLeasing": {
    "ConnectionString": "AccountEndpoint=https://mycosmosaccount.documents.azure.com:443/;AccountKey=...",
    "DatabaseName": "DistributedLeasing",
    "ContainerName": "Leases"
  }
}
```

**Managed Identity Mode:**
```json
{
  "CosmosLeasing": {
    "AccountEndpoint": "https://mycosmosaccount.documents.azure.com:443/",
    "DatabaseName": "DistributedLeasing",
    "ContainerName": "Leases"
  }
}
```

**Step 3: Run Demo Instances**

```bash
# Terminal 1
dotnet run --instance us-east-1 --region us-east

# Terminal 2
dotnet run --instance eu-west-1 --region eu-west
```

### Option 3: Run Competition Demo Script

Use the automated demo runner:

```bash
./run-competition-demo.sh
```

This interactive script will:
- Guide you through running multiple instances
- Display commands for each terminal
- Show what to observe during the demo

## Demo Output

**Instance 1 Output (Winner):**
```
================================================================================
DISTRIBUTED LOCK DEMO - COSMOS DB
Instance ID: us-east-1
Region: us-east
================================================================================

╔════════════════════════════════════════════════════════╗
║  ✓ LOCK ACQUIRED SUCCESSFULLY                          ║
║  This region is now the ACTIVE processor               ║
╚════════════════════════════════════════════════════════╝

Lock Details:
  • Lease ID: abc123-def456-ghi789
  • Instance: us-east-1
  • Region: us-east
  • ETag: "0000abc1-0000-0000-0000-000000000000"

▶ Starting critical work execution...
  (Auto-renewal is active - lock will be maintained)

[us-east-1] Processing work item #1 | Elapsed: 00:03 | Renewals: 0
[us-east-1] Processing work item #2 | Elapsed: 00:06 | Renewals: 0
...
```

**Instance 2 Output (Loser):**
```
================================================================================
DISTRIBUTED LOCK DEMO - COSMOS DB
Instance ID: eu-west-1
Region: eu-west
================================================================================

╔════════════════════════════════════════════════════════╗
║  LOCK ACQUISITION FAILED                               ║
║  Another region is currently holding the lock         ║
╚════════════════════════════════════════════════════════╝

Current holder: us-east-1 (us-east region)
This instance cannot execute critical work at this time.
The lock is held by another instance in a different region.
Exiting gracefully...
```

## Configuration Modes

The sample supports two authentication modes:

### Mode 1: Connection String (Simple)

- **Best for**: Local development, quick testing
- **Requires**: Cosmos DB connection string (includes account key)
- **Security**: Lower (credentials stored in file)
- **Setup**: Get from Azure Portal → Cosmos DB → Keys

### Mode 2: DefaultAzureCredential (Recommended)

- **Best for**: Production, CI/CD, team environments  
- **Requires**: Azure CLI login (`az login`) or managed identity
- **Security**: Higher (no credentials stored)
- **Falls back through**: Managed Identity → Azure CLI → Environment variables
- **Setup**: Assign "Cosmos DB Built-in Data Contributor" role to identity

The application automatically detects which mode to use based on your configuration.

## How It Works

### ETag-Based Optimistic Concurrency

Unlike Azure Blob's pessimistic locking, Cosmos DB uses optimistic concurrency:

1. **Read Lease Document**: Retrieve current lease with ETag
2. **Check Availability**: Verify lease is available or expired
3. **Conditional Update**: Try to update with ETag constraint
4. **Conflict Detection**: If ETag changed, another instance acquired it
5. **Winner/Loser**: Successful update wins, failed update loses

**Key Characteristics:**
- **No Server-Side Locks**: Cosmos DB doesn't hold locks
- **ETag Validation**: Ensures atomic updates
- **Fast Conflict Detection**: Immediate feedback on conflicts
- **Global Distribution**: Works across regions

### Lease Document Structure

Each lease is stored as a JSON document:

```json
{
  "id": "lease-critical-section-lock",
  "leaseKey": "critical-section-lock",
  "leaseId": "abc123-def456-ghi789",
  "ownerId": "us-east-1",
  "acquiredAt": "2025-12-25T12:00:00Z",
  "expiresAt": "2025-12-25T12:01:00Z",
  "metadata": {
    "instanceId": "us-east-1",
    "region": "us-east",
    "hostname": "MACHINE-NAME",
    "startTime": "2025-12-25T12:00:00Z"
  },
  "_etag": "\"0000abc1-0000-0000-0000-000000000000\""
}
```

**Important Fields:**
- `id`: Document ID (lease-{leaseKey})
- `leaseId`: Unique identifier for this lease instance
- `ownerId`: Instance holding the lease
- `expiresAt`: When lease expires if not renewed
- `_etag`: Cosmos DB ETag for optimistic concurrency
- `metadata`: Custom instance information

## Inspecting Lease State

### Azure Portal Inspection

1. Navigate to Azure Portal → Cosmos DB account
2. Select "Data Explorer"
3. Expand database → container → Items
4. Click on lease document to view JSON
5. Observe `_etag`, `ownerId`, `expiresAt` fields

### Query Lease Documents

Using Azure Cosmos DB Data Explorer or SDK:

```sql
-- Get all active leases
SELECT * FROM c
WHERE c.expiresAt > GetCurrentDateTime()

-- Get leases by owner
SELECT * FROM c
WHERE c.ownerId = "us-east-1"

-- Get lease metadata
SELECT c.leaseKey, c.ownerId, c.metadata
FROM c
```

### Using Azure CLI

```bash
# Set variables
COSMOS_ACCOUNT="mycosmosaccount"
RESOURCE_GROUP="pranshu-rg"
DATABASE="DistributedLeasing"
CONTAINER="Leases"

# Query lease documents
az cosmosdb sql container query \
  --account-name $COSMOS_ACCOUNT \
  --resource-group $RESOURCE_GROUP \
  --database-name $DATABASE \
  --name $CONTAINER \
  --query-text "SELECT * FROM c WHERE c.leaseKey = 'critical-section-lock'"
```

## Demo Scenarios

### Scenario 1: Simultaneous Startup

Start both instances at the same time to see ETag conflict resolution:

```bash
# Terminal 1
dotnet run --instance us-east-1 --region us-east &

# Terminal 2 (immediately)
dotnet run --instance eu-west-1 --region eu-west
```

**Result**: First instance to write wins, second detects ETag mismatch and fails.

### Scenario 2: Takeover on Failure

1. Start Instance 1
2. Verify it's processing work
3. Stop Instance 1 (simulates crash)
4. Start Instance 2 within 60 seconds
5. Instance 2 detects expired lease and acquires it

### Scenario 3: Multi-Region Competition

Run 3+ instances to simulate global deployment:

```bash
# Terminal 1 - US East
dotnet run --instance us-east-1 --region us-east

# Terminal 2 - EU West  
dotnet run --instance eu-west-1 --region eu-west

# Terminal 3 - AP South
dotnet run --instance ap-south-1 --region ap-south
```

**Result**: Only ONE instance wins, all others fail gracefully.

### Scenario 4: Document Inspection During Execution

While an instance is running:

1. Open Azure Portal → Data Explorer
2. Navigate to the lease document
3. Observe `_etag` changing with each renewal
4. See `expiresAt` being updated

## Configuration Files

| File | Purpose | Auto-Generated | Version Controlled |
|------|---------|----------------|--------------------|
| `appsettings.json` | Template with placeholders | No | Yes (safe to commit) |
| `appsettings.Local.json` | Your Cosmos credentials | Yes | No (git-ignored) |

**appsettings.json (Template - Do Not Modify):**
```json
{
  "CosmosLeasing": {
    "AccountEndpoint": "https://[YOUR_COSMOS_ACCOUNT].documents.azure.com:443/",
    "DatabaseName": "DistributedLeasing",
    "ContainerName": "Leases",
    "DefaultLeaseDuration": "00:01:00",
    "AutoRenew": true,
    "AutoRenewInterval": "00:00:40"
  }
}
```

**appsettings.Local.json (Auto-Generated):**

This file is created by the interactive setup or manually. Never commit it.

## Troubleshooting

### "Failed to convert configuration value" or "Invalid URI"

**Cause**: `appsettings.Local.json` missing or contains placeholders.

**Fix**: Run one of:
```bash
# Option 1: Interactive setup
dotnet run --configure

# Option 2: Create file manually
# See Configuration Files section
```

### "Forbidden" or "Insufficient permissions"

**Cause**: Managed identity lacks permissions on Cosmos DB.

**Fix**:
1. Assign "Cosmos DB Built-in Data Contributor" role
2. Or use connection string mode for development

### Both instances acquire the lock

**Cause**: Running against different databases or containers.

**Fix**: Verify both use the same `appsettings.Local.json` with same database/container.

### Frequent ETag conflicts in logs

**Cause**: This is expected - it's how concurrency works!

**Observation**: Normal behavior when multiple instances compete. The losing instance will see ETag mismatches and fail gracefully.

### High Request Unit (RU) consumption

**Cause**: Frequent renewals or many concurrent instances.

**Fix**:
```json
{
  "CosmosLeasing": {
    "DefaultLeaseDuration": "00:02:00",  // Increase to 2 minutes
    "AutoRenewInterval": "00:01:20"      // Reduce renewal frequency
  }
}
```

### Lease not released after application crash

**Cause**: Application terminated before releasing.

**Solution**: This is expected - the lease document remains but expires based on `expiresAt`. Next acquisition attempt will detect expiration and reacquire.

## Clean Up

Delete Azure resources:

```bash
# Delete Cosmos DB account (removes everything)
az cosmosdb delete --name mycosmosaccount --resource-group pranshu-rg --yes

# Or delete just the database
az cosmosdb sql database delete \
  --account-name mycosmosaccount \
  --resource-group pranshu-rg \
  --name DistributedLeasing \
  --yes
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
         │   (Read + ETag Update)    │   (Read + ETag Update)    │
         └───────────┬───────────────┴───────────┬───────────────┘
                     │                           │
                     ▼                           ▼
         ┌─────────────────────────────────────────────────┐
         │   Azure Cosmos DB - Leases Container            │
         │   ┌───────────────────────────────────┐         │
         │   │  Document: lease-critical-...     │         │
         │   │  • leaseId: abc123...             │         │
         │   │  • ownerId: us-east-1             │  ✓ Winner (ETag matched)
         │   │  • expiresAt: 12:01:00 UTC        │         │
         │   │  • _etag: "0000abc1..."           │         │
         │   └───────────────────────────────────┘         │
         └─────────────────────────────────────────────────┘
                     │                           │
                   SUCCESS                  ETag MISMATCH (Conflict)
                     │                           │
                     ▼                           ▼
         ┌─────────────────────┐     ┌─────────────────────┐
         │  Execute Work       │     │  Exit Gracefully    │
         │  • Auto-renewal ON  │     │  • Log failure      │
         │  • Process items    │     │  • Return false     │
         └─────────────────────┘     └─────────────────────┘
```

## Performance Characteristics

### Latency

| Operation | Typical Latency | Notes |
|-----------|----------------|-------|
| Acquire (success) | 5-20ms | Single region, Session consistency |
| Acquire (conflict) | 5-20ms | Fast ETag conflict detection |
| Renew | 5-20ms | Point write operation |
| Release | 5-20ms | Document delete |

### Request Units (RUs)

- **Acquire**: ~5-10 RUs (read + conditional write)
- **Renew**: ~5-10 RUs (conditional update)
- **Release**: ~5 RUs (delete)

**Example**: 100 leases renewed every 40 seconds = ~25 RU/s minimum

## Additional Resources

- [Azure Cosmos DB Documentation](https://docs.microsoft.com/azure/cosmos-db/)
- [ETag and Optimistic Concurrency](https://docs.microsoft.com/azure/cosmos-db/sql/database-transactions-optimistic-concurrency)
- [DistributedLeasing Library Documentation](../../README.md)
- [DistributedLeasing.Azure.Cosmos Package](../../src/DistributedLeasing.Azure.Cosmos/README.md)

## License

This sample is part of the DistributedLeasing project and is licensed under the MIT License.
