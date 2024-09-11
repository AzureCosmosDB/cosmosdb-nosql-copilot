using Azure.Identity;
using Cosmos.Copilot.Options;
using Cosmos.Copilot.Services;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire components.
builder.AddServiceDefaults();

builder.RegisterConfiguration();
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// Configure Azure Cosmos DB Aspire integration
builder.AddAzureCosmosClient(
    "cosmos",
    settings =>
    {
        settings.Credential = new DefaultAzureCredential();
        settings.DisableTracing = false;
    },
    clientOptions => {
        clientOptions.ApplicationName = "cosmos-copilot";
        clientOptions.SerializerOptions = new()
        {
            PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
        };
        clientOptions.CosmosClientTelemetryOptions = new()
        {
            CosmosThresholdOptions = new()
            {
                PointOperationLatencyThreshold = TimeSpan.FromMilliseconds(10),
                NonPointOperationLatencyThreshold = TimeSpan.FromMilliseconds(20)
            }
        };
    });

// Configure OpenAI Aspire integration
var endpoint = builder.Configuration.GetSection(nameof(OpenAi)).GetValue<string>("Endpoint");
if (endpoint is null)
{
    throw new ArgumentException($"{nameof(IOptions<OpenAi>)} was not resolved through dependency injection.");
}
builder.AddAzureOpenAIClient("openAiConnectionName",
    configureSettings: settings =>
    {
        settings.Credential = new DefaultAzureCredential();
        settings.Endpoint = new Uri(endpoint);
    });

builder.Services.RegisterServices();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

await app.RunAsync();

static class ProgramExtensions
{
    public static void RegisterConfiguration(this WebApplicationBuilder builder)
    {
        builder.Services.AddOptions<CosmosDb>()
            .Bind(builder.Configuration.GetSection(nameof(CosmosDb)));

        builder.Services.AddOptions<OpenAi>()
            .Bind(builder.Configuration.GetSection(nameof(OpenAi)));

        builder.Services.AddOptions<SemanticKernel>()
            .Bind(builder.Configuration.GetSection(nameof(SemanticKernel)));

        builder.Services.AddOptions<Chat>()
            .Bind(builder.Configuration.GetSection(nameof(Chat)));
    }

    public static void RegisterServices(this IServiceCollection services)
    {
        services.AddSingleton<CosmosDbService, CosmosDbService>();
        services.AddSingleton<OpenAiService, OpenAiService>();
        services.AddSingleton<SemanticKernelService, SemanticKernelService>((provider) =>
        {
            var semanticKernalOptions = provider.GetRequiredService<IOptions<SemanticKernel>>();
            if (semanticKernalOptions is null)
            {
                throw new ArgumentException($"{nameof(IOptions<SemanticKernel>)} was not resolved through dependency injection.");
            }
            else
            {
                return new SemanticKernelService(
                    endpoint: semanticKernalOptions.Value?.Endpoint ?? String.Empty,
                    completionDeploymentName: semanticKernalOptions.Value?.CompletionDeploymentName ?? String.Empty,
                    embeddingDeploymentName: semanticKernalOptions.Value?.EmbeddingDeploymentName ?? String.Empty
                );
            }
        });
        services.AddSingleton<ChatService>((provider) =>
        {
            var chatOptions = provider.GetRequiredService<IOptions<Chat>>();
            if (chatOptions is null)
            {
                throw new ArgumentException($"{nameof(IOptions<Chat>)} was not resolved through dependency injection.");
            }
            else
            {
                var cosmosDbService = provider.GetRequiredService<CosmosDbService>();
                var openAiService = provider.GetRequiredService<OpenAiService>();
                var semanticKernelService = provider.GetRequiredService<SemanticKernelService>();
                return new ChatService(
                    openAiService: openAiService,
                    cosmosDbService: cosmosDbService,
                    semanticKernelService: semanticKernelService,
                    maxConversationTokens: chatOptions.Value?.MaxConversationTokens ?? String.Empty,
                    cacheSimilarityScore: chatOptions.Value?.CacheSimilarityScore ?? String.Empty,
                    productMaxResults: chatOptions.Value?.ProductMaxResults ?? String.Empty
                );
            }
        });
    }
}
