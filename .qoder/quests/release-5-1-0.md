# Release 5.1.0 - Execution Workflow

## Objective

Execute the release process for DistributedLeasing version 5.1.0 with no code changes. The version has already been bumped to 5.1.0 in Directory.Build.props, and all release artifacts are ready for publication.

## Release Scope

Version 5.1.0 includes the following changes already committed:
- Redis distributed locking sample with comprehensive documentation
- Enhanced setup-resources.sh script with --project argument (blob/cosmos/redis/all)
- Interactive configuration wizard for all samples
- RedisMetadataInspector for state inspection
- Version bump from 5.0.0 to 5.1.0
- Updated package release notes

**Type**: Minor release (no breaking changes, safe upgrade from 5.0.x)

## Pre-Release Validation

### Version Confirmation

Verify current version state:

```
File: Directory.Build.props
Expected: <Version>5.1.0</Version>
```

### Git Status Check

Confirm all changes are committed and working tree is clean:
- No uncommitted changes should exist
- All new Redis sample files should be tracked
- appsettings.Local.json files should remain excluded (gitignore)

### Release Notes Verification

Confirm PackageReleaseNotes in Directory.Build.props contains:
- Description of Redis sample addition
- Setup script enhancements
- Clear statement of no breaking API changes
- Safe upgrade notice from 5.0.x

## Release Workflow

### Phase 1: Local Build and Test

**Objective**: Validate that all packages build successfully and tests pass

**Operations**:

1. Clean previous build artifacts
   - Remove existing nupkgs directory
   - Clear bin/obj directories

2. Restore dependencies
   - Execute dotnet restore for entire solution
   - Verify all package references resolve

3. Build solution
   - Configuration: Release
   - Target: All projects in solution
   - Validate compilation success

4. Execute test suite
   - Run all unit tests
   - Verify 100% test pass rate
   - Configuration: Release (no-rebuild)

5. Generate NuGet packages
   - Pack all five library projects:
     - DistributedLeasing.Abstractions
     - DistributedLeasing.Azure.Blob
     - DistributedLeasing.Azure.Cosmos
     - DistributedLeasing.Azure.Redis
     - DistributedLeasing.ChaosEngineering
   - Include symbols (.snupkg) for debugging
   - Include source for source link support
   - Output location: /nupkgs directory

**Validation Criteria**:
- All tests pass
- All 5 packages generated successfully
- Package version matches 5.1.0
- Symbol packages created for all libraries

**Script**: `./scripts/pack-all.sh --configuration Release`

### Phase 2: Git Commit and Tagging

**Objective**: Create immutable release marker in version control

**Operations**:

1. Stage all changes
   - Add Directory.Build.props
   - Add all Redis sample files
   - Add enhanced setup script
   - Verify no sensitive files staged (appsettings.Local.json excluded)

2. Create release commit
   - Message format: Conventional Commits standard
   - Type: feat (new feature addition)
   - Scope: Redis sample and setup enhancements
   - Breaking change footer: None

3. Create annotated version tag
   - Tag name: v5.1.0
   - Tag message: "Release v5.1.0"
   - Annotated tag (not lightweight) for full metadata

**Validation Criteria**:
- Commit includes all intended files
- No unintended files committed
- Tag points to correct commit
- Tag message is descriptive

**Commit Message Structure**:
```
feat: Add Redis distributed locking sample and enhance setup script

- Add complete RedisLeaseSample with interactive configuration wizard
- Enhance setup-resources.sh with --project argument (blob/cosmos/redis/all)
- Add comprehensive README and demo materials for Redis sample
- Implement RedisMetadataInspector for state inspection
- Add atomic SET NX locking mechanism with TTL support
- Bump version to 5.1.0
```

### Phase 3: Remote Synchronization

**Objective**: Push release to remote repository for team visibility and CI/CD triggers

**Operations**:

1. Push commits to main branch
   - Branch: main (or master depending on convention)
   - Remote: origin
   - Force flag: Not used (fast-forward only)

2. Push tags to remote
   - Push all tags or specific v5.1.0 tag
   - Trigger tag-based workflows if configured

**Validation Criteria**:
- Commits appear on remote repository
- Tag visible in repository tags list
- No push conflicts or rejections
- Branch protection rules satisfied (if applicable)

### Phase 4: NuGet Package Publishing

**Objective**: Publish packages to NuGet.org for public consumption

**Authentication Requirements**:
- NuGet API key with push permissions
- Storage options:
  - File: .nuget-api-key (gitignored)
  - Environment variable: NUGET_API_KEY
  - Interactive prompt: Manual entry

**Publishing Sequence**:

Packages published in dependency order:
1. DistributedLeasing.Abstractions (no dependencies on other packages)
2. DistributedLeasing.Azure.Blob (depends on Abstractions)
3. DistributedLeasing.Azure.Cosmos (depends on Abstractions)
4. DistributedLeasing.Azure.Redis (depends on Abstractions)
5. DistributedLeasing.ChaosEngineering (depends on Abstractions)

**Operations for Each Package**:
1. Push .nupkg file to NuGet.org
2. Push .snupkg symbol file (if exists)
3. Wait 2 seconds between packages (rate limiting)
4. Use --skip-duplicate flag (idempotent, safe for retries)

**Target Repository**: https://api.nuget.org/v3/index.json

**Validation Criteria**:
- All 5 packages published successfully
- No duplicate version errors
- Packages appear in NuGet.org account
- Symbol packages uploaded for debugging support

**Script**: `./scripts/publish.sh`

### Phase 5: GitHub Release Creation

**Objective**: Create user-facing release notes and download artifacts

**Release Metadata**:
- Tag: v5.1.0
- Release title: "Release v5.1.0 - Redis Sample & Setup Enhancements"
- Target branch: main
- Release type: Latest release

**Release Notes Content**:

**What's New**:
- Redis distributed locking sample with comprehensive documentation
- Enhanced setup-resources.sh with provider selection (--project blob/cosmos/redis/all)
- Interactive configuration wizard for all samples
- RedisMetadataInspector for debugging and state inspection

**Technical Details**:
- Version: 5.1.0
- Type: Minor release
- Breaking Changes: None
- Upgrade: Safe upgrade from 5.0.x

**Packages Published**:
- DistributedLeasing.Abstractions 5.1.0
- DistributedLeasing.Azure.Blob 5.1.0
- DistributedLeasing.Azure.Cosmos 5.1.0
- DistributedLeasing.Azure.Redis 5.1.0
- DistributedLeasing.ChaosEngineering 5.1.0

**Documentation Links**:
- Redis Sample README
- Enhanced Setup Script Documentation
- Migration Guide (if applicable)

**Artifacts**:
- Source code (auto-generated zip and tar.gz)
- Optional: Attach compiled .nupkg files for offline installation

**Validation Criteria**:
- Release visible on GitHub releases page
- Release notes formatted correctly
- All links functional
- Auto-generated source archives available

**Manual Step**: Navigate to GitHub repository and create release via UI or API

## Post-Release Activities

### Package Verification

1. NuGet.org indexing
   - Wait 5-10 minutes for package indexing
   - Search for packages on NuGet.org
   - Verify version 5.1.0 appears in search results

2. Package metadata validation
   - Check release notes display correctly
   - Verify dependency chain is accurate
   - Confirm symbol packages are linked

3. Installation test
   - Create test project
   - Install package: `dotnet add package DistributedLeasing.Azure.Redis --version 5.1.0`
   - Verify dependencies auto-install
   - Confirm package restores successfully

### Documentation Updates

If not already completed:
- Update main README.md to reference 5.1.0
- Update samples documentation with version references
- Add migration notes if applicable
- Update changelog or release history

### Communication

Announce release to stakeholders:
- Internal team notification
- Public announcement (Twitter, blog, etc.)
- Update project status boards
- Notify dependent projects of new version

### Monitoring

Monitor for issues:
- NuGet package download metrics
- GitHub issue tracker for new reports
- Community feedback channels
- Build failures in dependent projects

## Rollback Procedure

If critical issues discovered post-release:

### Option 1: Unlist Package (Preferred)

- Log into NuGet.org
- Unlist affected package version
- Package remains installable for existing users but hidden from new installs
- Allows time to fix issues without breaking existing consumers

### Option 2: Patch Release

- Create hotfix branch from v5.1.0 tag
- Apply minimal fix
- Release as 5.1.1 with urgent patch notes
- Deprecate 5.1.0 in release notes

### Option 3: Delete Tag (Extreme Cases Only)

- Delete v5.1.0 tag from remote
- Force push corrected tag
- **Warning**: Breaks immutability contract, use only for security issues
- Communicate clearly to all consumers

## Execution Checklist

Pre-Flight:
- [ ] Verify version is 5.1.0 in Directory.Build.props
- [ ] Confirm git working tree is clean
- [ ] Review release notes content
- [ ] Ensure .nuget-api-key file exists or NUGET_API_KEY env var set

Build and Test:
- [ ] Execute pack-all.sh successfully
- [ ] All tests pass
- [ ] 5 .nupkg files generated in nupkgs directory
- [ ] 5 .snupkg symbol files generated

Version Control:
- [ ] Stage all release changes with git add
- [ ] Create commit with conventional commit message
- [ ] Create annotated tag v5.1.0
- [ ] Review commit and tag with git log and git show

Push:
- [ ] Push commits to origin/main
- [ ] Push tags to origin
- [ ] Verify commits and tags appear on GitHub

Publish:
- [ ] Execute publish.sh
- [ ] Confirm all 5 packages pushed to NuGet
- [ ] Verify no errors during publishing
- [ ] Check NuGet.org account for new versions

GitHub Release:
- [ ] Navigate to GitHub releases page
- [ ] Create new release from tag v5.1.0
- [ ] Add release notes
- [ ] Publish release

Post-Release:
- [ ] Wait 10 minutes for NuGet indexing
- [ ] Search for packages on NuGet.org
- [ ] Test installation in new project
- [ ] Announce release to stakeholders

## Risk Mitigation

### Potential Issues and Mitigations

**Issue**: NuGet publish fails with authentication error
**Mitigation**: Verify API key validity, regenerate if expired, use interactive prompt fallback

**Issue**: Git push rejected due to branch protection
**Mitigation**: Request temporary bypass or create PR for release commit

**Issue**: Package already exists on NuGet
**Mitigation**: --skip-duplicate flag handles gracefully, verify version number is correct

**Issue**: Tests fail during build phase
**Mitigation**: Do not proceed with release, investigate test failures, fix before retrying

**Issue**: Tag already exists on remote
**Mitigation**: Verify tag points to correct commit, delete and recreate if incorrect

**Issue**: Package indexing delayed on NuGet
**Mitigation**: Normal, wait 15-30 minutes before raising concern

## Success Criteria

Release is considered successful when:
- All 5 packages published to NuGet.org with version 5.1.0
- Git tag v5.1.0 exists on remote repository
- GitHub release created and visible
- Packages installable via dotnet add package
- No critical issues reported within 24 hours
- Package search results display correct version
### Phase 5: GitHub Release Creation

**Objective**: Create user-facing release notes and download artifacts

**Release Metadata**:
- Tag: v5.1.0
- Release title: "Release v5.1.0 - Redis Sample & Setup Enhancements"
- Target branch: main
- Release type: Latest release

**Release Notes Content**:

**What's New**:
- Redis distributed locking sample with comprehensive documentation
- Enhanced setup-resources.sh with provider selection (--project blob/cosmos/redis/all)
- Interactive configuration wizard for all samples
- RedisMetadataInspector for debugging and state inspection

**Technical Details**:
- Version: 5.1.0
- Type: Minor release
- Breaking Changes: None
- Upgrade: Safe upgrade from 5.0.x

**Packages Published**:
- DistributedLeasing.Abstractions 5.1.0
- DistributedLeasing.Azure.Blob 5.1.0
- DistributedLeasing.Azure.Cosmos 5.1.0
- DistributedLeasing.Azure.Redis 5.1.0
- DistributedLeasing.ChaosEngineering 5.1.0

**Documentation Links**:
- Redis Sample README
- Enhanced Setup Script Documentation
- Migration Guide (if applicable)

**Artifacts**:
- Source code (auto-generated zip and tar.gz)
- Optional: Attach compiled .nupkg files for offline installation

**Validation Criteria**:
- Release visible on GitHub releases page
- Release notes formatted correctly
- All links functional
- Auto-generated source archives available

**Manual Step**: Navigate to GitHub repository and create release via UI or API

## Post-Release Activities

### Package Verification

1. NuGet.org indexing
   - Wait 5-10 minutes for package indexing
   - Search for packages on NuGet.org
   - Verify version 5.1.0 appears in search results

2. Package metadata validation
   - Check release notes display correctly
   - Verify dependency chain is accurate
   - Confirm symbol packages are linked

3. Installation test
   - Create test project
   - Install package: `dotnet add package DistributedLeasing.Azure.Redis --version 5.1.0`
   - Verify dependencies auto-install
   - Confirm package restores successfully

### Documentation Updates

If not already completed:
- Update main README.md to reference 5.1.0
- Update samples documentation with version references
- Add migration notes if applicable
- Update changelog or release history

### Communication

Announce release to stakeholders:
- Internal team notification
- Public announcement (Twitter, blog, etc.)
- Update project status boards
- Notify dependent projects of new version

### Monitoring

Monitor for issues:
- NuGet package download metrics
- GitHub issue tracker for new reports
- Community feedback channels
- Build failures in dependent projects

## Rollback Procedure

If critical issues discovered post-release:

### Option 1: Unlist Package (Preferred)

- Log into NuGet.org
- Unlist affected package version
- Package remains installable for existing users but hidden from new installs
- Allows time to fix issues without breaking existing consumers

### Option 2: Patch Release

- Create hotfix branch from v5.1.0 tag
- Apply minimal fix
- Release as 5.1.1 with urgent patch notes
- Deprecate 5.1.0 in release notes

### Option 3: Delete Tag (Extreme Cases Only)

- Delete v5.1.0 tag from remote
- Force push corrected tag
- **Warning**: Breaks immutability contract, use only for security issues
- Communicate clearly to all consumers

## Execution Checklist

Pre-Flight:
- [ ] Verify version is 5.1.0 in Directory.Build.props
- [ ] Confirm git working tree is clean
- [ ] Review release notes content
- [ ] Ensure .nuget-api-key file exists or NUGET_API_KEY env var set

Build and Test:
- [ ] Execute pack-all.sh successfully
- [ ] All tests pass
- [ ] 5 .nupkg files generated in nupkgs directory
- [ ] 5 .snupkg symbol files generated

Version Control:
- [ ] Stage all release changes with git add
- [ ] Create commit with conventional commit message
- [ ] Create annotated tag v5.1.0
- [ ] Review commit and tag with git log and git show

Push:
- [ ] Push commits to origin/main
- [ ] Push tags to origin
- [ ] Verify commits and tags appear on GitHub

Publish:
- [ ] Execute publish.sh
- [ ] Confirm all 5 packages pushed to NuGet
- [ ] Verify no errors during publishing
- [ ] Check NuGet.org account for new versions

GitHub Release:
- [ ] Navigate to GitHub releases page
- [ ] Create new release from tag v5.1.0
- [ ] Add release notes
- [ ] Publish release

Post-Release:
- [ ] Wait 10 minutes for NuGet indexing
- [ ] Search for packages on NuGet.org
- [ ] Test installation in new project
- [ ] Announce release to stakeholders

## Risk Mitigation

### Potential Issues and Mitigations

**Issue**: NuGet publish fails with authentication error
**Mitigation**: Verify API key validity, regenerate if expired, use interactive prompt fallback

**Issue**: Git push rejected due to branch protection
**Mitigation**: Request temporary bypass or create PR for release commit

**Issue**: Package already exists on NuGet
**Mitigation**: --skip-duplicate flag handles gracefully, verify version number is correct

**Issue**: Tests fail during build phase
**Mitigation**: Do not proceed with release, investigate test failures, fix before retrying

**Issue**: Tag already exists on remote
**Mitigation**: Verify tag points to correct commit, delete and recreate if incorrect

**Issue**: Package indexing delayed on NuGet
**Mitigation**: Normal, wait 15-30 minutes before raising concern

## Success Criteria

Release is considered successful when:
- All 5 packages published to NuGet.org with version 5.1.0
- Git tag v5.1.0 exists on remote repository
- GitHub release created and visible
- Packages installable via dotnet add package
- No critical issues reported within 24 hours
- Package search results display correct version
### Phase 5: GitHub Release Creation

**Objective**: Create user-facing release notes and download artifacts

**Release Metadata**:
- Tag: v5.1.0
- Release title: "Release v5.1.0 - Redis Sample & Setup Enhancements"
- Target branch: main
- Release type: Latest release

**Release Notes Content**:

**What's New**:
- Redis distributed locking sample with comprehensive documentation
- Enhanced setup-resources.sh with provider selection (--project blob/cosmos/redis/all)
- Interactive configuration wizard for all samples
- RedisMetadataInspector for debugging and state inspection

**Technical Details**:
- Version: 5.1.0
- Type: Minor release
- Breaking Changes: None
- Upgrade: Safe upgrade from 5.0.x

**Packages Published**:
- DistributedLeasing.Abstractions 5.1.0
- DistributedLeasing.Azure.Blob 5.1.0
- DistributedLeasing.Azure.Cosmos 5.1.0
- DistributedLeasing.Azure.Redis 5.1.0
- DistributedLeasing.ChaosEngineering 5.1.0

**Documentation Links**:
- Redis Sample README
- Enhanced Setup Script Documentation
- Migration Guide (if applicable)

**Artifacts**:
- Source code (auto-generated zip and tar.gz)
- Optional: Attach compiled .nupkg files for offline installation

**Validation Criteria**:
- Release visible on GitHub releases page
- Release notes formatted correctly
- All links functional
- Auto-generated source archives available

**Manual Step**: Navigate to GitHub repository and create release via UI or API

## Post-Release Activities

### Package Verification

1. NuGet.org indexing
   - Wait 5-10 minutes for package indexing
   - Search for packages on NuGet.org
   - Verify version 5.1.0 appears in search results

2. Package metadata validation
   - Check release notes display correctly
   - Verify dependency chain is accurate
   - Confirm symbol packages are linked

3. Installation test
   - Create test project
   - Install package: `dotnet add package DistributedLeasing.Azure.Redis --version 5.1.0`
   - Verify dependencies auto-install
   - Confirm package restores successfully

### Documentation Updates

If not already completed:
- Update main README.md to reference 5.1.0
- Update samples documentation with version references
- Add migration notes if applicable
- Update changelog or release history

### Communication

Announce release to stakeholders:
- Internal team notification
- Public announcement (Twitter, blog, etc.)
- Update project status boards
- Notify dependent projects of new version

### Monitoring

Monitor for issues:
- NuGet package download metrics
- GitHub issue tracker for new reports
- Community feedback channels
- Build failures in dependent projects

## Rollback Procedure

If critical issues discovered post-release:

### Option 1: Unlist Package (Preferred)

- Log into NuGet.org
- Unlist affected package version
- Package remains installable for existing users but hidden from new installs
- Allows time to fix issues without breaking existing consumers

### Option 2: Patch Release

- Create hotfix branch from v5.1.0 tag
- Apply minimal fix
- Release as 5.1.1 with urgent patch notes
- Deprecate 5.1.0 in release notes

### Option 3: Delete Tag (Extreme Cases Only)

- Delete v5.1.0 tag from remote
- Force push corrected tag
- **Warning**: Breaks immutability contract, use only for security issues
- Communicate clearly to all consumers

## Execution Checklist

Pre-Flight:
- [ ] Verify version is 5.1.0 in Directory.Build.props
- [ ] Confirm git working tree is clean
- [ ] Review release notes content
- [ ] Ensure .nuget-api-key file exists or NUGET_API_KEY env var set

Build and Test:
- [ ] Execute pack-all.sh successfully
- [ ] All tests pass
- [ ] 5 .nupkg files generated in nupkgs directory
- [ ] 5 .snupkg symbol files generated

Version Control:
- [ ] Stage all release changes with git add
- [ ] Create commit with conventional commit message
- [ ] Create annotated tag v5.1.0
- [ ] Review commit and tag with git log and git show

Push:
- [ ] Push commits to origin/main
- [ ] Push tags to origin
- [ ] Verify commits and tags appear on GitHub

Publish:
- [ ] Execute publish.sh
- [ ] Confirm all 5 packages pushed to NuGet
- [ ] Verify no errors during publishing
- [ ] Check NuGet.org account for new versions

GitHub Release:
- [ ] Navigate to GitHub releases page
- [ ] Create new release from tag v5.1.0
- [ ] Add release notes
- [ ] Publish release

Post-Release:
- [ ] Wait 10 minutes for NuGet indexing
- [ ] Search for packages on NuGet.org
- [ ] Test installation in new project
- [ ] Announce release to stakeholders

## Risk Mitigation

### Potential Issues and Mitigations

**Issue**: NuGet publish fails with authentication error
**Mitigation**: Verify API key validity, regenerate if expired, use interactive prompt fallback

**Issue**: Git push rejected due to branch protection
**Mitigation**: Request temporary bypass or create PR for release commit

**Issue**: Package already exists on NuGet
**Mitigation**: --skip-duplicate flag handles gracefully, verify version number is correct

**Issue**: Tests fail during build phase
**Mitigation**: Do not proceed with release, investigate test failures, fix before retrying

**Issue**: Tag already exists on remote
**Mitigation**: Verify tag points to correct commit, delete and recreate if incorrect

**Issue**: Package indexing delayed on NuGet
**Mitigation**: Normal, wait 15-30 minutes before raising concern

## Success Criteria

Release is considered successful when:
- All 5 packages published to NuGet.org with version 5.1.0
- Git tag v5.1.0 exists on remote repository
- GitHub release created and visible
- Packages installable via dotnet add package
- No critical issues reported within 24 hours
- Package search results display correct version
