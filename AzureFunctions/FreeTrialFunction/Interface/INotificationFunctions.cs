using AzureFunctions.Models.ArchieveAutomation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace AzureFunctions.FreeTrialFunction.Interface
{
    public interface INotificationFunctions
    {
        Task<IActionResult> JourneyEndingFunction(ILogger log, InnerRequestModel innerRequestModel, CredentialsModel credentialsModel);
        Task<IActionResult> SendJourneyEndingEmail(ILogger log, InnerRequestModel innerRequestModel, CredentialsModel credentialsModel);

    }
}
