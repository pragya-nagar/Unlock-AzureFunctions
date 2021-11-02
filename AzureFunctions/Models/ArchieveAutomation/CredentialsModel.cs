namespace AzureFunctions.Models.ArchieveAutomation
{
    public class CredentialsModel
    {
        public string Environment { get; set; }
        public string ClientId { get; set; }
        public string ClientSecretId { get; set; }
        public string ObjectId { get; set; }
        public string TenantId { get; set; }
        public string KeyVaultUrl { get; set; }
        public string Domain { get; set; }
        public string CNameRecord { get; set; }
        public string SubscriptionId { get; set; }
        public string ResourceGroupName { get; set; }
        public string TTL { get; set; }
        public string TenantConnectionString { get; set; }
        public string OkrTrialConnectionString { get; set; }
        public string InviteMessage { get; set; }
        public string StorageName { get; set; }
        public string StorageKey { get; set; }
        public string DBServerPassword { get; set; }
        public string DBServerName { get; set; }
        public string DBServerUserId { get; set; }
        public string InfraDeletionDays { get; set; }
        public string JourneyEndingDays { get; set; }
    }
}
