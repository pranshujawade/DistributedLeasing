# Publishing to NuGet.org - Step by Step Guide

## Prerequisites ✓
- [x] NuGet.org account created
- [x] Repository URLs updated to https://github.com/pranshujawade/DistributedLeasing
- [x] All 6 packages built and ready in `nupkgs/` folder

## Step 2: Generate NuGet API Key

1. **Login to NuGet.org**
   - Go to https://www.nuget.org
   - Sign in with your account

2. **Create an API Key**
   - Click your username (top right) → "API Keys"
   - Click "Create" button
   - Fill in the form:
     - **Key Name**: `DistributedLeasing-Upload` (or any name you prefer)
     - **Select Scopes**: Check "Push" (and "Push new packages and package versions")
     - **Select Packages**: Choose "Glob Pattern"
     - **Glob Pattern**: Enter `DistributedLeasing.*`
     - **Expiration**: Choose your preferred expiration (recommended: 365 days)
   - Click "Create"
   - **IMPORTANT**: Copy the API key immediately - you won't be able to see it again!

## Step 3: Publish Packages to NuGet.org

### Option A: Publish All at Once (Recommended)

Run this single command to publish all packages:

```bash
cd /Users/pjawade/repos/DistributedLeasing

# Replace YOUR_API_KEY with the actual key you copied
dotnet nuget push "nupkgs/*.nupkg" \
  --api-key YOUR_API_KEY \
  --source https://api.nuget.org/v3/index.json \
  --skip-duplicate
```

### Option B: Publish One by One

If you prefer to publish packages individually (useful for testing or if there are errors):

```bash
cd /Users/pjawade/repos/DistributedLeasing

# Replace YOUR_API_KEY with your actual key

# 1. Core (no dependencies - publish first)
dotnet nuget push nupkgs/DistributedLeasing.Core.1.0.0.nupkg \
  --api-key YOUR_API_KEY \
  --source https://api.nuget.org/v3/index.json

# 2. Abstractions (depends on Core)
dotnet nuget push nupkgs/DistributedLeasing.Abstractions.1.0.0.nupkg \
  --api-key YOUR_API_KEY \
  --source https://api.nuget.org/v3/index.json

# 3. Provider packages (depend on Abstractions)
dotnet nuget push nupkgs/DistributedLeasing.Azure.Blob.1.0.0.nupkg \
  --api-key YOUR_API_KEY \
  --source https://api.nuget.org/v3/index.json

dotnet nuget push nupkgs/DistributedLeasing.Azure.Cosmos.1.0.0.nupkg \
  --api-key YOUR_API_KEY \
  --source https://api.nuget.org/v3/index.json

dotnet nuget push nupkgs/DistributedLeasing.Azure.Redis.1.0.0.nupkg \
  --api-key YOUR_API_KEY \
  --source https://api.nuget.org/v3/index.json

# 4. DI Extensions (depends on all providers)
dotnet nuget push nupkgs/DistributedLeasing.Extensions.DependencyInjection.1.0.0.nupkg \
  --api-key YOUR_API_KEY \
  --source https://api.nuget.org/v3/index.json
```

**Note**: Symbol packages (.snupkg) are automatically uploaded alongside the main packages!

## Step 4: Verify Upload

After running the push commands:

1. **Check Command Output**
   - You should see: "Your package was pushed."
   - If you see warnings, they're usually informational (like README recommendations)

2. **Wait for Processing**
   - NuGet.org validates and scans packages (takes 5-15 minutes)
   - You'll receive an email when validation is complete

3. **Verify on NuGet.org**
   - Go to https://www.nuget.org/profiles/YOUR_USERNAME/packages
   - Your packages should appear here (may take a few minutes)

4. **Search for Your Packages**
   - After ~15-30 minutes, search for "DistributedLeasing" on https://www.nuget.org
   - All 6 packages should be discoverable

## Step 5: Test Installation

Once published, test that installation works:

```bash
# Create a test project
mkdir test-install
cd test-install
dotnet new console

# Try installing your package
dotnet add package DistributedLeasing.Azure.Blob

# Verify it installed Core and Abstractions automatically
dotnet list package
```

You should see:
```
DistributedLeasing.Azure.Blob          1.0.0
DistributedLeasing.Abstractions        1.0.0  (transitive)
DistributedLeasing.Core                1.0.0  (transitive)
```

## Common Issues & Solutions

### Issue: "Package already exists"
**Solution**: Increment the version number in `Directory.Build.props` and rebuild packages.

### Issue: "Invalid API key"
**Solution**: 
- Verify you copied the key correctly
- Check the key hasn't expired
- Ensure the glob pattern `DistributedLeasing.*` matches your package IDs

### Issue: "Package validation failed"
**Solution**: 
- Check your email for validation errors
- Common issues: missing license, invalid icon, broken README links
- Fix issues, increment version, and republish

### Issue: Symbol packages not showing
**Solution**: 
- Symbol packages may take longer to process
- They're stored separately at https://www.nuget.org/packages/PACKAGE_NAME/VERSION/symbols

## Step 6: Post-Publication Tasks

### 6.1 Update README.md

Update your GitHub repository README with installation instructions:

```markdown
## Installation

Install the provider you need:

```bash
# For Azure Blob Storage
dotnet add package DistributedLeasing.Azure.Blob

# For Azure Cosmos DB  
dotnet add package DistributedLeasing.Azure.Cosmos

# For Azure Redis
dotnet add package DistributedLeasing.Azure.Redis

# For ASP.NET Core with DI
dotnet add package DistributedLeasing.Extensions.DependencyInjection
```

### 6.2 Add NuGet Badges to README

Add these badges at the top of your README.md:

```markdown
[![NuGet](https://img.shields.io/nuget/v/DistributedLeasing.Azure.Blob.svg)](https://www.nuget.org/packages/DistributedLeasing.Azure.Blob/)
[![Downloads](https://img.shields.io/nuget/dt/DistributedLeasing.Azure.Blob.svg)](https://www.nuget.org/packages/DistributedLeasing.Azure.Blob/)
```

### 6.3 Reserve Package ID Prefix (Optional but Recommended)

To protect your package namespace:

1. Go to https://www.nuget.org/account/Packages
2. Click "Request prefix reservation"
3. Request: `DistributedLeasing`
4. Provide justification: "I am the owner of github.com/pranshujawade/DistributedLeasing"
5. Wait for approval (usually within a few days)

This prevents others from publishing packages like `DistributedLeasing.Fake`

## Future Updates

When you want to publish new versions:

1. **Update Version**
   ```xml
   <!-- In Directory.Build.props -->
   <Version>1.1.0</Version>  <!-- or 1.0.1 for patches -->
   ```

2. **Rebuild Packages**
   ```bash
   cd /Users/pjawade/repos/DistributedLeasing
   rm -rf nupkgs
   dotnet pack src/DistributedLeasing.Core -c Release -o ./nupkgs
   # ... repeat for all packages
   ```

3. **Push Updates**
   ```bash
   dotnet nuget push "nupkgs/*.nupkg" \
     --api-key YOUR_API_KEY \
     --source https://api.nuget.org/v3/index.json \
     --skip-duplicate
   ```

## Package URLs After Publishing

Your packages will be available at:

- https://www.nuget.org/packages/DistributedLeasing.Core
- https://www.nuget.org/packages/DistributedLeasing.Abstractions
- https://www.nuget.org/packages/DistributedLeasing.Azure.Blob
- https://www.nuget.org/packages/DistributedLeasing.Azure.Cosmos
- https://www.nuget.org/packages/DistributedLeasing.Azure.Redis
- https://www.nuget.org/packages/DistributedLeasing.Extensions.DependencyInjection

## Success Checklist

- [ ] API Key created and saved securely
- [ ] All 6 packages pushed successfully
- [ ] Received validation success emails
- [ ] Packages visible on NuGet.org
- [ ] Test installation works correctly
- [ ] GitHub README updated with installation instructions
- [ ] (Optional) Package ID prefix reserved

## Need Help?

- NuGet Documentation: https://docs.microsoft.com/en-us/nuget/
- NuGet Support: https://www.nuget.org/policies/Contact
- Check Package Status: https://www.nuget.org/account/Packages

---

**Ready to publish?** Start with Step 2 above to generate your API key!
