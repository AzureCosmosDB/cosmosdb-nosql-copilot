using Microsoft.Azure.Cosmos;

namespace Cosmos.Copilot.Models
{
    public class UserParameters
    {
        public string UserId { get; set; }
        public string TenantId { get; set; }

        public UserParameters(string _userId, string _tenantId)
        {
            UserId = _userId;
            TenantId = _tenantId;
        }
    }
}
