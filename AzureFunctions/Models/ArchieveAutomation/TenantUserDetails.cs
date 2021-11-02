using System;

namespace AzureFunctions.Models.ArchieveAutomation
{
    public class TenantUserDetails
    {
        public long TenantUserId { get; set; }
        public string EmailId { get; set; }
        public Guid TenantId { get; set; }
        public bool IsActive { get; set; }
        public long CreatedBy { get; set; }
        public DateTime CreatedOn { get; set; }
        public long? UpdatedBy { get; set; }
        public DateTime? UpdatedOn { get; set; }
    }
}
