using AzureFunctions.FreeTrialFunction.Interface;
using AzureFunctions.Models.ArchieveAutomation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace AzureFunctions.FreeTrialFunction
{
    public class NotificationFunctions : INotificationFunctions
    {
        public async Task<IActionResult> JourneyEndingFunction(ILogger log, InnerRequestModel innerRequestModel, CredentialsModel credentialsModel)
        {
            try
            {
                log.LogInformation("Start Journey Ending Function");
                string domainList = string.Join("','", innerRequestModel.SubDomains.ToArray());
                log.LogInformation("domainsList to be InActivated: " + domainList);

                //update Tenant database
                await using (var connection = new SqlConnection(credentialsModel.TenantConnectionString))
                {
                    connection.Open();
                    var queryTenantMaster = $"UPDATE TenantMaster SET IsActive=0, UpdatedBy=-1, UpdatedOn = GETDATE() WHERE SubDomain IN ('" + domainList + "')"; 
                    await using (var commandTenantMaster = new SqlCommand(queryTenantMaster, connection))
                    {
                        commandTenantMaster.CommandTimeout = 180;
                        await commandTenantMaster.ExecuteNonQueryAsync();
                    }
                }
                log.LogInformation("End Journey Ending Function");
            }
            catch (Exception e)
            {
                log.LogInformation(e.Message);
            }
            return new OkObjectResult("Journey Ending Function - Ok");
        }
        public async Task<IActionResult> SendJourneyEndingEmail(ILogger log, InnerRequestModel innerRequestModel, CredentialsModel credentialsModel)
        {
            try
            {
                log.LogInformation("Start Send Journey Ending Email");
            }
            catch (Exception e)
            {
                log.LogInformation(e.Message);
            }
            return new OkObjectResult("Send Journey Ending Email - Ok");
        }

    }
}
