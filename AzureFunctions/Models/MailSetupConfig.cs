using System;
using System.Collections.Generic;
using System.Text;

namespace AzureFunctions.Models
{
    public class MailSetupConfig
    {
        public long MailSetupConfigId { get; set; }
        public string AwsemailId { get; set; }
        public string AccountName { get; set; }
        public string AccountPassword { get; set; }
        public string ServerName { get; set; }
        public bool IsSslenable { get; set; }
        public int Port { get; set; }
        public string Host { get; set; }
        public DateTime? CreatedOn { get; set; } = DateTime.UtcNow;
        public long? CreatedBy { get; set; }
        public DateTime? UpdatedOn { get; set; }
        public long? UpdatedBy { get; set; }
        public bool? IsActive { get; set; }
    }
}
