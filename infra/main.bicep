targetScope = 'subscription'

@minLength(1)
@maxLength(64)
@description('Name of the environment that can be used as part of naming resource convention.')
param environmentName string

@minLength(1)
@allowed([
  'canadaeast'
  'eastus'
  'eastus2'
  'francecentral'
  'japaneast'
  'norwayeast'
  'polandcentral'
  'southindia'
  'swedencentral'
  'switzerlandnorth'
  'westus3'
])
@description('Primary location for all resources.')
param location string

@description('Id of the principal to assign database and application roles.')
param principalId string = ''

@description('UPN (User Principal Name) of the owner for resource tagging.')
param ownerUpn string = ''

// Optional parameters
param openAiAccountName string = ''
param cosmosDbAccountName string = ''
param userAssignedIdentityName string = ''
param appServicePlanName string = ''
param appServiceWebAppName string = ''

// serviceName is used as value for the tag (azd-service-name) azd uses to identify deployment host
param serviceName string = 'web'

var abbreviations = loadJsonContent('abbreviations.json')
var resourceToken = toLower(uniqueString(subscription().id, environmentName, location))
var tags = union(
  {
    'azd-env-name': environmentName
    repo: 'https://github.com/AzureCosmosDB/cosmosdb-nosql-copilot'
  },
  !empty(ownerUpn) ? { owner: ownerUpn } : {}
)

var chatSettings = {
  maxContextWindow: '3'
  cacheSimilarityScore: '0.95'
  productMaxResults: '10'
}

var openAiSettings = {
  completionModelName: 'gpt-4o'
  completionDeploymentName: 'gpt-4o'
  embeddingModelName: 'text-embedding-3-large'
  embeddingDeploymentName: 'text-embedding-3-large'
  maxRagTokens: '1500'
  maxContextTokens: '500'
}

var productDataSourceUri = 'https://cosmosdbcosmicworks.blob.core.windows.net/cosmic-works-vectorized/product-text-3-large-1536-llm-gen-2.json'

var principalType = 'User'

resource resourceGroup 'Microsoft.Resources/resourceGroups@2022-09-01' = {
  name: environmentName
  location: location
  tags: tags
}

module identity 'app/identity.bicep' = {
  name: 'identity'
  scope: resourceGroup
  params: {
    identityName: !empty(userAssignedIdentityName) ? userAssignedIdentityName : '${abbreviations.userAssignedIdentity}-${resourceToken}'
    location: location
    tags: tags
  }
}

module ai 'app/ai.bicep' = {
  name: 'ai'
  scope: resourceGroup
  params: {
    accountName: !empty(openAiAccountName) ? openAiAccountName : '${abbreviations.openAiAccount}-${resourceToken}'
    location: location
    completionModelName: openAiSettings.completionModelName
    completionsDeploymentName: openAiSettings.completionDeploymentName
    embeddingsModelName: openAiSettings.embeddingModelName
    embeddingsDeploymentName: openAiSettings.embeddingDeploymentName
    tags: tags
  }
}

module web 'app/web.bicep' = {
  name: 'web'
  scope: resourceGroup
  params: {
    appName: !empty(appServiceWebAppName) ? appServiceWebAppName : '${abbreviations.appServiceWebApp}-${resourceToken}'
    planName: !empty(appServicePlanName) ? appServicePlanName : '${abbreviations.appServicePlan}-${resourceToken}'
    databaseAccountEndpoint: database.outputs.endpoint
    openAiAccountEndpoint: ai.outputs.endpoint
    cosmosDbSettings: {
      database: database.outputs.database.name
      chatContainer: database.outputs.containers[0].name
      cacheContainer: database.outputs.containers[1].name
      productContainer: database.outputs.containers[2].name
      productDataSourceUri: productDataSourceUri
    }
    openAiSettings: {
      completionDeploymentName: ai.outputs.deployments[0].name
      embeddingDeploymentName: ai.outputs.deployments[1].name
      maxRagTokens: openAiSettings.maxRagTokens
      maxContextTokens: openAiSettings.maxContextTokens
    }
    chatSettings: {
      maxContextWindow: chatSettings.maxContextWindow
      cacheSimilarityScore: chatSettings.cacheSimilarityScore
      productMaxResults: chatSettings.productMaxResults
    }
    userAssignedManagedIdentity: {
      resourceId: identity.outputs.resourceId
      clientId: identity.outputs.clientId
    }
    location: location
    tags: tags
    serviceTag: serviceName
  }
}

module database 'app/database.bicep' = {
  name: 'database'
  scope: resourceGroup
  params: {
    accountName: !empty(cosmosDbAccountName) ? cosmosDbAccountName : '${abbreviations.cosmosDbAccount}-${resourceToken}'
    location: location
    tags: tags
  }
}

module security 'app/security.bicep' = {
  name: 'security'
  scope: resourceGroup
  params: {
    databaseAccountName: database.outputs.accountName
    appPrincipalId: identity.outputs.principalId
    userPrincipalId: !empty(principalId) ? principalId : null
    principalType: principalType
  }
}

// Database outputs
output AZURE_COSMOS_DB_ENDPOINT string = database.outputs.endpoint
output AZURE_COSMOS_DB_DATABASE_NAME string = database.outputs.database.name
output AZURE_COSMOS_DB_CHAT_CONTAINER_NAME string = database.outputs.containers[0].name
output AZURE_COSMOS_DB_CACHE_CONTAINER_NAME string = database.outputs.containers[1].name
output AZURE_COSMOS_DB_PRODUCT_CONTAINER_NAME string = database.outputs.containers[2].name
output AZURE_COSMOS_DB_PRODUCT_DATA_SOURCE_URI string = productDataSourceUri

// AI outputs
output AZURE_OPENAI_ACCOUNT_ENDPOINT string = ai.outputs.endpoint
output AZURE_OPENAI_COMPLETION_DEPLOYMENT_NAME string = ai.outputs.deployments[0].name
output AZURE_OPENAI_EMBEDDING_DEPLOYMENT_NAME string = ai.outputs.deployments[1].name
output AZURE_OPENAI_MAX_RAG_TOKENS string = openAiSettings.maxRagTokens
output AZURE_OPENAI_MAX_CONTEXT_TOKENS string = openAiSettings.maxContextTokens

// Chat outputs
output AZURE_CHAT_MAX_CONTEXT_WINDOW string = chatSettings.maxContextWindow
output AZURE_CHAT_CACHE_SIMILARITY_SCORE string = chatSettings.cacheSimilarityScore
output AZURE_CHAT_PRODUCT_MAX_RESULTS string = chatSettings.productMaxResults
