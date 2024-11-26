metadata description = 'Create database accounts.'

param accountName string
param location string = resourceGroup().location
param tags object = {}

var database = {
  name: 'cosmoscopilotdb' // Database for application
}

var containers = [
  {
    name: 'chat' // Container for chat sessions and messages
    partitionKeyPaths: [
      '/tenantId'  // Partition on the tenant identifier, l1 of HPK
      '/userId'    // Partition on the user identifier, l2 of HPK
      '/sessionId' // Partition on the session identifier, l3 of HPK
    ]
    indexingPolicy: {
      automatic: true
      indexingMode: 'consistent'
      includedPaths: [
        {
          path: '/tenantId/?'
        }
        {
          path: '/userId/?'
        }
        {
          path: '/sessionId/?'
        }
        {
          path: '/timeStamp/?'
        }
      ]
      excludedPaths: [
        {
          path: '/*'
        }
      ]
    }
    vectorEmbeddingPolicy: {
      vectorEmbeddings: []
    }
    fullTextPolicy: {
      fullTextPaths: []
    }
  }
  {
    name: 'cache' // Container for cached messages
    partitionKeyPaths: [
      '/id' // Partition on cache identifier
    ]
    indexingPolicy: {
      automatic: true
      indexingMode: 'consistent'
      includedPaths: [
        {
          path: '/*'
        }
      ]
      excludedPaths: [
        {
          path: '/vectors/?'
        }
      ]
      vectorIndexes: [
        {
          path: '/vectors'
          type: 'diskANN'
        }
      ]
    }
    vectorEmbeddingPolicy: {
      vectorEmbeddings: [
        {
          path: '/vectors'
          dataType: 'float32'
          dimensions: 1536
          distanceFunction: 'cosine'
        }
      ]
    }
    fullTextPolicy: {
      fullTextPaths: []
    }
  }
  {
    name: 'products' // Container for products
    partitionKeyPaths: [
      '/categoryId' // Partition for product data
    ]
    indexingPolicy: {
      automatic: true
      indexingMode: 'consistent'
      includedPaths: [
        {
          path: '/*'
        }
      ]
      //excludedPaths: [{}]
      vectorIndexes: [
        {
          path: '/vectors'
          type: 'diskANN'
        }
      ]
      fullTextIndexes: [
        {
          path: '/tags'
        }
        {
          path: '/description'
        }
      ]
    }
    vectorEmbeddingPolicy: {
      vectorEmbeddings: [
        {
          path: '/vectors'
          dataType: 'float32'
          dimensions: 1536
          distanceFunction: 'cosine'
        }
      ]
    }
    fullTextPolicy: {
      defaultLanguage: 'en-US'
      fullTextPaths: [
        {
          path: '/tags'
          language: 'en-US'
        }
        {
          path: '/description'
          language: 'en-US'
        }
      ]
    }
  }
]

module cosmosDbAccount '../core/database/cosmos-db/nosql/account.bicep' = {
  name: 'cosmos-db-account'
  params: {
    name: accountName
    location: location
    tags: tags
    enableServerless: true
    enableVectorSearch: true
    enableNoSQLFullTextSearch: true
    disableKeyBasedAuth: true
  }
}

module cosmosDbDatabase '../core/database/cosmos-db/nosql/database.bicep' = {
  name: 'cosmos-db-database-${database.name}'
  params: {
    name: database.name
    parentAccountName: cosmosDbAccount.outputs.name
    tags: tags
    setThroughput: false
  }
}

module cosmosDbContainers '../core/database/cosmos-db/nosql/container.bicep' = [
  for (container, _) in containers: {
    name: 'cosmos-db-container-${container.name}'
    params: {
      name: container.name
      parentAccountName: cosmosDbAccount.outputs.name
      parentDatabaseName: cosmosDbDatabase.outputs.name
      tags: tags
      setThroughput: false
      partitionKeyPaths: container.partitionKeyPaths
      indexingPolicy: container.indexingPolicy
      vectorEmbeddingPolicy: container.vectorEmbeddingPolicy
      fullTextPolicy: container.fullTextPolicy
    }
  }
]

output endpoint string = cosmosDbAccount.outputs.endpoint
output accountName string = cosmosDbAccount.outputs.name

output database object = {
  name: cosmosDbDatabase.outputs.name
}
output containers array = [
  for (_, index) in containers: {
    name: cosmosDbContainers[index].outputs.name
  }
]
