#!/bin/bash

# =============================================================================
# Redis Lease Competition Demo - Minimal Output
# =============================================================================

set -e

# Configuration
INSTANCE1_ID="us-east-1"
INSTANCE1_REGION="us-east"
INSTANCE2_ID="eu-west-1"
INSTANCE2_REGION="eu-west"
LOG_DIR="/tmp/redis-lease-demo"

# Create log directory
mkdir -p "$LOG_DIR"

# Cleanup function
cleanup() {
    pkill -f "dotnet run.*RedisLeaseSample" 2>/dev/null || true
}
trap cleanup EXIT INT TERM

# Check configuration
if [ ! -f "appsettings.Local.json" ]; then
    echo "ERROR: appsettings.Local.json not found!"
    echo "Run: cd ../../scripts && ./setup-resources.sh --resource-type blob"
    exit 1
fi

# Build quietly
dotnet build --nologo -v quiet > /dev/null 2>&1 || {
    echo "Build failed"
    exit 1
}

# Launch both instances
echo "Launching instances..."
dotnet run --instance "$INSTANCE1_ID" --region "$INSTANCE1_REGION" > "$LOG_DIR/instance1.log" 2>&1 &
PID1=$!
sleep 0.2
dotnet run --instance "$INSTANCE2_ID" --region "$INSTANCE2_REGION" > "$LOG_DIR/instance2.log" 2>&1 &
PID2=$!

# Wait for completion
wait $PID1 2>/dev/null
wait $PID2 2>/dev/null

echo ""
echo "================================================="
echo "DEMO RESULTS"
echo "================================================="
echo ""

# Analyze logs and extract data
WINNER_INSTANCE=""
LOSER_INSTANCE=""
LEASE_ID=""
DURATION=""
RENEWALS=""
HELD_BY=""

if grep -q "✓ Lock acquired" "$LOG_DIR/instance1.log" 2>/dev/null; then
    WINNER_INSTANCE="$INSTANCE1_ID ($INSTANCE1_REGION)"
    LOSER_INSTANCE="$INSTANCE2_ID ($INSTANCE2_REGION)"
    LEASE_ID=$(grep "Lock acquired" "$LOG_DIR/instance1.log" | sed -n 's/.*Lease: \([^ ]*\).*/\1/p')
    DURATION=$(grep "Completed successfully" "$LOG_DIR/instance1.log" | sed -n 's/.*Duration: \([0-9]*\)s.*/\1/p')
    RENEWALS=$(grep "Completed successfully" "$LOG_DIR/instance1.log" | sed -n 's/.*Renewals: \([0-9]*\).*/\1/p')
    HELD_BY=$(grep "Held by:" "$LOG_DIR/instance2.log" | sed -n 's/.*Held by: \([^ ]*\).*/\1/p')
elif grep -q "✓ Lock acquired" "$LOG_DIR/instance2.log" 2>/dev/null; then
    WINNER_INSTANCE="$INSTANCE2_ID ($INSTANCE2_REGION)"
    LOSER_INSTANCE="$INSTANCE1_ID ($INSTANCE1_REGION)"
    LEASE_ID=$(grep "Lock acquired" "$LOG_DIR/instance2.log" | sed -n 's/.*Lease: \([^ ]*\).*/\1/p')
    DURATION=$(grep "Completed successfully" "$LOG_DIR/instance2.log" | sed -n 's/.*Duration: \([0-9]*\)s.*/\1/p')
    RENEWALS=$(grep "Completed successfully" "$LOG_DIR/instance2.log" | sed -n 's/.*Renewals: \([0-9]*\).*/\1/p')
    HELD_BY=$(grep "Held by:" "$LOG_DIR/instance1.log" | sed -n 's/.*Held by: \([^ ]*\).*/\1/p')
fi

# Display concise summary
echo "Winner Instance:    $WINNER_INSTANCE"
echo "Loser Instance:     $LOSER_INSTANCE"
echo "Lease ID:           $LEASE_ID"
echo "Execution Duration: ${DURATION}s"
echo "Lock Renewals:      $RENEWALS"
echo "Lock Held By:       $HELD_BY"
echo ""
echo "Full logs: $LOG_DIR/instance1.log, $LOG_DIR/instance2.log"
echo ""

# Auto-cleanup
rm -rf "$LOG_DIR"
