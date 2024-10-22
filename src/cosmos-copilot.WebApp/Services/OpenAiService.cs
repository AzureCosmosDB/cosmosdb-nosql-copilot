// TODO: Remove class as we are now using AzureOpenAI from Semantic Kernel isself.


// using Cosmos.Copilot.Models;
// using Cosmos.Copilot.Options;
// using Microsoft.Extensions.Options;
// using Newtonsoft.Json;
// using OpenAI;
// using OpenAI.Chat;
// using OpenAI.Embeddings;
// using System.ClientModel;

// namespace Cosmos.Copilot.Services;

// /// <summary>
// /// Service to access Azure OpenAI.
// /// </summary>
// public class OpenAiService
// {
//     private readonly string _completionDeploymentName = String.Empty;
//     private readonly string _embeddingDeploymentName = String.Empty;
//     private readonly OpenAIClient _client;
//     private readonly EmbeddingClient _embeddingClient;
//     private readonly ChatClient _chatClient;

//     /// <summary>
//     /// System prompt to send with user prompts to instruct the model for chat session
//     /// </summary>
//     private readonly string _systemPrompt = @"
//         You are an AI assistant that helps people find information.
//         Provide concise answers that are polite and professional." + Environment.NewLine;

//     /// <summary>
//     /// System prompt to send with user prompts as a Retail AI Assistant for chat session
//     /// </summary>
//     private readonly string _systemPromptRetailAssistant = @"
//         You are an intelligent assistant for the Cosmic Works Bike Company. 
//         You are designed to provide helpful answers to user questions about 
//         bike products and accessories provided in JSON format below.

//         Instructions:
//         - Only answer questions related to the information provided below,
//         - Don't reference any product data not provided below.
//         - If you're unsure of an answer, you can say ""I don't know"" or ""I'm not sure"" and recommend users search themselves.

//         Text of relevant information:";

//     /// <summary>    
//     /// System prompt to send with user prompts to instruct the model for summarization
//     /// </summary>
//     private readonly string _summarizePrompt = @"
//         Summarize this prompt in one or two words to use as a label in a button on a web page.
//         Do not use any punctuation." + Environment.NewLine;

//     /// <summary>
//     /// Creates a new instance of the service.
//     /// </summary>
//     /// <param name="endpoint">Endpoint URI.</param>
//     /// <param name="completionDeploymentName">Name of the deployed Azure OpenAI completion model.</param>
//     /// <param name="embeddingDeploymentName">Name of the deployed Azure OpenAI embedding model.</param>
//     /// <exception cref="ArgumentNullException">Thrown when endpoint, key, or modelName is either null or empty.</exception>
//     /// <remarks>
//     /// This constructor will validate credentials and create a HTTP client instance.
//     /// </remarks>
//     public OpenAiService(OpenAIClient client, IOptions<OpenAi> cosmosOptions)
//     {
//         var completionDeploymentName = cosmosOptions.Value.CompletionDeploymentName;
//         var embeddingDeploymentName = cosmosOptions.Value.EmbeddingDeploymentName;

//         ArgumentNullException.ThrowIfNullOrEmpty(completionDeploymentName);
//         ArgumentNullException.ThrowIfNullOrEmpty(embeddingDeploymentName);

//         _completionDeploymentName = completionDeploymentName;
//         _embeddingDeploymentName = embeddingDeploymentName;

//         _client = client;
//         _embeddingClient = _client.GetEmbeddingClient(_embeddingDeploymentName);
//         _chatClient = _client.GetChatClient(_completionDeploymentName);
//     }

//     /// <summary>
//     /// Sends a prompt to the deployed OpenAI LLM model and returns the response.
//     /// </summary>
//     /// <param name="sessionId">Chat session identifier for the current conversation.</param>
//     /// <param name="conversation">List of Message objects containign the context window (chat history) to send to the model.</param>
//     /// <returns>Generated response along with tokens used to generate it.</returns>
//     public async Task<(string completion, int tokens)> GetChatCompletionAsync(string sessionId, List<Message> conversation)
//     {
//         ChatCompletionOptions options = new()
//         {
//             EndUserId = sessionId,
//             MaxTokens = 1000,
//             Temperature = 0.2f,
//             TopP = 0.7f,
//             FrequencyPenalty = 0,
//             PresencePenalty = 0
//         };

//         List<ChatMessage> messages = new List<ChatMessage>()
//         {
//             new SystemChatMessage(_systemPrompt),
//         };
//         foreach (Message message in conversation)
//         {
//             messages.Add(new UserChatMessage(message.Prompt));
//             //Context Window always ends with the last prompt, add the completion if it exists
//             if (!string.IsNullOrEmpty(message.Completion))
//                 messages.Add(new AssistantChatMessage(message.Completion));
//         }

//         ClientResult<ChatCompletion> completionsResponse = await _chatClient.CompleteChatAsync(messages, options);

//         ChatCompletion completions = completionsResponse.Value;

//         string completion = completions.Content[0].Text;
//         int tokens = completions.Usage.TotalTokens;

//         return (completion, tokens);
//     }

//     /// <summary>
//     /// Sends a prompt and vector search results to the deployed OpenAI LLM model and returns the response.
//     /// </summary>
//     /// <param name="sessionId">Chat session identifier for the current conversation.</param>
//     /// <param name="conversation">List of Message objects containign the context window (chat history) to send to the model.</param>
//     /// <param name="products">List of Product objects containing vector search results to augment the LLM completion.</param>
//     /// <returns>Generated response along with tokens used to generate it.</returns>
//     public async Task<(string completion, int tokens)> GetRagCompletionAsync(string sessionId, List<Message> contextWindow, List<Product> products)
//     {
//         //Serialize List<Product> to a JSON string to send to OpenAI
//         string productsString = JsonConvert.SerializeObject(products);

//         ChatCompletionOptions options = new()
//         {
//             EndUserId = sessionId,
//             MaxTokens = 1000,
//             Temperature = 0.2f,
//             TopP = 0.7f,
//             FrequencyPenalty = 0,
//             PresencePenalty = 0
//         };

//         List<ChatMessage> messages = new List<ChatMessage>()
//         {
//             new SystemChatMessage(_systemPromptRetailAssistant + productsString),
//         };
//         foreach (Message message in contextWindow)
//         {
//             messages.Add(new UserChatMessage(message.Prompt));
//             //Context Window always ends with the last prompt, only add the completion if it exists
//             if (!string.IsNullOrEmpty(message.Completion))
//                 messages.Add(new AssistantChatMessage(message.Completion));
//         }

//         ClientResult<ChatCompletion> completionsResponse = await _chatClient.CompleteChatAsync(messages, options);

//         ChatCompletion completions = completionsResponse.Value;

//         string completion = completions.Content[0].Text;
//         int tokens = completions.Usage.TotalTokens;

//         return (completion, tokens);
//     }

//     /// <summary>
//     /// Sends the existing conversation to the OpenAI model and returns a two word summary.
//     /// </summary>
//     /// <param name="sessionId">Chat session identifier for the current conversation.</param>
//     /// <param name="conversationText">conversation history to send to OpenAI.</param>
//     /// <returns>Summarization response from the OpenAI completion model deployment.</returns>
//     public async Task<string> SummarizeAsync(string sessionId, string conversationText)
//     {
//         ChatCompletionOptions options = new()
//         {
//             EndUserId = sessionId,
//             MaxTokens = 200,
//             Temperature = 0.0f,
//             TopP = 1.0f,
//             FrequencyPenalty = 0,
//             PresencePenalty = 0
//         };

//         var messages = new List<ChatMessage>()
//         {
//             new SystemChatMessage(_summarizePrompt),
//             new UserChatMessage(conversationText)
//         };

//         ClientResult<ChatCompletion> completionsResponse = await _chatClient.CompleteChatAsync(messages, options);

//         ChatCompletion completions = completionsResponse.Value;

//         string completionText = completions.Content[0].Text;

//         return completionText;
//     }

//     /// <summary>
//     /// Generates embeddings from the deployed OpenAI embeddings model and returns an array of vectors.
//     /// </summary>
//     /// <param name="input">Text to send to OpenAI.</param>
//     /// <returns>Array of vectors from the OpenAI embedding model deployment.</returns>
//     public async Task<float[]> GetEmbeddingsAsync(string input)
//     {

//         float[] embedding = new float[0];

//         EmbeddingGenerationOptions options = new()
//         {
//             Dimensions = 1536
//         };

//         var response = await _embeddingClient.GenerateEmbeddingAsync(input);

//         Embedding embeddings = response.Value;

//         embedding = embeddings.Vector.ToArray();

//         return embedding;
//     }
// }
