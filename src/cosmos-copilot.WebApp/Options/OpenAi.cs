namespace Cosmos.Copilot.Options;

public record OpenAi
{
    public required string Endpoint { get; init; }

    public required string CompletionDeploymentName { get; init; }

    public required string EmbeddingDeploymentName { get; init; }
}