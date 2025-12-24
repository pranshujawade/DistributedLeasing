#!/bin/bash

# Script to build all NuGet packages for DistributedLeasing
# Usage: ./pack-all.sh [--configuration Release|Debug] [--version x.y.z]

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
CONFIGURATION="Release"
VERSION=""

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --configuration)
            CONFIGURATION="$2"
            shift 2
            ;;
        --version)
            VERSION="$2"
            shift 2
            ;;
        *)
            echo "Unknown option: $1"
            echo "Usage: $0 [--configuration Release|Debug] [--version x.y.z]"
            exit 1
            ;;
    esac
done

cd "$REPO_ROOT"

echo "========================================"
echo "Building DistributedLeasing NuGet Packages"
echo "Configuration: $CONFIGURATION"
if [ ! -z "$VERSION" ]; then
    echo "Version: $VERSION"
fi
echo "========================================"

# Clean previous build artifacts
echo ""
echo "Cleaning previous builds..."
rm -rf nupkgs
mkdir -p nupkgs

# Update version if specified
if [ ! -z "$VERSION" ]; then
    echo ""
    echo "Updating version to $VERSION in Directory.Build.props..."
    sed -i.bak "s|<Version>.*</Version>|<Version>$VERSION</Version>|" Directory.Build.props
    rm -f Directory.Build.props.bak
fi

# Restore dependencies
echo ""
echo "Restoring dependencies..."
dotnet restore

# Build solution
echo ""
echo "Building solution..."
dotnet build --configuration $CONFIGURATION --no-restore

# Run tests
echo ""
echo "Running tests..."
dotnet test --configuration $CONFIGURATION --no-build --verbosity normal

# Pack all projects
echo ""
echo "Packing NuGet packages..."

PROJECTS=(
    "src/DistributedLeasing.Abstractions/DistributedLeasing.Abstractions.csproj"
    "src/DistributedLeasing.Azure.Blob/DistributedLeasing.Azure.Blob.csproj"
    "src/DistributedLeasing.Azure.Cosmos/DistributedLeasing.Azure.Cosmos.csproj"
    "src/DistributedLeasing.Azure.Redis/DistributedLeasing.Azure.Redis.csproj"
    "src/DistributedLeasing.ChaosEngineering/DistributedLeasing.ChaosEngineering.csproj"
)

for project in "${PROJECTS[@]}"; do
    echo ""
    echo "Packing $project..."
    dotnet pack "$project" \
        --configuration $CONFIGURATION \
        --no-build \
        --output nupkgs \
        --include-symbols \
        --include-source
done

# List generated packages
echo ""
echo "========================================"
echo "Generated packages:"
echo "========================================"
ls -lh nupkgs/*.nupkg nupkgs/*.snupkg

echo ""
echo "✅ All packages built successfully!"
echo "Packages are available in: $REPO_ROOT/nupkgs"
