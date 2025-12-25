#!/bin/bash

# =============================================================================
# Run Two Instances - Distributed Lock Demo
# =============================================================================
# This script orchestrates running two competing instances in a single terminal
# with color-coded output to distinguish between instances.
#
# Usage:
#   chmod +x run-demo.sh
#   ./run-demo.sh
# =============================================================================

# Color definitions
GREEN='\033[0;32m'
CYAN='\033[0;36m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
BLUE='\033[0;34m'
NC='\033[0m'

# Temp files for instance outputs
INSTANCE1_LOG="/tmp/blob-lease-demo-instance1-$$.log"
INSTANCE2_LOG="/tmp/blob-lease-demo-instance2-$$.log"

# Process IDs
INSTANCE1_PID=""
INSTANCE2_PID=""
TAIL1_PID=""
TAIL2_PID=""

# Cleanup function
cleanup() {
    echo ""
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    echo -e "${YELLOW}Stopping instances...${NC}"
    
    # Kill tail processes first
    [ -n "$TAIL1_PID" ] && kill $TAIL1_PID 2>/dev/null
    [ -n "$TAIL2_PID" ] && kill $TAIL2_PID 2>/dev/null
    
    # Kill instance processes
    if [ -n "$INSTANCE1_PID" ]; then
        kill $INSTANCE1_PID 2>/dev/null
        wait $INSTANCE1_PID 2>/dev/null
    fi
    if [ -n "$INSTANCE2_PID" ]; then
        kill $INSTANCE2_PID 2>/dev/null
        wait $INSTANCE2_PID 2>/dev/null
    fi
    
    # Clean up log files
    rm -f "$INSTANCE1_LOG" "$INSTANCE2_LOG"
    
    echo -e "${GREEN}✓ Demo stopped${NC}"
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    exit 0
}

# Register cleanup on exit
trap cleanup SIGINT SIGTERM

echo ""
echo "═══════════════════════════════════════════════════════════════"
echo -e "${BLUE}  DISTRIBUTED LOCK DEMO - DUAL INSTANCE MODE${NC}"
echo "═══════════════════════════════════════════════════════════════"
echo ""

# Check prerequisites
echo -e "${CYAN}Checking prerequisites...${NC}"
echo ""

if ! command -v dotnet &> /dev/null; then
    echo -e "${RED}✗ .NET SDK not found${NC}"
    echo ""
    echo "Please install .NET SDK from: https://dotnet.microsoft.com/download"
    exit 1
fi

DOTNET_VERSION=$(dotnet --version)
echo -e "${GREEN}✓ .NET SDK installed (version: $DOTNET_VERSION)${NC}"

# Check configuration
echo ""
echo -e "${CYAN}Checking configuration...${NC}"
echo ""

if [ ! -f "appsettings.Local.json" ]; then
    echo -e "${RED}✗ appsettings.Local.json not found${NC}"
    echo ""
    echo "Please run the Azure setup first:"
    echo -e "  ${BLUE}./azure-onetime-setup.sh${NC}"
    echo ""
    exit 1
fi

echo -e "${GREEN}✓ Configuration found${NC}"

# Launch instances
echo ""
echo -e "${CYAN}Launching instances...${NC}"
echo ""

# Launch Instance 1 in background
dotnet run --instance us-east-1 --region us-east > "$INSTANCE1_LOG" 2>&1 &
INSTANCE1_PID=$!
echo -e "${GREEN}✓ Instance 1 started (PID: $INSTANCE1_PID) - us-east-1${NC}"

# Wait a moment to let instance 1 start
sleep 2

# Launch Instance 2 in background
dotnet run --instance eu-west-1 --region eu-west > "$INSTANCE2_LOG" 2>&1 &
INSTANCE2_PID=$!
echo -e "${CYAN}✓ Instance 2 started (PID: $INSTANCE2_PID) - eu-west-1${NC}"

echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""

# Wait for log files to be created
sleep 1

# Tail both log files with color-coded prefixes
(tail -f "$INSTANCE1_LOG" 2>/dev/null | while IFS= read -r line; do
    echo -e "${GREEN}[us-east-1]${NC} $line"
done) &
TAIL1_PID=$!

(tail -f "$INSTANCE2_LOG" 2>/dev/null | while IFS= read -r line; do
    echo -e "${CYAN}[eu-west-1]${NC} $line"
done) &
TAIL2_PID=$!

echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo -e "${BLUE}Press Ctrl+C to stop the demo${NC}"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""

# Wait for processes
wait $INSTANCE1_PID 2>/dev/null
wait $INSTANCE2_PID 2>/dev/null

# If we get here, both instances exited naturally
cleanup
