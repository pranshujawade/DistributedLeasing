# NuGet Package Automation Guide

This guide explains how to use the automated scripts for building, versioning, and publishing the DistributedLeasing NuGet packages.

## Available Scripts

All scripts are located in the `/scripts` directory:

| Script | Purpose | Usage |
|--------|---------|-------|
| `release.sh` | **Complete release workflow** - bump, build, test, tag, and publish | **Most users start here** |
| `version-bump.sh` | Bump version using semantic versioning | Manual version management |
| `pack-all.sh` | Build all NuGet packages | Manual build |
| `publish.sh` | Publish packages to NuGet.org | Manual publish |

## Quick Start: Complete Release Workflow

The `release.sh` script handles everything in one command:

### Patch Release (Bug Fixes)

```bash
# Example: 1.0.0 -> 1.0.1
./scripts/release.sh patch

# With automatic publishing
./scripts/release.sh patch --publish

# Dry run to see what would happen
./scripts/release.sh patch --dry-run
```

### Minor Release (New Features)

```bash
# Example: 1.0.0 -> 1.1.0
./scripts/release.sh minor

# With automatic publishing
./scripts/release.sh minor --publish
```

### Major Release (Breaking Changes)

```bash
# Example: 1.0.0 -> 2.0.0
./scripts/release.sh major --publish
```

### Pre-release Versions

```bash
# Alpha release: 1.0.0 -> 1.1.0-alpha.1
./scripts/release.sh minor --pre-release alpha

# Beta release: 1.1.0-alpha.1 -> 1.1.0-beta.1
./scripts/release.sh minor --pre-release beta

# Release candidate: 1.1.0-beta.2 -> 1.1.0-rc.1
./scripts/release.sh minor --pre-release rc
```

## What the Release Script Does

1. **Checks for uncommitted changes** (warns you if working directory is dirty)
2. **Bumps version** in `Directory.Build.props` using semantic versioning
3. **Builds all packages** in Release configuration
4. **Runs all tests** to ensure quality
5. **Creates git commit** with version bump
6. **Creates git tag** (e.g., `v1.0.1`)
7. **Publishes to NuGet.org** (if `--publish` flag is used)
8. **Provides next steps** for pushing to GitHub

## API Key Management (Secure, Never Committed)

The scripts support three methods for providing your NuGet API key, in order of priority:

### Method 1: API Key File (Recommended for Local Development)

```bash
# Create a file to store your API key (already in .gitignore)
echo "your-api-key-here" > .nuget-api-key

# The publish script will automatically use this file
./scripts/publish.sh
```

**Security**: The `.nuget-api-key` file is in `.gitignore` and will never be committed to git.

### Method 2: Environment Variable (Recommended for CI/CD)

```bash
# Set environment variable
export NUGET_API_KEY="your-api-key-here"

# Run publish
./scripts/publish.sh
```

For permanent setup, add to your shell profile (`~/.zshrc` or `~/.bashrc`):
```bash
export NUGET_API_KEY="your-api-key-here"
```

### Method 3: Interactive Prompt (Fallback)

If no API key is found, the script will prompt you:

```bash
./scripts/publish.sh
# Enter your API key when prompted
# Option to save it to .nuget-api-key for future use
```

## Individual Script Usage

### 1. Version Bump Only

```bash
# Bump patch version: 1.0.0 -> 1.0.1
./scripts/version-bump.sh patch

# Bump minor version: 1.0.0 -> 1.1.0
./scripts/version-bump.sh minor

# Bump major version: 1.0.0 -> 2.0.0
./scripts/version-bump.sh major

# Pre-release versions
./scripts/version-bump.sh minor --pre-release alpha  # 1.0.0 -> 1.1.0-alpha.1
./scripts/version-bump.sh patch --pre-release beta   # 1.1.0-alpha.1 -> 1.1.1-beta.1
```

After bumping, the script provides next steps for committing and tagging.

### 2. Build Packages Only

```bash
# Build with current version
./scripts/pack-all.sh

# Build with specific version (updates Directory.Build.props)
./scripts/pack-all.sh --version 1.2.3

# Build in Debug configuration
./scripts/pack-all.sh --configuration Debug
```

**What it does:**
- Cleans previous builds
- Restores dependencies
- Builds all projects
- Runs all tests
- Creates `.nupkg` and `.snupkg` files in `/nupkgs` directory

### 3. Publish Packages Only

```bash
# Publish all packages in /nupkgs
./scripts/publish.sh

# Dry run (see what would be published without actually publishing)
./scripts/publish.sh --dry-run

# Use specific API key file
./scripts/publish.sh --api-key-file /path/to/api-key
```

**Publishing order** (ensures dependencies are available):
1. DistributedLeasing.Core
2. DistributedLeasing.Abstractions
3. DistributedLeasing.Azure.Blob
4. DistributedLeasing.Azure.Cosmos
5. DistributedLeasing.Azure.Redis
6. DistributedLeasing.Extensions.DependencyInjection

## Common Workflows

### Workflow 1: Standard Patch Release

```bash
# Complete workflow in one command
./scripts/release.sh patch --publish

# Or step by step:
./scripts/version-bump.sh patch
git add Directory.Build.props
git commit -m "Bump version to 1.0.1"
git tag -a v1.0.1 -m "Release v1.0.1"
./scripts/pack-all.sh
./scripts/publish.sh
git push && git push --tags
```

### Workflow 2: Feature Release with Testing

```bash
# Test the release first
./scripts/release.sh minor --dry-run

# If everything looks good, do it for real
./scripts/release.sh minor --publish

# Push to GitHub
git push && git push --tags
```

### Workflow 3: Pre-release Alpha

```bash
# Create alpha release
./scripts/release.sh minor --pre-release alpha

# Test locally, then publish manually
./scripts/publish.sh

# Push to GitHub
git push && git push --tags
```

### Workflow 4: Manual Control

```bash
# 1. Bump version manually
./scripts/version-bump.sh patch

# 2. Review changes
git diff Directory.Build.props

# 3. Commit when ready
git add Directory.Build.props
git commit -m "Bump version to 1.0.1"

# 4. Build packages
./scripts/pack-all.sh

# 5. Inspect packages before publishing
ls -lh nupkgs/

# 6. Publish when satisfied
./scripts/publish.sh

# 7. Tag and push
git tag -a v1.0.1 -m "Release v1.0.1"
git push && git push --tags
```

## Version Numbering Guide (Semantic Versioning)

Format: `MAJOR.MINOR.PATCH[-PRERELEASE]`

| Version Part | When to Bump | Example |
|--------------|--------------|---------|
| **MAJOR** | Breaking changes, incompatible API changes | 1.0.0 → 2.0.0 |
| **MINOR** | New features, backward-compatible | 1.0.0 → 1.1.0 |
| **PATCH** | Bug fixes, backward-compatible | 1.0.0 → 1.0.1 |
| **PRERELEASE** | Alpha, beta, or RC versions | 1.1.0-alpha.1 |

### Pre-release Progression

```
1.0.0 (stable)
  ↓
1.1.0-alpha.1 (early testing)
  ↓
1.1.0-alpha.2 (more alpha fixes)
  ↓
1.1.0-beta.1 (feature complete, stabilizing)
  ↓
1.1.0-beta.2 (beta fixes)
  ↓
1.1.0-rc.1 (release candidate)
  ↓
1.1.0 (stable release)
```

## Getting Your NuGet API Key

1. Log in to [NuGet.org](https://www.nuget.org)
2. Go to your account settings
3. Navigate to **API Keys**
4. Click **Create** or use an existing key
5. Set permissions: **Push new packages and package versions**
6. Set glob pattern: `DistributedLeasing.*`
7. Copy the API key and save it using one of the methods above

**Important**: Treat your API key like a password. Never commit it to git!

## Troubleshooting

### "Package already exists" Error

```bash
# Use --skip-duplicate flag (already in publish.sh)
# Or manually delete and republish
dotnet nuget delete DistributedLeasing.Core 1.0.1 \
  --api-key your-key \
  --source https://api.nuget.org/v3/index.json
```

### API Key Not Found

```bash
# Check if file exists
ls -la .nuget-api-key

# Check environment variable
echo $NUGET_API_KEY

# If neither works, you'll be prompted interactively
```

### Version Already Tagged

```bash
# Delete local tag
git tag -d v1.0.1

# Delete remote tag (careful!)
git push origin :refs/tags/v1.0.1

# Or create new version
./scripts/release.sh patch
```

### Build Failures

```bash
# Clean everything and try again
dotnet clean
rm -rf nupkgs/
./scripts/pack-all.sh
```

### Uncommitted Changes Warning

```bash
# Commit or stash your changes first
git status
git add .
git commit -m "Your changes"

# Or stash temporarily
git stash
./scripts/release.sh patch
git stash pop
```

## Best Practices

1. **Always run tests before publishing**: The scripts do this automatically, but if you build manually, run `dotnet test` first.

2. **Use dry run for major releases**: Test the workflow with `--dry-run` before executing for real.

3. **Keep API key secure**: Use `.nuget-api-key` file or environment variable, never commit it to git.

4. **Tag your releases**: The scripts create git tags automatically. These are used for GitHub releases and version tracking.

5. **Write clear commit messages**: The version bump creates a commit. Add more details manually if needed.

6. **Test packages locally first**: Install packages from the `nupkgs/` folder in a test project before publishing.

7. **Create GitHub releases**: After pushing tags, create releases on GitHub with release notes.

## CI/CD Integration

For automated releases via GitHub Actions (see `.github/workflows/publish-nuget.yml`):

1. Add `NUGET_API_KEY` to GitHub repository secrets
2. Push a version tag: `git tag v1.0.0 && git push --tags`
3. GitHub Actions automatically builds, tests, and publishes

## What Gets Created

After running the scripts, you'll have:

```
nupkgs/
├── DistributedLeasing.Core.1.0.0.nupkg
├── DistributedLeasing.Core.1.0.0.snupkg (symbols)
├── DistributedLeasing.Abstractions.1.0.0.nupkg
├── DistributedLeasing.Abstractions.1.0.0.snupkg
├── DistributedLeasing.Azure.Blob.1.0.0.nupkg
├── DistributedLeasing.Azure.Blob.1.0.0.snupkg
├── DistributedLeasing.Azure.Cosmos.1.0.0.nupkg
├── DistributedLeasing.Azure.Cosmos.1.0.0.snupkg
├── DistributedLeasing.Azure.Redis.1.0.0.nupkg
├── DistributedLeasing.Azure.Redis.1.0.0.snupkg
├── DistributedLeasing.Extensions.DependencyInjection.1.0.0.nupkg
└── DistributedLeasing.Extensions.DependencyInjection.1.0.0.snupkg
```

**Note**: The `nupkgs/` directory is in `.gitignore` and won't be committed.

## Need Help?

- Check script help: `./scripts/release.sh` (without arguments shows usage)
- Check NuGet package status: https://www.nuget.org/account/Packages
- View published packages: https://www.nuget.org/packages?q=DistributedLeasing

## Summary Cheat Sheet

| Task | Command |
|------|---------|
| Patch release | `./scripts/release.sh patch --publish` |
| Minor release | `./scripts/release.sh minor --publish` |
| Major release | `./scripts/release.sh major --publish` |
| Alpha release | `./scripts/release.sh minor --pre-release alpha` |
| Test release | `./scripts/release.sh patch --dry-run` |
| Bump version only | `./scripts/version-bump.sh patch` |
| Build only | `./scripts/pack-all.sh` |
| Publish only | `./scripts/publish.sh` |
| Set API key | `echo "key" > .nuget-api-key` |
