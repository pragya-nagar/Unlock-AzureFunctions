using AzureFunctions.Models.ArchieveAutomation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace AzureFunctions.FreeTrialFunction.Interface
{
    public interface IArchieveFunctions
    {
        Task<CredentialsModel> GetEnvironmentVariable(ILogger log);
        Task<CredentialsModel> GetEnvironmentVariable1(ILogger log, string environment);
        Task<IActionResult> GetExpiredDomains(ILogger log, InnerRequestModel innerRequestModel, CredentialsModel credentialsModel, int days, bool? isActive = false);
        Task<IActionResult> DeleteTenantDbScript(ILogger log, InnerRequestModel innerRequestModel, CredentialsModel credentialsModel);
        Task<IActionResult> DeleteDatabase(ILogger log, InnerRequestModel innerRequestModel, CredentialsModel credentialsModel);
        Task<IActionResult> DeleteAppRegistration(ILogger log, InnerRequestModel innerRequestModel, CredentialsModel credentialsModel);
        Task<IActionResult> DeleteContainer(ILogger log, InnerRequestModel innerRequestModel, CredentialsModel credentialsModel);
        Task<IActionResult> DeleteSubDomain(ILogger log, InnerRequestModel innerRequestModel, CredentialsModel credentialsModel);
        Task<IActionResult> DeleteAdUser(ILogger log, InnerRequestModel innerRequestModel, CredentialsModel credentialsModel);
        Task<IActionResult> DeleteKeyVaultSecrets(ILogger log, InnerRequestModel innerRequestModel, CredentialsModel credentialsModel);
    }
}
