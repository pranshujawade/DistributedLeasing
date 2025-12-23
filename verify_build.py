#!/usr/bin/env python3
import subprocess
import sys
import os

def run_command(cmd, description):
    print(f"\n{'='*60}")
    print(f"{description}")
    print('='*60)
    result = subprocess.run(cmd, shell=True, capture_output=True, text=True)
    print(result.stdout)
    if result.stderr:
        print(result.stderr, file=sys.stderr)
    return result.returncode == 0

if __name__ == "__main__":
    os.chdir("/Users/pjawade/repos/DistributedLeasing")
    
    # Build
    if not run_command("dotnet build DistributedLeasing.sln", "Building Solution"):
        print("\n❌ Build FAILED")
        sys.exit(1)
    
    print("\n✅ Build SUCCESS")
    
    # Test
    if not run_command("dotnet test DistributedLeasing.sln --no-build", "Running Tests"):
        print("\n❌ Tests FAILED")
        sys.exit(1)
    
    print("\n✅ Tests PASSED")
    print("\n🎉 All tasks completed successfully!")
