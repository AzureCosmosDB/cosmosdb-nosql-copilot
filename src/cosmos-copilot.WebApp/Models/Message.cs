using Newtonsoft.Json;
using System.Diagnostics;

namespace Cosmos.Copilot.Models;

public record Message
{
    /// <summary>
    /// Unique identifier
    /// </summary>
    public string Id { get; set; }

    public string Type { get; set; }

    /// <summary>
    /// Partition key- L1
    /// </summary>
    public string TenantId { get; set; }
    /// <summary>
    /// Partition key- L2
    /// </summary>
    public string UserId { get; set; }
    /// <summary>
    /// Partition key- L3
    /// </summary>
    public string SessionId { get; set; }

    public DateTime TimeStamp { get; set; }

    public string Prompt { get; set; }

    public int PromptTokens { get; set; }

    public string Completion { get; set; }

    public int CompletionTokens { get; set; }

    public int GenerationTokens { get; set; }

    public bool CacheHit {get; set;}

    public long ElapsedMilliseconds { get; set; }

    private Stopwatch stopwatch  = new Stopwatch();

    public Message(string tenantId, string userId, string sessionId, int promptTokens, string prompt, string completion = "", int completionTokens = 0, int generationTokens = 0, bool cacheHit = false, long elapsedMilliseconds = 0)
    {
        Id = Guid.NewGuid().ToString();
        Type = nameof(Message);
        TenantId = tenantId;
        UserId = userId;
        SessionId = sessionId;
        TimeStamp = DateTime.UtcNow;
        Prompt = prompt;
        Completion = completion;
        PromptTokens = promptTokens;
        CompletionTokens = completionTokens;
        GenerationTokens = generationTokens;
        CacheHit = cacheHit;
        ElapsedMilliseconds = elapsedMilliseconds;

        stopwatch.Start();
    }

    public void CalculateElapsedTime()
    {
        stopwatch.Stop();
        ElapsedMilliseconds = stopwatch.ElapsedMilliseconds;
    }
}