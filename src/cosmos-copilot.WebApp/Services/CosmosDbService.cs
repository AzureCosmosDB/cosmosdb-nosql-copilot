﻿using System.Text.Json;
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

        _chatContainer =
            chatContainer
            ?? throw new ArgumentException(
                "Unable to connect to existing Azure Cosmos DB container or database."
            );

        _cacheContainer =
            cacheContainer
            ?? throw new ArgumentException(
                "Unable to connect to existing Azure Cosmos DB container or database."
            );

        _productContainer =
            productContainer
            ?? throw new ArgumentException(
                "Unable to connect to existing Azure Cosmos DB container or database."
            );
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
        if (
            !string.IsNullOrEmpty(tenantId)
            && !string.IsNullOrEmpty(userId)
            && !string.IsNullOrEmpty(sessionId)
        )
        {
            PartitionKey partitionKey = new PartitionKeyBuilder()
                .Add(tenantId)
                .Add(userId)
                .Add(sessionId)
                .Build();

            return partitionKey;
        }
        else if (!string.IsNullOrEmpty(tenantId) && !string.IsNullOrEmpty(userId))
        {
            PartitionKey partitionKey = new PartitionKeyBuilder().Add(tenantId).Add(userId).Build();

            return partitionKey;
        }
        else
        {
            PartitionKey partitionKey = new PartitionKeyBuilder().Add(tenantId).Build();

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
        PartitionKey partitionKey = GetPK(tenantId, userId, session.SessionId);
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

        QueryDefinition query = new QueryDefinition(
            "SELECT DISTINCT * FROM c WHERE c.type = @type"
        ).WithParameter("@type", nameof(Session));

        FeedIterator<Session> response = _chatContainer.GetItemQueryIterator<Session>(
            query,
            null,
            new QueryRequestOptions() { PartitionKey = partitionKey }
        );

        List<Session> output = new();
        while (response.HasMoreResults)
        {
            FeedResponse<Session> results = await response.ReadNextAsync();
            output.AddRange(results);
        }
        return output;
    }

    /// <summary>
    /// Gets the current context window of chat messages for a specified session identifier.
    /// </summary>
    /// <param name="tenantId">Id of Tenant.</param>
    /// <param name="userId">Id of User.</param>
    /// <param name="sessionId">Chat session identifier used to filter messsages.</param>
    /// <returns>List of chat message items for the specified session.</returns>
    public async Task<List<Message>> GetSessionContextWindowAsync(
        string tenantId,
        string userId,
        string sessionId,
        int maxContextWindow
    )
    {
        PartitionKey partitionKey = GetPK(tenantId, userId, sessionId);

        //Select the last N messages in the context window
        //Using Top and Order By on the timestamp
        string queryText = $"""
            SELECT Top @maxContextWindow
                *
            FROM c  
            WHERE 
                c.tenantId = @tenantId AND 
                c.userId = @userId AND
                c.sessionId = @sessionId AND 
                c.type = @type
            ORDER BY 
                c.timeStamp DESC
            """;

        QueryDefinition query = new QueryDefinition(query: queryText)
            .WithParameter("@tenantId", tenantId)
            .WithParameter("@userId", userId)
            .WithParameter("@sessionId", sessionId)
            .WithParameter("@type", nameof(Message))
            .WithParameter("@maxContextWindow", maxContextWindow);

        FeedIterator<Message> results = _chatContainer.GetItemQueryIterator<Message>(
            query,
            null,
            new QueryRequestOptions() { PartitionKey = partitionKey }
        );

        List<Message> output = new();
        while (results.HasMoreResults)
        {
            FeedResponse<Message> response = await results.ReadNextAsync();
            output.AddRange(response);
        }

        //Reverse to put back into chronological order
        output.Reverse();

        return output;
    }

    /// <summary>
    /// Gets a list of all current chat messages for a specified session identifier.
    /// </summary>
    /// <param name="tenantId">Id of Tenant.</param>
    /// <param name="userId">Id of User.</param>
    /// <param name="sessionId">Chat session identifier used to filter messsages.</param>
    /// <returns>List of chat message items for the specified session.</returns>
    public async Task<List<Message>> GetSessionMessagesAsync(
        string tenantId,
        string userId,
        string sessionId
    )
    {
        PartitionKey partitionKey = GetPK(tenantId, userId, sessionId);

        QueryDefinition query = new QueryDefinition(
            "SELECT * FROM c WHERE c.sessionId = @sessionId AND c.type = @type"
        )
            .WithParameter("@sessionId", sessionId)
            .WithParameter("@type", nameof(Message));

        FeedIterator<Message> results = _chatContainer.GetItemQueryIterator<Message>(
            query,
            null,
            new QueryRequestOptions() { PartitionKey = partitionKey }
        );

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
    public async Task UpsertSessionBatchAsync(
        string tenantId,
        string userId,
        params dynamic[] messages
    )
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
    /// Deletes an existing chat session and all chat messages in the same partition key
    /// </summary>
    /// <param name="tenantId">Id of Tenant</param>
    /// <param name="userId">Id of User</param>
    /// <param name="sessionId">Chat session id for the session and all the chat messages in the partition.</param>
    public async Task DeleteSessionAndMessagesAsync(
        string tenantId,
        string userId,
        string sessionId
    )
    {
        PartitionKey partitionKey = GetPK(tenantId, userId, sessionId);

        await _chatContainer.DeleteAllItemsByPartitionKeyStreamAsync(partitionKey);
    }

    /// <summary>
    /// Performs full text search on the CosmosDB product container
    /// </summary>
    /// <param name="promptText">Text used to do the search</param>
    /// <param name="productMaxResults">Limit the number of returned items</param>
    /// <returns>List of returned products</returns>
    public async Task<List<Product>> FullTextSearchProductsAsync(string promptText, int productMaxResults) 
    {
        List<Product> results = new();

        string[] words = promptText.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string rankedWords = $"[{string.Join(", ", words.Select(word => $"'{word}'"))}]";

        string queryText = $"""
                SELECT
                    Top {productMaxResults} c.id, c.categoryId, c.categoryName, c.sku, c.name, c.description, c.price, c.tags
                FROM c
                WHERE 
                    FullTextContainsAny(c.description, {rankedWords}) OR
                    FullTextContainsAny(c.tags, {rankedWords})
            """;

        var queryDef = new QueryDefinition(query: queryText);
            //These are broken during early preview, pass in directly
            //.WithParameter("@maxResults", productMaxResults)
            //.WithParameter("@words", rankedWords);

        using FeedIterator<Product> resultSet = _productContainer.GetItemQueryIterator<Product>(
            queryDefinition: queryDef
        );

        while (resultSet.HasMoreResults)
        {
            FeedResponse<Product> response = await resultSet.ReadNextAsync();

            results.AddRange(response);
        }

        return results;
    }

    /// <summary>
    /// Performs hybrid search on the CosmosDB product container
    /// </summary>
    /// <param name="promptText">Text used to do the search</param>
    /// <param name="promptVectors">Vectors used to do the search</param>
    /// <param name="productMaxResults">Limit the number of returned items</param>
    /// <returns>List of returned products</returns>
    public async Task<List<Product>> HybridSearchProductsAsync(
        string promptText,
        float[] promptVectors,
        int productMaxResults
    )
    {
        List<Product> results = new();

        string[] words = promptText.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string rankedWords = $"[{string.Join(", ", words.Select(word => $"'{word}'"))}]";

        string queryText = $"""
                SELECT
                    Top {productMaxResults} c.id, c.categoryId, c.categoryName, c.sku, c.name, c.description, c.price, c.tags
                FROM c
                ORDER BY RANK RRF(
                    FullTextScore(c.description, {rankedWords}),
                    FullTextScore(c.tags, {rankedWords}),
                    VectorDistance(c.vectors, @vectors)
                    )
            """;

        var queryDef = new QueryDefinition(query: queryText)
            //These are broken during early preview, pass in directly
            //.WithParameter("@maxResults", productMaxResults)
            //.WithParameter("@rankedWords", rankedWords)
            .WithParameter("@vectors", promptVectors);

        using FeedIterator<Product> resultSet = _productContainer.GetItemQueryIterator<Product>(
            queryDefinition: queryDef
        );

        while (resultSet.HasMoreResults)
        {
            FeedResponse<Product> response = await resultSet.ReadNextAsync();

            results.AddRange(response);
        }

        return results;
    }

    /// <summary>
    /// Perform a vector search to find an item in the cache collection
    /// OrderBy returns the highest similary score first.
    /// Select Top 1 to get only get one result.
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

        var queryDef = new QueryDefinition(query: queryText)
            .WithParameter("@vectors", vectors)
            .WithParameter("@similarityScore", similarityScore);

        using FeedIterator<CacheItem> resultSet = _cacheContainer.GetItemQueryIterator<CacheItem>(
            queryDefinition: queryDef
        );

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
    /// Add a new item to the cache collection
    /// </summary>
    /// <param name="cacheItem">Item to add to the cache collection</param>
    public async Task CachePutAsync(CacheItem cacheItem)
    {
        await _cacheContainer.UpsertItemAsync<CacheItem>(item: cacheItem);
    }

    /// <summary>
    /// Remove a cache item using a vector search
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

        var queryDef = new QueryDefinition(query: queryText)
            .WithParameter("@vectors", vectors)
            .WithParameter("@similarityScore", similarityScore);

        using FeedIterator<CacheItem> resultSet = _cacheContainer.GetItemQueryIterator<CacheItem>(
            queryDefinition: queryDef
        );

        while (resultSet.HasMoreResults)
        {
            FeedResponse<CacheItem> response = await resultSet.ReadNextAsync();

            foreach (CacheItem item in response)
            {
                await _cacheContainer.DeleteItemAsync<CacheItem>(
                    partitionKey: new PartitionKey(item.Id),
                    id: item.Id
                );
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

        using FeedIterator<CacheItem> resultSet = _cacheContainer.GetItemQueryIterator<CacheItem>(
            queryDefinition: queryDef
        );

        while (resultSet.HasMoreResults)
        {
            FeedResponse<CacheItem> response = await resultSet.ReadNextAsync();

            foreach (CacheItem item in response)
            {
                await _cacheContainer.DeleteItemAsync<CacheItem>(
                    partitionKey: new PartitionKey(item.Id),
                    id: item.Id
                );
            }
        }
    }
}
