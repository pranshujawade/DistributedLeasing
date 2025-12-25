# Project Cleanup and Blob Lease Sample Creation

## Objective

Clean up the DistributedLeasing project by removing unnecessary documentation, test, and temporary files, then create a comprehensive sample application demonstrating Azure Blob Lease functionality with dependency injection and automatic renewal.

## Scope

### Phase 1: Project Cleanup

Remove redundant and non-essential files while preserving core functionality and essential project structure.

#### Files and Folders to Delete

| Category | Items | Rationale |
|----------|-------|-----------|
| Documentation Files | DISTINGUISHED_ENGINEER_CODE_REVIEW.md<br>DUPLICATION_ANALYSIS.md<br>IMPLEMENTATION_SUMMARY.md<br>TEST_INFRASTRUCTURE_SUMMARY.md<br>PUBLISHING_GUIDE.md<br>docs/ARCHITECTURE.md<br>tests/DistributedLeasing.Tests.Shared/README.md | Internal review and analysis documents not needed for end users |
| Test Infrastructure | build_test.sh<br>generate_coverage.sh<br>coverlet.runsettings<br>verify_build.py | Test-specific tooling files |
| Documentation Folder | docs/ | Entire documentation directory after removing contents |
| Build Scripts | scripts/check-duplication.sh<br>scripts/internalize-abstractions.sh | Development-time scripts not needed in production repository |

#### Files to Preserve

| Category | Items | Rationale |
|----------|-------|-----------|
| Essential Documentation | README.md | Primary user-facing documentation |
| Build Configuration | Directory.Build.props<br>Directory.Packages.props<br>DistributedLeasing.sln<br>.gitignore | Core build and version control files |
| Source Code | src/ directory | All implementation code |
| Tests | tests/ directory | All test projects (excluding internal documentation) |
| Release Scripts | scripts/pack-all.sh<br>scripts/publish.sh<br>scripts/release.sh<br>scripts/version-bump.sh | Required for package publishing workflow |

### Phase 2: Blob Lease Sample Application

Create a production-ready sample demonstrating best practices for using the DistributedLeasing.Azure.Blob provider.

#### Sample Structure

```
samples/
└── BlobLeaseSample/
    ├── BlobLeaseSample.csproj
    ├── Program.cs
    ├── appsettings.json
    ├── appsettings.Development.json
    └── README.md
```

#### Application Design

##### Project Configuration

The sample will be a .NET console application targeting .NET 8.0 with the following dependencies:

- DistributedLeasing.Azure.Blob
- Microsoft.Extensions.Hosting
- Microsoft.Extensions.Configuration
- Microsoft.Extensions.Configuration.Json
- Microsoft.Extensions.DependencyInjection
- Microsoft.Extensions.Logging.Console

##### Configuration Structure (appsettings.json)

The application will use a structured configuration file with placeholders for user-supplied values:

| Configuration Section | Parameters | Purpose |
|----------------------|------------|---------|
| BlobLeasing | StorageAccountUri<br>ContainerName<br>CreateContainerIfNotExists<br>DefaultLeaseDuration<br>AutoRenew<br>AutoRenewInterval<br>AutoRenewRetryInterval<br>AutoRenewMaxRetries<br>KeyPrefix | Core lease provider configuration |
| Authentication | Mode | Authentication strategy (ManagedIdentity, DefaultAzureCredential, ServicePrincipal) |
| Logging | LogLevel settings | Console logging configuration for observability |

Placeholder values will be provided for:
- StorageAccountUri: "https://[YOUR_STORAGE_ACCOUNT].blob.core.windows.net"
- ContainerName: "leases"
- Authentication Mode: "DefaultAzureCredential"

##### Application Architecture

The sample will demonstrate:

1. **Host-based Application Structure**
   - Use of Generic Host for dependency injection and lifecycle management
   - Configuration binding from appsettings.json
   - Structured logging throughout the application

2. **Dependency Injection Setup**
   - Registration of ILeaseManager using AddBlobLeaseManager extension method
   - Configuration binding from IConfiguration
   - Service lifetime management

3. **Lease Acquisition and Management**
   - Acquire a lease for a named resource
   - Enable automatic renewal with configurable intervals
   - Handle lease lifecycle events (renewed, renewal failed, lost)
   - Graceful shutdown and lease release

4. **Event Handling**
   - Subscribe to LeaseRenewed event to log successful renewals
   - Subscribe to LeaseRenewalFailed event to monitor retry attempts
   - Subscribe to LeaseLost event to handle lease expiration scenarios

5. **Observability**
   - Structured logging using ILogger
   - Log lease acquisition, renewal, and release operations
   - Include lease metadata (LeaseId, LeaseName, expiration times)

##### Application Flow

The application will execute the following workflow:

1. **Initialization Phase**
   - Build host with configuration and dependency injection
   - Validate configuration settings
   - Initialize ILeaseManager from DI container

2. **Lease Acquisition Phase**
   - Attempt to acquire a lease named "sample-resource"
   - Use TryAcquireAsync for non-blocking acquisition
   - Log acquisition success or failure

3. **Active Lease Phase**
   - Subscribe to lease lifecycle events
   - Monitor automatic renewal through event handlers
   - Simulate work being performed while holding the lease
   - Demonstrate lease status checking (IsAcquired property)

4. **Shutdown Phase**
   - Gracefully handle cancellation signals
   - Explicitly release the lease using ReleaseAsync
   - Clean up resources through DisposeAsync

##### Error Handling Strategy

The sample will demonstrate proper error handling:

- Catch and log LeaseException for lease-specific errors
- Handle OperationCanceledException for graceful shutdown
- Validate configuration before attempting lease operations
- Provide clear error messages for common misconfigurations

##### Development vs Production Configuration

Two configuration files will be provided:

1. **appsettings.json**: Production-like configuration with placeholders
   - Uses DefaultAzureCredential authentication mode
   - Conservative lease duration and renewal settings
   - Appropriate retry configurations

2. **appsettings.Development.json**: Development-friendly settings
   - Optional ConnectionString-based authentication
   - Shorter lease durations for faster testing
   - Verbose logging enabled

##### Sample README Content

The sample README will include:

- Purpose and overview of the sample
- Prerequisites (Azure Storage account, appropriate permissions)
- Configuration instructions (filling in placeholders)
- Local development setup
- Azure authentication options (Managed Identity, Service Principal, DefaultAzureCredential)
- Common troubleshooting scenarios
- Links to main project documentation

## Implementation Approach

### Phase 1 Execution: Cleanup Operations

1. Delete specified documentation files from root directory
2. Delete test infrastructure scripts
3. Delete development scripts from scripts/ folder
4. Remove docs/ directory
5. Remove test README file
6. Verify remaining project structure integrity
7. Commit changes with message: "Clean up project: remove internal documentation and temporary files"
8. Push to remote repository

### Phase 2 Execution: Sample Creation

1. Create samples/ directory at repository root
2. Create BlobLeaseSample/ subdirectory
3. Generate BlobLeaseSample.csproj with appropriate package references
4. Implement Program.cs with complete sample logic
5. Create appsettings.json with placeholder configuration
6. Create appsettings.Development.json with developer-friendly settings
7. Write comprehensive README.md for the sample
8. Verify sample compiles successfully
9. Commit changes with message: "Add Azure Blob Lease sample with DI and automatic renewal"
10. Push to remote repository

## Configuration Placeholders

The following placeholders will be included in appsettings.json for user customization:

| Placeholder | Description | Example Value |
|-------------|-------------|---------------|
| [YOUR_STORAGE_ACCOUNT] | Azure Storage account name | mystorageaccount |
| ContainerName | Blob container for lease storage | leases |
| DefaultLeaseDuration | Initial lease duration | 00:00:30 (30 seconds) |
| AutoRenewInterval | Time between renewal attempts | 00:00:20 (20 seconds, 2/3 of duration) |
| Authentication.Mode | Azure credential type | DefaultAzureCredential |

## Success Criteria

### Phase 1: Project Cleanup

- All specified documentation files removed
- All temporary and test infrastructure files removed
- Core source code and tests remain intact
- Essential build and release scripts preserved
- README.md remains as primary documentation
- Changes committed and pushed to repository

### Phase 2: Sample Application

- Sample compiles without errors
- Configuration binds correctly from appsettings.json
- ILeaseManager successfully registered via DI
- Lease acquisition, renewal, and release work correctly
- All lifecycle events fire appropriately
- Logging provides clear operational visibility
- README provides clear setup and usage instructions
- Code follows established project conventions
- Changes committed and pushed to repository

## Risk Considerations

| Risk | Impact | Mitigation |
|------|--------|------------|
| Accidental deletion of required files | Project build failure | Verify build after cleanup, follow specified deletion list exactly |
| Sample configuration errors | Sample fails to run | Provide comprehensive README with troubleshooting section |
| Missing package references | Compilation failure | Verify all required NuGet packages are referenced |
| Authentication misconfiguration | Runtime failure | Include multiple authentication examples in configuration |
| Repository push conflicts | Integration issues | Ensure working directory is clean before starting |

## Post-Implementation Verification

After completing both phases:

1. Clean clone the repository to a new directory
2. Verify solution builds successfully
3. Run all existing tests to ensure no regressions
4. Attempt to compile the sample application
5. Review sample README for completeness and clarity
6. Verify all deleted files are no longer present
7. Confirm essential files remain intact6. Verify all deleted files are no longer present
7. Confirm essential files remain intact