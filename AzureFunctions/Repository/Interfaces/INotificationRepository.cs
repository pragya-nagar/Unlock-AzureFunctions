using AzureFunctions.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace AzureFunctions.Repository.Interfaces
{
    public interface INotificationRepository
    {
    
       
        Task<MailerTemplate> GetMailerTemplate(string templateCode);
        Task<IEnumerable<MailerTemplate>> GetTemplate(string templateCode);
        Task InsertNotificationDetails(NotificationsRequest notificationsRequest);
        Task<bool> SentMailWithoutAuthenticationAsync(MailRequest mailRequest);
    }
}
