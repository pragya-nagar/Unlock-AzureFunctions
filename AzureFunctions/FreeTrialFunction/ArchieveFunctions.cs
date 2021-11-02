using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Management.Dns;
using Microsoft.Azure.Management.Dns.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Auth;
using Microsoft.Identity.Client;
using Microsoft.Rest.Azure.Authentication;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Azure;
using System.Threading;
using AzureFunctions.FreeTrialFunction.Interface;
using AzureFunctions.Models.ArchieveAutomation;
using Dapper;

namespace AzureFunctions.FreeTrialFunction
{
    public class ArchieveFunctions : IArchieveFunctions
    {
        public async Task<IActionResult> GetExpiredDomains(ILogger log, InnerRequestModel innerRequestModel, CredentialsModel credentialsModel, int days, bool? isActive = false)
        {
            var list = new List<TenantMaster>();
            try
            {
                log.LogInformation("Start getting expired domains list");
                await using (var connection = new SqlConnection(credentialsModel.TenantConnectionString))
                {
                    connection.Open();
                    var queryTenantMaster = "";
                    if (isActive == true) // only active record for notification function
                    {
                        queryTenantMaster = $"SELECT * FROM TenantMaster WHERE IsLicensed = 0 AND IsActive = 1 AND CAST(CreatedOn AS DATE) < CAST(DATEADD(DAY, -" + days + ", GETDATE()) AS DATE) AND DemoExpiryDate < GETDATE()";
                    }
                    else
                    {
                        queryTenantMaster = $"SELECT * FROM TenantMaster WHERE IsLicensed = 0 AND CAST(CreatedOn AS DATE) < CAST(DATEADD(DAY, -" + days + ", GETDATE()) AS DATE) AND DemoExpiryDate < GETDATE()";
                    }

                    await using (var command = new SqlCommand(queryTenantMaster, connection))
                    {
                        command.CommandTimeout = 180;
                        var reader = command.ExecuteReaderAsync().Result;
                        if (reader != null)
                        {
                            var dataTable = new DataTable();
                            dataTable.Load(reader);
                            if (dataTable.Rows.Count > 0)
                            {
                                var serializedMyObjects = JsonConvert.SerializeObject(dataTable);
                                list = (List<TenantMaster>)JsonConvert.DeserializeObject(serializedMyObjects, typeof(List<TenantMaster>));
                                innerRequestModel.TenantMaster = list;
                                innerRequestModel.SubDomains = list.Select(x => x.SubDomain).ToList();
                                innerRequestModel.TenantIds = list.Select(x => x.TenantId).ToList();
                            }
                        }
                    }
                    //get all registered emails
                    if (innerRequestModel.TenantIds.Count > 0)
                    {
                        string tenantIdList = string.Join("','", innerRequestModel.TenantIds.ToArray());
                        var queryTenantUserDetails = $"SELECT * FROM TenantUserDetails WHERE TenantId IN ('" + tenantIdList + "')";
                        await using (var command = new SqlCommand(queryTenantUserDetails, connection))
                        {
                            command.CommandTimeout = 180;
                            var reader = command.ExecuteReaderAsync().Result;
                            if (reader != null)
                            {
                                var dataTable = new DataTable();
                                dataTable.Load(reader);
                                if (dataTable.Rows.Count > 0)
                                {
                                    var serializedMyObjects = JsonConvert.SerializeObject(dataTable);
                                    var userDetails = (List<TenantUserDetails>)JsonConvert.DeserializeObject(serializedMyObjects, typeof(List<TenantUserDetails>));
                                    innerRequestModel.Emails = userDetails.Select(x => x.EmailId).ToList();
                                }
                            }
                        }
                    }
                }
                log.LogInformation("End getting expired domains list");
            }
            catch (Exception e)
            {
                log.LogInformation(e.Message);
            }
            return new OkObjectResult("Getting expired domains list - Ok");
        }

        public async Task<IActionResult> DeleteTenantDbScript(ILogger log, InnerRequestModel innerRequestModel, CredentialsModel credentialsModel)
        {
            try
            {
                log.LogInformation("Start Delete TenantDb Script");
                string domainList = string.Join("','", innerRequestModel.SubDomains.ToArray());
                log.LogInformation("domainsList to be deleted: " + domainList);

                string tenantIdList = string.Join("','", innerRequestModel.TenantIds.ToArray());
                log.LogInformation("tenantIdList to be deleted: " + tenantIdList);

                //update Tenant database
                await using (var connection = new SqlConnection(credentialsModel.TenantConnectionString))
                {
                    connection.Open();
                    var queryTenantMaster = $"DELETE FROM TenantMaster WHERE SubDomain IN ('" + domainList + "')";
                    var queryTenantUserDetails = $"DELETE FROM TenantUserDetails WHERE TenantId IN ('" + tenantIdList + "')";
                    await using (var commandTenantUserDetails = new SqlCommand(queryTenantUserDetails, connection))
                    {
                        commandTenantUserDetails.CommandTimeout = 180;
                        await commandTenantUserDetails.ExecuteNonQueryAsync();
                    }
                    await using (var commandTenantMaster = new SqlCommand(queryTenantMaster, connection))
                    {
                        commandTenantMaster.CommandTimeout = 180;
                        await commandTenantMaster.ExecuteNonQueryAsync();
                    }
                }

                //update OkrTrial database
                await using (var connection = new SqlConnection(credentialsModel.OkrTrialConnectionString))
                {
                    connection.Open();
                    var queryTrialDetails = $"UPDATE TrialDetails SET StatusCode=2, IsActive=0, ExpiryDate = GETDATE() WHERE SubDomain IN ('" + domainList + "')"; //statusCode=2 for deleted record
                    await using (var commandTrialDetails = new SqlCommand(queryTrialDetails, connection))
                    {
                        commandTrialDetails.CommandTimeout = 180;
                        await commandTrialDetails.ExecuteNonQueryAsync();
                    }
                }
                log.LogInformation("End Delete TenantDb Script");
            }
            catch (Exception e)
            {
                log.LogInformation(e.Message);
            }
            return new OkObjectResult("Delete TenantDb Script - Ok");
        }
        public async Task<IActionResult> DeleteDatabase(ILogger log, InnerRequestModel innerRequestModel, CredentialsModel credentialsModel)
        {

            try
            {
                //get all db list
                var dbList = new List<FunctionDb>();
                await using (var conn = new SqlConnection(credentialsModel.TenantConnectionString))
                {
                    conn.Open();
                    var queryFunctionDbList = $"SELECT DBName,ScriptName,ConnectionServiceName FROM FunctionDb WHERE IsActive = 1";
                    var functionDbList = await conn.QueryAsync<FunctionDb>(queryFunctionDbList);
                    dbList = functionDbList.ToList();
                }
                log.LogInformation("All database List: " + dbList);

                await using var connection = new SqlConnection("Server=" + credentialsModel.DBServerName + ";Initial Catalog=Master;Persist Security Info=False;User ID=" + credentialsModel.DBServerUserId + ";Password=" + credentialsModel.DBServerPassword + ";MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=60;");
                connection.Open();

                foreach (var tenantsId in innerRequestModel.TenantIds)
                {
                    for (int i = 0; i < dbList.Count; i++)
                    {
                        var dbName = dbList[i].DBName + "_" + tenantsId;
                        if (await IsDbExists(dbName, connection))
                        {
                            log.LogInformation("DB Deletion Start: " + dbName);
                            var queryFeedback = $" DROP DATABASE [" + dbName + "] ";
                            await using (var commandFeedback = new SqlCommand(queryFeedback, connection))
                            {
                                commandFeedback.CommandTimeout = 180;
                                await commandFeedback.ExecuteNonQueryAsync();
                            }
                            log.LogInformation("DB Deletion End " + dbName);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                log.LogInformation("Database Creation Error - " + e.StackTrace);
            }
            return new OkObjectResult("Database Creation Done - Ok");
        }
        public async Task<IActionResult> DeleteAppRegistration(ILogger log, InnerRequestModel innerRequestModel, CredentialsModel credentialsModel)
        {
            log.LogInformation("Delete AppRegistration: C# Timer trigger function - AppRegistration delete processed a request.");
            try
            {
                var graphClient = GetGraphServiceClient(credentialsModel);
                var application = await graphClient.Applications[credentialsModel.ObjectId].Request().GetAsync();
                var ListRedirectUris = application.Spa.RedirectUris.ToList();

                foreach (var item in innerRequestModel.SubDomains)
                {
                    var httpsSubDomain = "https://" + item;
                    ListRedirectUris.Remove(httpsSubDomain + "/secretlogin");
                    ListRedirectUris.Remove(httpsSubDomain + "/logout");
                }

                var removeRedirectinApp = new Application()
                {
                    Spa = new SpaApplication() { RedirectUris = ListRedirectUris }
                };
                await graphClient.Applications[credentialsModel.ObjectId].Request().UpdateAsync(removeRedirectinApp);
            }
            catch (Exception e)
            {
                log.LogInformation("App Registration Error -" + e.Message);
            }
            return new OkObjectResult("App Registration Done");
        }
        public async Task<IActionResult> DeleteContainer(ILogger log, InnerRequestModel innerRequestModel, CredentialsModel credentialsModel)
        {
            string tenantIdList = string.Join("','", innerRequestModel.TenantIds.ToArray());
            log.LogInformation("DeleteContainer tenantIdList to be deleted: " + tenantIdList);
            try
            {
                var account = new CloudStorageAccount(new StorageCredentials(credentialsModel.StorageName, credentialsModel.StorageKey), true);
                var cloudBlobClient = account.CreateCloudBlobClient();
                foreach (var tenantId in innerRequestModel.TenantIds)
                {
                    log.LogInformation("tenantId to be deleted: " + tenantId);
                    var cloudBlobContainer = cloudBlobClient.GetContainerReference(tenantId.ToString().ToLower());
                    await cloudBlobContainer.DeleteIfExistsAsync();
                }
            }
            catch (Exception e)
            {
                log.LogInformation(e.Message);
            }
            return new OkObjectResult("Delete Container - Ok");
        }
        public async Task<IActionResult> DeleteAdUser(ILogger log, InnerRequestModel innerRequestModel, CredentialsModel credentialsModel)
        {
            log.LogInformation("C# timer trigger function - DeleteAdUser processed a request.");
            try
            {
                string emailList = string.Join("','", innerRequestModel.Emails.ToArray());
                log.LogInformation("emailList to be deleted from Ad: " + emailList);

                var graphClient = GetGraphServiceClient(credentialsModel);
                var filterString = $"userType eq 'guest' and (mail eq ";
                var filterCondition = ""; var isUserForDelete = false;

                for (int i = 0; i < innerRequestModel.Emails.Count; i++)
                {
                    using (var connection = new SqlConnection(credentialsModel.TenantConnectionString))
                    {
                        connection.Open();
                        var queryTenantUserDetails = $"SELECT * FROM TenantUserDetails WHERE EmailId = '" + innerRequestModel.Emails[i] + "'";
                        using (var command = new SqlCommand(queryTenantUserDetails, connection))
                        {
                            command.CommandTimeout = 180;
                            var reader = command.ExecuteReaderAsync().Result;
                            if (reader != null)
                            {
                                var dataTable = new DataTable();
                                dataTable.Load(reader);
                                if (dataTable.Rows.Count <= 0)
                                {
                                    isUserForDelete = true;
                                    if (string.IsNullOrEmpty(filterCondition))
                                        filterCondition += "'" + innerRequestModel.Emails[i] + "'";
                                    else
                                        filterCondition += " or mail eq '" + innerRequestModel.Emails[i] + "'";
                                }
                                else
                                {
                                    log.LogInformation("Email Id can not be deleted from Ad as it is registered for another domain: " + innerRequestModel.Emails[i]);
                                }
                            }
                        }
                    }
                }
                var filter = filterString + filterCondition + ")";
                log.LogInformation("DeleteAdUser filterString: " + filter);
                log.LogInformation("DeleteAdUser isUserForDelete: " + isUserForDelete);
                if (isUserForDelete)
                {
                    var request = await graphClient.Users.Request().Filter(filter).GetAsync();
                    var guestUsers = request.CurrentPage.ToList();
                    var users = guestUsers.ToList();
                    foreach (var user in users)
                    {
                        await graphClient.Users[user.Id].Request().DeleteAsync();
                        log.LogInformation("Email Id deleted from Ad: " + user.Mail);
                    }
                }
            }
            catch (Exception e)
            {
                log.LogInformation(" Delete Ad User Error - " + e.Message);
            }
            return new OkObjectResult("Delete Ad User Done");
        }

        public async Task<IActionResult> DeleteSubDomain(ILogger log, InnerRequestModel innerRequestModel, CredentialsModel credentialsModel)
        {
            log.LogInformation("Start Delete SubDomain");
            string domainList = string.Join("','", innerRequestModel.SubDomains.ToArray());
            log.LogInformation("domainsList to be deleted: " + domainList);

            try
            {
                var serviceCredentials = await ApplicationTokenProvider.LoginSilentAsync(credentialsModel.TenantId, credentialsModel.ClientId, credentialsModel.ClientSecretId);
                var dnsClient = new DnsManagementClient(serviceCredentials) { SubscriptionId = credentialsModel.SubscriptionId };

                foreach (var domain in innerRequestModel.SubDomains)
                {
                    log.LogInformation("subDomain to be deleted: " + domain);
                    var subDomain = domain.Split('.')[0];
                    var isExist = await HasDomainNameAsync(credentialsModel, subDomain ?? domain, dnsClient);
                    log.LogInformation("isExist: " + isExist);
                    if (isExist)
                    {
                        await dnsClient.RecordSets.DeleteAsync(credentialsModel.ResourceGroupName, credentialsModel.Domain, subDomain, RecordType.CNAME);
                    }
                }
            }
            catch (Exception e)
            {
                log.LogInformation("SubDomain - failed: {0}", e.Message);
            }
            return new OkObjectResult("Delete SubDomain - Ok");
        }

        public async Task<IActionResult> DeleteKeyVaultSecrets(ILogger log, InnerRequestModel innerRequestModel, CredentialsModel credentialsModel)
        {

            log.LogInformation("Key vault secrets deletion started");
            try
            {
                foreach (var tenantId in innerRequestModel.TenantIds)
                {
                    var credentials = new ClientSecretCredential(credentialsModel.TenantId, credentialsModel.ClientId, credentialsModel.ClientSecretId);
                    var client = new SecretClient(new Uri(credentialsModel.KeyVaultUrl), credentials);

                    var connectionAdmin = tenantId + "-ConnectionAdminService";
                    var connectionOkr = tenantId + "-ConnectionOkrService";
                    var connectionFeedback = tenantId + "-ConnectionFeedbackService";
                    var connectionNotification = tenantId + "-ConnectionNotificationService";


                    AsyncPageable<SecretProperties> allSecrets = client.GetPropertiesOfSecretsAsync();
                    await foreach (SecretProperties secretProperties in allSecrets)
                    {
                        //log.LogInformation("DeleteKeyVaultSecrets secretProperties.Name: " + secretProperties.Name);
                        if (secretProperties.Name == connectionAdmin)
                        {
                            DeleteSecretOperation operation = client.StartDeleteSecret(connectionAdmin);
                            DeletedSecret secret = operation.Value;
                            // You should call `UpdateStatus` in another thread or after doing additional work like pumping messages.
                            while (!operation.HasCompleted)
                            {
                                Thread.Sleep(2000);
                                operation.UpdateStatus();
                            }
                            client.PurgeDeletedSecret(secret.Name);
                            log.LogInformation("DeleteKeyVaultSecrets connectionAdmin: " + connectionAdmin);
                        }
                        if (secretProperties.Name == connectionOkr)
                        {
                            DeleteSecretOperation operation = client.StartDeleteSecret(connectionOkr);
                            while (!operation.HasCompleted)
                            {
                                Thread.Sleep(2000);
                                operation.UpdateStatus();
                            }
                            DeletedSecret secret = operation.Value;
                            client.PurgeDeletedSecret(secret.Name);
                            log.LogInformation("DeleteKeyVaultSecrets connectionOkr: " + connectionOkr);
                        }
                        if (secretProperties.Name == connectionFeedback)
                        {
                            DeleteSecretOperation operation = client.StartDeleteSecret(connectionFeedback);
                            while (!operation.HasCompleted)
                            {
                                Thread.Sleep(2000);
                                operation.UpdateStatus();
                            }
                            DeletedSecret secret = operation.Value;
                            client.PurgeDeletedSecret(secret.Name);
                            log.LogInformation("DeleteKeyVaultSecrets connectionFeedback: " + connectionFeedback);
                        }
                        if (secretProperties.Name == connectionNotification)
                        {
                            DeleteSecretOperation operation = client.StartDeleteSecret(connectionNotification);
                            while (!operation.HasCompleted)
                            {
                                Thread.Sleep(2000);
                                operation.UpdateStatus();
                            }
                            DeletedSecret secret = operation.Value;
                            client.PurgeDeletedSecret(secret.Name);
                            log.LogInformation("DeleteKeyVaultSecrets connectionNotification: " + connectionNotification);
                        }
                    }
                    log.LogInformation("Key vault secrets deleted for tenant Id {0}", tenantId);
                }
            }
            catch (Exception e)
            {
                log.LogInformation("Error occurred - failed: {0}", e.Message);
            }
            return new OkObjectResult("Key vault secrets deletion Completed");
        }

        public async Task<CredentialsModel> GetEnvironmentVariable(ILogger log)
        {
            var credentialsModel = new CredentialsModel
            {
                Environment = Environment.GetEnvironmentVariable("Environment", EnvironmentVariableTarget.Process),
                ClientId = Environment.GetEnvironmentVariable("ClientId", EnvironmentVariableTarget.Process),
                ClientSecretId = Environment.GetEnvironmentVariable("ClientSecretId", EnvironmentVariableTarget.Process),
                CNameRecord = Environment.GetEnvironmentVariable("CNameRecord", EnvironmentVariableTarget.Process),
                Domain = Environment.GetEnvironmentVariable("Domain", EnvironmentVariableTarget.Process),
                KeyVaultUrl = Environment.GetEnvironmentVariable("KeyVaultUrl", EnvironmentVariableTarget.Process),
                ObjectId = Environment.GetEnvironmentVariable("ObjectId", EnvironmentVariableTarget.Process),
                ResourceGroupName = Environment.GetEnvironmentVariable("ResourceGroupName", EnvironmentVariableTarget.Process),
                SubscriptionId = Environment.GetEnvironmentVariable("SubscriptionId", EnvironmentVariableTarget.Process),
                TenantConnectionString = Environment.GetEnvironmentVariable("TenantConnectionString", EnvironmentVariableTarget.Process),
                OkrTrialConnectionString = Environment.GetEnvironmentVariable("OkrTrialConnectionString", EnvironmentVariableTarget.Process),
                TenantId = Environment.GetEnvironmentVariable("TenantId", EnvironmentVariableTarget.Process),
                TTL = Environment.GetEnvironmentVariable("TTL", EnvironmentVariableTarget.Process),
                StorageName = Environment.GetEnvironmentVariable("StorageName", EnvironmentVariableTarget.Process),
                StorageKey = Environment.GetEnvironmentVariable("StorageKey", EnvironmentVariableTarget.Process),
                InviteMessage = Environment.GetEnvironmentVariable("InviteMessage", EnvironmentVariableTarget.Process),
                //DomainCreationMessage = Environment.GetEnvironmentVariable("DomainCreationMessage", EnvironmentVariableTarget.Process),
                //SMTPAWSEmailID = Environment.GetEnvironmentVariable("SMTPAWSEmailID", EnvironmentVariableTarget.Process),
                //SMTPAccountName = Environment.GetEnvironmentVariable("SMTPAccountName", EnvironmentVariableTarget.Process),
                //SMTPPassword = Environment.GetEnvironmentVariable("SMTPPassword", EnvironmentVariableTarget.Process),
                //SMTPPort = Environment.GetEnvironmentVariable("SMTPPort", EnvironmentVariableTarget.Process),
                //SMTPHost = Environment.GetEnvironmentVariable("SMTPHost", EnvironmentVariableTarget.Process),
                DBServerPassword = Environment.GetEnvironmentVariable("DBServerPassword", EnvironmentVariableTarget.Process),
                DBServerName = Environment.GetEnvironmentVariable("DBServerName", EnvironmentVariableTarget.Process),
                DBServerUserId = Environment.GetEnvironmentVariable("DBServerUserId", EnvironmentVariableTarget.Process),
                InfraDeletionDays = Environment.GetEnvironmentVariable("InfraDeletionDays", EnvironmentVariableTarget.Process),
                JourneyEndingDays = Environment.GetEnvironmentVariable("JourneyEndingDays", EnvironmentVariableTarget.Process)

            };
            log.LogInformation("TenantID - " + credentialsModel.TenantId);
            log.LogInformation("ClientID - " + credentialsModel.ClientId);
            log.LogInformation("ClientSecretID - " + credentialsModel.ClientSecretId);
            log.LogInformation("Environment - " + credentialsModel.Environment);
            log.LogInformation("CNameRecord - " + credentialsModel.CNameRecord);
            log.LogInformation("Domain - " + credentialsModel.Domain);
            log.LogInformation("KeyVaultUrl - " + credentialsModel.KeyVaultUrl);
            log.LogInformation("ObjectId - " + credentialsModel.ObjectId);
            log.LogInformation("ResourceGroupName - " + credentialsModel.ResourceGroupName);
            log.LogInformation("StorageKey - " + credentialsModel.StorageKey);
            log.LogInformation("StorageName - " + credentialsModel.StorageName);
            log.LogInformation("SubscriptionId - " + credentialsModel.SubscriptionId);
            log.LogInformation("TenantConnectionString - " + credentialsModel.TenantConnectionString);
            log.LogInformation("TTL - " + credentialsModel.TTL);
            log.LogInformation("DBServerName - " + credentialsModel.DBServerName);
            log.LogInformation("DBServerPassword - " + credentialsModel.DBServerPassword);
            log.LogInformation("DBServerUserId - " + credentialsModel.DBServerUserId);
            log.LogInformation("InfraDeletionDays - " + credentialsModel.InfraDeletionDays);
            log.LogInformation("JourneyEndingDays - " + credentialsModel.JourneyEndingDays);
            return credentialsModel;
        }

        #region Test Method
        public async Task<CredentialsModel> GetEnvironmentVariable1(ILogger log, string environment)
        {
            if (environment == "PROD")
            {
                var credentialsModel = new CredentialsModel
                {
                    Environment = "PROD",
                    ClientId = "ca2caa9d-b491-4b46-ae6f-a2f55538537d",
                    ClientSecretId = "-E3kxajgdo39.-VuZiso0gMx~_7yEnp8rp",
                    CNameRecord = "unlockokr-ui-prod.azurewebsites.net",
                    Domain = "unlockokr.com",
                    KeyVaultUrl = "https://unlockokr-vault-prod.vault.azure.net/",
                    ObjectId = "eff95352-0e7f-4c11-8ea3-7cb83ac1c58b",
                    ResourceGroupName = "unlockokr-prod",
                    SubscriptionId = "a8b508b6-da16-4c45-84f5-cac5c9f57513",
                    TenantConnectionString =
                "Server=unlockokr-db-prod.database.windows.net;Initial Catalog= Tenants;Persist Security Info=False;User ID=unlockokr-db-prod-admin;Password=E6gzeY9FMliqQ5Fi;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=60;",
                    OkrTrialConnectionString = "Server=unlockokr-db-prod.database.windows.net;Initial Catalog= OKR_Trial;Persist Security Info=False;User ID=unlockokr-db-prod-admin;Password=E6gzeY9FMliqQ5Fi;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=60;",
                    TenantId = "e648628f-f65c-40cc-8a28-c601daf26a89",
                    TTL = "60",
                    StorageName = "unlockokrblobprod",
                    StorageKey = "gdbV6ZH3PACccAH5PTuxGKvbyTqe2DGfHXDT24+APYoDco7iAqqJvz4XlxHV/tV18VlmfNM7up8KmPKoZwFNxg==",
                    InviteMessage = "Dear <user>,\n\n You are invited to join Unlock OKR software. It is integrated and intelligent platform to collaborate and achieve organizations OKRs. You will be delighted to explore and adopt OKRs by leveraging world-class technology that is as powerful as it is intuitive. Please login with your Domain Credentials. \n\n Example- Ms. Jen is working at Compunnel with domain-id: Jen@compunnel.com & Password: Use domain password. \n\n Click on the below accept invitation button to join the platform.",
                    //DomainCreationMessage = "<!DOCTYPE html> <html><body> <table bgcolor=\"#fff\" border=\"0\" cellpadding=\"0\" cellspacing=\"0\" width=\"100%\"><tbody><tr><td align=\"center\"><table border=\"0\" cellpadding=\"0\" cellspacing=\"0\" class=\"wrapper\" style=\"background: #f9f9f9\" width=\"750\"><tbody><tr><td align=\"center\" style=\"width:100%;\"valign=\"top\"><table border=\"0\" cellpadding=\"0\" cellspacing=\"0\" class=\"wrapper\" style=\"background:#f9f9f9\" width=\"100%\"><tbody> <tr><td align=\"center\" style=\"padding: 10px 0 20px 0;\" valign=\"top\" class=\"\"><img alt=\"\" border=\"0\" class=\"img-responsive\" src=\"https://inspireproduction.s3.amazonaws.com/EmailerPhotos/Unlock-OKR-Logo.png\" style=\"border-style:solid; border-width:0px\"></a></td></tr><tr><td style=\"font-size: 0px\" valign=\"top\" class=\"\"><img class=\"img-responsive\" src=\"https://inspireproduction.s3.amazonaws.com/EmailerPhotos/OKR-Emailer-img4.jpg\" alt=\"\" style=\"display: block;\"></td></tr></tbody></table><table border=\"0\" cellpadding=\"0\" cellspacing=\"0\" class=\"wrapper wrapper-txt\" width=\"90%\"><tbody><tr><td class=\"\" align=\"left\"><p style=\"border-top:#DADADA 1px solid; margin-bottom: 15px; padding-top: 35px; font-weight: 500; font-size: 18px;line-height: 24px;font-family: &#39;Arial&#39;, sans-serif; color:#000;\" class=\"txt-size\"><strong>Congratulations, <user>! </strong></p></td></tr><tr><td class=\"subheadingbottom\"><p style=\"margin: 0px; font-family:&#39;Arial&#39;, sans-serif; font-size: 18px;line-height: 24px;color:#292929;padding-bottom:0;\" class=\"txt-size\">you got the invitation mail in your registered account kindly accept it and start utilizing the domain.</p></td></tr></tr></tr><tr><td class=\"subheadingbottom\"><p style=\"margin: 0px; font-family: &#39;Arial&#39;, sans-serif; font-size: 18px;line-height: 24px;color:#292929;padding-bottom:0;\" class=\"txt-size\"><strong>Your domain is: <a href='<domain>'><domain></a> </strong></p></td></tr><tr>&nbsp;</tr></tr><tr><td class=\"subheadingbottom\"><p style=\"border-top:#DADADA 1px solid; margin-bottom: 15px; padding-top: 35px; font-weight: 500; font-size: 18px;line-height: 24px;font-family: &#39;Arial&#39;, sans-serif; color:#000;\" class=\"txt-size\"></p></td></tr><tr><td class=\"subheadingbottom\"><p style=\"margin: 0px;padding-bottom:0; font-family: &#39;Arial&#39;, sans-serif; font-size: 18px;line-height: 24px;color:#292929;\" class=\"txt-size\">Regards,</p></td><tr><td class=\"subheadingbottom\"><p style=\"margin:0 0 30px 0;font-family: &#39;Arial&#39;, sans-serif; font-size: 18px;line-height: 24px;color:#292929;padding-bottom:0;\" class=\"txt-size\">The Unlock OKR Team</p></td>           </tr></tbody></table></td></tr></tbody></table></body></html>",
                    //SMTPAWSEmailID = "adminsupport@unlockokr.com",
                    //SMTPAccountName = "AKIAJVT7R6HES36CNLWQ",
                    //SMTPPassword = "AmbzlYKroTfzrc2+tXUTXYcO55HBd0EfOn1rheEma6Kp",
                    //SMTPServerName = "email-smtp.us-east-1.amazonaws.com",
                    //SMTPPort = "587",
                    //SMTPIsSSLEnabled = "false",
                    //SMTPHost = "email-smtp.us-east-1.amazonaws.com",
                    DBServerPassword = "E6gzeY9FMliqQ5Fi",
                    DBServerName = "unlockokr-db-prod.database.windows.net",
                    DBServerUserId = "unlockokr-db-prod-admin",
                    InfraDeletionDays = "30",
                    JourneyEndingDays = "14"
                };
                return credentialsModel;
            }
            else if (environment == "UAT")
            {
                var credentialsModel = new CredentialsModel
                {
                    Environment = "UAT",
                    ClientId = "ad2e6351-5e26-46e7-90bc-e449934f43e2",
                    ClientSecretId = "Ph_qb4deK3Bm.~0_4-JsPsR6p~~Dw56q-l",
                    CNameRecord = "unlockokr-ui-prod.azurewebsites.net",
                    Domain = "unlockokr.com",
                    KeyVaultUrl = "https://unlockokr-vault-dev.vault.azure.net/",
                    ObjectId = "8858d976-ca69-4f74-a266-a07f41e2e323",
                    ResourceGroupName = "unlockokr-prod",
                    SubscriptionId = "a8b508b6-da16-4c45-84f5-cac5c9f57513",
                    TenantConnectionString =
                   "Server=unlockokr-db-dev.database.windows.net;Initial Catalog= Tenants;Persist Security Info=False;User ID=unlockokr-db-dev-admin;Password=fbBfXENoi2WCACXK;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=60;",
                    OkrTrialConnectionString = "Server=unlockokr-db-dev.database.windows.net;Initial Catalog= OKR_Trial;Persist Security Info=False;User ID=unlockokr-db-dev-admin;Password=fbBfXENoi2WCACXK;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=60;",
                    TenantId = "e648628f-f65c-40cc-8a28-c601daf26a89",
                    TTL = "60",
                    StorageName = "unlockokrblobdev",
                    StorageKey = "oXP92O+N3SRxHT3GHUT0uhppDmbVjQYwJUB7pOY1bYdxjCV93JH/FbIaWE/hNT6obd+T9vxYGeBLkKT/DZZkow==",
                    InviteMessage = "Dear <user>,\n\n You are invited to join Unlock OKR software. It is integrated and intelligent platform to collaborate and achieve organizations OKRs. You will be delighted to explore and adopt OKRs by leveraging world-class technology that is as powerful as it is intuitive. Please login with your Domain Credentials. \n\n Example- Ms. Jen is working at Compunnel with domain-id: Jen@compunnel.com & Password: Use domain password. \n\n Click on the below accept invitation button to join the platform.",
                    DBServerPassword = "fbBfXENoi2WCACXK",
                    DBServerName = "unlockokr-db-dev.database.windows.net",
                    DBServerUserId = "unlockokr-db-dev-admin",
                    InfraDeletionDays = "30",
                    JourneyEndingDays = "14"
                };
                return credentialsModel;
            }
            else
            {
                var credentialsModel = new CredentialsModel
                {
                    Environment = "Development",
                    ClientId = "ad2e6351-5e26-46e7-90bc-e449934f43e2",
                    ClientSecretId = "Ph_qb4deK3Bm.~0_4-JsPsR6p~~Dw56q-l",
                    CNameRecord = "unlockokr-ui-prod.azurewebsites.net",
                    Domain = "unlockokr.com",
                    KeyVaultUrl = "https://unlockokr-vault-dev.vault.azure.net/",
                    ObjectId = "8858d976-ca69-4f74-a266-a07f41e2e323",
                    ResourceGroupName = "unlockokr-prod",
                    SubscriptionId = "a8b508b6-da16-4c45-84f5-cac5c9f57513",
                    TenantConnectionString =
                "Server=unlockokr-db-dev.database.windows.net;Initial Catalog= Tenants;Persist Security Info=False;User ID=unlockokr-db-dev-admin;Password=fbBfXENoi2WCACXK;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=60;",
                    OkrTrialConnectionString = "Server=unlockokr-db-dev.database.windows.net;Initial Catalog= OKR_Trial;Persist Security Info=False;User ID=unlockokr-db-dev-admin;Password=fbBfXENoi2WCACXK;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=60;",
                    TenantId = "e648628f-f65c-40cc-8a28-c601daf26a89",
                    TTL = "60",
                    StorageName = "unlockokrblobdev",
                    StorageKey = "oXP92O+N3SRxHT3GHUT0uhppDmbVjQYwJUB7pOY1bYdxjCV93JH/FbIaWE/hNT6obd+T9vxYGeBLkKT/DZZkow==",
                    InviteMessage = "Dear <user>,\n\n You are invited to join Unlock OKR software. It is integrated and intelligent platform to collaborate and achieve organizations OKRs. You will be delighted to explore and adopt OKRs by leveraging world-class technology that is as powerful as it is intuitive. Please login with your Domain Credentials. \n\n Example- Ms. Jen is working at Compunnel with domain-id: Jen@compunnel.com & Password: Use domain password. \n\n Click on the below accept invitation button to join the platform.",
                    DBServerPassword = "fbBfXENoi2WCACXK",
                    DBServerName = "unlockokr-db-dev.database.windows.net",
                    DBServerUserId = "unlockokr-db-dev-admin",
                    InfraDeletionDays = "30",
                    JourneyEndingDays = "14"
                };
                return credentialsModel;
            }
        }
        #endregion

        #region Private Method
        private GraphServiceClient GetGraphServiceClient(CredentialsModel credentialsModel)
        {
            IConfidentialClientApplication confidentialClientApplication = ConfidentialClientApplicationBuilder
                .Create(credentialsModel.ClientId)
                .WithTenantId(credentialsModel.TenantId)
                .WithClientSecret(credentialsModel.ClientSecretId)
                .Build();

            ClientCredentialProvider authProvider = new ClientCredentialProvider(confidentialClientApplication);
            GraphServiceClient graphClient = new GraphServiceClient(authProvider);
            return graphClient;
        }

        private async Task<bool> IsDbExists(string dbName, SqlConnection connection)
        {
            var dbExists = false;
            dbName = dbName.Replace("[", "").Replace("]", "");
            var query = $"SELECT * FROM SYS.SYSDATABASES WHERE NAME =" + "'" + dbName + "'";
            await using var command = new SqlCommand(query, connection);
            command.CommandTimeout = 180;
            await using var reader = await command.ExecuteReaderAsync();
            if (reader != null && reader.ReadAsync().Result)
            {
                dbExists = true;
            }
            return dbExists;
        }

        private static async Task<bool> HasDomainNameAsync(CredentialsModel credentialsModel, string dnsName, DnsManagementClient dnsClient)
        {
            var page = await dnsClient.RecordSets.ListAllByDnsZoneAsync(credentialsModel.ResourceGroupName, credentialsModel.Domain);
            while (true)
            {
                if (page.Any(x => x.Type == "Microsoft.Network/dnszones/CNAME" && string.Equals(x.Name, dnsName, StringComparison.CurrentCultureIgnoreCase)))
                {
                    return true;
                }
                if (string.IsNullOrEmpty(page.NextPageLink))
                {
                    break;
                }
                page = await dnsClient.RecordSets.ListAllByDnsZoneNextAsync(page.NextPageLink);
            }
            return false;
        }

        #endregion
    }

}
