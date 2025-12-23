# Quick Start: Publishing Your First Release

This is a quick walkthrough for publishing your first NuGet package release.

## Prerequisites

1. **NuGet.org Account**: [Create account](https://www.nuget.org) if you don't have one
2. **NuGet API Key**: Get it from [NuGet.org API Keys](https://www.nuget.org/account/apikeys)
3. **Git Repository**: Already set up on GitHub

## Local Publishing (First Time)

### Step 1: Save Your API Key Securely

```bash
# Navigate to your repository
cd /Users/pjawade/repos/DistributedLeasing

# Save your API key to a file (this file is in .gitignore)
echo "your-nuget-api-key-here" > .nuget-api-key

# Verify it's ignored by git
git status  # Should NOT show .nuget-api-key
```

### Step 2: Do a Complete Release

```bash
# For your first release (patch: 1.0.0 -> 1.0.1)
./scripts/release.sh patch --publish

# This will:
# 1. Bump version to 1.0.1
# 2. Build all packages
# 3. Run all tests
# 4. Create git commit "Bump version to 1.0.1"
# 5. Create git tag "v1.0.1"
# 6. Publish to NuGet.org
```

### Step 3: Push to GitHub

```bash
# Push the commit and tag
git push origin main
git push origin --tags
```

### Step 4: Monitor Package Publication

1. Visit [NuGet.org Packages](https://www.nuget.org/account/Packages)
2. Wait 5-10 minutes for packages to appear in search
3. Check package pages: `https://www.nuget.org/packages/DistributedLeasing.Azure.Blob`

## Automated Publishing via GitHub Actions

Once your first release is successful, you can automate future releases:

### Setup GitHub Secret

1. Go to your repository on GitHub
2. Navigate to **Settings** → **Secrets and variables** → **Actions**
3. Click **New repository secret**
4. Name: `NUGET_API_KEY`
5. Value: Your NuGet API key
6. Click **Add secret**

### Automated Release Process

```bash
# 1. Bump version locally (doesn't publish)
./scripts/version-bump.sh minor

# 2. Commit and tag
git add Directory.Build.props
git commit -m "Bump version to 1.1.0"
git tag -a v1.1.0 -m "Release v1.1.0"

# 3. Push tag to trigger automated publishing
git push origin main
git push origin v1.1.0
```

**What happens automatically:**
1. GitHub Actions triggers on the `v1.1.0` tag
2. Builds all packages on Ubuntu
3. Runs all tests
4. Publishes to NuGet.org using your secret API key
5. Creates a GitHub Release with package links

## Common Release Scenarios

### Scenario 1: Bug Fix (Patch Release)

```bash
# Complete workflow with one command
./scripts/release.sh patch --publish
git push && git push --tags
```

### Scenario 2: New Feature (Minor Release)

```bash
# Test first with dry run
./scripts/release.sh minor --dry-run

# If all looks good, do it for real
./scripts/release.sh minor --publish
git push && git push --tags
```

### Scenario 3: Breaking Change (Major Release)

```bash
# Major releases should be tested thoroughly
./scripts/release.sh major
# Manual testing here...
./scripts/publish.sh
git push && git push --tags
```

### Scenario 4: Beta Release

```bash
# Create beta version
./scripts/release.sh minor --pre-release beta
./scripts/publish.sh
git push && git push --tags
```

## Verification Checklist

After publishing, verify:

- [ ] Packages appear on [NuGet.org](https://www.nuget.org/packages?q=DistributedLeasing)
- [ ] All 6 packages have the same version number
- [ ] Symbol packages (.snupkg) are also published
- [ ] Package descriptions and tags look correct
- [ ] Test installation: `dotnet add package DistributedLeasing.Azure.Blob --version x.y.z`
- [ ] GitHub release was created at https://github.com/pranshujawade/DistributedLeasing/releases

## Testing Packages Locally Before Publishing

```bash
# Build packages
./scripts/pack-all.sh

# Create a test project
cd /tmp
dotnet new console -n TestDistributedLeasing
cd TestDistributedLeasing

# Add local package
dotnet add package DistributedLeasing.Azure.Blob \
  --source /Users/pjawade/repos/DistributedLeasing/nupkgs \
  --version 1.0.0

# Test it works
dotnet build
```

## Troubleshooting

### "Package already exists"
- **Cause**: You're trying to republish the same version
- **Fix**: Bump version and try again

### "Invalid API key"
- **Cause**: API key is wrong or expired
- **Fix**: Get new API key from NuGet.org and update `.nuget-api-key` file

### "Could not find version"
- **Cause**: Version in Directory.Build.props doesn't match git tag
- **Fix**: Use `./scripts/version-bump.sh` to ensure consistency

### Symbol packages fail to publish
- **Cause**: This is usually not critical; symbol server may be temporarily unavailable
- **Fix**: Retry manually or wait; main packages are what matter

## Need Help?

See the complete guide: [AUTOMATION_GUIDE.md](./AUTOMATION_GUIDE.md)

---

**Remember**: 
- Never commit your API key (`.nuget-api-key` is in `.gitignore`)
- Always test locally before publishing
- Use semantic versioning correctly (major.minor.patch)
- Write meaningful git commit messages
