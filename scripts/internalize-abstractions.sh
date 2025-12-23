#!/bin/bash

# Script to copy and internalize abstractions into providers
# This automates Phase 2 of the refactoring

REPO_ROOT="/Users/pjawade/repos/DistributedLeasing"
SRC_DIR="$REPO_ROOT/src"
ABSTRACTIONS_SRC="$SRC_DIR/DistributedLeasing.Abstractions"

PROVIDERS=("DistributedLeasing.Azure.Blob" "DistributedLeasing.Azure.Cosmos" "DistributedLeasing.Azure.Redis")

for provider in "${PROVIDERS[@]}"; do
    echo "Processing provider: $provider"
    
    PROVIDER_DIR="$SRC_DIR/$provider"
    INTERNAL_ABS_DIR="$PROVIDER_DIR/Internal/Abstractions"
    
    # Create directory if it doesn't exist
    mkdir -p "$INTERNAL_ABS_DIR"
    
    # Copy abstraction files
    cp "$ABSTRACTIONS_SRC/ILeaseProvider.cs" "$INTERNAL_ABS_DIR/"
    cp "$ABSTRACTIONS_SRC/LeaseBase.cs" "$INTERNAL_ABS_DIR/"
    cp "$ABSTRACTIONS_SRC/LeaseManagerBase.cs" "$INTERNAL_ABS_DIR/"
    
    # Update namespace and visibility in copied files
    for file in "$INTERNAL_ABS_DIR"/*.cs; do
        # Replace namespace
        sed -i.bak "s/namespace DistributedLeasing.Abstractions/namespace ${provider}.Internal.Abstractions/" "$file"
        
        # Make public types internal
        sed -i.bak "s/public interface/internal interface/" "$file"
        sed -i.bak "s/public abstract class/internal abstract class/" "$file"
        
        # Remove backup files
        rm -f "${file}.bak"
    done
    
    echo "Completed: $provider"
done

echo "Abstraction internalization complete!"
