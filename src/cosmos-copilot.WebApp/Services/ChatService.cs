using Cosmos.Copilot.Models;
using Cosmos.Copilot.Options;
using Microsoft.Extensions.Options;
using Microsoft.ML.Tokenizers;

namespace Cosmos.Copilot.Services;

public class ChatService
{

    private readonly CosmosDbService _cosmosDbService;
    private readonly SemanticKernelService _semanticKernelService;
    private readonly int _maxContextWindow;
    private readonly double _cacheSimilarityScore;
    private readonly int _productMaxResults;

    private readonly Tokenizer _tokenizer;

    public ChatService(CosmosDbService cosmosDbService, SemanticKernelService semanticKernelService, IOptions<Chat> chatOptions)
    {
        _cosmosDbService = cosmosDbService;
        _semanticKernelService = semanticKernelService;

        var maxContextWindow = chatOptions.Value.MaxContexWindow;
        var cacheSimilarityScore = chatOptions.Value.CacheSimilarityScore;
        var productMaxResults = chatOptions.Value.ProductMaxResults;

        _maxContextWindow = Int32.TryParse(maxContextWindow, out _maxContextWindow) ? _maxContextWindow : 3;
        _cacheSimilarityScore = Double.TryParse(cacheSimilarityScore, out _cacheSimilarityScore) ? _cacheSimilarityScore : 0.99;
        _productMaxResults = Int32.TryParse(productMaxResults, out _productMaxResults) ? _productMaxResults: 10;

        _tokenizer = Tokenizer.CreateTiktokenForModel("gpt-4o");
    }

    /// <summary>
    /// Get a completion for a user prompt from Azure OpenAi Service
    /// This is the main LLM Workflow for the Chat Service
    /// </summary>
    public async Task<Message> GetChatCompletionAsync(string tenantId, string userId, string sessionId, string promptText)
    {

        //Create a message object for the new User Prompt and calculate the tokens for the prompt
        Message chatMessage = await CreateChatMessageAsync(tenantId, userId, sessionId, promptText);
        
        //Grab context window from the conversation history up to the maximum conversation depth
        List<Message> contextWindow = await GetChatSessionContextWindow(tenantId, userId, sessionId);

        //Grab the user prompts for the context window
        string prompts = string.Join(Environment.NewLine, contextWindow.Select(m => m.Prompt));

        //Get the embeddings for the user prompts
        float[] promptVectors = await _semanticKernelService.GetEmbeddingsAsync(prompts);

        //Perform a cache search to see if this prompt has already been used in the same sequence and depth as this conversation
        string cacheResponse = await GetCacheAsync(promptVectors);

        //Cache hit, return the cached completion
        if (!string.IsNullOrEmpty(cacheResponse))
        {
            chatMessage.CacheHit = true;
            chatMessage.Completion = cacheResponse;

            //Persist the prompt/completion, elapsed time, update the session tokens
            await UpdateSessionAndMessage(tenantId, userId, sessionId, chatMessage);

            return chatMessage;
        }

        //RAG Pattern Vector search results for product data
        string vectorSearchResults = await _semanticKernelService.SearchProductsAsync(promptVectors, _productMaxResults);

        //Call Semantic Kernel to do a vector search generate a new completion
        (chatMessage.Completion, chatMessage.GenerationTokens, chatMessage.CompletionTokens) = await _semanticKernelService.GetRagCompletionAsync(contextWindow, vectorSearchResults);


        //Cache the prompts in the current context window and their vectors with the generated completion
        await CachePutAsync(prompts, promptVectors, chatMessage.Completion);

        //Persist the prompt/completion, elapsed time, update the session tokens
        await UpdateSessionAndMessage(tenantId, userId, sessionId, chatMessage);

        return chatMessage;
    }

    /// <summary>
    /// Get the context window for this conversation. This is used in cache search as well as generating completions
    /// </summary>
    private async Task<List<Message>> GetChatSessionContextWindow(string tenantId, string userId, string sessionId)
    {
        
        List<Message> allMessages = await _cosmosDbService.GetSessionMessagesAsync(tenantId, userId, sessionId);
 
        //Build the contextWindow from allMessages, start from the end and work backwards
        //This includes the latest user prompt which is already cached
        
        return allMessages.TakeLast(_maxContextWindow).ToList();

    }

    /// <summary>
    /// Query the semantic cache with user prompt vectors for the current context window in this conversation
    /// </summary>
    private async Task<string> GetCacheAsync(float[] vectors)
    {

        //Check the cache for similar vectors
        return await _cosmosDbService.GetCacheAsync(vectors, _cacheSimilarityScore);

    }

    /// <summary>
    /// Cache the last generated completion with user prompt vectors for the current context window in this conversation
    /// </summary>
    private async Task CachePutAsync(string cachePrompts, float[] cacheVectors, string generatedCompletion)
    {
        //Include the user prompts text to view. They are not used in the cache search.
        CacheItem cacheItem = new(cacheVectors, cachePrompts, generatedCompletion);

        //Put the prompts, vectors and completion into the cache
        await _cosmosDbService.CachePutAsync(cacheItem);
    }

    /// <summary>
    /// Use OpenAI to summarize the conversation to give it a relevant name on the web page
    /// </summary>
    public async Task<string> SummarizeChatSessionNameAsync(string tenantId, string userId, string sessionId)
    {

        //Get the messages for the session
        List<Message> messages = await _cosmosDbService.GetSessionMessagesAsync( tenantId,  userId, sessionId);

        //Create a conversation string from the messages
        string conversationText = string.Join(" ", messages.Select(m => m.Prompt + " " + m.Completion));

        //Send to OpenAI to summarize the conversation
        string completionText = await _semanticKernelService.SummarizeConversationAsync(conversationText);

        await RenameChatSessionAsync( tenantId,  userId, sessionId, completionText);

        return completionText;
    }

    /// <summary>
    /// Add user prompt to a new chat session message object, calculate token count for prompt text.
    /// </summary>
    private async Task<Message> CreateChatMessageAsync(string tenantId, string userId, string sessionId, string promptText)
    {
        
        //Calculate tokens for the user prompt message.
        int promptTokens = _tokenizer.CountTokens(promptText);

        //Create a new message object.
        Message chatMessage = new(tenantId, userId, sessionId, promptTokens, promptText);

        await _cosmosDbService.InsertMessageAsync(tenantId, userId, chatMessage);

        return chatMessage;
    }

    /// <summary>
    /// Update session with user prompt and completion tokens and update the cache
    /// </summary>
    private async Task UpdateSessionAndMessage(string tenantId, string userId, string sessionId, Message chatMessage)
    {
        
        //Stop the stopwatch and calculate the elapsed time
        chatMessage.CalculateElapsedTime();

        //Update the tokens used in the session
        Session session = await _cosmosDbService.GetSessionAsync(tenantId, userId, sessionId);

        //Update the session tokens based upon Completion + Generation tokens. These combined is the cost of the request to OpenAI
        session.Tokens += chatMessage.CompletionTokens + chatMessage.GenerationTokens;

        //Insert new message and Update session in a transaction
        await _cosmosDbService.UpsertSessionBatchAsync(tenantId,  userId, session, chatMessage);

    }

    /// <summary>
    /// Calculate the number of tokens from the user prompt
    /// </summary>
    private int GetTokens(string userPrompt)
    {
        Tokenizer _tokenizer = Tokenizer.CreateTiktokenForModel("gpt-3.5-turbo");

        return _tokenizer.CountTokens(userPrompt);

    }

    /// <summary>
    /// Clear the Semantic Cache
    /// </summary>
    public async Task ClearCacheAsync()
    {
        await _cosmosDbService.CacheClearAsync();
    }

    public async Task InitializeAsync()
    {
        await _semanticKernelService.LoadProductDataAsync();
    }

    /// <summary>
    /// Returns list of chat session ids and names for left-hand nav to bind to (display Name and ChatSessionId as hidden)
    /// </summary>
    public async Task<List<Session>> GetAllChatSessionsAsync(string tenantId, string userId)
    {
        return await _cosmosDbService.GetSessionsAsync(tenantId, userId);
    }

    /// <summary>
    /// Returns the chat messages to display on the main web page when the user selects a chat from the left-hand nav
    /// </summary>
    public async Task<List<Message>> GetChatSessionMessagesAsync(string tenantId, string userId, string sessionId)
    {

        return await _cosmosDbService.GetSessionMessagesAsync(tenantId, userId, sessionId);
    }

    /// <summary>
    /// User creates a new Chat Session.
    /// </summary>
    public async Task<Session> CreateNewChatSessionAsync(string tenantId, string userId)
    {

        Session session = new(tenantId, userId);

        await _cosmosDbService.InsertSessionAsync(tenantId, userId, session);

        return session;

    }

    /// <summary>
    /// Rename the Chat Session from "New Chat" to the summary provided by OpenAI
    /// </summary>
    public async Task RenameChatSessionAsync(string tenantId, string userId, string sessionId, string newChatSessionName)
    {

        Session session = await _cosmosDbService.GetSessionAsync(tenantId, userId, sessionId);

        session.Name = newChatSessionName;

        await _cosmosDbService.UpdateSessionAsync(tenantId, userId, session);
    }

    /// <summary>
    /// User deletes a chat session
    /// </summary>
    public async Task DeleteChatSessionAsync(string tenantId, string userId, string sessionId)
    {

        await _cosmosDbService.DeleteSessionAndMessagesAsync(tenantId, userId, sessionId);
    }
}
