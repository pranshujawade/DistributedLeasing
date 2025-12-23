#!/bin/bash

# Script to bump version in Directory.Build.props
# Usage: ./version-bump.sh [major|minor|patch] [--pre-release alpha|beta|rc]

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
DIRECTORY_BUILD_PROPS="$REPO_ROOT/Directory.Build.props"

# Function to extract current version
get_current_version() {
    grep -o '<Version>.*</Version>' "$DIRECTORY_BUILD_PROPS" | sed 's|<Version>\(.*\)</Version>|\1|'
}

# Function to update version in file
update_version() {
    local new_version=$1
    sed -i.bak "s|<Version>.*</Version>|<Version>$new_version</Version>|" "$DIRECTORY_BUILD_PROPS"
    rm -f "$DIRECTORY_BUILD_PROPS.bak"
}

# Parse arguments
BUMP_TYPE=""
PRE_RELEASE=""

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
        *)
            echo "Unknown option: $1"
            echo "Usage: $0 [major|minor|patch] [--pre-release alpha|beta|rc]"
            exit 1
            ;;
    esac
done

if [ -z "$BUMP_TYPE" ]; then
    echo "Error: You must specify bump type (major, minor, or patch)"
    echo "Usage: $0 [major|minor|patch] [--pre-release alpha|beta|rc]"
    exit 1
fi

# Get current version
CURRENT_VERSION=$(get_current_version)
echo "Current version: $CURRENT_VERSION"

# Remove pre-release suffix if exists
BASE_VERSION=$(echo "$CURRENT_VERSION" | sed 's/-.*$//')

# Parse version components
IFS='.' read -r -a VERSION_PARTS <<< "$BASE_VERSION"
MAJOR="${VERSION_PARTS[0]}"
MINOR="${VERSION_PARTS[1]}"
PATCH="${VERSION_PARTS[2]}"

# Bump version based on type
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

# Add pre-release suffix if specified
if [ ! -z "$PRE_RELEASE" ]; then
    # Find next pre-release number
    PRE_NUMBER=1
    if [[ "$CURRENT_VERSION" == *"-$PRE_RELEASE."* ]]; then
        # Extract current pre-release number and increment
        CURRENT_PRE_NUMBER=$(echo "$CURRENT_VERSION" | sed "s/.*-$PRE_RELEASE\.\([0-9]*\)/\1/")
        PRE_NUMBER=$((CURRENT_PRE_NUMBER + 1))
    fi
    NEW_VERSION="$NEW_VERSION-$PRE_RELEASE.$PRE_NUMBER"
fi

echo "New version: $NEW_VERSION"
echo ""
read -p "Do you want to update to version $NEW_VERSION? (y/n) " -n 1 -r
echo ""

if [[ $REPLY =~ ^[Yy]$ ]]; then
    update_version "$NEW_VERSION"
    echo "✅ Version updated to $NEW_VERSION in Directory.Build.props"
    echo ""
    echo "Next steps:"
    echo "1. Review the changes: git diff Directory.Build.props"
    echo "2. Commit the version bump: git add Directory.Build.props && git commit -m 'Bump version to $NEW_VERSION'"
    echo "3. Create a git tag: git tag -a v$NEW_VERSION -m 'Release v$NEW_VERSION'"
    echo "4. Build packages: ./scripts/pack-all.sh"
    echo "5. Publish: ./scripts/publish.sh"
else
    echo "Version bump cancelled."
    exit 1
fi
