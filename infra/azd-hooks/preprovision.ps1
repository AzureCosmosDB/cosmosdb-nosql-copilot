# Register any required resource providers
Register-AzResourceProvider -ProviderNamespace Microsoft.AlertsManagement

# Get the current user's UPN and set it as an azd environment variable
$ownerUpn = (az ad signed-in-user show --query userPrincipalName -o tsv)
if ($ownerUpn) {
    azd env set OWNER_UPN $ownerUpn
    Write-Host "Set OWNER_UPN to: $ownerUpn"
}