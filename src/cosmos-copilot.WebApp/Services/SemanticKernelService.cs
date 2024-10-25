using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Cosmos.Copilot.Models;
using Microsoft.SemanticKernel.Embeddings;
using Azure.Core;
using Azure.Identity;
using Newtonsoft.Json;
using OpenAI.Chat;

namespace Cosmos.Copilot.Services;

/// <summary>
/// Semantic Kernel implementation for Azure OpenAI.
/// </summary>
public class SemanticKernelService
{
    //Semantic Kernel
    readonly Kernel kernel;

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
    /// <param name="endpoint">Endpoint URI.</param>
    /// <param name="completionDeploymentName">Name of the deployed Azure OpenAI completion model.</param>
    /// <param name="embeddingDeploymentName">Name of the deployed Azure OpenAI embedding model.</param>
    /// <exception cref="ArgumentNullException">Thrown when endpoint, key, or modelName is either null or empty.</exception>
    /// <remarks>
    /// This constructor will validate credentials and create a Semantic Kernel instance.
    /// </remarks>
    public SemanticKernelService(string endpoint, string completionDeploymentName, string embeddingDeploymentName)
    {
        ArgumentNullException.ThrowIfNullOrEmpty(endpoint);
        ArgumentNullException.ThrowIfNullOrEmpty(completionDeploymentName);
        ArgumentNullException.ThrowIfNullOrEmpty(embeddingDeploymentName);

        TokenCredential credential = new DefaultAzureCredential();
        // Initialize the Semantic Kernel
        kernel = Kernel.CreateBuilder()
            .AddAzureOpenAIChatCompletion(completionDeploymentName, endpoint, credential)
            .AddAzureOpenAITextEmbeddingGeneration(deploymentName: embeddingDeploymentName, endpoint: endpoint, credential: credential, dimensions: 1536)
            .Build();
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

        ChatTokenUsage completionUsage = (ChatTokenUsage)result.Metadata!["Usage"]!;

        string completion = result.Items[0].ToString()!;
        int tokens = completionUsage.OutputTokens;

        return (completion, tokens);
    }

    /// <summary>
    /// Generates a completion using a user prompt with chat history and vector search results to Semantic Kernel and returns the response.
    /// </summary>
    /// <param name="sessionId">Chat session identifier for the current conversation.</param>
    /// <param name="contextWindow">List of Message objects containing the context window (chat history) to send to the model.</param>
    /// <param name="products">List of Product objects containing vector search results to send to the model.</param>
    /// <returns>Generated response along with tokens used to generate it.</returns>
    public async Task<(string completion, int tokens)> GetRagCompletionAsync(string sessionId, List<Message> contextWindow, List<Product> products)
    {
        //Serialize List<Product> to a JSON string to send to OpenAI
        string productsString = JsonConvert.SerializeObject(products);

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
        int tokens = completionUsage.OutputTokens;

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
}
