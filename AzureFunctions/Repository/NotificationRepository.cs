using AzureFunctions.Common;
using AzureFunctions.Models;
using AzureFunctions.Repository.Interfaces;
using Dapper;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Configuration;
using MimeKit;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AzureFunctions.Repository
{
    public class NotificationRepository : BaseRepository, INotificationRepository
    {
        public IConfiguration Configuration { get; set; }
        public NotificationRepository(IConfiguration _configuration) : base(_configuration)
        {
        }


        public async Task<MailerTemplate> GetMailerTemplate(string templateCode)
        {
            var data = new MailerTemplate();
            using (var connection = DbConnectionNotifications)
            {
                if (ConnNotifications != null)
                {
                    data = await connection.QueryFirstAsync<MailerTemplate>("select * from MailerTemplate where templateCode = @templateCode ", new
                    {

                        TemplateCode = templateCode,
                        IsActive = 1

                    });
                }
            }
            return data;
        }

        public async Task<IEnumerable<MailerTemplate>> GetTemplate(string templateCode)
        {
            IEnumerable<MailerTemplate> data = null;
            using (var connection = DbConnectionNotifications)
            {
                if (ConnNotifications != null)
                {

                    data = await connection.QueryAsync<MailerTemplate>("select * from MailerTemplate where templatecode = " + templateCode + " and isActive = 1");
                }
            }
            return data;
        }


        public async Task<bool> SentMailWithoutAuthenticationAsync(MailRequest mailRequest)
        {
            bool IsMailSent = false;
            MailLogRequest log = new MailLogRequest();

            try
            {
                MimeMessage message = new MimeMessage();

                string aWSEmailId = AppConstants.AwsEmailId;
                string account = AppConstants.AccountName; 
                string password = AppConstants.Password;
                int port = AppConstants.Port;

                string host = AppConstants.Host;
                string environment = "QA";


                if (string.IsNullOrWhiteSpace(mailRequest.MailFrom) && mailRequest.MailFrom == "")
                {
                    MailboxAddress from = new MailboxAddress("UnlockOKR", aWSEmailId);
                    message.From.Add(from);
                }
                else
                {
                    var isMailExist = IsMailExist(mailRequest.MailFrom);
                    if (isMailExist != null)
                    {
                        MailboxAddress mailboxAddress = new MailboxAddress("User", mailRequest.MailFrom);
                        message.From.Add(mailboxAddress);
                    }
                }

                MailboxAddress From = new MailboxAddress("UnlockOKR", aWSEmailId);
                message.From.Add(From);


                if (environment != "LIVE")
                {
                    mailRequest.Subject = mailRequest.Subject + " - Azure " + environment + " This mail is for " + mailRequest.MailTo;

                    var emails = await GetEmailAddress();
                    foreach (var address in emails)
                    {
                        var emailAddress = new MailboxAddress(address.FullName, address.EmailAddress);
                        message.To.Add(emailAddress);
                    }
                    MailboxAddress CC = new MailboxAddress("alok.parhi@compunneldigital.com");
                    message.Cc.Add(CC);
                }

                else if (environment == "LIVE")
                {
                    string[] strTolist = mailRequest.MailTo.Split(';');

                    foreach (var item in strTolist)
                    {
                        MailboxAddress mailto = new MailboxAddress(item);
                        message.To.Add(mailto);
                    }


                    if (mailRequest.Bcc != "")
                    {
                        string[] strbcclist = mailRequest.CC.Split(';');
                        foreach (var item in strbcclist)
                        {
                            MailboxAddress bcc = new MailboxAddress(item);
                            message.Bcc.Add(bcc);
                        }
                    }

                    if (mailRequest.CC != "")
                    {
                        string[] strCcList = mailRequest.CC.Split(';');
                        foreach (var item in strCcList)
                        {
                            MailboxAddress CC = new MailboxAddress(item);
                            message.Cc.Add(CC);
                        }
                    }
                }


                message.Subject = mailRequest.Subject;
                BodyBuilder bodyBuilder = new BodyBuilder();
                bodyBuilder.HtmlBody = mailRequest.Body;
                message.Body = bodyBuilder.ToMessageBody();


               

                if (message.Subject != "")
                {
                    SmtpClient client = new SmtpClient();
                    client.Connect(host, port, false);
                    client.Authenticate(account, password);
                    client.Send(message);
                    client.Disconnect(true);
                    client.Dispose();
               
                    IsMailSent = true;
                  
                }

            }
            catch (Exception e)
            { 
              
                IsMailSent = false;

            }

            return IsMailSent;
        }

        public async Task<MailSetupConfig> IsMailExist(string emailId)
        {

            var data = new MailSetupConfig();
            using (var connection = DbConnectionNotifications)
            {
                if (ConnNotifications != null)
                {

                    //data = connection.QuerySingle<MailerTemplate>("select * from MailerTemplate where templateCode = " + templateCode + "and isActive=1");

                    data = connection.QueryFirst<MailSetupConfig>("select * from MailSetupConfig where AwsemailId = @emailId ", new
                    {

                        TemplateCode = emailId,
                        IsActive = 1

                    });
                }
            }
            return data;

        }

        public async Task<IEnumerable<Emails>> GetEmailAddress()
        {
            IEnumerable<Emails> data = null;
            using (var connection = DbConnectionNotifications)
            {
                if (ConnNotifications != null)
                {

                    data = await connection.QueryAsync<Emails>("select * from Emails");
                }
            }
            return data.ToList();

        }


        /// <summary>
        /// Method that will save notification details in NotificationsDetails table
        /// </summary>
        /// <param name="notificationsRequest"></param>
        /// <returns></returns>
        public async Task InsertNotificationDetails(NotificationsRequest notificationsRequest)
        {
            using (var connection = DbConnectionNotifications)
            {
                if (ConnAdmin != null)
                {
                    string insertQuery = @"INSERT INTO [dbo].[NotificationsDetails] ([NotificationsBy], [NotificationsTo], [NotificationsMessage], [ApplicationMasterId], [IsRead], [IsDeleted],[NotificationTypeId],[MessageTypeId],[Url],[CreatedOn]) VALUES (@By,@To,@Text,@AppId,@IsRead,@IsDeleted,@NotificationType,@MessageType,@Url,@CreatedOn)";

                    var result = await connection.ExecuteAsync(insertQuery, new
                    {
                        notificationsRequest.By,
                        notificationsRequest.To,
                        notificationsRequest.Text,
                        notificationsRequest.AppId,
                        IsRead = 0,
                        IsDeleted = 0,
                        notificationsRequest.NotificationType,
                        notificationsRequest.MessageType,
                        notificationsRequest.Url,
                        CreatedOn = DateTime.Now
                    });

                }
            }

        }

        //public async Task InsertNotifications(NotificationsRequest notificationsRequest)

        //{
        //    try
        //    {
        //        using (SqlConnection connection = new SqlConnection(Environment.GetEnvironmentVariable("SqlConnectionStringNotification")))
        //        {
        //            connection.Open();

        //            string insertQuery = @"INSERT INTO [dbo].[NotificationsDetails] ([NotificationsBy], [NotificationsTo], [NotificationsMessage], [ApplicationMasterId], [IsRead], [IsDeleted],[NotificationTypeId],[MessageTypeId],[Url],[CreatedOn]) 
        //                                VALUES(" + notificationsRequest.By + "," + notificationsRequest.To + ",'" + notificationsRequest.Text + "'," + notificationsRequest.AppId + ",0,0" + notificationsRequest.NotificationType + "," + notificationsRequest.MessageType + ",'" + notificationsRequest.Url + "',GETDATE())";

        //            SqlCommand command = new SqlCommand(insertQuery, connection);
        //            await command.ExecuteNonQueryAsync();

        //        }
        //    }
        //    catch (Exception e)
        //    {

        //        var msg = e.Message;
        //    }

        //}

        //public async Task<bool> SentMailWithoutAuthenticationAsync(MailRequest mailRequest)
        //{
        //    bool IsMailSent = false;
        //    MailLogRequest log = new MailLogRequest();

        //    try
        //    {
        //        MimeMessage message = new MimeMessage();

        //        string aWSEmailId = AppConstants.AwsEmailId;
        //        string account = AppConstants.AccountName;
        //        string password = AppConstants.Password;
        //        int port = AppConstants.Port;

        //        string host = AppConstants.Host;
        //        string environment = "Dev";


        //        if (string.IsNullOrWhiteSpace(mailRequest.MailFrom) && mailRequest.MailFrom == "")
        //        {
        //            MailboxAddress from = new MailboxAddress("UnlockOKR", aWSEmailId);
        //            message.From.Add(from);
        //        }
        //        else
        //        {
        //            var isMailExist = IsMailExist(mailRequest.MailFrom);
        //            if (isMailExist != null)
        //            {
        //                MailboxAddress mailboxAddress = new MailboxAddress("User", mailRequest.MailFrom);
        //                message.From.Add(mailboxAddress);
        //            }
        //        }

        //        MailboxAddress From = new MailboxAddress("UnlockOKR", aWSEmailId);
        //        message.From.Add(From);


        //        if (environment != "LIVE")
        //        {
        //            mailRequest.Subject = mailRequest.Subject + " - " + environment + " This mail is for " + mailRequest.MailTo;

        //            var emails = await GetEmailAddress();
        //            foreach (var address in emails)
        //            {
        //                var emailAddress = new MailboxAddress(address.FullName, address.EmailAddress);
        //                message.To.Add(emailAddress);
        //            }
        //            MailboxAddress CC = new MailboxAddress("alok.parhi@compunneldigital.com");
        //            message.Cc.Add(CC);
        //        }

        //        else if (environment == "LIVE")
        //        {
        //            string[] strTolist = mailRequest.MailTo.Split(';');

        //            foreach (var item in strTolist)
        //            {
        //                MailboxAddress mailto = new MailboxAddress(item);
        //                message.To.Add(mailto);
        //            }


        //            if (mailRequest.Bcc != "")
        //            {
        //                string[] strbcclist = mailRequest.CC.Split(';');
        //                foreach (var item in strbcclist)
        //                {
        //                    MailboxAddress bcc = new MailboxAddress(item);
        //                    message.Bcc.Add(bcc);
        //                }
        //            }

        //            if (mailRequest.CC != "")
        //            {
        //                string[] strCcList = mailRequest.CC.Split(';');
        //                foreach (var item in strCcList)
        //                {
        //                    MailboxAddress CC = new MailboxAddress(item);
        //                    message.Cc.Add(CC);
        //                }
        //            }
        //        }


        //        message.Subject = mailRequest.Subject;
        //        BodyBuilder bodyBuilder = new BodyBuilder();
        //        bodyBuilder.HtmlBody = mailRequest.Body;
        //        message.Body = bodyBuilder.ToMessageBody();

        //        if (message.Subject != "")
        //        {
        //            SmtpClient client = new SmtpClient();
        //            client.Connect(host, port, false);
        //            client.Authenticate(account, password);
        //            client.Send(message);
        //            client.Disconnect(true);
        //            client.Dispose();
        //            log.MailTo = mailRequest.MailTo;


        //        }

        //    }
        //    catch (Exception e)
        //    {

        //        var msg = e.Message;


        //    }

        //    return IsMailSent;
        //}

        //public async Task<List<Emails>> GetEmailAddress()
        //{
        //    List<Emails> emails = new List<Emails>();
        //    using (SqlConnection connection = new SqlConnection(Environment.GetEnvironmentVariable("SqlConnectionStringNotification")))
        //    {
        //        connection.Open();
        //        var query = @"Select * from Emails";
        //        SqlCommand command = new SqlCommand(query, connection);
        //        var reader = await command.ExecuteReaderAsync();
        //        while (reader.Read())
        //        {
        //            Emails email = new Emails()
        //            {
        //                Id = (int)reader["Id"],
        //                EmailAddress = reader["EmailAddress"].ToString(),
        //                FullName = reader["FullName"].ToString(),

        //            };
        //            emails.Add(email);
        //        }

        //    }

        //    return emails;
        //}

        //public async Task<MailSetupConfig> IsMailExist(string emailId)
        //{

        //    var data = new MailSetupConfig();

        //    using (SqlConnection connection = new SqlConnection(Environment.GetEnvironmentVariable("SqlConnectionStringNotification")))
        //    {
        //        connection.Open();
        //        var query = @"select * from MailSetupConfig where AwsemailId ='" + emailId + "' and isActive = 1";
        //        SqlCommand command = new SqlCommand(query, connection);
        //        var reader = await command.ExecuteReaderAsync();
        //        while (reader.Read())
        //        {
        //            MailSetupConfig email = new MailSetupConfig()
        //            {
        //                MailSetupConfigId = (long)reader["MailSetupConfigId"],
        //                AwsemailId = reader["AwsemailId"].ToString(),
        //                AccountName = reader["AccountName"].ToString(),
        //                AccountPassword = reader["AccountPassword"].ToString(),

        //            };
        //            data = email;
        //        }

        //    }
        //    return data;
        //}

        //public async Task<MailerTemplate> GetMailerTemplate(string templateCode)
        //{
        //    var data = new MailerTemplate();
        //    using (SqlConnection connection = new SqlConnection(Environment.GetEnvironmentVariable("SqlConnectionStringNotification")))
        //    {
        //        connection.Open();
        //        var query = @"select * from MailerTemplate where templateCode = '" + templateCode + "' and isActive = 1";
        //        SqlCommand command = new SqlCommand(query, connection);
        //        var reader = await command.ExecuteReaderAsync();
        //        while (reader.Read())
        //        {
        //            MailerTemplate template = new MailerTemplate()
        //            {
        //                Id = (long)reader["Id"],
        //                TemplateName = reader["TemplateName"].ToString(),
        //                TemplateCode = reader["TemplateCode"].ToString(),
        //                Subject = reader["Subject"].ToString(),
        //                Body = reader["Body"].ToString(),
        //                CreatedBy = (long)reader["CreatedBy"],
        //                CreatedOn = (DateTime)reader["CreatedOn"],
        //                IsActive = (bool)reader["IsActive"],

        //            };
        //            data = template;
        //        }

        //    }
        //    return data;
        //}
    }

    }

