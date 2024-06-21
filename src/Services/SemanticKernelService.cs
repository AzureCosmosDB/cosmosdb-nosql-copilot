using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Cosmos.Copilot.Models;
using Microsoft.SemanticKernel.Embeddings;
using Azure.AI.OpenAI;
using Azure.Core;
using Azure.Identity;
using Newtonsoft.Json;

#pragma warning disable SKEXP0010, SKEXP0001

namespace Cosmos.Copilot.Services
{
    public class SemanticKernelService
    {
        readonly Kernel kernel;

        private readonly string _systemPrompt = @"
        You are an AI assistant that helps people find information.
        Provide concise answers that are polite and professional.";

        private readonly string _systemPromptRetailAssistant = @"
        You are an intelligent assistant for the Cosmic Works Bike Company. 
        You are designed to provide helpful answers to user questions about 
        bike products and accessories provided in JSON format below.

        Instructions:
        - Only answer questions related to the information provided below,
        - Don't reference any product data not provided below.
        - If you're unsure of an answer, you can say ""I don't know"" or ""I'm not sure"" and recommend users search themselves.

        Text of relevant information:";

        private readonly string _summarizePrompt = @"
        Summarize this text. One to three words maximum length. 
        Plain text only. No punctuation, markup or tags.";

        public SemanticKernelService(string endpoint, string completionDeploymentName, string embeddingDeploymentName)
        {
            ArgumentNullException.ThrowIfNullOrEmpty(endpoint);
            ArgumentNullException.ThrowIfNullOrEmpty(completionDeploymentName);
            ArgumentNullException.ThrowIfNullOrEmpty(embeddingDeploymentName);

            TokenCredential credential = new DefaultAzureCredential();
            // Initialize the Semantic Kernel
            kernel = Kernel.CreateBuilder()
                .AddAzureOpenAIChatCompletion(completionDeploymentName, endpoint, credential)
                .AddAzureOpenAITextEmbeddingGeneration(embeddingDeploymentName, endpoint, credential)
                .Build();
        }

        public async Task<float[]> GetEmbeddingsAsync(string text)
        {
            await Task.Delay(0);
            float[] embeddingsArray = new float[0];

            return embeddingsArray;
        }

        public async Task<(string completion, int tokens)> GetChatCompletionAsync(string sessionId, List<Message> chatHistory)
        {
            var skChatHistory = new ChatHistory();
            skChatHistory.AddSystemMessage(string.Empty);

            foreach (var message in chatHistory)
            {
                skChatHistory.AddUserMessage(message.Prompt);
                skChatHistory.AddAssistantMessage(string.Empty);
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

            string completion = "Place holder response";
            int tokens = 0;
            await Task.Delay(0);

            return (completion, tokens);
        }

        public async Task<string> SummarizeAsync(string conversation)
        {
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

            CompletionsUsage completionUsage = (CompletionsUsage)result.Metadata!["Usage"]!;

            string completion = result.Items[0].ToString()!;
            int tokens = completionUsage.CompletionTokens;

            return (completion, tokens);
        }
    }
}
