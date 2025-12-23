# Complete NuGet Package Publishing Workflow

This diagram shows the complete workflow from development to published NuGet packages.

## Visual Workflow

```
┌─────────────────────────────────────────────────────────────────┐
│                    LOCAL DEVELOPMENT                             │
└─────────────────────────────────────────────────────────────────┘

    Developer writes code
           ↓
    Commits changes
           ↓
    
┌──────────────────────────────────────────────────────────────────┐
│                   RELEASE AUTOMATION                              │
└──────────────────────────────────────────────────────────────────┘

    Option A: Complete Automated Release
    ┌─────────────────────────────────────┐
    │  ./scripts/release.sh patch --publish │
    └─────────────────────────────────────┘
           ↓
    ┌─────────────────────────────────────┐
    │  1. Bump version (1.0.0 → 1.0.1)    │
    │  2. Build all packages               │
    │  3. Run all tests                    │
    │  4. Create git commit                │
    │  5. Create git tag (v1.0.1)         │
    │  6. Publish to NuGet.org            │
    └─────────────────────────────────────┘
           ↓
    git push && git push --tags
           ↓
    ✅ Done!


    Option B: Manual Control
    ┌─────────────────────────────────────┐
    │  ./scripts/version-bump.sh patch     │
    └─────────────────────────────────────┘
           ↓
    Review & commit changes
           ↓
    ┌─────────────────────────────────────┐
    │  ./scripts/pack-all.sh               │
    └─────────────────────────────────────┘
           ↓
    Test packages locally
           ↓
    ┌─────────────────────────────────────┐
    │  ./scripts/publish.sh                │
    └─────────────────────────────────────┘
           ↓
    Create tag & push
           ↓
    ✅ Done!


┌──────────────────────────────────────────────────────────────────┐
│                   CI/CD AUTOMATION                                │
└──────────────────────────────────────────────────────────────────┘

    Push version tag (v1.0.1)
           ↓
    ┌─────────────────────────────────────┐
    │  GitHub Actions Trigger             │
    │  (.github/workflows/publish-nuget.yml)│
    └─────────────────────────────────────┘
           ↓
    ┌─────────────────────────────────────┐
    │  Extract version from tag           │
    │  Update Directory.Build.props       │
    │  Restore dependencies               │
    │  Build solution (Release)           │
    │  Run all tests                      │
    └─────────────────────────────────────┘
           ↓
    ┌─────────────────────────────────────┐
    │  Pack 6 packages:                   │
    │  • DistributedLeasing.Core          │
    │  • DistributedLeasing.Abstractions  │
    │  • DistributedLeasing.Azure.Blob    │
    │  • DistributedLeasing.Azure.Cosmos  │
    │  • DistributedLeasing.Azure.Redis   │
    │  • Extensions.DependencyInjection   │
    └─────────────────────────────────────┘
           ↓
    ┌─────────────────────────────────────┐
    │  Publish to NuGet.org               │
    │  (using NUGET_API_KEY secret)       │
    └─────────────────────────────────────┘
           ↓
    ┌─────────────────────────────────────┐
    │  Create GitHub Release              │
    │  with package links                 │
    └─────────────────────────────────────┘
           ↓
    ✅ Fully Automated!


┌──────────────────────────────────────────────────────────────────┐
│                   API KEY SECURITY                                │
└──────────────────────────────────────────────────────────────────┘

    Three-tier fallback system:
    
    1. .nuget-api-key file (gitignored)
           ↓ (if not found)
    2. NUGET_API_KEY environment variable
           ↓ (if not found)
    3. Interactive prompt with save option
    
    ✅ Never committed to git!


┌──────────────────────────────────────────────────────────────────┐
│                   PACKAGE DEPENDENCY CHAIN                        │
└──────────────────────────────────────────────────────────────────┘

    User installs:
    dotnet add package DistributedLeasing.Azure.Blob
    
           ↓
    NuGet automatically installs:
    
    DistributedLeasing.Azure.Blob (1.0.1)
         ↓
    DistributedLeasing.Abstractions (1.0.1)
         ↓
    DistributedLeasing.Core (1.0.1)
         ↓
    Azure.Storage.Blobs (12.26.0)
    Azure.Identity (1.17.1)
    
    ✅ One command, all dependencies resolved!


┌──────────────────────────────────────────────────────────────────┐
│                   VERSION PROGRESSION                             │
└──────────────────────────────────────────────────────────────────┘

    Development Cycle:
    
    1.0.0 (stable)
      ↓ patch (bug fixes)
    1.0.1
      ↓ patch (more fixes)
    1.0.2
      ↓ minor (new feature)
    1.1.0
      ↓ minor --pre-release beta
    1.2.0-beta.1 (testing)
      ↓ beta refinement
    1.2.0-beta.2
      ↓ release
    1.2.0 (stable)
      ↓ major (breaking change)
    2.0.0
```

## Script Execution Flow

### release.sh (Complete Workflow)

```
./scripts/release.sh patch --publish
    │
    ├─→ Check for uncommitted changes
    │
    ├─→ Calculate new version
    │   (reads Directory.Build.props)
    │
    ├─→ Prompt for confirmation
    │
    ├─→ Update Directory.Build.props
    │
    ├─→ Call pack-all.sh
    │   │
    │   ├─→ dotnet restore
    │   ├─→ dotnet build (Release)
    │   ├─→ dotnet test
    │   ├─→ dotnet pack (6 projects)
    │   └─→ Generate .nupkg + .snupkg
    │
    ├─→ Create git commit
    │   "Bump version to 1.0.1"
    │
    ├─→ Create git tag
    │   v1.0.1
    │
    ├─→ Call publish.sh (if --publish)
    │   │
    │   ├─→ Find API key
    │   │   (file → env → prompt)
    │   │
    │   ├─→ Confirm publication
    │   │
    │   └─→ dotnet nuget push
    │       (in dependency order)
    │
    └─→ Display next steps
```

## File Organization

```
DistributedLeasing/
├── scripts/                          # Automation scripts
│   ├── release.sh                    # ⭐ Main release workflow
│   ├── version-bump.sh               # Version management
│   ├── pack-all.sh                   # Package builder
│   └── publish.sh                    # NuGet publisher
│
├── .github/workflows/                # CI/CD automation
│   ├── build-and-test.yml           # PR/push builds
│   └── publish-nuget.yml            # Tag-triggered publishing
│
├── .gitignore                        # Includes API key exclusions
├── .nuget-api-key                    # Your API key (gitignored)
│
├── Directory.Build.props             # Shared build properties
├── Directory.Packages.props          # Central package management
│
├── AUTOMATION_GUIDE.md              # Complete usage guide
├── QUICKSTART_PUBLISHING.md         # Quick start guide
├── AUTOMATION_SUMMARY.md            # This summary
└── WORKFLOW_DIAGRAM.md              # Visual workflows
```

## Quick Command Reference

| Task | Command |
|------|---------|
| **Patch release** | `./scripts/release.sh patch --publish` |
| **Minor release** | `./scripts/release.sh minor --publish` |
| **Major release** | `./scripts/release.sh major --publish` |
| **Beta release** | `./scripts/release.sh minor --pre-release beta` |
| **Test release** | `./scripts/release.sh patch --dry-run` |
| **Just bump** | `./scripts/version-bump.sh patch` |
| **Just build** | `./scripts/pack-all.sh` |
| **Just publish** | `./scripts/publish.sh` |

## Integration Points

### Local Development
- Developer runs `./scripts/release.sh`
- Uses `.nuget-api-key` file for authentication
- Builds and publishes from local machine

### GitHub Actions
- Triggered by version tags (`v*.*.*`)
- Uses `NUGET_API_KEY` repository secret
- Fully automated build → test → publish

### NuGet.org
- Receives packages from both local and CI/CD
- Package validation and scanning
- Indexing for search (5-10 minutes)

## Success Indicators

After running release automation:

✅ **Local Artifacts**
- `nupkgs/` contains 12 files (6 .nupkg + 6 .snupkg)
- Git commit created with version bump
- Git tag created (v1.0.1)

✅ **Git Repository**
- Commit pushed to GitHub
- Tag visible in releases
- GitHub Actions workflow runs (if tag-triggered)

✅ **NuGet.org**
- 6 packages appear in "Manage Packages"
- Packages are indexed and searchable
- Package pages show README and dependencies

✅ **GitHub**
- Release created with package links
- CI/CD workflow badge shows passing
- Users can download from releases page

## End Result

Users can now install your library with one command:

```bash
dotnet add package DistributedLeasing.Azure.Blob
```

And get:
- ✅ Main package
- ✅ All dependencies (automatic)
- ✅ XML documentation (IntelliSense)
- ✅ Symbol packages (debugging)
- ✅ Source Link (step-through debugging)
- ✅ README (package description)

**Mission Accomplished! 🎉**
