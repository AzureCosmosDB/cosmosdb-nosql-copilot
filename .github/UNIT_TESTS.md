# Unit Tests Documentation

This document describes the unit tests implemented for the cosmos-copilot project.

## Overview

The unit tests are implemented as GitHub Actions workflows that run automatically on:
- Push to main branch
- Pull requests to main branch
- Manual workflow dispatch

## Test Jobs

### 1. Package Restore and Build (`package-restore-and-build`)

**Purpose**: Ensure packages are up to date and compatible with each other, and the project compiles.

**Steps**:
- Checkout code
- Setup .NET 8.0.x
- Install .NET Aspire workload (required for the project)
- Restore NuGet dependencies
- Check for outdated packages (informational)
- Check for deprecated packages (informational)
- Build solution in Release configuration
- Verify web app can be built independently

**Success Criteria**: 
- All packages restore successfully
- Solution builds without errors
- No critical package compatibility issues

### 2. Bicep Deployment Validation (`bicep-validation`)

**Purpose**: Validate the Bicep deployment files are syntactically correct and can be compiled.

**Steps**:
- Checkout code
- Install Bicep CLI
- Build and validate main Bicep file
- Lint Bicep files for best practices
- Check for compilation errors

**Success Criteria**:
- Bicep files compile without errors
- No critical linting violations

### 3. Deployment What-If Analysis (`deployment-validation`)

**Purpose**: Ensure the solution can be deployed (if Azure credentials are available).

**Steps**:
- Checkout code
- Login to Azure (if credentials are configured)
- Run deployment validation using `az deployment sub validate`
- Perform what-if analysis on the deployment

**Success Criteria**:
- If credentials are available: deployment template is valid
- If credentials are not available: skip gracefully with informational message

**Note**: This job is optional and requires the `AZURE_CREDENTIALS` secret to be configured in the repository settings.

### 4. Code Quality Checks (`code-quality`)

**Purpose**: Additional quality checks including build warnings and code formatting.

**Steps**:
- Checkout code
- Setup .NET 8.0.x
- Install .NET Aspire workload
- Restore dependencies
- Build with warnings as errors (informational)
- Check code formatting using `dotnet format` (informational)

**Success Criteria**:
- Code builds successfully
- Minimal build warnings
- Consistent code formatting (informational only)

## Configuration

### Required Secrets

- None (for basic functionality)

### Optional Secrets

- `AZURE_CREDENTIALS`: Azure service principal credentials for deployment validation
  - Format: JSON with clientId, clientSecret, subscriptionId, tenantId
  - If not configured, deployment validation will be skipped

## Running Tests Locally

### Package and Build Tests
```bash
cd src
dotnet workload install aspire
dotnet restore cosmos-copilot.sln
dotnet build cosmos-copilot.sln --configuration Release
```

### Bicep Validation
```bash
cd infra
bicep build main.bicep
```

### Check for Outdated Packages
```bash
dotnet list ./src/cosmos-copilot.WebApp/cosmos-copilot.WebApp.csproj package --outdated
```

### Code Formatting
```bash
cd src
dotnet format cosmos-copilot.sln --verify-no-changes
```

## Continuous Improvement

These tests should be expanded as the project grows to include:
- Unit tests for individual components
- Integration tests for services
- End-to-end tests for critical workflows
- Performance and load tests
- Security scanning
