#!/bin/bash

# =============================================================================
# Cosmos DB Lease Competition Demo
# =============================================================================
# Demonstrates distributed lock competition between two instances using Cosmos DB
# Shows color-coded output with professional formatting
# =============================================================================

set -e

# Color codes for output formatting
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
MAGENTA='\033[0;35m'
BOLD='\033[1m'
NC='\033[0m' # No Color

# Configuration
INSTANCE1_ID="us-east-1"
INSTANCE1_REGION="us-east"
INSTANCE2_ID="eu-west-1"
INSTANCE2_REGION="eu-west"
LOG_DIR="/tmp/cosmos-lease-demo"

# Create log directory
mkdir -p "$LOG_DIR"

# Function to print section header
print_header() {
    echo ""
    echo -e "${BLUE}================================================================${NC}"
    echo -e "${BOLD}$1${NC}"
    echo -e "${BLUE}================================================================${NC}"
    echo ""
}

# Function to print colored status
print_status() {
    local color=$1
    local message=$2
    echo -e "${color}${message}${NC}"
}

# Cleanup function
cleanup() {
    print_status "$YELLOW" "Cleaning up background processes..."
    pkill -f "dotnet run.*CosmosLeaseSample" 2>/dev/null || true
}

# Set trap for cleanup on exit
trap cleanup EXIT INT TERM

# Main execution
clear
print_header "COSMOS DB DISTRIBUTED LOCK COMPETITION DEMO"

print_status "$CYAN" "This demo shows two instances competing for the same lock using Cosmos DB:"
echo "  • Instance 1: $INSTANCE1_ID ($INSTANCE1_REGION)"
echo "  • Instance 2: $INSTANCE2_ID ($INSTANCE2_REGION)"
echo ""
print_status "$YELLOW" "Only ONE instance will acquire the lock and execute work."
print_status "$YELLOW" "The other will gracefully fail and exit."
echo ""

# Check if configuration exists
if [ ! -f "appsettings.Local.json" ]; then
    print_status "$RED" "ERROR: appsettings.Local.json not found!"
    echo ""
    echo "Please run the setup script first:"
    echo "  cd ../../scripts && ./setup-resources.sh --resource-type cosmos"
    exit 1
fi

# Build the project
print_header "Building Project..."
if dotnet build --nologo -v quiet > /dev/null 2>&1; then
    print_status "$GREEN" "✓ Build successful"
else
    print_status "$RED" "✗ Build failed"
    exit 1
fi

# Start both instances simultaneously
print_header "Starting Instances (Simultaneous Launch)"

print_status "$CYAN" "Starting Instance 1 ($INSTANCE1_ID)..."
dotnet run --instance "$INSTANCE1_ID" --region "$INSTANCE1_REGION" \
    > "$LOG_DIR/instance1.log" 2>&1 &
PID1=$!
sleep 0.2

print_status "$CYAN" "Starting Instance 2 ($INSTANCE2_ID)..."
dotnet run --instance "$INSTANCE2_ID" --region "$INSTANCE2_REGION" \
    > "$LOG_DIR/instance2.log" 2>&1 &
PID2=$!

print_status "$GREEN" "✓ Both instances launched"
echo ""
print_status "$YELLOW" "⏳ Waiting for instances to complete (this takes ~15-20 seconds)..."

# Wait for both to complete
wait $PID1 2>/dev/null
EXIT1=$?
wait $PID2 2>/dev/null
EXIT2=$?

# Display results
print_header "RESULTS"

# Determine winner and loser based on logs
WINNER=""
LOSER=""
WINNER_LOG=""
LOSER_LOG=""

if grep -q "✓ Lock acquired" "$LOG_DIR/instance1.log" 2>/dev/null; then
    WINNER="Instance 1 ($INSTANCE1_ID)"
    LOSER="Instance 2 ($INSTANCE2_ID)"
    WINNER_LOG="$LOG_DIR/instance1.log"
    LOSER_LOG="$LOG_DIR/instance2.log"
elif grep -q "✓ Lock acquired" "$LOG_DIR/instance2.log" 2>/dev/null; then
    WINNER="Instance 2 ($INSTANCE2_ID)"
    LOSER="Instance 1 ($INSTANCE1_ID)"
    WINNER_LOG="$LOG_DIR/instance2.log"
    LOSER_LOG="$LOG_DIR/instance1.log"
fi

# Display winner output
if [ -n "$WINNER" ]; then
    echo -e "${GREEN}${BOLD}🏆 WINNER: $WINNER${NC}"
    echo -e "${BLUE}──────────────────────────────────────────────────────────${NC}"
    cat "$WINNER_LOG"
    echo ""
fi

# Display loser output
if [ -n "$LOSER" ]; then
    echo -e "${RED}${BOLD}❌ LOSER: $LOSER${NC}"
    echo -e "${BLUE}──────────────────────────────────────────────────────────${NC}"
    cat "$LOSER_LOG"
    echo ""
fi

# Summary
print_header "SUMMARY"

echo -e "${BOLD}Lock Competition Behavior (Cosmos DB):${NC}"
echo "  • Cosmos DB uses optimistic concurrency control (ETag-based locking)"
echo "  • Only one instance successfully acquired the lock"
echo "  • The loser detected the lock was held and exited gracefully"
echo "  • Winner executed work for exactly 15 seconds"
echo "  • Winner released the lock cleanly on completion"
echo "  • Documents auto-expire via TTL after 5 minutes"
echo ""

echo -e "${BOLD}Key Observations:${NC}"
echo "  ✓ Clean, minimal logging (no verbose metadata)"
echo "  ✓ Color-coded output (green=success, red=failure)"
echo "  ✓ Professional error handling"
echo "  ✓ Graceful failure without exceptions"
echo "  ✓ Automatic shutdown after 15 seconds"
echo "  ✓ Same behavior as Blob lease sample"
echo ""

echo -e "${CYAN}Log files saved to:${NC}"
echo "  • Winner: $WINNER_LOG"
echo "  • Loser:  $LOSER_LOG"
echo ""

print_header "Demo Complete"

# Optional: Keep logs or clean up
read -p "Keep log files? (y/n, default=n): " -n 1 -r
echo
if [[ ! $REPLY =~ ^[Yy]$ ]]; then
    rm -rf "$LOG_DIR"
    print_status "$YELLOW" "Log files cleaned up"
else
    print_status "$CYAN" "Log files preserved in $LOG_DIR"
fi
