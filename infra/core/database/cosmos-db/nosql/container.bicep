metadata description = 'Create an Azure Cosmos DB for NoSQL container.'

param name string
param tags object = {}

@description('Name of the parent Azure Cosmos DB account.')
param parentAccountName string

@description('Name of the parent Azure Cosmos DB database.')
param parentDatabaseName string

@description('Enables throughput setting at this resource level. Defaults to true.')
param setThroughput bool = false

@description('Enables autoscale. If setThroughput is enabled, defaults to false.')
param autoscale bool = false

@description('The amount of throughput set. If setThroughput is enabled, defaults to 400.')
param throughput int = 400

@description('List of hierarhical partition key paths. Defaults to an array that only contains /id.')
param partitionKeyPaths string[] = [
  '/id'
]

@description('Optional custom indexing policy for the container.')
param indexingPolicy object = {}

@description('Optional vector embedding policy for the container.')
param vectorEmbeddingPolicy object = {}

@description('Optional full text policy for the container.')
param fullTextPolicy object = {}

var options = setThroughput
  ? autoscale
      ? {
          autoscaleSettings: {
            maxThroughput: throughput
          }
        }
      : {
          throughput: throughput
        }
  : {}

resource account 'Microsoft.DocumentDB/databaseAccounts@2024-05-15' existing = {
  name: parentAccountName
}

resource database 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2024-05-15' existing = {
  name: parentDatabaseName
  parent: account
}

resource container 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-05-15' = {
  name: name
  parent: database
  tags: tags
  properties: {
    options: options
    resource: union(
      {
        id: name
        partitionKey: {
          paths: partitionKeyPaths
          kind: 'MultiHash'
          version: 2
        }
      },
      !empty(indexingPolicy)
        ? {
            indexingPolicy: indexingPolicy
          }
        : {},
      !empty(vectorEmbeddingPolicy)
        ? {
            vectorEmbeddingPolicy: vectorEmbeddingPolicy
          }
        : {},
      !empty(fullTextPolicy)
        ? {
            fullTextPolicy: fullTextPolicy
          }
        : {}
    )
  }
}

output name string = container.name
