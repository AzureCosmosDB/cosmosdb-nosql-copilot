﻿using Azure.AI.Inference;
using Cosmos.Copilot.Models;
using Cosmos.Copilot.Options;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.VectorData;
using Microsoft.ML.Tokenizers;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureCosmosDBNoSQL;
using Microsoft.SemanticKernel.Embeddings;
using OpenAI;
using OpenAI.Chat;
using System.Text.Json;


namespace Cosmos.Copilot.Services;

/// <summary>
/// Semantic Kernel implementation for Azure OpenAI.
/// </summary>
public class SemanticKernelService
{
    //Semantic Kernel
    readonly Kernel kernel;

    private readonly AzureCosmosDBNoSQLVectorStoreRecordCollection<Product> _productContainer;
    private readonly string _productDataSourceURI;
    private readonly Tokenizer _tokenizer;
    private readonly int _maxRagTokens;
    private readonly int _maxContextTokens;


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
    /// <param name="openAiClient">OpenAIClient injected into the service for embeddings and completions</param>
    /// <param name="cosmosClient">CosmosClient injected into the service for product vector search</param>
    /// <param name="openAIOptions">values for embeddings and completions models</param>
    /// <param name="cosmosOptions">values for product data</param>
    /// <exception cref="ArgumentNullException">Thrown when any value is either null or empty.</exception>
    /// <remarks>
    /// This constructor will validate credentials and create a Semantic Kernel instance.
    /// </remarks>
    public SemanticKernelService(OpenAIClient openAiClient, CosmosClient cosmosClient, IOptions<OpenAi> openAIOptions, IOptions<CosmosDb> cosmosOptions)
    {

        var completionDeploymentName = openAIOptions.Value.CompletionDeploymentName;
        var embeddingDeploymentName = openAIOptions.Value.EmbeddingDeploymentName;
        var maxRagTokens = openAIOptions.Value.MaxRagTokens;
        var maxContextTokens = openAIOptions.Value.MaxContextTokens;

        var databaseName = cosmosOptions.Value.Database;
        var productContainerName = cosmosOptions.Value.ProductContainer;
        var productDataSourceURI = cosmosOptions.Value.ProductDataSourceURI;

        ArgumentNullException.ThrowIfNullOrEmpty(completionDeploymentName);
        ArgumentNullException.ThrowIfNullOrEmpty(embeddingDeploymentName);
        ArgumentNullException.ThrowIfNullOrEmpty(maxRagTokens);
        ArgumentNullException.ThrowIfNullOrEmpty(maxContextTokens);
        ArgumentNullException.ThrowIfNullOrEmpty(databaseName);
        ArgumentNullException.ThrowIfNullOrEmpty(productContainerName);
        ArgumentNullException.ThrowIfNullOrEmpty(productDataSourceURI);

        //Set the product data source URI for loading data
        _productDataSourceURI = productDataSourceURI;

        // Initialize the Semantic Kernel
        var builder = Kernel.CreateBuilder();

        //Add Azure OpenAI chat completion service
        builder.AddOpenAIChatCompletion(modelId: completionDeploymentName, openAIClient: openAiClient);

        //Add Azure OpenAI text embedding generation service
        builder.AddOpenAITextEmbeddingGeneration(modelId: embeddingDeploymentName, openAIClient: openAiClient, dimensions: 1536);

        //Add Azure CosmosDB NoSql client and Database to the Semantic Kernel
        builder.Services.AddSingleton<Database>(
            sp =>
            {
                var client = cosmosClient;
                return client.GetDatabase(databaseName);
            });

        // Add the Azure CosmosDB NoSQL Vector Store Record Collection for Products
        var options = new AzureCosmosDBNoSQLVectorStoreRecordCollectionOptions<Product> { PartitionKeyPropertyName = "categoryId" };
        builder.AddAzureCosmosDBNoSQLVectorStoreRecordCollection<Product>(productContainerName, options);

        kernel = builder.Build();

        //Get a reference to the product container from Semantic Kernel for vector search and adding/updating products
        _productContainer = (AzureCosmosDBNoSQLVectorStoreRecordCollection<Product>)kernel.Services.GetRequiredService<IVectorStoreRecordCollection<string, Product>>();

        //Create a tokenizer for the model
        _tokenizer = Tokenizer.CreateTiktokenForModel(modelName: "gpt-4o");
        _maxRagTokens = Int32.TryParse(maxRagTokens, out _maxRagTokens) ? _maxRagTokens : 1500;
        _maxContextTokens = Int32.TryParse(maxContextTokens, out _maxContextTokens) ? _maxContextTokens : 500;

    }

    /// <summary>
    /// Generates a completion using a user prompt with chat history and vector search results to Semantic Kernel and returns the response.
    /// </summary>
    /// <param name="contextWindow">List of Message objects containing the context window (chat history) to send to the model.</param>
    /// <param name="ragData">Vector search results to send to the model.</param>
    /// <returns>Generated response along with tokens used to generate it and tokens for the completion text.</returns>
    public async Task<(string completion, int generationTokens, int completionTokens)> GetRagCompletionAsync(List<Message> contextWindow, List<Product> ragData)
    {

        //Manage token consumption per request by trimming the amount of vector search data sent to the model
        string ragDataString = LimitPayloadToMaxRagTokens(_maxRagTokens, ragData);

        //Add the system prompt and vector search data to the chat history
        var skChatHistory = new ChatHistory();
        skChatHistory.AddSystemMessage(_systemPromptRetailAssistant + ragDataString);

        //Manage token consumption by trimming the amount of chat history sent to the model
        //Useful if the chat history is very large. It can also be summarized before sending to the model
        int currentTokens = 0;

        foreach (var message in contextWindow)
        {
            //Add up to the max tokens allowed
            if ((currentTokens += message.PromptTokens + message.CompletionTokens) > _maxContextTokens) break;

            skChatHistory.AddUserMessage(message.Prompt);
            if (message.Completion != string.Empty)
                skChatHistory.AddAssistantMessage(message.Completion);

        }

        PromptExecutionSettings settings = new()
        {
            ExtensionData = new Dictionary<string, object>()
            {
                { "temperature", 0.2 },
                { "top_p", 0.7 },
                { "max_tokens", 1000  }
            }
        };

        var result = await kernel.GetRequiredService<IChatCompletionService>().GetChatMessageContentAsync(skChatHistory, settings);

        ChatTokenUsage completionUsage = (ChatTokenUsage)result.Metadata!["Usage"]!;

        string completion = result.Items[0].ToString()!;

        //Separate the amount of tokens used to process the completion vs. the tokens used on the returned text of the completion
        //The completion text is fed into subsequent requests so want an accurate count of tokens for that text in case
        int generationTokens = completionUsage.TotalTokenCount - completionUsage.OutputTokenCount;
        int completionTokens = completionUsage.OutputTokenCount;

        return (completion, generationTokens, completionTokens);
    }

    /// <summary>
    /// Performs a vector search on the Cosmos DB product container using Semantic Kernel
    /// </summary>
    /// <param name="promptVectors">Vectors used to do the search</param>
    /// <param name="productMaxResults">Limit the number of returned items</param>
    /// <returns>JSON string of returned products</returns>
    public async Task<List<Product>> SearchProductsAsync(ReadOnlyMemory<float> promptVectors, int productMaxResults)
    {
        var options = new VectorSearchOptions { VectorPropertyName = "vectors", Top = productMaxResults };

        //Call Semantic Kernel to perform the vector search
        var searchResult = await _productContainer.VectorizedSearchAsync(promptVectors, options);

        var resultRecords = new List<VectorSearchResult<Product>>();
        await foreach (var result in searchResult.Results)
        {
            resultRecords.Add(result);
        }

        List<Product> productResults = new();

        //add the product vector search results to products list
        productResults.AddRange(resultRecords.Select(r => r.Record));
        return productResults;

        //Serialize List<Product> to a JSON string to send to OpenAI
        //string productsString = JsonSerializer.Serialize(resultRecords);
        //return productsString;
    }

    /// <summary>
    /// Limit the amount of RAG Pattern data sent to the LLM
    /// </summary>
    /// <param name="maxTokens">Maximum number of tokens for RAG Pattern Data</param>
    /// <param name="ragData">List of data objects from RAG Pattern query</param>
    /// <returns>String of returned products</returns>
    private string LimitPayloadToMaxRagTokens(int maxTokens, List<Product> ragData)
    {
        // Get the index of the array of objects up to the maxTokens
        int trimIndex = 0;
        int currentTokens = 0;

        foreach (var text in ragData)
        {
            if ((currentTokens += _tokenizer.CountTokens(text.ToString()!)) > maxTokens) break;
            trimIndex++;
        }

        // Return the trimmed text based upon the maxTokens
        return string.Join(" ", ragData.GetRange(0, trimIndex));
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
    /// Generates a completion using a user prompt with chat history to Semantic Kernel and returns the response.
    /// </summary>
    /// <param name="contextWindow">List of Message objects containing the context window (chat history) to send to the model.</param>
    /// <returns>Generated response along with tokens used to generate it.</returns>
    public async Task<(string completion, int tokens)> GetChatCompletionAsync(List<Message> contextWindow)
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
                { "temperature", 0.2 },
                { "top_p", 0.7 },
                { "max_tokens", 1000  }
            }
        };

        var result = await kernel.GetRequiredService<IChatCompletionService>().GetChatMessageContentAsync(skChatHistory, settings);

        CompletionsUsage completionUsage = (CompletionsUsage)result.Metadata!["Usage"]!;

        string completion = result.Items[0].ToString()!;
        int tokens = completionUsage.CompletionTokens;

        return (completion, tokens);
    }

    /// <summary>
    /// Sends the existing conversation to the Semantic Kernel and returns a two word summary.
    /// </summary>
    /// <param name="conversationText">conversation history to send to Semantic Kernel.</param>
    /// <returns>Summarized text from the OpenAI completion model deployment.</returns>
    public async Task<string> SummarizeConversationAsync(string conversation)
    {
        var skChatHistory = new ChatHistory();
        skChatHistory.AddSystemMessage(_summarizePrompt);
        skChatHistory.AddUserMessage(conversation);

        PromptExecutionSettings settings = new()
        {
            ExtensionData = new Dictionary<string, object>()
            {
                { "temperature", 0.0 },
                { "top_p", 1.0 },
                { "max_tokens", 100 }
            }
        };

        var result = await kernel.GetRequiredService<IChatCompletionService>().GetChatMessageContentAsync(skChatHistory, settings);

        string completion = result.Items[0].ToString()!;

        return completion;
    }

    public async Task LoadProductDataAsync()
    {
        //Read the product container to see if there are any items
        Product? item = null;
        try {
            
            var compositeKey = new AzureCosmosDBNoSQLCompositeKey(recordKey: "d4e4f47b-fcd1-4cb0-84fc-db0948d26e9a", partitionKey: "598aede4-8b86-466b-ba48-3038a9a3b5fc");
            item = await _productContainer.GetAsync(compositeKey);
            
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
                    await UpsertProductAsync(product);
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
    /// <param name="product">Product item to create or update</param>
    /// <returns>The newly created product item</returns>
    public async Task<string> UpsertProductAsync(Product product)
    {
        
        return await _productContainer.UpsertAsync(product);

    }

    /// <summary>
    /// Delete a product.
    /// </summary>
    /// <param name="product">Product item to delete.</param>
    public async Task DeleteProductAsync(Product product)
    {
        
        var compositeKey = new AzureCosmosDBNoSQLCompositeKey(recordKey: product.id, partitionKey: product.categoryId);
        await _productContainer.DeleteAsync(compositeKey);
    }

}
