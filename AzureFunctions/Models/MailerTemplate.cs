using System;


namespace AzureFunctions.Models
{
    public class MailerTemplate
    {
        public long Id { get; set; }
        public string TemplateName { get; set; }
        public string TemplateCode { get; set; }
        public string Subject { get; set; }
        public string Body { get; set; }
        public long CreatedBy { get; set; }
        public DateTime CreatedOn { get; set; }
        public bool IsActive { get; set; }
    }
}
