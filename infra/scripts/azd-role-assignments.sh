#!/bin/bash

azd env get-values > .env

# Load variables from .env file into your shell
if [ -f .env ]; then
    source ../../.azure/cosmosdb-nosql-copilot/.env
else
    echo ".env file not found!"
    exit 1
fi

# Get principal id from authenticated account
PRINCIPAL_ID=$(az ad signed-in-user show --query id -o tsv)
RESOURCE_GROUP="cosmosdb-nosql-copilot"

# Cognitive Services OpenAI User
# Read access to view files, models, deployments. The ability to create completion and embedding calls.
az role assignment create \
        --role "5e0bd9bd-7b93-4f28-af87-19fc36ad61bd" \
        --assignee-object-id "${PRINCIPAL_ID}" \
        --scope /"/subscriptions/${AZURE_SUBSCRIPTION_ID}/resourceGroups/${RESOURCE_GROUP}" \
        --assignee-principal-type 'User'

# Gets account name
COSMOSDB_NAME=$(az cosmosdb list --resource-group ${RESOURCE_GROUP} --query "[0].name" -o tsv)

# https://aka.ms/cosmos-native-rbac
# Note: Azure Cosmos DB data plane roles are distinct from built-in Azure control plane roles
az cosmosdb sql role assignment create \
        --account-name "${COSMOSDB_NAME}" \
        --resource-group "${RESOURCE_GROUP}" \
        --role-definition-id "00000000-0000-0000-0000-000000000002" \
        --scope /"/" \
        --principal-id "${PRINCIPAL_ID}" 
