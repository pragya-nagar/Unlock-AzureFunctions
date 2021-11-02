using System.Collections.Generic;


namespace AzureFunctions.Models
{
    public class NotificationsRequest
    {
        public long By { get; set; }
        public List<long> To { get; set; }
        public string Text { get; set; }
        public int AppId { get; set; }
        public long NotificationType { get; set; }
        public int MessageType { get; set; }
        public string Url { get; set; } = "";
    }
}
