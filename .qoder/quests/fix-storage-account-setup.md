# Design Document: Simplified Storage Account Setup and Reusability

## Implementation TODO List

This section outlines the complete implementation plan to be executed sequentially:

### Phase 1: Azure Cleanup (User Action Required)
- [ ] Run setup script to discover existing storage accounts
- [ ] Review the generated report of storage accounts
- [ ] Select one storage account to retain
- [ ] Execute generated Azure CLI delete commands to remove unwanted accounts
- [ ] Verify only one storage account remains

### Phase 2: Setup Script Enhancement
- [ ] Add cache file read/write functions to `setup-azure-resources.sh`
- [ ] Add credentials file read/write functions with chmod 600 permissions
- [ ] Implement storage account discovery logic using Azure CLI
- [ ] Add selection interface for multiple storage accounts
- [ ] Implement cache validation and invalidation logic
- [ ] Add `--reset` flag to clear cache and credentials
- [ ] Add `--silent` flag for non-interactive regeneration
- [ ] Generate cleanup commands for user to execute
- [ ] Update configuration generation to read from credentials file
- [ ] Add connection string security measures (no logging, proper permissions)

### Phase 3: Script Integration
- [ ] Modify `run-demo.sh` to validate configuration before launch
- [ ] Add silent setup invocation when cache exists but config missing
- [ ] Update `run-competition-demo.sh` with same validation logic
- [ ] Modify `inspect-lease-state.sh` to read from `.azure-credentials`
- [ ] Add credentials file existence checks to all scripts

### Phase 4: Git Ignore Configuration
- [ ] Add `.azure-storage-cache` to `.gitignore`
- [ ] Add `.azure-credentials` to `.gitignore`
- [ ] Verify `appsettings.Local.json` is already in `.gitignore`
- [ ] Test that `git status` never shows credentials files

### Phase 5: Testing and Validation
- [ ] Test fresh environment (no cache, no credentials, multiple storage accounts)
- [ ] Test cache exists but config missing scenario
- [ ] Test complete reuse scenario (cache and config exist)
- [ ] Test `--reset` flag functionality
- [ ] Test `--silent` flag functionality
- [ ] Verify credentials file has 600 permissions
- [ ] Verify no connection string in console output
- [ ] Test all demo scripts end-to-end
- [ ] Verify idempotency (run setup multiple times)
- [ ] Test error recovery scenarios

### Phase 6: Documentation Updates
- [ ] Update README.md with new workflow
- [ ] Document credentials file purpose and security
- [ ] Update troubleshooting section
- [ ] Add git ignore verification steps

## Problem Statement

The current sample project creates a new storage account on every setup run, leading to:

- Resource proliferation in Azure with multiple unused storage accounts
- Unnecessary cost accumulation from abandoned resources
- Manual cleanup burden on the user
- Redundant resource creation workflow
- Configuration friction with multiple temporary setups

The user needs a streamlined approach that:
- Identifies and consolidates to a single storage account
- Reuses the existing storage account for all subsequent runs
- Automates configuration provisioning without manual intervention
- Eliminates the need for Azure-side manual operations

## Objectives

1. Identify existing storage accounts in the resource group and clean up all but one
2. Modify setup script to detect and reuse existing storage accounts instead of creating new ones
3. Automate connection string and configuration file generation
4. Ensure all sample scripts work seamlessly with the persistent storage account
5. Provide clear instructions for the one-time Azure cleanup operation
6. Validate the entire workflow end-to-end after implementation

## Design Strategy

### Storage Account Consolidation

**Existing State Discovery**

The setup script will first query Azure to detect existing storage accounts in the target resource group before taking any action.

**Detection Logic**

Query Azure CLI to list all storage accounts within the resource group that match the naming pattern `pranshuleasestore*`. This pattern-based search will identify all accounts created by previous setup runs.

**Cleanup Strategy**

Since Azure operations require user authorization and verification:
- The script will display a table of all discovered storage accounts with their creation dates and resource details
- Present the user with a selection prompt to choose which storage account to retain
- The script will recommend keeping the oldest or most recently used account
- Generate Azure CLI delete commands for the user to execute for unwanted accounts
- Provide a single consolidated command to delete all non-selected accounts

**Reuse Mechanism**

Once a single storage account is identified or selected:
- Store the storage account name in a persistent local file for future reference
- Skip storage account creation if a valid existing account is found
- Validate the existing account's accessibility and configuration
- Ensure the lease container exists within the retained account

### Script Modifications

**Setup Script Enhancement**

Transform `setup-azure-resources.sh` from a create-only script to an intelligent detect-and-provision script:

**Phase 1: Discovery**
- Check for local cache file containing preferred storage account name
- If cache exists, validate that the storage account is accessible in Azure
- If no cache exists, scan Azure for matching storage accounts in the resource group

**Phase 2: Selection**
- If one storage account exists, automatically select it
- If multiple storage accounts exist, present selection interface to user
- If no storage accounts exist, create a new one with predictable naming
- Store selected account name in local cache file for future runs

**Phase 3: Configuration**
- Retrieve connection string from selected storage account
- Store connection string securely in `.azure-credentials` file
- Ensure the lease container exists, create if missing
- Generate or update `appsettings.Local.json` by reading from `.azure-credentials`
- Update `.gitignore` if needed to exclude both `.azure-credentials` and `appsettings.Local.json`

**Cache File Structure**

Create a simple persistent configuration file named `.azure-storage-cache` in the sample directory:

```
STORAGE_ACCOUNT_NAME=pranshuleasestore123456
RESOURCE_GROUP=pranshu-rg
CONTAINER_NAME=leases
LAST_VALIDATED=2025-12-25T13:00:00Z
```

This file serves as a single source of truth for the preferred storage account, eliminating redundant lookups and enabling instant reuse across script runs.

**Credentials File Structure**

Create a separate git-ignored file named `.azure-credentials` to store sensitive connection string:

```
CONNECTION_STRING=DefaultEndpointsProtocol=https;AccountName=pranshuleasestore123456;AccountKey=...;EndpointSuffix=core.windows.net
```

This file is:
- Automatically created by `setup-azure-resources.sh`
- Read by all scripts that need Azure Storage access (`setup-azure-resources.sh`, `inspect-lease-state.sh`)
- Strictly git-ignored to prevent accidental commits
- Separated from cache file to isolate secrets from metadata
- Protected with file permissions 600 (owner read/write only)
- Single source of truth for connection string
- Never logged or displayed in console output

**Script Usage Pattern**

All scripts will source the connection string using:

```bash
# Load connection string from credentials file
if [ -f ".azure-credentials" ]; then
    source .azure-credentials
    # CONNECTION_STRING variable is now available
else
    echo "Error: Credentials file not found. Run ./setup-azure-resources.sh"
    exit 1
fi
```

This pattern ensures:
- Consistent credential access across all scripts
- No hardcoded credentials in scripts
- Clear error messages when credentials are missing
- Easy credential rotation (update one file)

**Validation Behavior**

When the cache file exists, the script will:
- Verify the cached storage account exists in Azure
- Confirm accessibility with current credentials
- Refresh the `LAST_VALIDATED` timestamp
- Regenerate `appsettings.Local.json` if it's missing or outdated
- If validation fails, delete cache and restart discovery process

### Configuration Automation

**Connection String Retrieval and Storage**

The script will automatically:
- Extract the connection string from Azure using the selected storage account
- Store the connection string in `.azure-credentials` file (git-ignored)
- Validate connection string format before writing to credentials file
- Set file permissions to restrict read access (chmod 600)

**Connection String Usage**

All scripts will:
- Read connection string from `.azure-credentials` file when needed
- Never log or display the connection string in console output
- Use the connection string for Azure CLI operations and configuration generation
- Inject the connection string into `appsettings.Local.json` by reading from credentials file

**Configuration File Generation**

The generated `appsettings.Local.json` will include:
- Full connection string for storage account authentication
- Container name consistent with the setup
- Lease duration and auto-renewal settings optimized for the demo
- Logging configuration appropriate for development

**No Manual Editing Required**

The entire configuration flow will be hands-free:
- User runs `./setup-azure-resources.sh` once
- Script detects or prompts for storage account selection
- Configuration is generated automatically
- Application runs immediately without additional setup

### User Interaction Flow

**First-Time Setup**

User executes the setup script, which will:

1. Check Azure authentication status
2. Set correct subscription context
3. Create or verify resource group existence
4. Scan for existing storage accounts matching the pattern
5. Present findings to user with recommendations
6. Allow user to select preferred account or create new
7. Generate cache file with selection
8. Provision lease container if missing
9. Generate `appsettings.Local.json` with connection details
10. Display ready-to-run instructions

**Subsequent Runs**

User executes the setup script again:

1. Detect cache file presence
2. Validate cached storage account in Azure
3. Regenerate `appsettings.Local.json` if missing
4. Skip all creation steps
5. Display confirmation and proceed

**Cache Reset Option**

Provide a command-line flag `--reset` to clear the cache and restart the discovery process, useful when switching Azure subscriptions or resource groups.

### Azure Cleanup Instructions

**Pre-Execution Report**

Before the user performs any Azure operations, the script will generate a comprehensive report listing:

- Total number of storage accounts found
- Each storage account name, creation date, location, and SKU
- Estimated monthly cost for each account
- Total combined cost of all discovered accounts
- Recommendation for which account to retain

**Deletion Command Generation**

The script will output a ready-to-execute Azure CLI command block:

```
# Generated cleanup commands - Review before executing
az storage account delete --name pranshuleasestore111111 --resource-group pranshu-rg --yes
az storage account delete --name pranshuleasestore222222 --resource-group pranshu-rg --yes
# Keep: pranshuleasestore333333
```

The user copies and executes these commands in their terminal to complete the cleanup.

**Safety Measures**

- Never automatically delete storage accounts without explicit user confirmation
- Display account metadata to help user make informed decisions
- Provide `--dry-run` option to preview actions without making changes
- Include rollback instructions in case of accidental deletion

### Run Script Enhancement

**Automatic Setup Triggering**

Modify `run-demo.sh` to intelligently invoke setup when needed:

**Pre-Flight Check**

Before launching instances:
- Verify `appsettings.Local.json` exists
- If missing, check for `.azure-storage-cache`
- If cache exists, run setup in silent mode to regenerate configuration
- If cache missing, prompt user to run full setup

**Silent Setup Mode**

Introduce a `--silent` flag for `setup-azure-resources.sh` that:
- Skips interactive prompts
- Uses cached storage account automatically
- Regenerates configuration without user input
- Returns exit code 0 on success for script chaining

This enables `run-demo.sh` to self-heal missing configuration files.

## Implementation Scope

### Files to Modify

1. **setup-azure-resources.sh**
   - Add storage account discovery logic
   - Implement selection interface
   - Add cache file read/write operations
   - Add credentials file read/write operations with secure permissions
   - Add validation routines for cached accounts
   - Generate cleanup commands for user execution
   - Support `--reset` and `--silent` flags
   - Read connection string from `.azure-credentials` when generating config

2. **run-demo.sh**
   - Add configuration file validation
   - Invoke setup in silent mode if cache exists but config missing
   - Improve error messaging to guide user through recovery
   - No direct access to credentials file needed

3. **run-competition-demo.sh**
   - Add same configuration validation as run-demo.sh
   - Ensure setup is triggered when needed
   - No direct access to credentials file needed

4. **inspect-lease-state.sh**
   - Modify to read connection string from `.azure-credentials` instead of `appsettings.Local.json`
   - Add validation to ensure credentials file exists
   - Add helper function to safely load credentials

5. **.gitignore** (repository root)
   - Add `.azure-storage-cache` to prevent accidental commits
   - Add `.azure-credentials` to prevent credential leakage
   - Ensure `appsettings.Local.json` exclusion remains

### Files to Create

1. **.azure-storage-cache**
   - Simple key-value format for persistent storage account selection
   - Created and managed by setup script
   - Read by all scripts for storage account information
   - Git-ignored but contains no secrets

2. **.azure-credentials**
   - Stores connection string securely
   - Created by setup script with restrictive permissions (600)
   - Read by scripts that need Azure Storage access
   - Strictly git-ignored to prevent credential exposure
   - Never logged or displayed in console output

### Files Not Changed

- **Program.cs** - No changes needed, configuration loading remains the same
- **ConfigurationHelper.cs** - No changes needed, interactive setup logic is independent
- **appsettings.json** - Template remains unchanged
- **README.md** - Will be updated as part of implementation, not design

## Execution Workflow

### User Actions Required

**Step 1: Azure Cleanup (One-Time)**

The user will execute the setup script once to discover existing storage accounts:

```
./setup-azure-resources.sh
```

The script will output a report and cleanup commands. User will:
- Review the list of storage accounts
- Copy and execute the generated delete commands for unwanted accounts
- Confirm retention of the selected storage account

**Step 2: Automated Configuration (Hands-Off)**

After cleanup, the script automatically:
- Caches the retained storage account
- Generates `appsettings.Local.json`
- Validates connectivity

**Step 3: Run Sample Application (No Azure Actions)**

User runs the demo:

```
./run-demo.sh
```

The script automatically:
- Validates configuration
- Launches instances
- Displays output

All subsequent runs use the cached storage account with zero Azure interaction.

### Post-Implementation Validation Steps

After implementing the design, validate the following scenarios:

**Scenario 1: Fresh Environment**
- Delete `.azure-storage-cache` and `appsettings.Local.json`
- Run `./setup-azure-resources.sh`
- Verify storage account discovery and selection works
- Verify configuration generation succeeds
- Run `./run-demo.sh` and verify instances start correctly

**Scenario 2: Cache Exists, Config Missing**
- Keep `.azure-storage-cache`, delete `appsettings.Local.json`
- Run `./run-demo.sh`
- Verify silent setup regenerates configuration
- Verify instances start without prompts

**Scenario 3: Complete Reuse**
- Keep both cache and config files
- Run `./setup-azure-resources.sh` again
- Verify script skips creation and validates existing setup
- Run `./run-demo.sh` and verify instant startup

**Scenario 4: Cache Reset**
- Run `./setup-azure-resources.sh --reset`
- Verify cache is cleared
- Verify discovery runs again
- Verify new selection can be made

**Scenario 5: Multiple Storage Accounts**
- Manually create 2-3 storage accounts with the naming pattern
- Run `./setup-azure-resources.sh`
- Verify selection interface appears
- Verify cleanup commands are generated
- Verify selected account is cached

## Storage Account Naming Strategy

### Current Naming Pattern

The existing script uses:
```
pranshuleasestore$(date +%s | tail -c 6)
```

This generates names like `pranshuleasestore123456` with a timestamp-based suffix.

### Recommended Naming for New Accounts

When creating a new storage account (none exist), use a predictable, stable name:
```
pranshuleasestore
```

If this name is taken, append a simple counter:
```
pranshuleasestore2
pranshuleasestore3
```

**Rationale**: Predictable names are easier to identify, remember, and manage. Random suffixes were useful for avoiding conflicts when creating multiple accounts, but in a reuse-first strategy, stability is preferred.

### Discovery Pattern

The script will search for accounts matching:
```
pranshuleasestore*
```

This captures both legacy timestamp-based names and new predictable names.

## Configuration Layering

### Configuration Priority

The application already supports configuration layering through .NET's configuration system:

1. `appsettings.json` - Template with placeholders (version controlled)
2. `appsettings.{Environment}.json` - Environment-specific overrides
3. `appsettings.Local.json` - Local development with secrets (git-ignored)
4. Environment variables - Runtime overrides

**No changes needed** to this layering. The script will continue generating `appsettings.Local.json` which takes precedence over the template.

### Cache vs Configuration Separation

**Cache File** (`.azure-storage-cache`)
- Purpose: Track which Azure storage account to use
- Audience: Scripts only
- Format: Simple key-value pairs
- Lifecycle: Persists across runs, deleted on `--reset`

**Configuration File** (`appsettings.Local.json`)
- Purpose: Provide runtime configuration to .NET application
- Audience: Application code
- Format: JSON matching .NET configuration schema
- Lifecycle: Regenerated by scripts when missing

This separation ensures scripts can rediscover and reconfigure without requiring user input.

## Error Handling and Recovery

### Validation Failures

**Storage Account Not Found**

If cached storage account no longer exists in Azure:
- Display warning message
- Delete invalid cache file
- Restart discovery process automatically
- Allow user to select from current accounts or create new

**Connection String Retrieval Failure**

If Azure CLI fails to retrieve connection string:
- Display Azure CLI error message
- Verify user is logged in with `az account show`
- Verify user has access to the storage account
- Provide recovery commands: `az login`, `az account set`

**Container Creation Failure**

If lease container cannot be created:
- Display detailed error from Azure
- Check if storage account allows blob container creation
- Verify firewall rules and network settings
- Suggest manual creation via Azure Portal as fallback

### Script Execution Failures

**Azure CLI Not Authenticated**

Pre-flight check ensures `az account show` succeeds before any operations. If not authenticated:
- Display clear message: "Please run 'az login' first"
- Exit with code 1
- Do not attempt any Azure operations

**Permission Denied**

If user lacks permissions to list, create, or delete storage accounts:
- Display permission error from Azure
- Identify required Azure RBAC role (Contributor or Storage Account Contributor)
- Suggest contacting Azure subscription administrator
- Provide link to Azure RBAC documentation

**Network Connectivity Issues**

If Azure CLI commands timeout or fail due to network:
- Display network error message
- Suggest checking internet connectivity
- Recommend retrying after network is stable
- Provide manual Azure Portal alternative

### Configuration Corruption

**Invalid appsettings.Local.json**

If configuration file is malformed or incomplete:
- Detect during application startup or script validation
- Delete corrupted file
- Regenerate from `.azure-credentials` if available
- If credentials file missing, prompt user to run full setup

**Invalid Cache File**

If `.azure-storage-cache` has incorrect format:
- Display warning about corrupted cache
- Delete cache file
- Restart discovery process
- Continue without interruption

**Invalid or Missing Credentials File**

If `.azure-credentials` is missing or corrupted:
- Display error message indicating credentials are needed
- Do not attempt to read connection string
- Prompt user to run `./setup-azure-resources.sh` to regenerate
- If cache exists but credentials missing, automatically re-fetch from Azure
- Validate connection string format before saving to file

## Security Considerations

### Credential Storage

**Connection String Protection**

Connection strings contain sensitive account keys and must be protected:
- Store in dedicated `.azure-credentials` file (git-ignored)
- Also included in `appsettings.Local.json` for application use (git-ignored)
- Never log connection strings to console or log files
- Set file permissions to 600 (read/write for owner only) on credentials file
- Do not include in error messages or diagnostics
- Scripts read from `.azure-credentials` as single source of truth

**Credentials File Isolation**

The `.azure-credentials` file serves as the secure credential store:
- Single source of truth for connection string
- Separate from configuration and cache files for security layering
- Scripts source from this file rather than parsing JSON
- Enables easy rotation: update one file, regenerate config
- Format is simple key=value for shell script compatibility

**Cache File Security**

The `.azure-storage-cache` file contains no secrets:
- Contains only storage account name and resource group
- Safe to store in plain text
- Still git-ignored to prevent environment coupling
- Separate from credentials for defense in depth

### Alternative Authentication

**DefaultAzureCredential Support**

The application already supports passwordless authentication via Azure CLI credentials:
- Users who prefer this mode can use `dotnet run --configure`
- Interactive setup wizard allows selecting credential mode
- Scripts focus on connection string mode for simplicity
- Both modes remain functional and supported

**Production Recommendations**

For production deployments:
- Use Managed Identity instead of connection strings
- Store secrets in Azure Key Vault
- Leverage workload identity for Kubernetes environments
- The sample demonstrates connection string mode for development ease only

## Testing Strategy

### Manual Testing Checklist

After implementation, manually verify:

- [ ] Script detects multiple storage accounts and presents selection
- [ ] Selected storage account is cached correctly
- [ ] Cache file is created with correct format
- [ ] Credentials file is created with connection string and proper permissions (600)
- [ ] Credentials file is git-ignored and not visible in `git status`
- [ ] Configuration file is generated by reading from credentials file
- [ ] Connection string in `appsettings.Local.json` matches credentials file
- [ ] Application starts successfully with generated configuration
- [ ] `inspect-lease-state.sh` reads connection string from credentials file
- [ ] No connection string appears in console output or logs
- [ ] Subsequent script runs skip creation and reuse cached account
- [ ] `--reset` flag clears both cache and credentials, restarts discovery
- [ ] `--silent` flag regenerates configuration without prompts
- [ ] Cleanup commands are accurate and safe to execute
- [ ] Error messages are clear and actionable
- [ ] Deleting credentials file triggers regeneration on next setup run

### Script Validation

Run the following validation checks:

**Syntax Validation**
- Execute `bash -n setup-azure-resources.sh` to check for syntax errors
- Validate all conditional logic paths are reachable
- Ensure proper error handling with `set -e` and conditional checks

**Idempotency Testing**
- Run setup script multiple times consecutively
- Verify no duplicate resources are created
- Verify configuration remains consistent across runs
- Verify cache and config files are stable

**Azure Integration Testing**
- Test with no existing storage accounts
- Test with one existing storage account
- Test with multiple existing storage accounts
- Test with invalid cache (account deleted externally)
- Test with missing Azure authentication

### End-to-End Demo Validation

Complete the full user journey:

1. Start with clean environment (no cache, no config, no credentials, multiple storage accounts in Azure)
2. Run `./setup-azure-resources.sh`
3. Select one storage account to keep
4. Execute provided cleanup commands to delete others
5. Verify cache, credentials, and config generation
6. Verify `.azure-credentials` has 600 permissions
7. Verify both `.azure-credentials` and `appsettings.Local.json` are git-ignored
8. Run `./run-demo.sh` and verify both instances compete correctly
9. Stop demo and run again to verify reuse
10. Delete `appsettings.Local.json` and run demo again to verify auto-regeneration from credentials file
11. Delete both cache and credentials, run setup to verify fresh regeneration from Azure
12. Run `./inspect-lease-state.sh` and verify it reads from credentials file
13. Run `./setup-azure-resources.sh --reset` and verify both cache and credentials are cleared
14. Verify `git status` never shows credentials file

## Success Criteria

The design is successfully implemented when:

1. **Storage Account Consolidation**: User has one storage account remaining after following cleanup instructions
2. **Automatic Reuse**: All script runs use the cached storage account without creating new ones
3. **Zero Manual Configuration**: Connection string and settings are automatically provisioned
4. **Secure Credential Storage**: Connection string stored in git-ignored `.azure-credentials` file with proper permissions
5. **Script Integration**: All scripts read connection string from credentials file, not from config
6. **Self-Healing**: Missing configuration files are regenerated automatically from credentials file
7. **Clear Instructions**: Azure cleanup commands are accurate, safe, and easy to execute
8. **Idempotent Scripts**: Running setup multiple times produces consistent results
9. **Validated Workflow**: All demo scenarios work correctly after implementation
10. **Git Safety**: Both `.azure-credentials` and `appsettings.Local.json` are properly git-ignored

## Future Enhancements

While not part of this design, future improvements could include:

**Terraform or Bicep Support**
- Provide infrastructure-as-code templates for resource provisioning
- Enable declarative resource management
- Support multi-environment deployments

**Cross-Subscription Support**
- Allow caching multiple storage accounts for different subscriptions
- Add subscription identifier to cache file
- Support switching between environments

**Automated Cleanup**
- Optional flag to automatically delete unused storage accounts
- Requires explicit user confirmation
- Provides dry-run preview before execution

**Container-Based Cache**
- Store cache in Azure Blob Storage itself instead of local file
- Enables shared configuration across team members
- Requires additional authentication setup

**Health Monitoring**
- Periodic validation of cached storage account accessibility
- Alert user if storage account becomes unavailable
- Automatic cache invalidation on persistent failures
