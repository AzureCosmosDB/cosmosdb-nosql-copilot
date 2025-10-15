# Instructions for Creating PRs to cosmos-native and cosmos-native-start Branches

This directory contains patch files that apply the changes from PRs #81, #85, and #91 to the `cosmos-native` and `cosmos-native-start` branches.

## What Changes Are Being Applied

### PR #81: Add comprehensive unit tests as GitHub Actions
- Adds `.github/workflows/unit-tests.yml` - GitHub Actions workflow for unit testing
- Adds `.github/UNIT_TESTS.md` - Documentation for the unit tests

### PR #85: Update OpenAI deployments to use GlobalStandard SKU and add owner tagging
- Updates `infra/app/ai.bicep` - Changes SKU from "Standard" to "GlobalStandard"
- Updates `infra/core/ai/cognitive-services/deployment.bicep` - Updates default SKU to "GlobalStandard"
- Updates `infra/azd-hooks/preprovision.ps1` - Adds owner UPN retrieval (PowerShell)
- Updates `infra/azd-hooks/preprovision.sh` - Adds owner UPN retrieval (Bash)
- Updates `infra/main.bicep` - Adds ownerUpn parameter and updates tags to include owner
- Updates `infra/main.parameters.json` - Adds OWNER_UPN parameter mapping
- Updates `infra/main.test.bicep` - Adds ownerUpn parameter to test configuration

### PR #91: Update location of product catalog data
- Updates `infra/main.bicep` - Changes productDataSource URL from Azure Blob Storage to GitHub raw content

## Files in This Directory

- `cosmos-native.patch` - Patch file for the cosmos-native branch
- `cosmos-native-start.patch` - Patch file for the cosmos-native-start branch
- `README.md` - This file

## Option 1: Apply Patches Locally and Create PRs Manually

### For cosmos-native branch:

```bash
# Fetch the latest cosmos-native branch
git fetch origin cosmos-native

# Create a new branch from cosmos-native
git checkout -b apply-prs-cosmos-native origin/cosmos-native

# Apply the patch
git apply patches/cosmos-native.patch

# Add and commit the changes
git add -A
git commit -m "Apply PRs #81, #85, and #91 to cosmos-native branch"

# Push to create a PR
git push -u origin apply-prs-cosmos-native

# Then create a PR targeting cosmos-native on GitHub
```

### For cosmos-native-start branch:

```bash
# Fetch the latest cosmos-native-start branch
git fetch origin cosmos-native-start

# Create a new branch from cosmos-native-start
git checkout -b apply-prs-cosmos-native-start origin/cosmos-native-start

# Apply the patch
git apply patches/cosmos-native-start.patch

# Add and commit the changes
git add -A
git commit -m "Apply PRs #81, #85, and #91 to cosmos-native-start branch"

# Push to create a PR
git push -u origin apply-prs-cosmos-native-start

# Then create a PR targeting cosmos-native-start on GitHub
```

## Option 2: Use the Pre-created Branches

Two branches have already been created with all the changes applied:
- `copilot/apply-prs-cosmos-native` (based on `cosmos-native`)
- `copilot/apply-prs-cosmos-native-start` (based on `cosmos-native-start`)

These branches exist locally in the repository but cannot be pushed due to authentication constraints. You can:

1. Cherry-pick the commits from these branches:
   ```bash
   git checkout -b pr-cosmos-native origin/cosmos-native
   git cherry-pick copilot/apply-prs-cosmos-native
   git push -u origin pr-cosmos-native
   
   git checkout -b pr-cosmos-native-start origin/cosmos-native-start
   git cherry-pick copilot/apply-prs-cosmos-native-start
   git push -u origin pr-cosmos-native-start
   ```

2. Or recreate the branches and apply the patches as described in Option 1.

## Creating the Pull Requests on GitHub

After pushing the branches, create pull requests on GitHub:

1. Navigate to https://github.com/AzureCosmosDB/cosmosdb-nosql-copilot/pulls
2. Click "New pull request"
3. For the first PR:
   - Set base branch to: `cosmos-native`
   - Set compare branch to: `apply-prs-cosmos-native` (or your branch name)
   - Title: "Apply PRs #81, #85, and #91 to cosmos-native"
   - Description: Include details about what's being merged
4. For the second PR:
   - Set base branch to: `cosmos-native-start`
   - Set compare branch to: `apply-prs-cosmos-native-start` (or your branch name)
   - Title: "Apply PRs #81, #85, and #91 to cosmos-native-start"
   - Description: Include details about what's being merged

## Summary of Changes

Both patch files include identical changes:
- 9 files changed
- 317-319 insertions
- 10 deletions
- 2 new files added (.github/UNIT_TESTS.md and .github/workflows/unit-tests.yml)
- 7 files modified (infrastructure and deployment configuration files)

The changes ensure that:
1. Both branches have comprehensive unit testing via GitHub Actions
2. Both branches use the GlobalStandard SKU for OpenAI deployments
3. Both branches automatically tag resources with owner information
4. Both branches use the correct product catalog data source from GitHub
