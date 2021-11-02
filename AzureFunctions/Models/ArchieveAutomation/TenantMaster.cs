using System;

namespace AzureFunctions.Models.ArchieveAutomation
{
    public class TenantMaster
    {
        public Guid TenantId { get; set; }
        public string SubDomain { get; set; }
        public bool IsActive { get; set; }
        public long CreatedBy { get; set; }
        public DateTime CreatedOn { get; set; }
        public long? UpdatedBy { get; set; }
        public DateTime? UpdatedOn { get; set; }
    }
}
