using Cosmos.Copilot.Models;
using Cosmos.Copilot.Options;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using Container = Microsoft.Azure.Cosmos.Container;
using PartitionKey = Microsoft.Azure.Cosmos.PartitionKey;

namespace Cosmos.Copilot.Services;

/// <summary>
/// Service to access Azure Cosmos DB for NoSQL.
/// </summary>
public class CosmosDbService
{
    private readonly Container _chatContainer;
    private readonly Container _cacheContainer;
    private readonly Container _productContainer;
    private readonly string _productDataSourceURI;

    /// <summary>
    /// Creates a new instance of the service.
    /// </summary>
    /// <param name="client">CosmosClient injected via DI.</param>
    /// <param name="cosmosOptions">Options.</param>
    /// <exception cref="ArgumentNullException">Thrown when endpoint, key, databaseName, cacheContainername or chatContainerName is either null or empty.</exception>
    /// <remarks>
    /// This constructor will validate credentials and create a service client instance.
    /// </remarks>
    public CosmosDbService(CosmosClient client, IOptions<CosmosDb> cosmosOptions)
    {
        var databaseName = cosmosOptions.Value.Database;
        var chatContainerName = cosmosOptions.Value.ChatContainer;
        var cacheContainerName = cosmosOptions.Value.CacheContainer;
        var productContainerName = cosmosOptions.Value.ProductContainer;
        var productDataSourceURI = cosmosOptions.Value.ProductDataSourceURI;

        ArgumentNullException.ThrowIfNullOrEmpty(databaseName);
        ArgumentNullException.ThrowIfNullOrEmpty(chatContainerName);
        ArgumentNullException.ThrowIfNullOrEmpty(cacheContainerName);
        ArgumentNullException.ThrowIfNullOrEmpty(productContainerName);
        ArgumentNullException.ThrowIfNullOrEmpty(productDataSourceURI);

        _productDataSourceURI = productDataSourceURI;

        Database database = client.GetDatabase(databaseName)!;
        Container chatContainer = database.GetContainer(chatContainerName)!;
        Container cacheContainer = database.GetContainer(cacheContainerName)!;
        Container productContainer = database.GetContainer(productContainerName)!;

        _chatContainer = chatContainer ??
            throw new ArgumentException("Unable to connect to existing Azure Cosmos DB container or database.");

        _cacheContainer = cacheContainer ??
            throw new ArgumentException("Unable to connect to existing Azure Cosmos DB container or database.");

        _productContainer = productContainer ??
            throw new ArgumentException("Unable to connect to existing Azure Cosmos DB container or database.");
    }

    /// <summary>
    /// Helper function to generate a full or partial hierarchical partition key based on parameters.
    /// </summary>
    /// <param name="tenantId">Id of Tenant.</param>
    /// <param name="userId">Id of User.</param>
    /// <param name="sessionId">Session Id of Chat/Session</param>
    /// <returns>Newly created chat session item.</returns>
    private static PartitionKey GetPK(string tenantId, string userId, string sessionId)
    {
        if (!string.IsNullOrEmpty(tenantId) && !string.IsNullOrEmpty(userId) && !string.IsNullOrEmpty(sessionId))
        {
            PartitionKey partitionKey = new PartitionKeyBuilder()
                .Add(tenantId)
                .Add(userId)
                .Add(sessionId)
                .Build();

            return partitionKey;
        }
        else if(!string.IsNullOrEmpty(tenantId) && !string.IsNullOrEmpty(userId))
        {

            PartitionKey partitionKey = new PartitionKeyBuilder()
                .Add(tenantId)
                .Add(userId)
                .Build();

            return partitionKey;

        }
        else 
        {
            PartitionKey partitionKey = new PartitionKeyBuilder()
            .Add(tenantId)
            .Build();

            return partitionKey;
        }
    }

    /// <summary>
    /// Creates a new chat session.
    /// </summary>
    /// <param name="tenantId">Id of Tenant.</param>
    /// <param name="userId">Id of User.</param>
    /// <param name="session">Chat session item to create.</param>
    /// <returns>Newly created chat session item.</returns>
    public async Task<Session> InsertSessionAsync(string tenantId, string userId, Session session)
    {
        PartitionKey partitionKey = GetPK(tenantId, userId,session.SessionId);
        return await _chatContainer.CreateItemAsync<Session>(
            item: session,
            partitionKey: partitionKey
        );
    }

    /// <summary>
    /// Creates a new chat message.
    /// </summary>
    /// <param name="tenantId">Id of Tenant.</param>
    /// <param name="userId">Id of User.</param>
    /// <param name="message">Chat message item to create.</param>
    /// <returns>Newly created chat message item.</returns>
    public async Task<Message> InsertMessageAsync(string tenantId, string userId, Message message)
    {
        PartitionKey partitionKey = GetPK(tenantId, userId, message.SessionId);
        Message newMessage = message with { TimeStamp = DateTime.UtcNow };
        return await _chatContainer.CreateItemAsync<Message>(
            item: message,
            partitionKey: partitionKey
        );
    }

    /// <summary>
    /// Gets a list of all current chat sessions.
    /// </summary>
    /// <param name="tenantId">Id of Tenant.</param>
    /// <param name="userId">Id of User.</param>
    /// <returns>List of distinct chat session items.</returns>
    public async Task<List<Session>> GetSessionsAsync(string tenantId, string userId)
    {
        PartitionKey partitionKey = GetPK(tenantId, userId, string.Empty);

        QueryDefinition query = new QueryDefinition("SELECT DISTINCT * FROM c WHERE c.type = @type")
            .WithParameter("@type", nameof(Session));

        FeedIterator<Session> response = _chatContainer.GetItemQueryIterator<Session>(query, null, new QueryRequestOptions() { PartitionKey = partitionKey });

        List<Session> output = new();
        while (response.HasMoreResults)
        {
            FeedResponse<Session> results = await response.ReadNextAsync();
            output.AddRange(results);
        }
        return output;
    }

    /// <summary>
    /// Gets a list of all current chat messages for a specified session identifier.
    /// </summary>
    /// <param name="tenantId">Id of Tenant.</param>
    /// <param name="userId">Id of User.</param>
    /// <param name="sessionId">Chat session identifier used to filter messsages.</param>
    /// <returns>List of chat message items for the specified session.</returns>
    public async Task<List<Message>> GetSessionMessagesAsync(string tenantId, string userId, string sessionId)
    {
        PartitionKey partitionKey = GetPK(tenantId, userId, sessionId);

        QueryDefinition query = new QueryDefinition("SELECT * FROM c WHERE c.sessionId = @sessionId AND c.type = @type")
            .WithParameter("@sessionId", sessionId)
        .WithParameter("@type", nameof(Message));

        FeedIterator<Message> results = _chatContainer.GetItemQueryIterator<Message>(query, null, new QueryRequestOptions() { PartitionKey = partitionKey });

        List<Message> output = new();
        while (results.HasMoreResults)
        {
            FeedResponse<Message> response = await results.ReadNextAsync();
            output.AddRange(response);
        }
        return output;
    }

    /// <summary>
    /// Updates an existing chat session.
    /// </summary>
    /// <param name="tenantId">Id of Tenant.</param>
    /// <param name="userId">Id of User.</param>
    /// <param name="session">Chat session item to update.</param>
    /// <returns>Revised created chat session item.</returns>
    public async Task<Session> UpdateSessionAsync(string tenantId, string userId, Session session)
    {
        PartitionKey partitionKey = GetPK(tenantId, userId, session.SessionId);
        return await _chatContainer.ReplaceItemAsync(
            item: session,
            id: session.Id,
            partitionKey: partitionKey
        );
    }

        /// <summary>
    /// Returns an existing chat session.
    /// </summary>
    /// <param name="tenantId">Id of Tenant.</param>
    /// <param name="userId">Id of User.</param>
    /// <param name="sessionId">Chat session id for the session to return.</param>
    /// <returns>Chat session item.</returns>
    public async Task<Session> GetSessionAsync(string tenantId, string userId, string sessionId)
    {
        PartitionKey partitionKey = GetPK(tenantId, userId, sessionId);
        return await _chatContainer.ReadItemAsync<Session>(
            partitionKey: partitionKey,
            id: sessionId
            );
    }

    /// <summary>
    /// Batch create chat message and update session.    
    /// </summary>
    /// <param name="tenantId">Id of Tenant.</param>
    /// <param name="userId">Id of User.</param>
    /// <param name="messages">Chat message and session items to create or replace.</param>
    public async Task UpsertSessionBatchAsync(string tenantId, string userId, params dynamic[] messages)
    {
        //Make sure items are all in the same partition
        if (messages.Select(m => m.SessionId).Distinct().Count() > 1)
        {
            throw new ArgumentException("All items must have the same partition key.");
        }

        PartitionKey partitionKey = GetPK(tenantId, userId, messages[0].SessionId);
        TransactionalBatch batch = _chatContainer.CreateTransactionalBatch(partitionKey);

        foreach (var message in messages)
        {
            batch.UpsertItem(item: message);
        }

        await batch.ExecuteAsync();
    }

    /// <summary>
    /// Batch deletes an existing chat session and all related messages.
    /// </summary>
    /// <param name="tenantId">Id of Tenant.</param>
    /// <param name="userId">Id of User.</param>
    /// <param name="sessionId">Chat session identifier used to flag messages and sessions for deletion.</param>
    public async Task DeleteSessionAndMessagesAsync(string tenantId, string userId, string sessionId)
    {
        PartitionKey partitionKey = GetPK(tenantId, userId, sessionId);

        QueryDefinition query = new QueryDefinition("SELECT VALUE c.id FROM c WHERE c.sessionId = @sessionId")
                .WithParameter("@sessionId", sessionId);

        FeedIterator<string> response = _chatContainer.GetItemQueryIterator<string>(query);

        TransactionalBatch batch = _chatContainer.CreateTransactionalBatch(partitionKey);
        while (response.HasMoreResults)
        {
            FeedResponse<string> results = await response.ReadNextAsync();
            foreach (var itemId in results)
            {
                batch.DeleteItem(
                    id: itemId
                );
            }
        }

        await batch.ExecuteAsync();
    }

    /// <summary>
    /// Find a cache item.
    /// Select Top 1 to get only get one result.
    /// OrderBy DESC to return the highest similarity score first.
    /// Use a subquery to get the similarity score so we can then use in a WHERE clause
    /// </summary>
    /// <param name="vectors">Vectors to do the semantic search in the cache.</param>
    /// <param name="similarityScore">Value to determine how similar the vectors. >0.99 is exact match.</param>
    public async Task<string> GetCacheAsync(float[] vectors, double similarityScore)
    {   
        string cacheResponse = "";

        string queryText = $"""
            SELECT Top 1 
                c.prompt, c.completion, VectorDistance(c.vectors, @vectors) as similarityScore
            FROM c  
            WHERE 
                VectorDistance(c.vectors, @vectors) > @similarityScore 
            ORDER BY 
                VectorDistance(c.vectors, @vectors)
            """;

        var queryDef = new QueryDefinition(
                query: queryText)
            .WithParameter("@vectors", vectors)
            .WithParameter("@similarityScore", similarityScore);

        using FeedIterator<CacheItem> resultSet = _cacheContainer.GetItemQueryIterator<CacheItem>(queryDefinition: queryDef);

        while (resultSet.HasMoreResults)
        {
            FeedResponse<CacheItem> response = await resultSet.ReadNextAsync();

            foreach (CacheItem item in response)
            {
                cacheResponse = item.Completion;
                return cacheResponse;
            }
        }

        return cacheResponse;
    }

    /// <summary>
    /// Add a new cache item.
    /// </summary>
    /// <param name="vectors">Vectors used to perform the semantic search.</param>
    /// <param name="prompt">Text value of the vectors in the search.</param>
    /// <param name="completion">Text value of the previously generated response to return to the user.</param>
    public async Task CachePutAsync(CacheItem cacheItem)
    {
        await _cacheContainer.UpsertItemAsync<CacheItem>(item: cacheItem);
    }

    /// <summary>
    /// Remove a cache item using its vectors.
    /// </summary>
    /// <param name="vectors">Vectors used to perform the semantic search. Similarity Score is set to 0.99 for exact match</param>
    public async Task CacheRemoveAsync(float[] vectors)
    {
        double similarityScore = 0.99;

        string queryText = $"""
            SELECT Top 1 c.id
            FROM c  
            WHERE VectorDistance(c.vectors, @vectors) > @similarityScore 
            ORDER BY VectorDistance(c.vectors, @vectors)
            """;

        var queryDef = new QueryDefinition(
             query: queryText)
            .WithParameter("@vectors", vectors)
            .WithParameter("@similarityScore", similarityScore);

        using FeedIterator<CacheItem> resultSet = _cacheContainer.GetItemQueryIterator<CacheItem>(queryDefinition: queryDef);

        while (resultSet.HasMoreResults)
        {
            FeedResponse<CacheItem> response = await resultSet.ReadNextAsync();

            foreach (CacheItem item in response)
            {
                await _cacheContainer.DeleteItemAsync<CacheItem>(partitionKey: new PartitionKey(item.Id), id: item.Id);
                return;
            }
        }
    }

    /// <summary>
    /// Clear the cache of all cache items.
    /// </summary>
    public async Task CacheClearAsync()
    {
        string queryText = "SELECT c.id FROM c";

        var queryDef = new QueryDefinition(query: queryText);

        using FeedIterator<CacheItem> resultSet = _cacheContainer.GetItemQueryIterator<CacheItem>(queryDefinition: queryDef);

        while (resultSet.HasMoreResults)
        {
            FeedResponse<CacheItem> response = await resultSet.ReadNextAsync();

            foreach (CacheItem item in response)
            {
                await _cacheContainer.DeleteItemAsync<CacheItem>(partitionKey: new PartitionKey(item.Id), id: item.Id);
            }
        }
    }
}