#!/bin/bash
set -e  # Exit on error

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

echo "Building DistributedLeasing solution..."
dotnet build DistributedLeasing.sln --configuration Release

echo "Running tests..."
dotnet test DistributedLeasing.sln --configuration Release --no-build --verbosity normal

echo "Build and test completed successfully!"
