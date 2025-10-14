#!/bin/bash

# # Register any required resource providers
az provider register --namespace Microsoft.AlertsManagement

# Get the current user's UPN and set it as an azd environment variable
OWNER_UPN=$(az ad signed-in-user show --query userPrincipalName -o tsv)
if [ -n "$OWNER_UPN" ]; then
    azd env set OWNER_UPN "$OWNER_UPN"
    echo "Set OWNER_UPN to: $OWNER_UPN"
fi