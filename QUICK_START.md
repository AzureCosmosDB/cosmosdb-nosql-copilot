# Quick Start: Creating PRs for cosmos-native Branches

## Overview

This PR contains patch files that apply changes from PRs #81, #85, and #91 to both:
- `cosmos-native` branch
- `cosmos-native-start` branch

## Changes Applied

### PR #81: GitHub Actions Unit Tests
- Adds comprehensive unit testing workflow
- Includes package restore, build, Bicep validation, and code quality checks
- **New files**: `.github/workflows/unit-tests.yml`, `.github/UNIT_TESTS.md`

### PR #85: OpenAI GlobalStandard SKU & Owner Tagging
- Upgrades OpenAI deployments from "Standard" to "GlobalStandard" SKU
- Adds automatic owner tagging for deployed resources
- **Modified files**: 5 infrastructure files (bicep and hooks)

### PR #91: Product Catalog Data Location
- Updates product data source from Azure Blob Storage to GitHub
- **Modified file**: `infra/main.bicep`

## Quick Instructions

### Step 1: Apply Patches

From your local repository:

```bash
# For cosmos-native branch
git checkout -b pr-cosmos-native origin/cosmos-native
git apply patches/cosmos-native.patch
git add -A
git commit -m "Apply PRs #81, #85, and #91"
git push -u origin pr-cosmos-native

# For cosmos-native-start branch
git checkout -b pr-cosmos-native-start origin/cosmos-native-start
git apply patches/cosmos-native-start.patch
git add -A
git commit -m "Apply PRs #81, #85, and #91"
git push -u origin pr-cosmos-native-start
```

### Step 2: Create Pull Requests on GitHub

1. Go to: https://github.com/AzureCosmosDB/cosmosdb-nosql-copilot/pulls
2. Click "New pull request"

**For cosmos-native:**
- Base: `cosmos-native`
- Compare: `pr-cosmos-native`
- Title: "Apply PRs #81, #85, and #91 to cosmos-native"

**For cosmos-native-start:**
- Base: `cosmos-native-start`
- Compare: `pr-cosmos-native-start`
- Title: "Apply PRs #81, #85, and #91 to cosmos-native-start"

## Files in This PR

- `patches/cosmos-native.patch` - Patch for cosmos-native (9 files, 317 insertions, 10 deletions)
- `patches/cosmos-native-start.patch` - Patch for cosmos-native-start (9 files, 319 insertions, 10 deletions)
- `patches/README.md` - Detailed instructions and documentation
- `QUICK_START.md` - This file (quick reference)

## Verification

Both patches have been tested and apply cleanly to their respective branches:
- ✅ `cosmos-native.patch` applies cleanly to `cosmos-native`
- ✅ `cosmos-native-start.patch` applies cleanly to `cosmos-native-start`

## Need Help?

See `patches/README.md` for:
- Detailed step-by-step instructions
- Alternative approaches
- Troubleshooting tips
- Complete change summary
