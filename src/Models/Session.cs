using Newtonsoft.Json;

namespace Cosmos.Copilot.Models;

public record Session
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

    public int? Tokens { get; set; }

    public string Name { get; set; }

    [JsonIgnore]
    public List<Message> Messages { get; set; }

    public Session(string tenantId, string userId)
    {
        Id = Guid.NewGuid().ToString();
        Type = nameof(Session);
        SessionId = this.Id;
        UserId = userId;
        TenantId= tenantId; 
        Tokens = 0;
        Name = "New Chat";
        Messages = new List<Message>();
    }

    public void AddMessage(Message message)
    {
        Messages.Add(message);
    }

    public void UpdateMessage(Message message)
    {
        var match = Messages.Single(m => m.Id == message.Id);
        var index = Messages.IndexOf(match);
        Messages[index] = message;
    }
}