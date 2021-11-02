using System;
using System.Collections.Generic;

namespace AzureFunctions.Models.ArchieveAutomation
{
    public class InnerRequestModel
    {
        public InnerRequestModel()
        {
            TenantMaster = new List<TenantMaster>();
            TenantIds = new List<Guid>();
            SubDomains = new List<string>();
            Emails = new List<string>();
        }
        public List<TenantMaster> TenantMaster { get; set; }
        public List<Guid> TenantIds { get; set; }
        public List<string> SubDomains { get; set; }
        public List<string> Emails { get; set; }
    }
}
