using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Cosmos.Copilot.Models;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.SemanticKernel.Connectors.AzureCosmosDBNoSQL;
using Microsoft.Extensions.VectorData;
using Microsoft.Azure.Cosmos;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Cosmos.Copilot.Options;
using Azure.AI.Inference;
using OpenAI;
using OpenAI.Chat;

namespace Cosmos.Copilot.Services;

/// <summary>
/// Semantic Kernel implementation for Azure OpenAI.
/// </summary>
public class SemanticKernelService
{
    //Semantic Kernel
    readonly Kernel kernel;
    #pragma warning disable SKEXP0020
    private readonly AzureCosmosDBNoSQLVectorStoreRecordCollection<Product> _productContainer;
    #pragma warning restore SKEXP0020
    private readonly string _productDataSourceURI;


    /// <summary>
    /// System prompt to send with user prompts to instruct the model for chat session
    /// </summary>
    private readonly string _systemPrompt = @"
        You are an AI assistant that helps people find information.
        Provide concise answers that are polite and professional.";

    /// <summary>
    /// System prompt to send with user prompts as a Retail AI Assistant for chat session
    /// </summary>
    private readonly string _systemPromptRetailAssistant = @"
        You are an intelligent assistant for the Cosmic Works Bike Company. 
        You are designed to provide helpful answers to user questions about 
        bike products and accessories provided in JSON format below.

        Instructions:
        - Only answer questions related to the information provided below,
        - Don't reference any product data not provided below.
        - If you're unsure of an answer, you can say ""I don't know"" or ""I'm not sure"" and recommend users search themselves.

        Text of relevant information:";

    /// <summary>    
    /// System prompt to send with user prompts to instruct the model for summarization
    /// </summary>
    private readonly string _summarizePrompt = @"
        Summarize this text. One to three words maximum length. 
        Plain text only. No punctuation, markup or tags.";

    /// <summary>
    /// Creates a new instance of the Semantic Kernel.
    /// </summary>
    /// <param name="skOptions">Options.</param>
    /// <exception cref="ArgumentNullException">Thrown when endpoint, key, or modelName is either null or empty.</exception>
    /// <remarks>
    /// This constructor will validate credentials and create a Semantic Kernel instance.
    /// </remarks>
    public SemanticKernelService(OpenAIClient openAiClient, CosmosClient cosmosClient, IOptions<OpenAi> openAIOptions, IOptions<CosmosDb> cosmosOptions)
    {
        var completionDeploymentName = openAIOptions.Value.CompletionDeploymentName;
        var embeddingDeploymentName = openAIOptions.Value.EmbeddingDeploymentName;

        var databaseName = cosmosOptions.Value.Database;
        var productContainerName = cosmosOptions.Value.ProductContainer;
        var productDataSourceURI = cosmosOptions.Value.ProductDataSourceURI;

        ArgumentNullException.ThrowIfNullOrEmpty(completionDeploymentName);
        ArgumentNullException.ThrowIfNullOrEmpty(embeddingDeploymentName);
        ArgumentNullException.ThrowIfNullOrEmpty(databaseName);
        ArgumentNullException.ThrowIfNullOrEmpty(productContainerName);

        // Initialize the Semantic Kernel
        var builder = Kernel.CreateBuilder();
        
        //Add Azure OpenAI chat completion service
        builder.AddOpenAIChatCompletion(completionDeploymentName, openAiClient);

        //Add Azure OpenAI text embedding generation service
        builder.AddOpenAITextEmbeddingGeneration(modelId: embeddingDeploymentName, openAIClient: openAiClient, dimensions: 1536);

        //Add Azure CosmosDB NoSql Vector Store
        builder.Services.AddSingleton<Database>(
            sp =>
            {
                var client = cosmosClient;
                return client.GetDatabase(databaseName);
            });
        #pragma warning disable SKEXP0020
        var options = new AzureCosmosDBNoSQLVectorStoreRecordCollectionOptions<Product>{ PartitionKeyPropertyName = "categoryId" };
        builder.AddAzureCosmosDBNoSQLVectorStoreRecordCollection<Product>(productContainerName, options);
        #pragma warning restore SKEXP0020
        kernel = builder.Build();

        _productDataSourceURI = productDataSourceURI;
        #pragma warning disable SKEXP0020
        _productContainer = (AzureCosmosDBNoSQLVectorStoreRecordCollection<Product>)kernel.Services.GetRequiredService<IVectorStoreRecordCollection<string, Product>>();
        #pragma warning restore SKEXP0020
    }

    /// <summary>
    /// Generates a completion using a user prompt with chat history to Semantic Kernel and returns the response.
    /// </summary>
    /// <param name="sessionId">Chat session identifier for the current conversation.</param>
    /// <param name="conversation">List of Message objects containign the context window (chat history) to send to the model.</param>
    /// <returns>Generated response along with tokens used to generate it.</returns>
    public async Task<(string completion, int tokens)> GetChatCompletionAsync(string sessionId, List<Message> contextWindow)
    {
        var skChatHistory = new ChatHistory();
        skChatHistory.AddSystemMessage(_systemPrompt);

        foreach (var message in contextWindow)
        {
            skChatHistory.AddUserMessage(message.Prompt);
            if (message.Completion != string.Empty)
                skChatHistory.AddAssistantMessage(message.Completion);
        }

        PromptExecutionSettings settings = new()
        {
            ExtensionData = new Dictionary<string, object>()
            {
                { "Temperature", 0.2 },
                { "TopP", 0.7 },
                { "MaxTokens", 1000  }
            }
        };


        var result = await kernel.GetRequiredService<IChatCompletionService>().GetChatMessageContentAsync(skChatHistory, settings);

        CompletionsUsage completionUsage = (CompletionsUsage)result.Metadata!["Usage"]!;

        string completion = result.Items[0].ToString()!;
        int tokens = completionUsage.CompletionTokens;

        return (completion, tokens);
    }

    /// <summary>
    /// Generates a completion using a user prompt with chat history and vector search results to Semantic Kernel and returns the response.
    /// </summary>
    /// <param name="sessionId">Chat session identifier for the current conversation.</param>
    /// <param name="contextWindow">List of Message objects containing the context window (chat history) to send to the model.</param>
    /// <param name="products">List of Product objects containing vector search results to send to the model.</param>
    /// <returns>Generated response along with tokens used to generate it.</returns>
    public async Task<(string completion, int tokens)> GetRagCompletionAsync(string sessionId, List<Message> contextWindow, string promptText, int productMaxResults)
    {
        float[] promptVectors = await GetEmbeddingsAsync(promptText);
        string productsString = await SearchProductsAsync(promptVectors, productMaxResults);

        var skChatHistory = new ChatHistory();
        skChatHistory.AddSystemMessage(_systemPromptRetailAssistant + productsString);
        

        foreach (var message in contextWindow)
        {
            skChatHistory.AddUserMessage(message.Prompt);
            if (message.Completion != string.Empty)
                skChatHistory.AddAssistantMessage(message.Completion);
        }

        PromptExecutionSettings settings = new()
        {
            ExtensionData = new Dictionary<string, object>()
            {
                { "Temperature", 0.2 },
                { "TopP", 0.7 },
                { "MaxTokens", 1000  }
            }
        };


        var result = await kernel.GetRequiredService<IChatCompletionService>().GetChatMessageContentAsync(skChatHistory, settings);

        ChatTokenUsage completionUsage = (ChatTokenUsage)result.Metadata!["Usage"]!;

        string completion = result.Items[0].ToString()!;
        int tokens = completionUsage.TotalTokenCount;

        return (completion, tokens);
    }


    /// <summary>
    /// Generates embeddings from the deployed OpenAI embeddings model using Semantic Kernel.
    /// </summary>
    /// <param name="input">Text to send to OpenAI.</param>
    /// <returns>Array of vectors from the OpenAI embedding model deployment.</returns>
    public async Task<float[]> GetEmbeddingsAsync(string text)
    {
        var embeddings = await kernel.GetRequiredService<ITextEmbeddingGenerationService>().GenerateEmbeddingAsync(text);

        float[] embeddingsArray = embeddings.ToArray();

        return embeddingsArray;
    }

    /// <summary>
    /// Sends the existing conversation to the Semantic Kernel and returns a two word summary.
    /// </summary>
    /// <param name="sessionId">Chat session identifier for the current conversation.</param>
    /// <param name="conversationText">conversation history to send to Semantic Kernel.</param>
    /// <returns>Summarization response from the OpenAI completion model deployment.</returns>
    public async Task<string> SummarizeConversationAsync(string conversation)
    {
        //return await summarizePlugin.SummarizeConversationAsync(conversation, kernel);

        var skChatHistory = new ChatHistory();
        skChatHistory.AddSystemMessage(_summarizePrompt);
        skChatHistory.AddUserMessage(conversation);

        PromptExecutionSettings settings = new()
        {
            ExtensionData = new Dictionary<string, object>()
            {
                { "Temperature", 0.0 },
                { "TopP", 1.0 },
                { "MaxTokens", 100 }
            }
        };


        var result = await kernel.GetRequiredService<IChatCompletionService>().GetChatMessageContentAsync(skChatHistory, settings);

        string completion = result.Items[0].ToString()!;

        return completion;
    }

    public async Task<string> SearchProductsAsync(ReadOnlyMemory<float> promptVectors, int productMaxResults)
    {
        var options = new VectorSearchOptions { VectorPropertyName = "vectors", Top = productMaxResults };
        #pragma warning disable SKEXP0020
        var searchResult = await _productContainer.VectorizedSearchAsync(promptVectors, options);
        #pragma warning restore SKEXP0020
        var resultRecords = new List<VectorSearchResult<Product>>();
        await foreach (var result in searchResult.Results)
        {
            resultRecords.Add(result);
        }


        //Serialize List<Product> to a JSON string to send to OpenAI
        string productsString = JsonSerializer.Serialize(resultRecords);
        return productsString;
    }

    public async Task LoadProductDataAsync()
    {
        //Read the product container to see if there are any items
        Product? item = null;
        try {
            #pragma warning disable SKEXP0020
            var compositeKey = new AzureCosmosDBNoSQLCompositeKey(recordKey: "d4e4f47b-fcd1-4cb0-84fc-db0948d26e9a", partitionKey: "598aede4-8b86-466b-ba48-3038a9a3b5fc");
            item = await _productContainer.GetAsync(compositeKey);
            #pragma warning restore SKEXP0020
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        { }

        if (item is null)
        {
            string json = "";
            string jsonFilePath = _productDataSourceURI;
            HttpClient client = new HttpClient();
            HttpResponseMessage response = await client.GetAsync(jsonFilePath);
            if(response.IsSuccessStatusCode)
            {
                json = await response.Content.ReadAsStringAsync();
            }
            List<Product> products = JsonSerializer.Deserialize<List<Product>>(json)!;

            foreach (var product in products)
            {
                try {
                    await InsertProductAsync(product);
                }
                catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
                {
                    Console.WriteLine($"Error: {ex.Message}, Product Name: {product.name}");
                }
            }
        }
    }

    /// <summary>
    /// Upserts a new product.
    /// </summary>
    /// <param name="product">Product item to create or update.</param>
    /// <returns>Id of the newly created product item.</returns>
    public async Task<string> InsertProductAsync(Product product)
    {
        // PartitionKey partitionKey = new(product.categoryId);
        // return await _productContainer.CreateItemAsync<Product>(
        //     item: product,
        //     partitionKey: partitionKey
        // );
        #pragma warning disable SKEXP0020
        return await _productContainer.UpsertAsync(product);
        #pragma warning restore SKEXP0020
    }

    /// <summary>
    /// Delete a product.
    /// </summary>
    /// <param name="product">Product item to delete.</param>
    public async Task DeleteProductAsync(Product product)
    {
        // PartitionKey partitionKey = new(product.categoryId);
        // await _productContainer.DeleteItemAsync<Product>(
        //     id: product.id,
        //     partitionKey: partitionKey
        // );

        #pragma warning disable SKEXP0020
        var compositeKey = new AzureCosmosDBNoSQLCompositeKey(recordKey: product.id, partitionKey: product.categoryId);
        await _productContainer.DeleteAsync(compositeKey);
        #pragma warning restore SKEXP0020
        
    }
}
