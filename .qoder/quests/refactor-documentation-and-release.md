# Design: Documentation Refactoring and Automated Release Pipeline for Version 5.0

## Objective

Transform the DistributedLeasing repository documentation to accurately reflect the current architecture, enhance package-specific READMEs, and establish a robust one-touch automated release pipeline supporting major and minor version upgrades with NuGet publishing.

## Current State Analysis

### Existing Documentation

The repository contains:

**Root-level README.md**:
- Focuses on quick start and basic usage patterns
- References non-existent packages like `DistributedLeasing.Extensions.DependencyInjection`
- Package structure lists `DistributedLeasing.Core` which does not exist
- Missing details on authentication architecture
- No coverage of observability features
- Outdated package structure section

**Sample READMEs**:
- BlobLeaseSample has comprehensive, well-structured documentation with real-world scenarios
- Demonstrates metadata usage, Azure inspection, and troubleshooting
- Missing CosmosLeaseSample README
- Missing Redis sample entirely

**Package Metadata**:
- Each `.csproj` file contains accurate descriptions in `<Description>` tags
- Tags are relevant and specific to each provider
- Metadata aligned with actual capabilities

### Existing Release Scripts

**version-bump.sh**:
- Extracts version from Directory.Build.props
- Supports major, minor, patch bumps
- Handles pre-release suffixes (alpha, beta, rc)
- Interactive confirmation required
- Issue: Does not handle .bak file cleanup on macOS consistently

**pack-all.sh**:
- Builds all NuGet packages
- Runs tests before packaging
- Supports custom version override
- Outputs to nupkgs directory
- Issue: Hardcoded project list requires manual updates when adding packages

**publish.sh**:
- Publishes packages to NuGet.org in dependency order
- Supports API key from file, environment variable, or prompt
- Includes symbol package publishing
- Hardcoded publish order
- Issue: Publish order list must be manually maintained

**release.sh**:
- Orchestrates version bump, build, test, tag creation
- Supports dry-run mode
- Optional automatic publishing
- Creates git commit and tag
- Issue: Version calculation duplicates logic from version-bump.sh
- Issue: Requires interactive confirmation even in automated workflows

### Current Package Structure

**Actual Packages**:
1. DistributedLeasing.Abstractions - Core framework with authentication
2. DistributedLeasing.Azure.Blob - Blob Storage provider
3. DistributedLeasing.Azure.Cosmos - Cosmos DB provider
4. DistributedLeasing.Azure.Redis - Redis provider
5. DistributedLeasing.ChaosEngineering - Testing utilities

**Current Version**: 4.0.0

**Target Version**: 5.0.0 (major version bump)

## Documentation Enhancement Strategy

### Root README.md Updates

Update the root README to reflect the actual architecture and provide accurate information for users.

#### Changes Required

**Package Structure Section**:
- Remove references to non-existent `DistributedLeasing.Core`
- Remove references to non-existent `DistributedLeasing.Extensions.DependencyInjection`
- List actual packages: Abstractions, Azure.Blob, Azure.Cosmos, Azure.Redis, ChaosEngineering
- Clarify that Abstractions package contains core contracts, base implementations, authentication, and observability

**Installation Examples**:
- Remove DI extension examples that reference non-existent package
- Update installation commands to reflect actual package names
- Simplify to direct provider usage patterns

**Usage Examples**:
- Retain Azure Blob Storage basic usage (accurate)
- Remove ASP.NET Core DI example section (references non-existent package)
- Add authentication configuration examples showing Azure Identity integration
- Add observability examples (health checks, metrics, activity source)

**New Sections to Add**:
- Authentication Configuration section showing how to configure Azure authentication modes
- Observability section showing health checks, metrics, and distributed tracing
- Event System section showing lease lifecycle events
- Advanced Scenarios section linking to sample projects

**Links to Update**:
- Replace generic GitHub URLs with actual repository URL
- Update documentation links to point to relevant sample READMEs
- Add links to specific provider package READMEs

### Package-Specific READMEs

Create individual README files for each NuGet package to be included in the package distribution.

#### DistributedLeasing.Abstractions Package README

Location: `src/DistributedLeasing.Abstractions/README.md`

Content Structure:
- Package overview and purpose
- What's included: contracts, base implementations, authentication, configuration, events, exceptions, observability
- When to use this package directly vs provider packages
- Authentication configuration guide with all supported modes
- Event system usage examples
- Observability integration guide (health checks, metrics, distributed tracing)
- Extension points for building custom providers
- Link to samples and root documentation

Key Differentiator: This package is the foundation - emphasize that provider packages automatically include it.

#### DistributedLeasing.Azure.Blob Package README

Location: `src/DistributedLeasing.Azure.Blob/README.md`

Content Structure:
- Package overview and Azure Blob lease mechanism
- Installation instructions
- Quick start code example
- Configuration options (connection string vs managed identity)
- Blob-specific features (native Azure lease, metadata storage)
- Performance characteristics
- Best practices for blob leasing
- Troubleshooting common issues
- Link to BlobLeaseSample for comprehensive examples

Leverage Content From: BlobLeaseSample README (extract relevant patterns)

#### DistributedLeasing.Azure.Cosmos Package README

Location: `src/DistributedLeasing.Azure.Cosmos/README.md`

Content Structure:
- Package overview and Cosmos DB optimistic concurrency mechanism
- Installation instructions
- Quick start code example
- Configuration options (connection string vs managed identity, container setup)
- Cosmos-specific features (ETag-based concurrency, lease documents)
- Performance characteristics and RU considerations
- Best practices for Cosmos leasing
- Container provisioning requirements
- Troubleshooting common issues
- Link to CosmosLeaseSample when created

Key Differentiator: ETag-based optimistic concurrency, global distribution capabilities

#### DistributedLeasing.Azure.Redis Package README

Location: `src/DistributedLeasing.Azure.Redis/README.md`

Content Structure:
- Package overview and Redlock algorithm
- Installation instructions
- Quick start code example
- Configuration options (connection string vs managed identity for Azure Cache for Redis)
- Redis-specific features (Redlock implementation, high-performance locking)
- Performance characteristics
- Best practices for Redis leasing
- Troubleshooting common issues

Key Differentiator: Redlock algorithm, lowest latency option

#### DistributedLeasing.ChaosEngineering Package README

Location: `src/DistributedLeasing.ChaosEngineering/README.md`

Content Structure:
- Package purpose: testing and resilience validation
- WARNING: Not for production use
- Installation instructions
- How to inject controlled failures
- Testing scenarios supported
- Integration with test frameworks
- Example test cases

### Sample Documentation Updates

#### CosmosLeaseSample README Creation

Location: `samples/CosmosLeaseSample/README.md`

Content Structure (mirror BlobLeaseSample structure):
- Demo overview and what it demonstrates
- Quick start options (automatic setup, manual setup, interactive)
- Configuration modes (connection string vs managed identity)
- Running the demo with multiple instances
- Demo scenarios (simultaneous startup, takeover on failure, multi-region)
- Understanding the output
- Cosmos-specific inspection (portal queries, document structure)
- Troubleshooting
- Clean up
- Architecture diagram

Model After: BlobLeaseSample README (proven comprehensive structure)

#### BlobLeaseSample README Updates

Minimal updates required - already excellent:
- Verify all links are functional
- Ensure consistency with package README terminology
- Add reference to DistributedLeasing.Azure.Blob package README

### Documentation Integration Requirements

All package READMEs must be included in NuGet packages via Directory.Build.props configuration.

Update Directory.Build.props to include package-specific READMEs:

Current Configuration:
```xml
<ItemGroup>
  <None Include="$(MSBuildThisFileDirectory)README.md" Pack="true" PackagePath="\" Condition="Exists('$(MSBuildThisFileDirectory)README.md')" />
</ItemGroup>
```

Enhanced Configuration Pattern:
```xml
<ItemGroup>
  <!-- Include root README -->
  <None Include="$(MSBuildThisFileDirectory)README.md" Pack="true" PackagePath="\" Condition="'$(IsPackable)' == 'true' AND Exists('$(MSBuildThisFileDirectory)README.md')" />
  
  <!-- Include package-specific README if it exists -->
  <None Include="$(MSBuildProjectDirectory)\README.md" Pack="true" PackagePath="\" Condition="'$(IsPackable)' == 'true' AND Exists('$(MSBuildProjectDirectory)\README.md')" />
</ItemGroup>
```

Priority Logic: Package-specific README takes precedence when both exist.

## Automated Release Pipeline Design

### Goals

1. One-touch version upgrade and publishing
2. Support for major and minor version increments
3. Automatic package building and testing
4. Automatic NuGet publishing
5. Git tagging and commit automation
6. Non-interactive operation for CI/CD compatibility
7. Dry-run capability for safety
8. Comprehensive validation and rollback support

### Enhanced Release Script Architecture

#### Core Script: release.sh Enhancement

Transform `release.sh` into a comprehensive one-touch release orchestrator.

**Input Parameters**:
- Version type: `major`, `minor`, `patch`
- Optional: `--pre-release [alpha|beta|rc]`
- Optional: `--publish` (auto-publish to NuGet)
- Optional: `--dry-run` (simulate without changes)
- Optional: `--skip-tests` (for emergency releases)
- Optional: `--force` (bypass interactive confirmations for CI/CD)
- Optional: `--api-key-env VAR_NAME` (specify environment variable for API key)

**Enhanced Features**:

**1. Dynamic Package Discovery**
- Scan `src/` directory for all `.csproj` files with `<IsPackable>true</IsPackable>`
- Eliminate hardcoded project lists
- Automatically detect dependency order from ProjectReference elements
- Build topological sort for correct publishing order

**2. Non-Interactive Mode**
- Add `--force` flag to bypass all interactive prompts
- Use in CI/CD pipelines
- Still require explicit confirmation in interactive mode for safety

**3. Enhanced Version Management**
- Extract version from Directory.Build.props
- Calculate new version using modular version-bump logic
- Update Directory.Build.props atomically
- Validate version format before applying

**4. Comprehensive Pre-Release Validation**
- Check for uncommitted changes (override with --force)
- Verify all tests pass
- Validate package metadata completeness
- Check for NuGet API key availability before building
- Verify git repository is clean and on correct branch

**5. Atomic Release Operations**
- Create checkpoint before version bump
- Support rollback on any failure
- Maintain transaction log of operations
- Cleanup temporary files automatically

**6. Enhanced Git Integration**
- Create commit: "Release v{VERSION}"
- Create annotated tag: "v{VERSION}" with release notes
- Optionally push to remote with `--push` flag
- Support custom commit messages with `--message`

**7. Publishing Enhancements**
- Use dynamic package discovery
- Publish in correct dependency order
- Implement retry logic for transient failures
- Validate packages before publishing
- Support `--skip-duplicate` for idempotency

**8. Release Notes Generation**
- Option to generate release notes from git commits since last tag
- Format: conventional commits style
- Include in PackageReleaseNotes in Directory.Build.props
- Support custom release notes file

**9. Post-Release Actions**
- Display published package URLs
- Show next steps (push tags, create GitHub release)
- Generate release checklist
- Optionally create GitHub release draft via API

#### Enhanced pack-all.sh

**Dynamic Package Discovery**:
- Replace hardcoded PROJECTS array with dynamic discovery
- Find all `.csproj` files in `src/` with `<IsPackable>true</IsPackable>`
- Use XML parsing or grep to detect packable projects
- Build dependency graph for correct build order

**Package Validation**:
- Validate package metadata completeness
- Check for README files
- Verify version consistency
- Validate dependency versions

**Improved Test Execution**:
- Run tests in parallel where possible
- Generate coverage reports
- Support test filtering with `--test-filter`
- Optional benchmark execution

**Build Optimization**:
- Use incremental builds where safe
- Parallel project builds with `-maxcpucount`
- Binary log generation for diagnostics

#### Enhanced publish.sh

**Dynamic Package Publishing**:
- Discover packages from nupkgs directory
- Auto-detect dependency order from package metadata
- Eliminate hardcoded publish order

**Enhanced API Key Management**:
- Support multiple key sources with priority:
  1. Command-line parameter `--api-key`
  2. Environment variable (configurable name)
  3. API key file
  4. Prompt user (interactive mode only)
- Validate key format before publishing
- Secure key handling (never log key value)

**Publishing Resilience**:
- Retry failed publishes with exponential backoff
- Skip already-published packages with `--skip-duplicate`
- Validate package upload success
- Report publishing progress with status updates

**Post-Publishing Validation**:
- Query NuGet.org API to verify package availability
- Wait for package indexing
- Display package URLs
- Verify symbol package publishing

#### Enhanced version-bump.sh

**Integration Focus**:
- Refactor to library-style for use by release.sh
- Support non-interactive mode with `--force`
- Return new version to caller for scripting
- Atomic file updates with validation

**Validation Enhancements**:
- Verify Directory.Build.props exists
- Validate XML structure
- Check version format compliance
- Verify version increment is valid

**Improved Pre-Release Handling**:
- Auto-increment pre-release numbers
- Support custom pre-release identifiers
- Validate pre-release naming conventions

### CI/CD Integration Considerations

**GitHub Actions Workflow Pattern**:

Trigger Conditions:
- Manual workflow dispatch with version type input
- Automatic on release branch push
- Pull request validation (dry-run only)

Environment Variables Required:
- `NUGET_API_KEY`: NuGet.org API key (stored as secret)
- `GITHUB_TOKEN`: For creating releases

Workflow Steps:
1. Checkout repository
2. Setup .NET SDK (all target frameworks)
3. Run release script with `--force --publish --api-key-env NUGET_API_KEY`
4. Push tags and commits
5. Create GitHub release with artifacts

**Azure DevOps Pipeline Pattern**:

Similar structure with Azure-specific authentication for package feeds if using Azure Artifacts in addition to NuGet.org.

### Directory.Build.props Version 5.0 Updates

**PackageReleaseNotes Update**:

Current:
```xml
<PackageReleaseNotes>Major version 2.0: Eliminated code duplication, merged authentication into abstractions, clean SOLID architecture. Breaking changes: Internal abstractions removed, providers now reference shared Abstractions package.</PackageReleaseNotes>
```

New for 5.0:
```xml
<PackageReleaseNotes>Major version 5.0: Enhanced documentation with package-specific READMEs, improved authentication guide, added observability examples, and established automated release pipeline. No breaking API changes - safe to upgrade from 4.x.</PackageReleaseNotes>
```

**Metadata Enhancements**:
- Update copyright year to 2025
- Verify all URLs are functional
- Ensure PackageProjectUrl and RepositoryUrl are correct

### Package-Specific README Integration

Each project's .csproj should pack its own README if it exists.

Approach: Modify Directory.Build.props to conditionally include package-specific READMEs.

Enhanced ItemGroup:
```xml
<ItemGroup>
  <!-- Each package includes its own README if present, falling back to root README -->
  <None Include="$(MSBuildProjectDirectory)\README.md" Pack="true" PackagePath="\" Condition="'$(IsPackable)' == 'true' AND Exists('$(MSBuildProjectDirectory)\README.md')" />
  <None Include="$(MSBuildThisFileDirectory)README.md" Pack="true" PackagePath="\" Condition="'$(IsPackable)' == 'true' AND !Exists('$(MSBuildProjectDirectory)\README.md') AND Exists('$(MSBuildThisFileDirectory)README.md')" />
</ItemGroup>
```

Logic: Package-specific README preferred, root README as fallback.

## Release Workflow for Version 5.0

### Pre-Release Checklist

1. Ensure all tests pass locally
2. Review and update all documentation
3. Verify package metadata accuracy
4. Validate NuGet API key availability
5. Confirm git working directory is clean
6. Verify on main/master branch

### Release Execution Steps

**Manual Release (Recommended for 5.0)**:

Step 1: Test the release process with dry-run
```bash
./scripts/release.sh major --dry-run
```

Step 2: Execute the actual release
```bash
./scripts/release.sh major --publish
```

Step 3: Push changes and tags
```bash
git push origin main
git push origin --tags
```

Step 4: Create GitHub release
- Navigate to GitHub releases page
- Create release from tag v5.0.0
- Include release notes
- Attach package artifacts from nupkgs/

**Automated Release (CI/CD)**:

Trigger GitHub Actions workflow with major version input
Workflow automatically:
- Bumps version to 5.0.0
- Runs tests
- Builds packages
- Publishes to NuGet.org
- Creates git tag
- Pushes changes
- Creates GitHub release

### Post-Release Verification

1. Verify packages appear on NuGet.org
   - Check DistributedLeasing.Abstractions 5.0.0
   - Check DistributedLeasing.Azure.Blob 5.0.0
   - Check DistributedLeasing.Azure.Cosmos 5.0.0
   - Check DistributedLeasing.Azure.Redis 5.0.0
   - Check DistributedLeasing.ChaosEngineering 5.0.0

2. Verify package READMEs display correctly on NuGet.org

3. Test package installation in clean project
   ```bash
   dotnet new console -n TestDistributedLeasing
   cd TestDistributedLeasing
   dotnet add package DistributedLeasing.Azure.Blob --version 5.0.0
   ```

4. Verify dependency chain correctness
   - Blob package should reference Abstractions 5.0.0

5. Monitor NuGet.org package statistics and errors

### Rollback Procedure

If critical issues discovered post-release:

1. Unlist affected packages on NuGet.org (do not delete)
2. Fix issues in codebase
3. Release patch version 5.0.1 with fixes
4. Update release notes with fix details

NuGet.org does not support package deletion for public packages, only unlisting.

## Script Enhancement Priorities

### High Priority (Required for 5.0 Release)

1. **release.sh: Add --force flag for non-interactive mode**
   - Bypass all confirmations
   - Enable CI/CD automation
   - Validate inputs before execution

2. **release.sh: Fix version calculation to use version-bump.sh logic**
   - Extract version calculation into shared function
   - Eliminate duplicate code
   - Ensure consistency

3. **Directory.Build.props: Update ItemGroup for package READMEs**
   - Support package-specific README inclusion
   - Maintain fallback to root README
   - Test packaging with and without package README

4. **Directory.Build.props: Update version to 5.0.0**
   - Update PackageReleaseNotes for version 5.0
   - Update copyright year to 2025

### Medium Priority (Enhances Robustness)

1. **pack-all.sh: Dynamic package discovery**
   - Scan src/ for IsPackable projects
   - Build dependency graph
   - Support future package additions without script changes

2. **publish.sh: Dynamic package publishing order**
   - Parse package dependencies
   - Build topological sort
   - Eliminate hardcoded order

3. **publish.sh: Add retry logic**
   - Handle transient network failures
   - Exponential backoff
   - Configurable retry count

### Low Priority (Future Enhancements)

1. **Release notes automation**
   - Generate from conventional commits
   - Include in PackageReleaseNotes
   - Support custom templates

2. **GitHub release creation automation**
   - Use GitHub API or CLI
   - Attach package artifacts
   - Include generated release notes

3. **Package validation enhancements**
   - Metadata completeness checks
   - Dependency version consistency
   - README presence validation

## Documentation Writing Standards

### Tone and Style

- Professional and approachable
- Focus on practical examples
- Assume reader familiarity with distributed systems concepts
- Provide links for deeper learning
- Use consistent terminology throughout

### Code Examples

- Complete, runnable code snippets
- Include necessary using statements
- Show both simple and advanced scenarios
- Highlight common pitfalls with warnings
- Include comments explaining key decisions

### Structure

- Start with package purpose and overview
- Installation instructions early
- Quick start for immediate value
- Detailed configuration options
- Advanced scenarios and patterns
- Troubleshooting section
- Links to related documentation

### Cross-Referencing

- Link between root README and package READMEs
- Reference samples for comprehensive examples
- Point to authentication documentation from all providers
- Link to observability documentation where relevant

## Success Criteria

### Documentation Quality

- All package READMEs created and comprehensive
- Root README accurately reflects architecture
- No references to non-existent packages
- Authentication guide complete with all modes
- Observability integration documented
- Sample READMEs complete (including Cosmos)

### Release Pipeline Functionality

- One-touch major version release successful
- All packages published to NuGet.org with version 5.0.0
- Package READMEs visible on NuGet.org
- Git tags created and pushed
- No manual intervention required in non-interactive mode
- Dry-run mode validates without making changes

### Package Quality

- All packages install correctly
- Dependencies resolve properly
- READMEs display on NuGet.org
- Metadata accurate and complete
- Symbol packages published successfully

### Process Validation

- Release workflow documented
- Rollback procedure tested
- CI/CD integration pattern defined
- Future releases simplified

## Risk Mitigation

### Documentation Risks

**Risk**: Outdated documentation after release
**Mitigation**: Establish documentation review as part of PR process

**Risk**: Package README not visible on NuGet.org
**Mitigation**: Test packaging locally before release, verify ItemGroup configuration

**Risk**: Inconsistent terminology across documents
**Mitigation**: Create terminology glossary, review for consistency

### Release Pipeline Risks

**Risk**: Publish failure mid-release
**Mitigation**: Implement rollback checkpoints, use --skip-duplicate flag

**Risk**: Version conflict with existing packages
**Mitigation**: Query NuGet.org API before publishing, validate version uniqueness

**Risk**: Broken dependency chain
**Mitigation**: Build and test all packages together, validate dependency versions

**Risk**: Interactive prompt blocks CI/CD
**Mitigation**: Implement --force flag, validate non-interactive mode

### Package Quality Risks

**Risk**: Incorrect version in some packages
**Mitigation**: Centralized version management in Directory.Build.props

**Risk**: Missing dependencies in package metadata
**Mitigation**: Automated validation before publishing

**Risk**: Broken links in documentation
**Mitigation**: Link validation in CI/CD pipeline

## Implementation Sequence

### Phase 1: Documentation Foundation

1. Create DistributedLeasing.Abstractions README
2. Create DistributedLeasing.Azure.Blob README
3. Create DistributedLeasing.Azure.Cosmos README
4. Create DistributedLeasing.Azure.Redis README
5. Create DistributedLeasing.ChaosEngineering README
6. Update root README to reflect actual architecture

### Phase 2: Sample Documentation

1. Create CosmosLeaseSample README
2. Review and update BlobLeaseSample README
3. Ensure consistency across all samples

### Phase 3: Package Integration

1. Update Directory.Build.props ItemGroup for package READMEs
2. Test local packaging with package-specific READMEs
3. Validate README inclusion in .nupkg files

### Phase 4: Release Script Enhancement

1. Add --force flag to release.sh
2. Fix version calculation duplication
3. Test dry-run mode thoroughly
4. Add validation logic
5. Test rollback procedures

### Phase 5: Version 5.0 Release

1. Update Directory.Build.props version to 5.0.0
2. Update PackageReleaseNotes
3. Execute dry-run release
4. Execute actual release
5. Publish to NuGet.org
6. Verify packages on NuGet.org
7. Create GitHub release

### Phase 6: Validation and Documentation

1. Test package installation from NuGet.org
2. Verify dependency resolution
3. Document release workflow
4. Create runbook for future releases

## Appendix: Package Dependency Graph

```
DistributedLeasing.Abstractions (foundation)
  ↑ (referenced by)
  ├── DistributedLeasing.Azure.Blob
  ├── DistributedLeasing.Azure.Cosmos
  ├── DistributedLeasing.Azure.Redis
  └── DistributedLeasing.ChaosEngineering
```

**Publishing Order** (dependency-first):
1. DistributedLeasing.Abstractions
2. All provider packages (parallel possible, no inter-provider dependencies)

## Appendix: Key File Locations

**Documentation**:
- Root: `/README.md`
- Abstractions: `/src/DistributedLeasing.Abstractions/README.md`
- Azure Blob: `/src/DistributedLeasing.Azure.Blob/README.md`
- Azure Cosmos: `/src/DistributedLeasing.Azure.Cosmos/README.md`
- Azure Redis: `/src/DistributedLeasing.Azure.Redis/README.md`
- Chaos Engineering: `/src/DistributedLeasing.ChaosEngineering/README.md`
- Blob Sample: `/samples/BlobLeaseSample/README.md`
- Cosmos Sample: `/samples/CosmosLeaseSample/README.md`

**Build Configuration**:
- Version and metadata: `/Directory.Build.props`
- Package versions: `/Directory.Packages.props`

**Release Scripts**:
- Main orchestrator: `/scripts/release.sh`
- Version bumping: `/scripts/version-bump.sh`
- Package building: `/scripts/pack-all.sh`
- NuGet publishing: `/scripts/publish.sh`

**Output**:
- Built packages: `/nupkgs/`
- API key storage: `/.nuget-api-key` (gitignored)
