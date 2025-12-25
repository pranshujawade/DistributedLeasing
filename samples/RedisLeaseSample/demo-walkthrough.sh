#!/bin/bash

# Color codes
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
MAGENTA='\033[0;35m'
NC='\033[0m'

clear

echo -e "${BLUE}================================================================================${NC}"
echo -e "${CYAN}                REDIS LEASE SAMPLE - COMPREHENSIVE DEMO${NC}"
echo -e "${BLUE}================================================================================${NC}"
echo ""
echo "This demonstration will show:"
echo "  1. Project structure and files"
echo "  2. Configuration options"
echo "  3. How two instances compete for a lock"
echo "  4. Key code components"
echo ""
echo -e "${YELLOW}Press Enter to start...${NC}"
read

# Part 1: Project Structure
clear
echo -e "${BLUE}================================================================================${NC}"
echo -e "${CYAN}                    PART 1: PROJECT STRUCTURE${NC}"
echo -e "${BLUE}================================================================================${NC}"
echo ""
echo -e "${GREEN}RedisLeaseSample Directory:${NC}"
echo ""
ls -lh | grep -E '\.(cs|json|sh|md|csproj)$' | awk '{printf "  %-30s %10s\n", $9, $5}'
echo ""
echo -e "${YELLOW}Key files:${NC}"
echo "  • Program.cs                - Application entry point with DI"
echo "  • DistributedLockWorker.cs  - Lock competition logic"
echo "  • ConfigurationHelper.cs    - Interactive setup wizard"
echo "  • RedisMetadataInspector.cs - Redis key state inspector"
echo "  • README.md                 - Complete documentation"
echo ""
echo -e "${YELLOW}Press Enter to continue...${NC}"
read

# Part 2: Configuration
clear
echo -e "${BLUE}================================================================================${NC}"
echo -e "${CYAN}                    PART 2: CONFIGURATION OPTIONS${NC}"
echo -e "${BLUE}================================================================================${NC}"
echo ""
echo -e "${GREEN}appsettings.json (Template):${NC}"
echo ""
cat appsettings.json | head -15
echo ""
echo -e "${YELLOW}Configuration supports two authentication modes:${NC}"
echo "  1. Connection String - Simple (for local dev)"
echo "     Format: mycache.redis.cache.windows.net:6380,password=KEY,ssl=True"
echo ""
echo "  2. DefaultAzureCredential - Recommended (for production)"
echo "     Uses Azure CLI login or Managed Identity"
echo ""
echo -e "${YELLOW}Press Enter to continue...${NC}"
read

# Part 3: Lock Competition Simulation
clear
echo -e "${BLUE}================================================================================${NC}"
echo -e "${CYAN}          PART 3: SIMULATED TWO-INSTANCE LOCK COMPETITION${NC}"
echo -e "${BLUE}================================================================================${NC}"
echo ""
echo -e "${GREEN}Scenario: Two instances start simultaneously${NC}"
echo "  • Instance 1 (us-east-1) - Will win the lock"
echo "  • Instance 2 (eu-west-1) - Will fail gracefully"
echo ""
echo -e "${YELLOW}Press Enter to simulate...${NC}"
read

echo ""
echo -e "${BLUE}────────────────────────────────────────────────────────────────────────────────${NC}"
echo -e "${GREEN}[Instance 1: us-east-1]${NC}"
echo -e "${BLUE}────────────────────────────────────────────────────────────────────────────────${NC}"
sleep 0.5
echo "[us-east-1] Attempting lock | Region: us-east"
sleep 0.3
echo -e "${GREEN}[us-east-1] ✓ Lock acquired | Lease: abc12345 | Duration: 15s${NC}"
sleep 0.3
echo "[us-east-1] Starting critical work execution..."
sleep 0.5

echo ""
echo -e "${BLUE}────────────────────────────────────────────────────────────────────────────────${NC}"
echo -e "${CYAN}[Instance 2: eu-west-1]${NC}"
echo -e "${BLUE}────────────────────────────────────────────────────────────────────────────────${NC}"
sleep 0.5
echo "[eu-west-1] Attempting lock | Region: eu-west"
sleep 0.3
echo -e "${RED}[eu-west-1] ✗ Lock unavailable | Held by: us-east-1 (us-east)${NC}"
sleep 0.3
echo "[eu-west-1] Exiting gracefully..."
sleep 0.5

echo ""
echo -e "${BLUE}────────────────────────────────────────────────────────────────────────────────${NC}"
echo -e "${GREEN}[Instance 1: us-east-1] - Continues working...${NC}"
echo -e "${BLUE}────────────────────────────────────────────────────────────────────────────────${NC}"
sleep 0.5
echo "[us-east-1] Working... [3s]"
sleep 1
echo "[us-east-1] Working... [6s]"
sleep 1
echo "[us-east-1] Working... [9s]"
sleep 1
echo "[us-east-1] Working... [12s]"
sleep 1
echo -e "${GREEN}[us-east-1] Completed | Duration: 15s | Renewals: 0${NC}"
sleep 0.5
echo "[us-east-1] Lock released"
sleep 0.5

echo ""
echo -e "${YELLOW}Result: Only ONE instance executed work - Distributed lock successful!${NC}"
echo ""
echo -e "${YELLOW}Press Enter to continue...${NC}"
read

# Part 4: Redis Locking Mechanism
clear
echo -e "${BLUE}================================================================================${NC}"
echo -e "${CYAN}              PART 4: REDIS DISTRIBUTED LOCKING MECHANISM${NC}"
echo -e "${BLUE}================================================================================${NC}"
echo ""
echo -e "${GREEN}How Redis ensures only one instance wins:${NC}"
echo ""
echo "1. Acquire Lock:"
echo "   Command: SET lease:critical-section-lock <value> NX PX 30000"
echo "   • NX = Only set if key does NOT exist (atomic operation)"
echo "   • PX = Set expiration in milliseconds (30 seconds)"
echo "   • Result: TRUE for winner, NULL for losers"
echo ""
echo "2. Store Metadata:"
echo "   Redis HASH fields:"
echo "   • leaseId: abc12345-def67890"
echo "   • acquiredAt: 2025-12-25T21:00:00Z"
echo "   • meta_instanceId: us-east-1"
echo "   • meta_region: us-east"
echo "   • TTL: 30 seconds"
echo ""
echo "3. Renew Lock:"
echo "   Command: SET lease:critical-section-lock <value> XX PX 30000"
echo "   • XX = Only set if key DOES exist"
echo "   • Updates expiration every 20 seconds"
echo ""
echo "4. Release Lock:"
echo "   Command: DEL lease:critical-section-lock"
echo "   • Immediate availability for next instance"
echo ""
echo -e "${YELLOW}Press Enter to continue...${NC}"
read

# Part 5: Key Code Components
clear
echo -e "${BLUE}================================================================================${NC}"
echo -e "${CYAN}                 PART 5: KEY CODE COMPONENTS${NC}"
echo -e "${BLUE}================================================================================${NC}"
echo ""
echo -e "${GREEN}1. Program.cs - Dependency Injection Setup:${NC}"
echo ""
echo "services.AddRedisLeaseManager(options =>"
echo "{"
echo "    configuration.GetSection(\"RedisLeasing\").Bind(options);"
echo "    options.Metadata[\"instanceId\"] = instanceId;"
echo "    options.Metadata[\"region\"] = region;"
echo "});"
echo ""
echo -e "${GREEN}2. DistributedLockWorker.cs - Lock Acquisition:${NC}"
echo ""
echo "var lease = await _leaseManager.TryAcquireAsync("
echo "    leaseName: \"critical-section-lock\","
echo "    duration: null,  // Use default 30 seconds"
echo "    cancellationToken: cancellationToken);"
echo ""
echo "if (lease == null)"
echo "{"
echo "    // Loser - another instance holds the lock"
echo "    await LogFailureAndHolderInfoAsync(cancellationToken);"
echo "    return false;"
echo "}"
echo ""
echo "// Winner - proceed with critical work"
echo "await ExecuteCriticalWorkAsync(lease, cancellationToken);"
echo ""
echo -e "${YELLOW}Press Enter to continue...${NC}"
read

# Part 6: Setup and Running
clear
echo -e "${BLUE}================================================================================${NC}"
echo -e "${CYAN}                  PART 6: SETUP AND RUNNING${NC}"
echo -e "${BLUE}================================================================================${NC}"
echo ""
echo -e "${GREEN}Option 1: Automatic Setup (Recommended)${NC}"
echo ""
echo "cd ../../scripts"
echo "./setup-resources.sh --project redis"
echo ""
echo "This will:"
echo "  • Create Azure Redis Cache (Basic C0 tier)"
echo "  • Generate appsettings.Local.json with connection string"
echo "  • Configure all required resources"
echo ""
echo -e "${GREEN}Option 2: Interactive Configuration${NC}"
echo ""
echo "cd samples/RedisLeaseSample"
echo "dotnet run --configure"
echo ""
echo "This will:"
echo "  • Prompt for Redis cache name"
echo "  • Ask for authentication mode"
echo "  • Generate configuration file"
echo "  • Start the application"
echo ""
echo -e "${GREEN}Option 3: Run Demo with Two Instances${NC}"
echo ""
echo "# Terminal 1"
echo "dotnet run --instance us-east-1 --region us-east"
echo ""
echo "# Terminal 2"
echo "dotnet run --instance eu-west-1 --region eu-west"
echo ""
echo -e "${YELLOW}Press Enter to continue...${NC}"
read

# Part 7: Redis Inspection
clear
echo -e "${BLUE}================================================================================${NC}"
echo -e "${CYAN}                 PART 7: INSPECTING LEASE STATE${NC}"
echo -e "${BLUE}================================================================================${NC}"
echo ""
echo -e "${GREEN}Using redis-cli:${NC}"
echo ""
echo "redis-cli -h mycache.redis.cache.windows.net -p 6380 -a KEY --tls"
echo ""
echo "Commands:"
echo "  HGETALL lease:critical-section-lock"
echo "  TTL lease:critical-section-lock"
echo "  HGET lease:critical-section-lock meta_instanceId"
echo ""
echo -e "${GREEN}Using Azure Portal:${NC}"
echo ""
echo "1. Navigate to Azure Cache for Redis"
echo "2. Select Console blade"
echo "3. Execute commands directly"
echo ""
echo -e "${GREEN}Using RedisMetadataInspector (built-in):${NC}"
echo ""
echo "Automatically displays:"
echo "  • Current lock holder"
echo "  • Lease ID and expiration"
echo "  • Instance metadata (region, hostname)"
echo "  • TTL remaining"
echo ""
echo -e "${YELLOW}Press Enter to continue...${NC}"
read

# Part 8: Performance Characteristics
clear
echo -e "${BLUE}================================================================================${NC}"
echo -e "${CYAN}              PART 8: PERFORMANCE CHARACTERISTICS${NC}"
echo -e "${BLUE}================================================================================${NC}"
echo ""
echo -e "${GREEN}Redis vs Other Providers:${NC}"
echo ""
printf "%-20s %-15s %-15s %-15s\n" "Feature" "Redis" "Blob Storage" "Cosmos DB"
printf "%-20s %-15s %-15s %-15s\n" "--------------------" "-------------" "-------------" "-------------"
printf "%-20s %-15s %-15s %-15s\n" "Acquire Latency" "5-15ms" "20-50ms" "10-30ms"
printf "%-20s %-15s %-15s %-15s\n" "Lock Mechanism" "SET NX" "Blob Lease" "ETag"
printf "%-20s %-15s %-15s %-15s\n" "Auto-Expiration" "Yes (TTL)" "Yes (Lease)" "Manual"
printf "%-20s %-15s %-15s %-15s\n" "Cost (Dev)" "\$0.016/hr" "\$0.021/hr" "\$0.008/hr"
echo ""
echo -e "${GREEN}Key Advantages of Redis:${NC}"
echo "  ✓ Fastest latency (5-15ms)"
echo "  ✓ Native TTL support"
echo "  ✓ Atomic operations (SET NX PX)"
echo "  ✓ Simple and reliable"
echo "  ✓ No complex consensus needed"
echo ""
echo -e "${YELLOW}Press Enter to finish...${NC}"
read

# Summary
clear
echo -e "${BLUE}================================================================================${NC}"
echo -e "${CYAN}                          DEMO SUMMARY${NC}"
echo -e "${BLUE}================================================================================${NC}"
echo ""
echo -e "${GREEN}✓ Demonstrated Components:${NC}"
echo "  • Project structure (11 files)"
echo "  • Configuration options (2 auth modes)"
echo "  • Lock competition simulation"
echo "  • Redis locking mechanism (SET NX PX)"
echo "  • Code walkthrough (DI, acquisition, work execution)"
echo "  • Setup and running instructions"
echo "  • Redis state inspection methods"
echo "  • Performance characteristics"
echo ""
echo -e "${GREEN}✓ Key Takeaways:${NC}"
echo "  • Redis uses atomic SET NX for distributed locking"
echo "  • Only ONE instance can acquire the lock at a time"
echo "  • Losers fail gracefully and immediately"
echo "  • TTL prevents deadlocks if holder crashes"
echo "  • 5-15ms latency makes it fastest option"
echo ""
echo -e "${GREEN}✓ Next Steps:${NC}"
echo "  1. Run setup script: ./setup-resources.sh --project redis"
echo "  2. Test with two instances in separate terminals"
echo "  3. Inspect Redis state using redis-cli or Azure Portal"
echo "  4. Verify lock takeover after instance stops"
echo ""
echo -e "${BLUE}================================================================================${NC}"
echo -e "${CYAN}                    Demo Complete!${NC}"
echo -e "${BLUE}================================================================================${NC}"
echo ""
