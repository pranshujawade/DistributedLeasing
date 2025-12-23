#!/bin/bash

# Script to analyze code duplication in Internal/ folders
# This helps identify SOLID/DRY violations

set -e

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SRC_DIR="$REPO_ROOT/src"

echo "=================================================="
echo "  Code Duplication Analysis - Internal/ Folders"
echo "=================================================="
echo ""

# Count files
TOTAL_FILES=$(find "$SRC_DIR"/DistributedLeasing.Azure.*/Internal -type f -name "*.cs" | wc -l | tr -d ' ')
TOTAL_SIZE=$(find "$SRC_DIR"/DistributedLeasing.Azure.*/Internal -type f -name "*.cs" -exec wc -c {} + | tail -1 | awk '{print $1}')

echo "📊 Overview:"
echo "  Total duplicated files: $TOTAL_FILES"
echo "  Total duplicated bytes: $(numfmt --to=iec-i --suffix=B $TOTAL_SIZE 2>/dev/null || echo "${TOTAL_SIZE}B")"
echo ""

# Analyze each provider
echo "📁 Per-Provider Breakdown:"
for provider in DistributedLeasing.Azure.Blob DistributedLeasing.Azure.Cosmos DistributedLeasing.Azure.Redis; do
    PROVIDER_FILES=$(find "$SRC_DIR/$provider/Internal" -type f -name "*.cs" 2>/dev/null | wc -l | tr -d ' ')
    PROVIDER_SIZE=$(find "$SRC_DIR/$provider/Internal" -type f -name "*.cs" -exec wc -c {} + 2>/dev/null | tail -1 | awk '{print $1}')
    
    echo ""
    echo "  $provider:"
    echo "    Files: $PROVIDER_FILES"
    echo "    Size:  $(numfmt --to=iec-i --suffix=B $PROVIDER_SIZE 2>/dev/null || echo "${PROVIDER_SIZE}B")"
    
    # List files
    echo "    Contents:"
    echo "      Abstractions:"
    find "$SRC_DIR/$provider/Internal/Abstractions" -name "*.cs" -exec basename {} \; 2>/dev/null | sed 's/^/        - /'
    echo "      Authentication:"
    find "$SRC_DIR/$provider/Internal/Authentication" -name "*.cs" -exec basename {} \; 2>/dev/null | sed 's/^/        - /'
done

echo ""
echo "=================================================="
echo "🔍 Verification - Are files identical?"
echo "=================================================="
echo ""

# Check if Abstractions files are identical
echo "Checking Abstractions folder:"
for file in ILeaseProvider.cs LeaseBase.cs LeaseManagerBase.cs; do
    echo -n "  $file: "
    
    BLOB_FILE="$SRC_DIR/DistributedLeasing.Azure.Blob/Internal/Abstractions/$file"
    COSMOS_FILE="$SRC_DIR/DistributedLeasing.Azure.Cosmos/Internal/Abstractions/$file"
    REDIS_FILE="$SRC_DIR/DistributedLeasing.Azure.Redis/Internal/Abstractions/$file"
    
    # Compare ignoring namespace differences
    DIFF1=$(diff <(grep -v "^namespace" "$BLOB_FILE" 2>/dev/null || echo "") \
                 <(grep -v "^namespace" "$COSMOS_FILE" 2>/dev/null || echo "") | wc -l | tr -d ' ')
    DIFF2=$(diff <(grep -v "^namespace" "$BLOB_FILE" 2>/dev/null || echo "") \
                 <(grep -v "^namespace" "$REDIS_FILE" 2>/dev/null || echo "") | wc -l | tr -d ' ')
    
    if [ "$DIFF1" -eq 0 ] && [ "$DIFF2" -eq 0 ]; then
        echo "✅ IDENTICAL (except namespace)"
    else
        echo "❌ DIFFERENT (drift detected!)"
    fi
done

echo ""
echo "Checking Authentication folder:"
for file in AuthenticationFactory.cs AuthenticationModes.cs AuthenticationOptions.cs \
            IAuthenticationFactory.cs ManagedIdentityOptions.cs ServicePrincipalOptions.cs \
            FederatedCredentialOptions.cs WorkloadIdentityOptions.cs; do
    echo -n "  $file: "
    
    BLOB_FILE="$SRC_DIR/DistributedLeasing.Azure.Blob/Internal/Authentication/$file"
    COSMOS_FILE="$SRC_DIR/DistributedLeasing.Azure.Cosmos/Internal/Authentication/$file"
    REDIS_FILE="$SRC_DIR/DistributedLeasing.Azure.Redis/Internal/Authentication/$file"
    
    # Compare ignoring namespace and global:: prefix differences
    DIFF1=$(diff <(grep -v "^namespace" "$BLOB_FILE" 2>/dev/null | sed 's/global:://g' || echo "") \
                 <(grep -v "^namespace" "$COSMOS_FILE" 2>/dev/null | sed 's/global:://g' || echo "") | wc -l | tr -d ' ')
    DIFF2=$(diff <(grep -v "^namespace" "$BLOB_FILE" 2>/dev/null | sed 's/global:://g' || echo "") \
                 <(grep -v "^namespace" "$REDIS_FILE" 2>/dev/null | sed 's/global:://g' || echo "") | wc -l | tr -d ' ')
    
    if [ "$DIFF1" -eq 0 ] && [ "$DIFF2" -eq 0 ]; then
        echo "✅ IDENTICAL (except namespace)"
    else
        echo "❌ DIFFERENT (drift detected!)"
    fi
done

echo ""
echo "=================================================="
echo "📈 SOLID/DRY Violation Summary"
echo "=================================================="
echo ""
echo "❌ DRY Violation:       HIGH"
echo "   - $TOTAL_FILES files duplicated"
echo "   - $(numfmt --to=iec-i --suffix=B $TOTAL_SIZE 2>/dev/null || echo "${TOTAL_SIZE}B") of duplicated code"
echo ""
echo "⚠️  Maintenance Risk:   MEDIUM-HIGH"
echo "   - Bug fixes require 3x changes"
echo "   - Script must be run consistently"
echo "   - Potential for drift over time"
echo ""
echo "💡 Recommendation:      Extract to shared packages"
echo "   - DistributedLeasing.Authentication (8 files)"
echo "   - DistributedLeasing.Abstractions (3 files)"
echo ""
echo "📖 See DUPLICATION_ANALYSIS.md for detailed recommendations"
echo ""
