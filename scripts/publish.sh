#!/bin/bash

# Script to publish NuGet packages to NuGet.org
# Usage: ./publish.sh [--dry-run] [--api-key-file path/to/file]

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
NUPKG_DIR="$REPO_ROOT/nupkgs"
API_KEY_FILE="$REPO_ROOT/.nuget-api-key"
DRY_RUN=false
NUGET_SOURCE="https://api.nuget.org/v3/index.json"

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --dry-run)
            DRY_RUN=true
            shift
            ;;
        --api-key-file)
            API_KEY_FILE="$2"
            shift 2
            ;;
        *)
            echo "Unknown option: $1"
            echo "Usage: $0 [--dry-run] [--api-key-file path/to/file]"
            exit 1
            ;;
    esac
done

cd "$REPO_ROOT"

echo "========================================"
echo "Publishing DistributedLeasing NuGet Packages"
if [ "$DRY_RUN" = true ]; then
    echo "MODE: DRY RUN (no actual publishing)"
fi
echo "========================================"

# Check if packages exist
if [ ! -d "$NUPKG_DIR" ] || [ -z "$(ls -A $NUPKG_DIR/*.nupkg 2>/dev/null)" ]; then
    echo "❌ Error: No packages found in $NUPKG_DIR"
    echo "Run ./scripts/pack-all.sh first to build packages."
    exit 1
fi

# Get API key with multiple fallback methods
API_KEY=""

# Method 1: Try to read from file
if [ -f "$API_KEY_FILE" ]; then
    echo "Reading API key from file: $API_KEY_FILE"
    API_KEY=$(cat "$API_KEY_FILE" | tr -d '[:space:]')
fi

# Method 2: Try environment variable
if [ -z "$API_KEY" ] && [ ! -z "$NUGET_API_KEY" ]; then
    echo "Using API key from environment variable NUGET_API_KEY"
    API_KEY="$NUGET_API_KEY"
fi

# Method 3: Prompt user
if [ -z "$API_KEY" ]; then
    echo ""
    echo "NuGet API key not found in file or environment variable."
    echo "Please enter your NuGet.org API key:"
    read -s API_KEY
    echo ""
    
    # Offer to save for future use
    read -p "Do you want to save this API key to $API_KEY_FILE for future use? (y/n) " -n 1 -r
    echo ""
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        echo "$API_KEY" > "$API_KEY_FILE"
        chmod 600 "$API_KEY_FILE"
        echo "✅ API key saved to $API_KEY_FILE (make sure it's in .gitignore!)"
    fi
fi

if [ -z "$API_KEY" ]; then
    echo "❌ Error: No API key provided"
    exit 1
fi

# List packages to be published
echo ""
echo "Packages to be published:"
ls -1 $NUPKG_DIR/*.nupkg | grep -v '\.symbols\.nupkg$'

echo ""
if [ "$DRY_RUN" = true ]; then
    echo "DRY RUN: Would publish the above packages"
    exit 0
fi

read -p "Do you want to publish these packages to NuGet.org? (y/n) " -n 1 -r
echo ""

if [[ ! $REPLY =~ ^[Yy]$ ]]; then
    echo "Publishing cancelled."
    exit 1
fi

# Publish packages in dependency order
echo ""
echo "Publishing packages..."

PUBLISH_ORDER=(
    "DistributedLeasing.Core"
    "DistributedLeasing.Abstractions"
    "DistributedLeasing.Authentication"
    "DistributedLeasing.Azure.Blob"
    "DistributedLeasing.Azure.Cosmos"
    "DistributedLeasing.Azure.Redis"
    "DistributedLeasing.Extensions.DependencyInjection"
)

for package_name in "${PUBLISH_ORDER[@]}"; do
    PACKAGE_FILE=$(ls $NUPKG_DIR/${package_name}.*.nupkg | grep -v '\.symbols\.nupkg$' | head -1)
    SYMBOL_FILE=$(ls $NUPKG_DIR/${package_name}.*.snupkg 2>/dev/null | head -1 || echo "")
    
    if [ -f "$PACKAGE_FILE" ]; then
        echo ""
        echo "Publishing $package_name..."
        dotnet nuget push "$PACKAGE_FILE" \
            --api-key "$API_KEY" \
            --source "$NUGET_SOURCE" \
            --skip-duplicate
        
        # Publish symbol package if exists
        if [ ! -z "$SYMBOL_FILE" ] && [ -f "$SYMBOL_FILE" ]; then
            echo "Publishing symbols for $package_name..."
            dotnet nuget push "$SYMBOL_FILE" \
                --api-key "$API_KEY" \
                --source "$NUGET_SOURCE" \
                --skip-duplicate
        fi
        
        # Small delay to avoid rate limiting
        sleep 2
    else
        echo "⚠️  Warning: Package file not found for $package_name"
    fi
done

echo ""
echo "========================================"
echo "✅ Publishing complete!"
echo "========================================"
echo ""
echo "Next steps:"
echo "1. Push your changes and tags: git push && git push --tags"
echo "2. Monitor package status at: https://www.nuget.org/account/Packages"
echo "3. Packages may take a few minutes to appear in search results"
