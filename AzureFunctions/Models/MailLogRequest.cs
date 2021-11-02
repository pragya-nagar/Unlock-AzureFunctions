using System;
using System.Collections.Generic;
using System.Text;

namespace AzureFunctions.Models
{
    public class MailLogRequest
    {
        public string MailTo { get; set; }
        public string MailFrom { get; set; }
        public string Bcc { get; set; }
        public string CC { get; set; }
        public string MailSubject { get; set; }
        public string Body { get; set; }
        public DateTime? MailSentOn { get; set; }
        public bool IsMailSent { get; set; }
    }
}
