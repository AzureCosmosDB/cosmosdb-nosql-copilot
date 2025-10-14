# Testing Unit Tests in GitHub Codespaces

This guide provides step-by-step instructions for testing the unit tests workflow, deploying the solution, and running the application using GitHub Codespaces.

## Quick Start in GitHub Codespaces

### 1. Open GitHub Codespaces

Click the badge to open the project in GitHub Codespaces:

[![Open in GitHub Codespaces](https://github.com/codespaces/badge.svg)](https://codespaces.new/AzureCosmosDB/cosmosdb-nosql-copilot)

This will create a new Codespace with all the necessary tools pre-installed:
- .NET 8 SDK
- Azure Developer CLI (azd)
- Azure CLI
- Bicep CLI
- All required VS Code extensions

### 2. Test the Unit Tests Locally

Once your Codespace is ready, open a terminal and run the following commands to validate the unit tests:

#### Install .NET Aspire Workload
```bash
dotnet workload install aspire
```

#### Test Package Restore and Build
```bash
cd src
dotnet restore cosmos-copilot.sln
dotnet build cosmos-copilot.sln --configuration Release
```

#### Check for Outdated/Deprecated Packages
```bash
dotnet list ./cosmos-copilot.WebApp/cosmos-copilot.WebApp.csproj package --outdated
dotnet list ./cosmos-copilot.WebApp/cosmos-copilot.WebApp.csproj package --deprecated
```

#### Validate Bicep Files
```bash
cd ../infra
bicep build main.bicep
```

Expected output: Bicep file compiles successfully with possible warnings (which is normal).

#### Code Quality Check
```bash
cd ../src
dotnet format cosmos-copilot.sln --verify-no-changes
```

### 3. Deploy the Solution to Azure

From the Codespace terminal:

#### Log in to Azure
```bash
azd auth login
```

This will open a browser window for authentication. Follow the prompts to sign in.

#### Initialize and Deploy
```bash
# Initialize the project (if not already done)
azd init -t AzureCosmosDB/cosmosdb-nosql-copilot

# Provision and deploy everything
azd up
```

Follow the prompts:
1. Select your Azure subscription
2. Choose a region (see [important notes](#important-deployment-notes) below)
3. Enter an environment name (e.g., `dev`, `test`)

The deployment will:
- Create all Azure resources (Cosmos DB, Azure OpenAI, App Service, etc.)
- Build the application
- Deploy the web app
- Configure all settings and secrets

#### Deployment Time
The full deployment typically takes **15-20 minutes**.

### 4. Run the Application Locally

After deployment, test the application locally in Codespaces:

#### Option A: Run with .NET Aspire (Recommended)
```bash
cd src/cosmos-copilot.AppHost
dotnet run
```

The Aspire dashboard will open automatically, showing:
- Application health status
- Logs and traces
- Resource connections

#### Option B: Run the Web App Directly
```bash
cd src/cosmos-copilot.WebApp
dotnet run
```

Access the app by:
1. VS Code will show a notification about port forwarding
2. Click "Open in Browser" or navigate to the forwarded port URL

### 5. Verify the Deployed Application

Access your deployed Azure application:

```bash
# Get the deployed app URL
azd env get-values | grep WEBSITE_URL
```

Open the URL in your browser to test the deployed application.

### 6. Run the GitHub Actions Workflow Manually

To test the GitHub Actions workflow:

1. Go to your GitHub repository
2. Click on **Actions** tab
3. Select **Unit Tests** workflow from the left sidebar
4. Click **Run workflow** dropdown
5. Select the branch and click **Run workflow**

The workflow will execute all test jobs:
- Package restore and build
- Bicep validation
- Deployment validation (if Azure credentials are configured)
- Code quality checks

## Important Deployment Notes

### Required Azure Quota
The solution requires:
- **gpt-4o**: 10K tokens per minute
- **text-embedding-3-large**: 5K tokens per minute

Check your quota at [Azure OpenAI Portal](https://oai.azure.com/portal)

### Supported Regions
Deploy to regions where both models are available:
- canadaeast
- eastus
- eastus2
- francecentral
- japaneast
- norwayeast
- polandcentral
- southindia
- swedencentral
- switzerlandnorth
- westus3

### Full-Text & Hybrid Search (Preview)
If you want to test the full-text/hybrid search feature:
1. Deploy to `northcentralus` or `uksouth`
2. Uncomment the hybrid search code in `ChatService.cs`

### Required Azure Permissions
Your account needs:
- [Managed Identity Contributor](https://learn.microsoft.com/azure/role-based-access-control/built-in-roles/identity#managed-identity-contributor)
- [DocumentDB Account Contributor](https://learn.microsoft.com/azure/role-based-access-control/built-in-roles/databases#documentdb-account-contributor)

Or simply **Subscription Owner** role.

## Testing Scenarios

### Test 1: Package Compatibility
Verify packages are compatible and up-to-date:
```bash
cd src
dotnet list cosmos-copilot.WebApp/cosmos-copilot.WebApp.csproj package --outdated
```

### Test 2: Build Validation
Ensure the solution builds without errors:
```bash
dotnet build cosmos-copilot.sln --configuration Release
```

### Test 3: Bicep Infrastructure Validation
Validate infrastructure as code:
```bash
cd infra
bicep build main.bicep
az deployment sub validate \
  --location eastus \
  --template-file main.bicep \
  --parameters environmentName=test location=eastus
```

### Test 4: End-to-End Deployment
Full deployment test:
```bash
azd up
```

### Test 5: Application Health
After deployment, verify the app responds:
```bash
# Get the app URL
APP_URL=$(azd env get-values | grep WEBSITE_URL | cut -d'=' -f2 | tr -d '"')

# Test the endpoint
curl -I $APP_URL
```

## Cleanup

To remove all deployed resources:

```bash
azd down --force --purge
```

This will delete all Azure resources created by the deployment.

## Troubleshooting

### Issue: Aspire Workload Not Found
```bash
dotnet workload install aspire
```

### Issue: Bicep CLI Not Available
Already installed in Codespaces. Verify with:
```bash
bicep --version
```

### Issue: Insufficient Azure OpenAI Quota
1. Visit [Azure OpenAI Portal](https://oai.azure.com/portal)
2. Navigate to Quotas section
3. Request quota increase for your subscription

### Issue: Deployment Fails
Check logs:
```bash
azd deploy --debug
```

View Azure resources:
```bash
azd env get-values
```

## Monitoring Test Results

### View GitHub Actions Results
1. Go to repository **Actions** tab
2. Click on the latest workflow run
3. Review each job's results:
   - ✅ Green checkmark = passed
   - ❌ Red X = failed
   - Click on a job to see detailed logs

### View Deployment Logs
```bash
# In Codespaces
azd monitor --logs
```

### View Application Logs
```bash
# Stream logs from Azure App Service
az webapp log tail --name <app-name> --resource-group <resource-group>
```

## Additional Resources

- [Azure Developer CLI Documentation](https://learn.microsoft.com/azure/developer/azure-developer-cli/)
- [GitHub Codespaces Documentation](https://docs.github.com/codespaces)
- [.NET Aspire Documentation](https://learn.microsoft.com/dotnet/aspire/)
- [Azure Cosmos DB Documentation](https://learn.microsoft.com/azure/cosmos-db/)
