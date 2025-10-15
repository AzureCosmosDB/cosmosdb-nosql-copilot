metadata description = 'Create AI accounts.'

param accountName string
param location string = resourceGroup().location
param tags object = {}
@secure()
param completionModelName string
param completionsDeploymentName string
param embeddingsModelName string
param embeddingsDeploymentName string

var deployments = [
  {
    name: completionsDeploymentName
    skuCapacity: 10
    modelName: completionModelName
    modelVersion: '2024-05-13'
  }
  {
    name: embeddingsDeploymentName
    skuCapacity: 5
    modelName: embeddingsModelName
    modelVersion: '1'
  }
]

module openAiAccount '../core/ai/cognitive-services/account.bicep' = {
  name: 'openai-account'
  params: {
    name: accountName
    location: location
    tags: tags
    kind: 'OpenAI'
    sku: 'S0'
    enablePublicNetworkAccess: true
  }
}

@batchSize(1)
module openAiModelDeployments '../core/ai/cognitive-services/deployment.bicep' = [
  for (deployment, _) in deployments: {
    name: 'openai-model-deployment-${deployment.name}'
    params: {
      name: deployment.name
      parentAccountName: openAiAccount.outputs.name
      skuName: 'GlobalStandard'
      skuCapacity: deployment.skuCapacity
      modelName: deployment.modelName
      modelVersion: deployment.modelVersion
      modelFormat: 'OpenAI'
    }
  }
]

output name string = openAiAccount.outputs.name
output endpoint string = openAiAccount.outputs.endpoint
output deployments array = [
  for (_, index) in deployments: {
    name: openAiModelDeployments[index].outputs.name
  }
]

