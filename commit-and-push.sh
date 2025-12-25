#!/bin/bash
set -e

cd /Users/pjawade/repos/DistributedLeasing

echo "=== Step 1: Git Status ==="
git status --short

echo ""
echo "=== Step 2: Adding all changes ==="
git add -A

echo ""
echo "=== Step 3: Git status after add ==="
git status --short

echo ""
echo "=== Step 4: Creating commit ==="
git commit -m "feat: Add Redis distributed locking sample and enhance setup script

- Add complete RedisLeaseSample with interactive configuration wizard
- Enhance setup-resources.sh with --project argument (blob/cosmos/redis/all)
- Add comprehensive README and demo materials for Redis sample
- Implement RedisMetadataInspector for state inspection
- Add atomic SET NX locking mechanism with TTL support
- Bump version to 5.1.0"

echo ""
echo "=== Step 5: Creating tag ==="
git tag -a v5.1.0 -m "Release v5.1.0"

echo ""
echo "=== Step 6: Pushing to remote ==="
git push origin main

echo ""
echo "=== Step 7: Pushing tags ==="
git push origin --tags

echo ""
echo "=== COMPLETE ==="
echo "✅ Version 5.1.0 committed and pushed"
echo "✅ Tag v5.1.0 created and pushed"
echo ""
echo "View release: https://github.com/pranshujawade/DistributedLeasing/releases/new?tag=v5.1.0"
