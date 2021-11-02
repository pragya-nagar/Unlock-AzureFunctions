
namespace AzureFunctions.Models
{
    public class MailRequest
    {
        public string MailTo { get; set; }
        public string MailFrom { get; set; } = "";
        public string Bcc { get; set; } = "";
        public string CC { get; set; } = "";
        public string Subject { get; set; }
        public string Body { get; set; }
    }
}
