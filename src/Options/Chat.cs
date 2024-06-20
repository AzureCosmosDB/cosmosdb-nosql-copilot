namespace Cosmos.Copilot.Options;

public record Chat
{
    public required string MaxConversationTokens { get; init; }

    public required string CacheSimilarityScore { get; init; }

    public required string ProductSimilarityScore { get; init; }

    public required string ProductMaxResults { get; init; }
}
