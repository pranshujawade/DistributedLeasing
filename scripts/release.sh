#!/bin/bash

# Complete release workflow: bump version, build, test, and optionally publish
# Usage: ./release.sh [major|minor|patch] [--pre-release alpha|beta|rc] [--publish] [--dry-run]

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

BUMP_TYPE=""
PRE_RELEASE=""
SHOULD_PUBLISH=false
DRY_RUN=false

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        major|minor|patch)
            BUMP_TYPE="$1"
            shift
            ;;
        --pre-release)
            PRE_RELEASE="$2"
            shift 2
            ;;
        --publish)
            SHOULD_PUBLISH=true
            shift
            ;;
        --dry-run)
            DRY_RUN=true
            shift
            ;;
        *)
            echo "Unknown option: $1"
            echo "Usage: $0 [major|minor|patch] [--pre-release alpha|beta|rc] [--publish] [--dry-run]"
            exit 1
            ;;
    esac
done

if [ -z "$BUMP_TYPE" ]; then
    echo "Error: You must specify bump type (major, minor, or patch)"
    echo "Usage: $0 [major|minor|patch] [--pre-release alpha|beta|rc] [--publish] [--dry-run]"
    exit 1
fi

cd "$REPO_ROOT"

echo "========================================"
echo "DistributedLeasing Release Workflow"
echo "========================================"
echo "Bump Type: $BUMP_TYPE"
if [ ! -z "$PRE_RELEASE" ]; then
    echo "Pre-release: $PRE_RELEASE"
fi
if [ "$SHOULD_PUBLISH" = true ]; then
    echo "Publish: Yes"
fi
if [ "$DRY_RUN" = true ]; then
    echo "Mode: DRY RUN"
fi
echo "========================================"
echo ""

# Check for uncommitted changes
if [ "$DRY_RUN" = false ]; then
    if ! git diff-index --quiet HEAD --; then
        echo "⚠️  Warning: You have uncommitted changes"
        read -p "Do you want to continue? (y/n) " -n 1 -r
        echo ""
        if [[ ! $REPLY =~ ^[Yy]$ ]]; then
            echo "Release cancelled."
            exit 1
        fi
    fi
fi

# Step 1: Bump version
echo "Step 1: Bumping version..."
VERSION_ARGS="$BUMP_TYPE"
if [ ! -z "$PRE_RELEASE" ]; then
    VERSION_ARGS="$VERSION_ARGS --pre-release $PRE_RELEASE"
fi

# Run version bump with auto-confirm for scripted execution
CURRENT_VERSION=$(grep -o '<Version>.*</Version>' "$REPO_ROOT/Directory.Build.props" | sed 's|<Version>\(.*\)</Version>|\1|')
echo "Current version: $CURRENT_VERSION"

# Calculate new version (simplified logic - uses version-bump.sh logic)
BASE_VERSION=$(echo "$CURRENT_VERSION" | sed 's/-.*$//')
IFS='.' read -r -a VERSION_PARTS <<< "$BASE_VERSION"
MAJOR="${VERSION_PARTS[0]}"
MINOR="${VERSION_PARTS[1]}"
PATCH="${VERSION_PARTS[2]}"

case $BUMP_TYPE in
    major)
        MAJOR=$((MAJOR + 1))
        MINOR=0
        PATCH=0
        ;;
    minor)
        MINOR=$((MINOR + 1))
        PATCH=0
        ;;
    patch)
        PATCH=$((PATCH + 1))
        ;;
esac

NEW_VERSION="$MAJOR.$MINOR.$PATCH"
if [ ! -z "$PRE_RELEASE" ]; then
    PRE_NUMBER=1
    if [[ "$CURRENT_VERSION" == *"-$PRE_RELEASE."* ]]; then
        CURRENT_PRE_NUMBER=$(echo "$CURRENT_VERSION" | sed "s/.*-$PRE_RELEASE\.\([0-9]*\)/\1/")
        PRE_NUMBER=$((CURRENT_PRE_NUMBER + 1))
    fi
    NEW_VERSION="$NEW_VERSION-$PRE_RELEASE.$PRE_NUMBER"
fi

echo "New version will be: $NEW_VERSION"

if [ "$DRY_RUN" = false ]; then
    echo ""
    read -p "Proceed with version $NEW_VERSION? (y/n) " -n 1 -r
    echo ""
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        echo "Release cancelled."
        exit 1
    fi
    
    sed -i.bak "s|<Version>.*</Version>|<Version>$NEW_VERSION</Version>|" "$REPO_ROOT/Directory.Build.props"
    rm -f "$REPO_ROOT/Directory.Build.props.bak"
    echo "✅ Version updated to $NEW_VERSION"
fi

# Step 2: Build and test
echo ""
echo "Step 2: Building and testing packages..."
if [ "$DRY_RUN" = false ]; then
    "$SCRIPT_DIR/pack-all.sh" --configuration Release
else
    echo "DRY RUN: Would build packages with version $NEW_VERSION"
fi

# Step 3: Create git commit and tag
if [ "$DRY_RUN" = false ]; then
    echo ""
    echo "Step 3: Creating git commit and tag..."
    git add Directory.Build.props
    git commit -m "Bump version to $NEW_VERSION"
    git tag -a "v$NEW_VERSION" -m "Release v$NEW_VERSION"
    echo "✅ Created commit and tag v$NEW_VERSION"
fi

# Step 4: Publish (if requested)
if [ "$SHOULD_PUBLISH" = true ]; then
    echo ""
    echo "Step 4: Publishing packages to NuGet.org..."
    if [ "$DRY_RUN" = false ]; then
        "$SCRIPT_DIR/publish.sh"
    else
        echo "DRY RUN: Would publish packages"
    fi
fi

# Summary
echo ""
echo "========================================"
echo "✅ Release workflow complete!"
echo "========================================"
echo "Version: $NEW_VERSION"
echo ""

if [ "$DRY_RUN" = true ]; then
    echo "This was a DRY RUN. No changes were made."
    echo "Remove --dry-run flag to execute for real."
else
    echo "Next steps:"
    if [ "$SHOULD_PUBLISH" = false ]; then
        echo "1. Publish packages: ./scripts/publish.sh"
        echo "2. Push changes: git push && git push --tags"
    else
        echo "1. Push changes: git push && git push --tags"
    fi
    echo "2. Create GitHub release at: https://github.com/pranshujawade/DistributedLeasing/releases/new?tag=v$NEW_VERSION"
    echo "3. Monitor packages at: https://www.nuget.org/account/Packages"
fi
