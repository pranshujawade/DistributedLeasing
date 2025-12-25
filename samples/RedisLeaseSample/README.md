# Distributed Lock Demo - Azure Redis Lease Sample

This sample demonstrates **distributed lock competition** using Azure Cache for Redis. It shows how multiple instances compete for the same lock using Redis's atomic operations, where only one winner can execute critical work while others fail gracefully.

## What This Demo Shows

✅ **Lock Competition**: Two instances simultaneously trying to acquire the same lock  
✅ **Winner/Loser Pattern**: Only one instance wins and executes work  
✅ **Graceful Failure**: Losing instances fail without blocking  
✅ **Automatic Renewal**: Winner maintains lock with auto-renewal  
✅ **Clean Takeover**: Lock becomes available when winner releases it  
✅ **Redis Atomic Operations**: SET NX PX for distributed locking

## Quick Start

### Option 1: Automatic Setup (Recommended)

Run the combined setup script:

```bash
cd ../../scripts
./setup-resources.sh --project redis
```

This script will:
- Create Azure Redis Cache (if needed)
- Generate `appsettings.Local.json` with connection details
- Configure all required resources

Then run the demo:

```bash
cd ../../samples/RedisLeaseSample
./run-competition-demo.sh
```

### Option 2: Interactive Configuration

Simply run the application - it will guide you through setup:

```bash
cd samples/RedisLeaseSample
dotnet run --instance demo-1 --region demo
```

If `appsettings.Local.json` is missing, you'll be prompted for:
- Azure Redis Cache name
- Key prefix (default: "lease:")
- Database number (default: 0)
- Authentication mode (Connection String or DefaultAzureCredential)

The application will generate the configuration file and start automatically.

### Option 3: Manual Setup

**Step 1: Create Azure Redis Cache**

```bash
# Using Azure CLI
REDIS_CACHE="myrediscache"
RESOURCE_GROUP="pranshu-rg"
LOCATION="eastus"

az redis create \
  --name $REDIS_CACHE \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION \
  --sku Basic \
  --vm-size C0

# Get access key
PRIMARY_KEY=$(az redis list-keys \
  --name $REDIS_CACHE \
  --resource-group $RESOURCE_GROUP \
  --query primaryKey \
  --output tsv)
```

**Step 2: Create Configuration File**

Create `appsettings.Local.json`:

**Connection String Mode:**
```json
{
  "RedisLeasing": {
    "ConnectionString": "myrediscache.redis.cache.windows.net:6380,password=YOUR_KEY,ssl=True"
  }
}
```

**Managed Identity Mode:**
```json
{
  "RedisLeasing": {
    "Endpoint": "myrediscache.redis.cache.windows.net:6380"
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

## Demo Output

**Instance 1 Output (Winner):**
```
================================================================================
DISTRIBUTED LOCK DEMO - REDIS
Instance ID: us-east-1
Region: us-east
================================================================================

[us-east-1] Attempting lock | Region: us-east
[us-east-1] ✓ Lock acquired | Lease: abc123de | Duration: 15s
[us-east-1] Working... [3s]
[us-east-1] Working... [6s]
[us-east-1] Working... [9s]
[us-east-1] Working... [12s]
[us-east-1] Completed | Duration: 15s | Renewals: 0
[us-east-1] Lock released
```

**Instance 2 Output (Loser):**
```
================================================================================
DISTRIBUTED LOCK DEMO - REDIS
Instance ID: eu-west-1
Region: eu-west
================================================================================

[eu-west-1] Attempting lock | Region: eu-west
[eu-west-1] ✗ Lock unavailable | Held by: us-east-1 (us-east)
```

## Configuration Modes

The sample supports two authentication modes:

### Mode 1: Connection String (Simple)

- **Best for**: Local development, quick testing
- **Requires**: Redis access key
- **Security**: Lower (credentials stored in file)
- **Setup**: Automatically configured by `setup-resources.sh`

### Mode 2: DefaultAzureCredential (Recommended)

- **Best for**: Production, CI/CD, team environments  
- **Requires**: Azure CLI login (`az login`) or managed identity
- **Security**: Higher (no credentials stored)
- **Falls back through**: Managed Identity → Azure CLI → Environment variables
- **Setup**: Requires "Redis Cache Contributor" or "Data Contributor" role

The application automatically detects which mode to use based on your configuration.

## How It Works

### Redis Distributed Locking

Redis uses the SET command with special options for atomic distributed locking:

1. **Acquire**: `SET key value NX PX milliseconds`
   - `NX`: Only set if key does NOT exist
   - `PX`: Set expiration in milliseconds
   - Atomic operation ensures only one instance succeeds

2. **Renew**: `SET key value XX PX milliseconds`
   - `XX`: Only set if key DOES exist
   - Updates expiration for existing lease
   - Maintains lock ownership

3. **Release**: `DEL key`
   - Explicitly removes the key
   - Makes lock immediately available

4. **Expiration**: Automatic cleanup via TTL
   - If holder crashes, lock expires automatically
   - No manual cleanup required

### Key Advantages

- **Fast**: 5-15ms latency for operations
- **Atomic**: Built-in atomic operations
- **TTL**: Automatic expiration prevents deadlocks
- **Simple**: No complex consensus algorithms needed

### Lease Storage Format

Redis stores lease as a hash with the following structure:

**Key**: `lease:critical-section-lock` (prefix + lease name)

**Hash Fields**:
```
leaseId: "abc123-def456-ghi789"
acquiredAt: "2025-12-25T21:00:00.000Z"
meta_instanceId: "us-east-1"
meta_region: "us-east"
meta_hostname: "MACHINE-NAME"
meta_startTime: "2025-12-25T21:00:00Z"
```

**TTL**: 30 seconds (default lease duration)

## Inspecting Lease State

### Using redis-cli

```bash
# Connect to Redis
redis-cli -h myrediscache.redis.cache.windows.net -p 6380 -a YOUR_KEY --tls

# Get lease data
HGETALL lease:critical-section-lock

# Check TTL
TTL lease:critical-section-lock

# Get specific field
HGET lease:critical-section-lock meta_instanceId
```

### Using Azure Portal

1. Navigate to Azure Portal → Azure Cache for Redis
2. Select your cache instance
3. Click on "Console" blade
4. Execute Redis commands:
   ```
   HGETALL lease:critical-section-lock
   TTL lease:critical-section-lock
   ```

### Using RedisMetadataInspector

The sample includes a built-in inspector that automatically:
- Displays lease state when acquisition fails
- Shows current lock holder information
- Parses and formats metadata

## Demo Scenarios

### Scenario 1: Simultaneous Startup

Start both instances at the same time to see Redis atomic operation:

```bash
# Terminal 1
dotnet run --instance us-east-1 --region us-east &

# Terminal 2 (immediately)
dotnet run --instance eu-west-1 --region eu-west
```

**Result**: First instance to execute SET NX wins, second fails immediately.

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

### Scenario 4: Lock Expiration

1. Start Instance 1 (acquires lock)
2. Stop Instance 1 without graceful shutdown (simulates crash)
3. Wait 30 seconds for lock to expire
4. Start Instance 2 - it will now acquire the lock

## Configuration Files

| File | Purpose | Auto-Generated | Version Controlled |
|------|---------|----------------|--------------------|
| `appsettings.json` | Template with defaults | No | Yes (safe to commit) |
| `appsettings.Local.json` | Your Redis credentials | Yes | No (git-ignored) |

### appsettings.json (Template - Do Not Modify)

This file contains default values for reference:

```json
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
    "SyncTimeout": 5000
  }
}
```

### appsettings.Local.json (Auto-Generated)

Generated by setup script or interactive configuration:

**Connection String Mode:**
```json
{
  "RedisLeasing": {
    "ConnectionString": "mycache.redis.cache.windows.net:6380,password=YOUR_KEY,ssl=True",
    "KeyPrefix": "lease:",
    "Database": 0
  }
}
```

**DefaultAzureCredential Mode:**
```json
{
  "RedisLeasing": {
    "Endpoint": "mycache.redis.cache.windows.net:6380",
    "KeyPrefix": "lease:",
    "Database": 0
  }
}
```

## Troubleshooting

### "Authentication failed" or "NOAUTH"

**Cause**: Incorrect access key or managed identity not configured.

**Fix**:
```bash
# Verify access key
az redis list-keys --name myrediscache --resource-group pranshu-rg

# Or run interactive setup
dotnet run --configure
```

### "Connection timeout" or "Unable to connect"

**Cause**: Firewall rules blocking access or incorrect hostname.

**Fix**:
1. Add your IP to Redis firewall rules:
   ```bash
   az redis firewall-rules create \
     --name myrediscache \
     --resource-group pranshu-rg \
     --rule-name AllowMyIP \
     --start-ip YOUR_IP \
     --end-ip YOUR_IP
   ```
2. Verify endpoint in configuration
3. Check UseSsl=true and port is 6380

### Both instances acquire the lock

**Cause**: Different Redis caches or different key prefixes.

**Fix**: Verify both instances use same `appsettings.Local.json` with same cache and KeyPrefix.

### High renewal failures

**Cause**: Network instability or Redis throttling.

**Fix**: Increase lease duration to 60 seconds:
```json
{
  "RedisLeasing": {
    "DefaultLeaseDuration": "00:01:00",
    "AutoRenewInterval": "00:00:40"
  }
}
```

### "SSL connection error"

**Cause**: TLS/SSL configuration mismatch.

**Fix**: Ensure `UseSsl=true` and port is 6380 (not 6379):
```json
{
  "RedisLeasing": {
    "UseSsl": true,
    "Port": 6380
  }
}
```

### "Failed to convert configuration" error

**Cause**: `appsettings.Local.json` missing or invalid.

**Fix**: Run one of:
```bash
# Option 1: Automatic setup
cd ../../scripts && ./setup-resources.sh --project redis

# Option 2: Interactive setup
dotnet run --configure

# Option 3: Create file manually (see Configuration Files section)
```

## Clean Up

Delete Azure resources:

```bash
# Delete Redis cache
az redis delete --name myrediscache --resource-group pranshu-rg --yes

# Or delete entire resource group
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
         │   (SET NX PX)             │   (SET NX PX)             │
         └───────────┬───────────────┴───────────┬───────────────┘
                     │                           │
                     ▼                           ▼
         ┌─────────────────────────────────────────────────┐
         │   Azure Cache for Redis                         │
         │   ┌───────────────────────────────────┐         │
         │   │  Key: lease:critical-section-lock │         │
         │   │  • leaseId: abc123...             │         │
         │   │  • meta_instanceId: us-east-1     │  ✓ Winner (SET NX succeeds)
         │   │  • TTL: 30 seconds                │         │
         │   └───────────────────────────────────┘         │
         └─────────────────────────────────────────────────┘
                     │                           │
                   SUCCESS                   NX FAILED (Key exists)
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
| Acquire (success) | 5-15ms | Single region, Basic tier |
| Acquire (conflict) | 5-15ms | Fast NX failure detection |
| Renew | 5-10ms | SET XX operation |
| Release | 3-8ms | DEL operation |

### Throughput

- **Basic C0**: ~1,000 operations per second
- **Standard C1**: ~5,000 operations per second
- **Premium P1**: ~10,000+ operations per second

**Example**: 100 leases renewed every 20 seconds = ~5 ops/s

### Redis Advantages

1. **Lower Latency**: Faster than Blob Storage and Cosmos DB for simple operations
2. **Native TTL**: Built-in expiration prevents orphaned locks
3. **Atomic Operations**: SET NX PX guarantees correctness
4. **Simpler Model**: No ETag conflicts or blob leases to manage
5. **Cost Effective**: Basic tier sufficient for most scenarios

### Comparison

| Feature | Redis | Blob Storage | Cosmos DB |
|---------|-------|--------------|-----------|
| Acquire Latency | 5-15ms | 20-50ms | 10-30ms |
| Lock Mechanism | SET NX | Blob Lease | ETag |
| Auto-Expiration | Yes (TTL) | Yes (Lease) | Manual |
| Cost (Dev) | $0.016/hr | $0.021/hr | $0.008/hr |
| Global Distribution | Limited | Yes | Yes |

## Additional Resources

- [Azure Cache for Redis Documentation](https://docs.microsoft.com/azure/azure-cache-for-redis/)
- [Redis SET Command](https://redis.io/commands/set/)
- [Distributed Locks with Redis](https://redis.io/topics/distlock)
- [DistributedLeasing Library Documentation](../../README.md)
- [DistributedLeasing.Azure.Redis Package](../../src/DistributedLeasing.Azure.Redis/README.md)

## License

This sample is part of the DistributedLeasing project and is licensed under the MIT License.
