using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using AzureFunctions.Common;
using AzureFunctions.FreeTrialFunction.Interface;
using AzureFunctions.Models;
using AzureFunctions.Models.ArchieveAutomation;
using AzureFunctions.Repository.Interfaces;
using FluentDateTime;
using Humanizer;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AzureFunctions
{
    public class Function : FunctionBase
    {
        private readonly IAdminRepository _adminDataRepository;
        private readonly IOkrServiceRepository _okrServiceDataRepository;
        private readonly INotificationRepository _notificationsAndEmails;
        private readonly IConfiguration _configuration;
        private readonly IArchieveFunctions _archieveFunctions;
        private readonly INotificationFunctions _notificationFunctions;

        public Function() : base()
        {
            _adminDataRepository = _serviceProvider.GetRequiredService<IAdminRepository>();
            _okrServiceDataRepository = _serviceProvider.GetRequiredService<IOkrServiceRepository>();
            _notificationsAndEmails = _serviceProvider.GetRequiredService<INotificationRepository>();
            _configuration = _serviceProvider.GetRequiredService<IConfiguration>();
            _archieveFunctions = _serviceProvider.GetRequiredService<IArchieveFunctions>();
            _notificationFunctions = _serviceProvider.GetRequiredService<INotificationFunctions>();
        }

        #region Free Trial

        #region Notification
        [FunctionName("FreeTrialJourneyEnding")]
        public async Task FreeTrialJourneyEnding([TimerTrigger("0 30 18 * * *")] TimerInfo myTimer, ILogger log)
        {
            log.LogInformation($"C# Timer trigger JourneyEnding executed at: {DateTime.Now}");
            var credentialsModel = await _archieveFunctions.GetEnvironmentVariable(log);
            var innerRequestModel = new InnerRequestModel();

            log.LogInformation(" Get JourneyEnding Domains List Start ");
            await _archieveFunctions.GetExpiredDomains(log, innerRequestModel, credentialsModel, Convert.ToInt32(credentialsModel.JourneyEndingDays), true);
            log.LogInformation(" Get JourneyEnding Domains List End ");

            if (innerRequestModel.TenantMaster != null && innerRequestModel.TenantMaster.Count > 0)
            {
                log.LogInformation(" Journey Ending Function Start ");
                await _notificationFunctions.JourneyEndingFunction(log, innerRequestModel, credentialsModel);
                log.LogInformation(" Journey Ending Function End ");

                log.LogInformation(" Send Journey Ending Email Start ");
                await _notificationFunctions.SendJourneyEndingEmail(log, innerRequestModel, credentialsModel);
                log.LogInformation(" Send Journey Ending Email End ");
            }
            else
            {
                log.LogInformation(" No JourneyEnding domains found!");
            }
        }
        #endregion

        #region Archieve Automation
        [FunctionName("ArchieveAutomation")]
        public async Task ArchieveAutomation([TimerTrigger("0 30 18 * * *")] TimerInfo myTimer, ILogger log) //0 */5 * * * *
        {
            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
            var credentialsModel = await _archieveFunctions.GetEnvironmentVariable(log);
            var innerRequestModel = new InnerRequestModel();

            log.LogInformation(" Get Expired Domains List Start ");
            await _archieveFunctions.GetExpiredDomains(log, innerRequestModel, credentialsModel, Convert.ToInt32(credentialsModel.InfraDeletionDays));
            log.LogInformation(" Get Expired Domains List End ");

            if (innerRequestModel.TenantMaster != null && innerRequestModel.TenantMaster.Count > 0)
            {
                log.LogInformation(" Delete SubDomain Start ");
                await _archieveFunctions.DeleteSubDomain(log, innerRequestModel, credentialsModel);
                log.LogInformation(" Delete SubDomain End ");

                log.LogInformation(" Delete AppRegistration Start ");
                await _archieveFunctions.DeleteAppRegistration(log, innerRequestModel, credentialsModel);
                log.LogInformation(" Delete AppRegistration End ");

                log.LogInformation(" Container Deletion Start ");
                await _archieveFunctions.DeleteContainer(log, innerRequestModel, credentialsModel);
                log.LogInformation(" Container Deletion End ");

                log.LogInformation(" Delete Key Vault Secret ");
                await _archieveFunctions.DeleteKeyVaultSecrets(log, innerRequestModel, credentialsModel);
                log.LogInformation(" Deleted Key Vault Secret");

                log.LogInformation(" Database Deletion Start ");
                await _archieveFunctions.DeleteDatabase(log, innerRequestModel, credentialsModel);
                log.LogInformation(" Database Deletion End ");

                log.LogInformation(" Delete TenantDbScript Start ");
                await _archieveFunctions.DeleteTenantDbScript(log, innerRequestModel, credentialsModel);
                log.LogInformation(" Delete TenantDbScript End ");

                log.LogInformation(" Delete Ad User Start ");
                await _archieveFunctions.DeleteAdUser(log, innerRequestModel, credentialsModel);
                log.LogInformation(" Delete Ad User End ");

            }
            else
            {
                log.LogInformation(" No domains found!");
            }
        }
        #endregion

        #endregion


        #region OKR
        [FunctionName("UpdateSource")]
        public async Task UpdateSource([TimerTrigger("0 30 18 * * *")] TimerInfo myTimer, ILogger log)
        {
            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");

            var organisations = await _adminDataRepository.GetOrganisationsData();
            var userDetails = await _adminDataRepository.GetAdminData();

            if (organisations != null)
            {
                foreach (var item in organisations)
                {
                    ////Will fetch active organisationCycle
                    var cycle = await _adminDataRepository.GetOrganisationCycles(item.OrganisationId);
                    foreach (var cycleItem in cycle)
                    {
                        ////will find which cycle is active now
                        bool isCurrentCycle = cycleItem.CycleStartDate <= DateTime.UtcNow && cycleItem.CycleEndDate >= DateTime.UtcNow;
                        if (isCurrentCycle)
                        {
                            ////we are getting the dates on which mail should be send to source in planning session
                            var goalUnlockDate = await _adminDataRepository.GetGoalUnlockDateData();
                            var goalLockedDate = goalUnlockDate.Where(x => x.OrganisationCycleId == cycleItem.OrganisationCycleId);

                            DateTime goalSubmitDate = goalLockedDate.Count() != 0 ? goalLockedDate.FirstOrDefault(x => x.Type == AppConstants.SubmitData).SubmitDate : cycleItem.CycleStartDate.AddDays(AppConstants.OkrLockDuration);

                            var keyDetails = await _okrServiceDataRepository.GetAllKeysAsync();
                            var pendingKeysOfCurrentCycle = keyDetails.Where(x => x.KrStatusId == (int)KrStatus.Pending && x.IsActive && x.CycleId == cycleItem.OrganisationCycleId && x.GoalStatusId != (int)GoalStatus.Archive);
                            if (pendingKeysOfCurrentCycle != null)
                            {
                                var sourceUsers = pendingKeysOfCurrentCycle.GroupBy(x => x.CreatedBy).Select(x => Convert.ToInt64(x.Key)).ToList();
                                foreach (var user in sourceUsers)
                                {
                                    //Source User Details to whom we are sending pending kr details
                                    var userData = userDetails.FirstOrDefault(x => x.EmployeeId == user && x.IsActive);
                                    if (userData != null)
                                    {
                                        var contributorWithPendingKey = pendingKeysOfCurrentCycle.Where(x => x.CreatedBy == user && x.KrStatusId == (int)KrStatus.Pending && x.IsActive);
                                        if (contributorWithPendingKey != null)
                                        {
                                            Dictionary<long, int> KeyCount = new Dictionary<long, int>();
                                            foreach (var cont in contributorWithPendingKey)
                                            {
                                                ////Adding contributors with pending KR whose createdOn+7 days date match todays date
                                                var date = cont.CreatedOn.AddDays(1);
                                                if (date.ToString("dd-MM-yyyy") == DateTime.Now.ToString("dd-MM-yyyy") && date.ToString("dd-MM-yyyy") != goalSubmitDate.ToString("dd-MM-yyyy"))
                                                {

                                                    if (!KeyCount.ContainsKey((long)cont.EmployeeId))
                                                    {
                                                        KeyCount.Add((long)cont.EmployeeId, 1);
                                                    }
                                                    else
                                                    {
                                                        KeyCount[(long)cont.EmployeeId]++;
                                                    }
                                                }
                                            }


                                            var template = await _notificationsAndEmails.GetMailerTemplate(TemplateCodes.CPS.ToString());
                                            string body = template.Body;
                                            body = body.Replace("topBar", AppConstants.CloudFrontUrl + AppConstants.TopBar).Replace("logo", AppConstants.CloudFrontUrl + AppConstants.LogoImage).Replace("<RedirectOkR>", AppConstants.ApplicationUrl + "?redirectUrl=unlock-me&empId=" + user)
                                                .Replace("<url>", AppConstants.ApplicationUrl).Replace("login", AppConstants.CloudFrontUrl + AppConstants.LoginImage).Replace("name", userData.FirstName).Replace("watch", AppConstants.CloudFrontUrl + AppConstants.Watch).Replace("<dashUrl>", AppConstants.ApplicationUrl + "?redirectUrl=unlock-me&empId=" + user)
                                                .Replace("<RedirectOkR>", AppConstants.ApplicationUrl).Replace("<supportEmailId>", AppConstants.UnlockSupportEmailId).Replace("<unlocklink>", AppConstants.ApplicationUrl).Replace("dot", AppConstants.CloudFrontUrl + AppConstants.DotImage).Replace("year", Convert.ToString(DateTime.Now.Year))
                                                .Replace("footer", AppConstants.CloudFrontUrl + AppConstants.footer).Replace("<pri>", AppConstants.PrivacyPolicy).Replace("<tos>", AppConstants.TermsOfUse)
                                                .Replace("srcFacebook", AppConstants.CloudFrontUrl + AppConstants.Facebook).Replace("srcInstagram", AppConstants.CloudFrontUrl + AppConstants.Instagram)
                                                .Replace("srcTwitter", AppConstants.CloudFrontUrl + AppConstants.Twitter).Replace("srcLinkedin", AppConstants.CloudFrontUrl + AppConstants.Linkedin)
                                                .Replace("ijk", AppConstants.InstagramUrl).Replace("lk", AppConstants.LinkedInUrl)
                                                .Replace("fb", AppConstants.FacebookURL).Replace("terp", AppConstants.TwitterUrl);

                                            if (KeyCount.Count > 0)
                                            {
                                                MailRequest mailRequest = new MailRequest();
                                                var summary = string.Empty;
                                                var counter = 0;
                                                foreach (var cont in KeyCount)
                                                {
                                                    counter = counter + 1;
                                                    ////Contributors details 
                                                    var childDetails = userDetails.FirstOrDefault(x => x.EmployeeId == cont.Key && x.IsActive);
                                                    summary = summary + "<tr><td valign =\"top\" cellpadding=\"0\" cellspacing=\"0\" style=\"font-size:16px;line-height:24px;color:#292929;font-family: Calibri,Arial;padding-right: 3px;\">" + " " + counter + " " + "." + " </td><td valign =\"top\" cellpadding=\"0\" cellspacing=\"0\" style=\"font-size:16px;line-height:24px;color:#292929;font-family: Calibri,Arial;\">" + " " + childDetails.FirstName + " " + "has" + " " + cont.Value.ToWords() + " " + "pending assignment.</td></tr>";
                                                }
                                                var updatedBody = body;
                                                updatedBody = updatedBody.Replace("<Gist>", summary);
                                                mailRequest.Body = updatedBody;
                                                mailRequest.MailTo = userData.EmailId;
                                                mailRequest.Subject = template.Subject;

                                                await _notificationsAndEmails.SentMailWithoutAuthenticationAsync(mailRequest);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        [FunctionName("UsersKrSummary")]
        public async Task UsersKrSummary([TimerTrigger("0 30 18 * * *")] TimerInfo myTimer, ILogger log)
        {
            var organisations = await _adminDataRepository.GetOrganisationsData();
            var userDetails = await _adminDataRepository.GetAdminData();

            if (organisations != null)
            {
                foreach (var item in organisations)
                {
                    ////Will fetch active organisationCycle
                    var cycle = await _adminDataRepository.GetOrganisationCycles(item.OrganisationId);
                    foreach (var cycleItem in cycle)
                    {
                        ////will find which cycle is active now
                        bool isCurrentCycle = cycleItem.CycleStartDate <= DateTime.UtcNow && cycleItem.CycleEndDate >= DateTime.UtcNow;
                        if (isCurrentCycle)
                        {
                            var goalUnlockDate = await _adminDataRepository.GetGoalUnlockDateData();
                            var goalLockedDate = goalUnlockDate.Where(x => x.OrganisationCycleId == cycleItem.OrganisationCycleId);

                            DateTime goalSubmitDate = goalLockedDate.Count() != 0 ? goalLockedDate.FirstOrDefault(x => x.Type == AppConstants.SubmitData).SubmitDate : cycleItem.CycleStartDate.AddDays(AppConstants.OkrLockDuration);

                            //if (goalSubmitDate.ToString("dd-MM-yyyy") == DateTime.Now.ToString("dd-MM-yyyy"))
                            //{
                            var keyDetails = await _okrServiceDataRepository.GetAllKeysAsync();
                            var pendingKeysOfCurrentCycle = keyDetails.Where(x => x.KrStatusId == (int)KrStatus.Pending && x.IsActive && x.CycleId == cycleItem.OrganisationCycleId && x.GoalStatusId != (int)GoalStatus.Archive);
                            if (pendingKeysOfCurrentCycle != null)
                            {
                                var sourceUsers = pendingKeysOfCurrentCycle.GroupBy(x => x.CreatedBy).Select(x => Convert.ToInt64(x.Key)).ToList();
                                foreach (var user in sourceUsers)
                                {
                                    //Source User Details to whom we are sending pending kr details
                                    var userData = userDetails.FirstOrDefault(x => x.EmployeeId == user && x.IsActive);
                                    if (userData != null)
                                    {
                                        var contributorWithPendingKey = pendingKeysOfCurrentCycle.Where(x => x.CreatedBy == user && x.KrStatusId == (int)KrStatus.Pending && x.IsActive).ToList();
                                        if (contributorWithPendingKey != null)
                                        {
                                            var totalCont = contributorWithPendingKey.Count;
                                            var template = await _notificationsAndEmails.GetMailerTemplate(TemplateCodes.LDS.ToString());
                                            string body = template.Body;
                                            body = body.Replace("topBar", AppConstants.CloudFrontUrl + AppConstants.TopBar).Replace("logo", AppConstants.CloudFrontUrl + AppConstants.LogoImage).Replace("infoo", AppConstants.CloudFrontUrl + AppConstants.InfoIcon).Replace("year", Convert.ToString(DateTime.Now.Year))
                                                .Replace("<url>", AppConstants.ApplicationUrl).Replace("login", AppConstants.CloudFrontUrl + AppConstants.LoginImage).Replace("name", userData.FirstName).Replace("watch", AppConstants.CloudFrontUrl + AppConstants.Watch).Replace("Nume", totalCont.ToString())
                                                .Replace("<RedirectOkR>", AppConstants.ApplicationUrl).Replace("<supportEmailId>", AppConstants.UnlockSupportEmailId).Replace("<unlocklink>", AppConstants.ApplicationUrl).Replace("dot", AppConstants.CloudFrontUrl + AppConstants.DotImage)
                                                .Replace("footer", AppConstants.CloudFrontUrl + AppConstants.footer)
                                                .Replace("srcFacebook", AppConstants.CloudFrontUrl + AppConstants.Facebook).Replace("srcInstagram", AppConstants.CloudFrontUrl + AppConstants.Instagram)
                                                .Replace("srcTwitter", AppConstants.CloudFrontUrl + AppConstants.Twitter).Replace("srcLinkedin", AppConstants.CloudFrontUrl + AppConstants.Linkedin)
                                                .Replace("ijk", AppConstants.InstagramUrl).Replace("lk", AppConstants.LinkedInUrl)
                                                .Replace("fb", AppConstants.FacebookURL).Replace("terp", AppConstants.TwitterUrl)
                                                .Replace("<pri>", AppConstants.PrivacyPolicy).Replace("<tos>", AppConstants.TermsOfUse)
                                                .Replace("<dashUrl>", AppConstants.ApplicationUrl + "?redirectUrl=unlock-me&empId=" + user);

                                            MailRequest mailRequest = new MailRequest();
                                            var summary = string.Empty;
                                            var cycleSymbolId = cycleItem.SymbolId;
                                            var cycleSymbol = _adminDataRepository.GetCycleSymbolById(cycleSymbolId);


                                            var topTwoPendingKeys = contributorWithPendingKey.Take(2);
                                            foreach (var key in topTwoPendingKeys)
                                            {
                                                if (key.GoalObjectiveId > 0)
                                                {
                                                    var objectiveDetails = _okrServiceDataRepository.GetGoalObjectiveById(key.GoalObjectiveId);
                                                    var totalKeys = keyDetails.Where(x => x.GoalObjectiveId == objectiveDetails.GoalObjectiveId).Count();
                                                    summary = summary + "<tr><td cellspacing =\"0\" cellpadding=\"0\"><table width =\"100%\" cellspacing=\"0\" cellpadding=\"0\"><tr><td cellspacing =\"0\" cellpadding=\"0\" style=\"padding-bottom: 10px;\"><table width =\"100%\" cellspacing=\"0\" cellpadding=\"0\" style=\"background-color: #ffffff;  border-radius: 6px;box-shadow:0px 0px 5px rgba(41, 41, 41, 0.1);\"><tr><td cellspacing =\"0\" cellpadding=\"0\" style=\"padding: 5px;\"><table width =\"100%\" cellspacing=\"0\" cellpadding=\"0\"><tr><td cellspacing =\"0\" cellpadding=\"0\" style=\"padding: 5px 15px;\"><table width =\"100%\" cellspacing=\"0\" cellpadding=\"0\"><tr><td width =\"75%\" cellspacing=\"0\" cellpadding=\"0\" style=\"width:75%\"><table width =\"100%\" cellspacing=\"0\" cellpadding=\"0\"><tr><td cellspacing =\"0\" cellpadding=\"0\" style=\"font-size:16px;line-height:22px;font-weight:400;color:#292929;font-family: Calibri,Arial;padding-bottom: 16px;\">" + " " + objectiveDetails.ObjectiveName + " " + "</td></tr><tr><td cellspacing =\"0\" cellpadding=\"0\"><table width =\"auto\" cellspacing=\"0\" cellpadding=\"0\"><tr><td cellspacing =\"0\" cellpadding=\"0\" valign=\"middle\" align=\"center\" height=\"20\" style=\"color: #ffffff; padding-left: 10px;padding-right:8px;border-radius: 3px;\" bgcolor=\"#39A3FA\"><table width =\"100%\" cellspacing=\"0\" cellpadding=\"0\"><tr><td cellspacing =\"0\" cellpadding=\"0\" valign=\"middle\"><img src =\"" + AppConstants.CloudFrontUrl + AppConstants.RightImage + "\" alt=\"arrow\" style=\"display: block;\"/></td><td cellspacing =\"0\" cellpadding=\"0\" valign=\"middle\" style=\"font-size:12px;line-height:14px;font-weight:bold;color:#ffffff;font-family: Calibri,Arial;padding-left: 6px;\"> " + " " + totalKeys + " " + " Key Results</td></tr></table></td></tr></table></tr></table></td><td cellspacing =\"0\" cellpadding=\"0\" align=\"right\" valign=\"top\"><table width =\"100%\" cellspacing=\"0\" cellpadding=\"0\"><tr><td cellspacing =\"0\" cellpadding=\"0\" align=\"right\" style=\"padding-top: 7px;\" valign=\"top\"><table cellspacing =\"0\" cellpadding=\"0\"> <tr><td cellspacing =\"0\" cellpadding=\"0\" valign=\"top\" style=\"font-size:16px;line-height:18px;font-weight:500;color:#292929;font-family: Calibri,Arial;padding-right: 18px;\">" + CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(objectiveDetails.Enddate.Month) + " " + objectiveDetails.Enddate.Day + "</td><td cellspacing =\"0\" cellpadding=\"0\" valign=\"top\"><img src =\"" + AppConstants.CloudFrontUrl + AppConstants.Calendar + "\" alt=\"cal\" style=\"display: inline-block;\"/></td></tr></table></td></tr><tr><td cellspacing =\"0\" cellpadding=\"0\" align=\"right\" valign= \"top\" style=\"text-align:right;font-size:12px;line-height:12px;font-weight:500;color:#626262;font-family: Calibri,Arial;padding-right: 5px;\">Cycle: " + " " + cycleSymbol.Symbol + " " + ", " + " " + cycleItem.CycleYear + " " + "</td></tr></table></td></tr></table></td></tr></table></td></tr></table></td></tr>";
                                                }
                                                else
                                                {
                                                    summary = summary + "<tr><td cellspacing =\"0\" cellpadding=\"0\" style=\"padding-bottom: 10px;\"><table width =\"100%\" cellspacing=\"0\" cellpadding=\"0\" style=\"background-color: #ffffff;  border-radius: 6px;box-shadow:0px 0px 5px rgba(41, 41, 41, 0.1);\"><tr><td cellspacing =\"0\" cellpadding=\"0\" style=\"padding: 5px;\"><table width =\"100%\" cellspacing=\"0\" cellpadding=\"0\"><tr><td cellspacing =\"0\" cellpadding=\"0\" bgcolor=\"#F1F3F4\" style=\"padding: 10px 15px;border-radius: 6px;\"><table width =\"100%\" cellspacing=\"0\" cellpadding=\"0\"><tr><td width =\"75%\" cellspacing=\"0\" cellpadding=\"0\" style=\"width:75%\"><table width =\"100%\" cellspacing=\"0\" cellpadding=\"0\"><tr><td cellspacing =\"0\" cellpadding=\"0\" style=\"font-size:16px;line-height:22px;font-weight:400;color:#292929;font-family: Calibri,Arial;padding-bottom: 16px;\">" + " " + key.KeyDescription + " " + "</td></tr><tr><td cellspacing =\"0\" cellpadding=\"0\"><table width =\"auto\" cellspacing=\"0\" cellpadding=\"0\"><tr><td cellspacing =\"0\" cellpadding=\"0\" valign=\"middle\" align=\"left\" width=\"\" height=\"20\" style=\"color: #ffffff;padding-left: 10px;border-radius: 3px;padding-right: 8px;\" bgcolor=\"#e3e5e5\"><table width =\"\" cellspacing=\"0\" cellpadding=\"0\"><tr><td cellspacing =\"0\" cellpadding=\"0\" valign=\"middle\"><img src =\"" + AppConstants.CloudFrontUrl + AppConstants.LinkImage + "\" alt=\"link\" style=\"display: block;\"/></td><td cellspacing =\"0\" cellpadding=\"0\" valign=\"middle\" style=\"font-size:12px;line-height:14px;font-weight:bold;color:#626262;font-family: Calibri,Arial;padding-left: 7px;\">Key Result</td></tr></table></td></tr></table></td></tr></table></td><td cellspacing =\"0\" cellpadding=\"0\" align=\"right\" valign=\"top\"><table width =\"100%\" cellspacing=\"0\" cellpadding=\"0\"><tr><td cellspacing =\"0\" cellpadding=\"0\" align=\"right\" style=\"padding-top: 7px;\" valign=\"top\"><table cellspacing =\"0\" cellpadding=\"0\"><tr><td cellspacing =\"0\" cellpadding=\"0\" valign=\"top\" style=\"font-size:16px;line-height:18px;font-weight:500;color:#292929;font-family: Calibri,Arial;padding-right: 18px;\">" + CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(key.DueDate.Month) + " " + key.DueDate.Day + "</td><td cellspacing =\"0\" cellpadding=\"0\" valign=\"top\"><img src =\"" + AppConstants.CloudFrontUrl + AppConstants.Calendar + "\" alt=\"cal\" style=\"display: inline-block;\"/></td></tr></table></td></tr><tr><td cellspacing =\"0\" cellpadding=\"0\" align=\"right\" valign=\"top\" style=\"text-align:right;font-size:12px;line-height:12px;font-weight:500;color:#626262;font-family: Calibri,Arial;padding-right: 5px;\">Cycle: " + " " + cycleSymbol.Symbol + " " + ", " + "" + cycleItem.CycleYear + " " + "</td></tr></table></td></tr></table></td></tr></table></td></tr></table></td></tr>";
                                                }
                                            }

                                            var updatedBody = body;
                                            updatedBody = updatedBody.Replace("<Gist>", summary);
                                            mailRequest.Body = updatedBody;
                                            mailRequest.MailTo = userData.EmailId;
                                            mailRequest.Subject = template.Subject;

                                            await _notificationsAndEmails.SentMailWithoutAuthenticationAsync(mailRequest);
                                        }
                                    }
                                }
                            }
                            //}
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Reminder email on last day of the planning for the draft OKRs 
        /// </summary>
        /// <returns></returns>
        /// 
        [FunctionName("UsersDraftKrSummary")]
        public async Task UsersDraftKrSummary([TimerTrigger("0 30 18 * * *")] TimerInfo myTimer, ILogger log)
        {
            var organisations = await _adminDataRepository.GetOrganisationsData();
            var userDetails = await _adminDataRepository.GetAdminData();

            if (organisations != null)
            {
                foreach (var item in organisations)
                {
                    ////Will fetch active organisationCycle
                    var cycle = await _adminDataRepository.GetOrganisationCycles(item.OrganisationId);
                    foreach (var cycleItem in cycle)
                    {
                        ////will find which cycle is active now
                        bool isCurrentCycle = cycleItem.CycleStartDate <= DateTime.UtcNow && cycleItem.CycleEndDate >= DateTime.UtcNow;
                        if (isCurrentCycle)
                        {
                            var goalUnlockDate = await _adminDataRepository.GetGoalUnlockDateData();
                            var goalLockedDate = goalUnlockDate.Where(x => x.OrganisationCycleId == cycleItem.OrganisationCycleId);

                            DateTime goalSubmitDate = goalLockedDate.Count() != 0 ? goalLockedDate.FirstOrDefault(x => x.Type == AppConstants.SubmitData).SubmitDate : cycleItem.CycleStartDate.AddDays(AppConstants.OkrLockDuration);
                            var reminderDate = goalSubmitDate.AddDays(-1);

                            //  if (reminderDate.ToString("dd-MM-yyyy") == DateTime.Now.ToString("dd-MM-yyyy"))
                            //  {
                            var goalDetails = await _okrServiceDataRepository.GetAllOkrAsync();
                            if (goalDetails != null)
                            {
                                var draftOkrOfCurrentCycle = goalDetails.Where(x => x.IsActive && x.GoalStatusId == (int)GoalStatus.Draft && x.ObjectiveCycleId == cycleItem.OrganisationCycleId).ToList();
                                if (draftOkrOfCurrentCycle.Count > 0 && draftOkrOfCurrentCycle.Any())
                                {
                                    var sourceUsers = draftOkrOfCurrentCycle.GroupBy(x => x.CreatedBy).Select(x => Convert.ToInt64(x.Key)).ToList();
                                    foreach (var user in sourceUsers)
                                    {
                                        var userData = userDetails.FirstOrDefault(x => x.EmployeeId == user && x.IsActive);
                                        if (userData != null)
                                        {
                                            var sourceWithDraftOkr = draftOkrOfCurrentCycle.Where(x => x.CreatedBy == user).ToList();

                                            if (sourceWithDraftOkr.Count > 0 && sourceWithDraftOkr.Any())
                                            {
                                                var summary = string.Empty;
                                                var count = string.Empty;
                                                var cycleSymbolDetails = _adminDataRepository.GetCycleSymbolById(cycleItem.SymbolId);
                                                var OkrList = sourceWithDraftOkr.Take(3);
                                                foreach (var draftOkr in OkrList)
                                                {
                                                    var keyDetails = await _okrServiceDataRepository.GetKeyByGoalObjectiveIdAsync(draftOkr.GoalObjectiveId);
                                                    var keyCount = keyDetails.Count();
                                                    if (keyCount <= 9)
                                                    {
                                                        count = "0" + Convert.ToString(keyCount);
                                                    }
                                                    else
                                                    {
                                                        count = Convert.ToString(keyCount);
                                                    }

                                                    var stringLen = draftOkr.ObjectiveName.Length;
                                                    if (stringLen > 117)
                                                    {
                                                        draftOkr.ObjectiveName = draftOkr.ObjectiveName.Substring(0, 117) + "...";
                                                    }

                                                    summary = summary + "<tr><td cellspacing =\"0\" cellpadding=\"0\" style=\"padding-bottom: 10px;\"><table width =\"100%\" cellspacing=\"0\" cellpadding=\"0\"style =\"background-color: #ffffff;  border-radius: 6px;box-shadow:0px 0px 5px rgba(41, 41, 41, 0.1);\"><tr><td cellspacing =\"0\" cellpadding=\"0\" style=\"padding: 5px;\"><table width =\"100%\" cellspacing=\"0\" cellpadding=\"0\"><tr><td cellspacing =\"0\" cellpadding=\"0\"style =\"padding: 5px 15px;\"><table width =\"100%\" cellspacing=\"0\" cellpadding=\"0\"><tr><td width =\"75%\" cellspacing=\"0\" cellpadding=\"0\"style =\"width:75%\"><table width =\"100%\" cellspacing=\"0\"cellpadding =\"0\"><tr><td cellspacing =\"0\" cellpadding=\"0\"style =\"font-size:16px;line-height:22px;font-weight:400;color:#292929;font-family: Calibri,Arial;padding-bottom: 16px;\">" + draftOkr.ObjectiveName + "</td></tr><tr><td cellspacing =\"0\" cellpadding=\"0\"><table width =\"auto\"cellspacing =\"0\"cellpadding =\"0\"><tr><td cellspacing =\"0\"cellpadding =\"0\"valign =\"middle\"align =\"center\"height =\"20\"style =\"color: #ffffff; padding-left: 10px;padding-right:8px;border-radius: 3px;\"bgcolor =\"#39A3FA\"><table width =\"100%\"cellspacing =\"0\"cellpadding =\"0\"><tr><td cellspacing =\"0\"cellpadding =\"0\"valign =\"middle\"><img src =\"" + AppConstants.CloudFrontUrl + AppConstants.RightImage + "\"alt =\"arrow\"style =\"display: block;\" /></td><td cellspacing =\"0\"cellpadding =\"0\"valign =\"middle\"style =\"font-size:12px;line-height:14px;font-weight:bold;color:#ffffff;font-family: Calibri,Arial;padding-left: 6px;\">" + count + " Key Results</td></tr></table></td></tr></table></tr></table></td><td cellspacing =\"0\" cellpadding=\"0\"align =\"right\" valign=\"top\"><table width =\"100%\" cellspacing=\"0\"cellpadding =\"0\"><tr><td cellspacing =\"0\" cellpadding=\"0\"align =\"right\"style =\"padding-top: 7px;\"valign =\"top\"><table cellspacing =\"0\"cellpadding =\"0\"><tr><td cellspacing =\"0\"cellpadding =\"0\"valign =\"top\"style =\"font-size:16px;line-height:18px;font-weight:500;color:#292929;font-family: Calibri,Arial;padding-right: 18px;\">" + CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(draftOkr.Enddate.Month) + " " + draftOkr.Enddate.Day + "</td><td cellspacing =\"0\"cellpadding =\"0\"valign =\"top\"><img src =\"" + AppConstants.CloudFrontUrl + AppConstants.Calendar + "\"alt =\"cal\"style =\"display: inline-block;\" /></td></tr></table></td></tr><tr><td cellspacing =\"0\" cellpadding=\"0\"align =\"right\" valign=\"top\" style =\"text-align:right;font-size:12px;line-height:12px;font-weight:500;color:#626262;font-family: Calibri,Arial;padding-right: 5px;\">Cycle: " + cycleSymbolDetails.Symbol + ", " + cycleItem.CycleYear + "</td></tr></table></td></tr></table></td></tr></table></td></tr></table></td></tr> ";

                                                }

                                                var template = await _notificationsAndEmails.GetMailerTemplate(TemplateCodes.DOS.ToString());
                                                string body = template.Body;
                                                var subject = template.Subject;
                                                var loginUrl = AppConstants.ApplicationUrl;
                                                if (!string.IsNullOrEmpty(loginUrl))
                                                {
                                                    loginUrl = loginUrl + "?redirectUrl=unlock-me&empId=" + user;
                                                }

                                                body = body.Replace("topBar", AppConstants.CloudFrontUrl + AppConstants.TopBar).Replace("<URL>", loginUrl).Replace("logo", AppConstants.CloudFrontUrl + AppConstants.LogoImage)
                                                    .Replace("srcFacebook", AppConstants.CloudFrontUrl + AppConstants.Facebook).Replace("srcInstagram", AppConstants.CloudFrontUrl + AppConstants.Instagram)
                                                    .Replace("srcTwitter", AppConstants.CloudFrontUrl + AppConstants.Twitter).Replace("srcLinkedin", AppConstants.CloudFrontUrl + AppConstants.Linkedin)
                                                    .Replace("ijk", AppConstants.InstagramUrl).Replace("lk", AppConstants.LinkedInUrl)
                                                    .Replace("fb", AppConstants.FacebookURL).Replace("terp", AppConstants.TwitterUrl)
                                                    .Replace("name", userData.FirstName).Replace("infoIcon", AppConstants.CloudFrontUrl + AppConstants.InfoIcon).Replace("count", Convert.ToString(sourceWithDraftOkr.Count))
                                                    .Replace("Listing", summary).Replace("<Button>", loginUrl).Replace("supportEmailId", AppConstants.UnlockSupportEmailId)
                                                    .Replace("year", Convert.ToString(DateTime.Now.Year));

                                                subject = subject.Replace("<username>", userData.FirstName);

                                                if (userData.EmailId != null && template.Subject != "")
                                                {
                                                    var mailRequest = new MailRequest
                                                    {
                                                        MailTo = userData.EmailId,
                                                        Subject = subject,
                                                        Body = body
                                                    };
                                                    await _notificationsAndEmails.SentMailWithoutAuthenticationAsync(mailRequest);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            //}
                        }
                    }
                }
            }
        }


        /// <summary>
        /// Mail to users after a span of every 3 working days if users have not picked up draft OKRs in the planning session
        /// </summary>
        /// <returns></returns>
        /// 
        [FunctionName("SendInterimMailForDraftOkr")]
        public async Task SendInterimMailForDraftOkr([TimerTrigger("0 30 18 * * *")] TimerInfo myTimer, ILogger log)
        {
            var organisations = await _adminDataRepository.GetOrganisationsData();
            var userDetails = await _adminDataRepository.GetAdminData();

            if (organisations != null)
            {
                foreach (var item in organisations)
                {
                    ////Will fetch active organisationCycle
                    var cycle = await _adminDataRepository.GetOrganisationCycles(item.OrganisationId);
                    foreach (var cycleItem in cycle)
                    {
                        ////will find which cycle is active now
                        bool isCurrentCycle = cycleItem.CycleStartDate <= DateTime.UtcNow && cycleItem.CycleEndDate >= DateTime.UtcNow;
                        if (isCurrentCycle)
                        {
                            var goalDetails = await _okrServiceDataRepository.GetAllOkrAsync();
                            if (goalDetails != null)
                            {
                                var draftOkrOfCurrentCycle = goalDetails.Where(x => x.IsActive && x.GoalStatusId == (int)GoalStatus.Draft && x.ObjectiveCycleId == cycleItem.OrganisationCycleId).ToList();

                                if (draftOkrOfCurrentCycle.Count > 0 && draftOkrOfCurrentCycle.Any())
                                {
                                    var sourceUsers = draftOkrOfCurrentCycle.GroupBy(x => x.CreatedBy).Select(x => Convert.ToInt64(x.Key)).ToList();
                                    foreach (var user in sourceUsers)
                                    {
                                        var userData = userDetails.FirstOrDefault(x => x.EmployeeId == user && x.IsActive);
                                        if (userData != null)
                                        {
                                            var sourceWithDraftOkr = draftOkrOfCurrentCycle.Where(x => x.CreatedBy == user).ToList();

                                            if (sourceWithDraftOkr.Count > 0 && sourceWithDraftOkr.Any())
                                            {
                                                var template = await _notificationsAndEmails.GetMailerTemplate(TemplateCodes.DIM.ToString());
                                                var body = template.Body;
                                                var subject = template.Subject;

                                                var sourceOldestDraftOkr = sourceWithDraftOkr.OrderBy(x => x.CreatedOn).FirstOrDefault();

                                                var loginUrl = AppConstants.ApplicationUrl;
                                                if (!string.IsNullOrEmpty(loginUrl))
                                                {
                                                    loginUrl = loginUrl + "?redirectUrl=unlock-me&empId=" + user;
                                                }

                                                body = body.Replace("topBar", AppConstants.CloudFrontUrl + AppConstants.TopBar).Replace("<URL>", loginUrl).Replace("logo", AppConstants.CloudFrontUrl + AppConstants.LogoImage)

                                                    .Replace("srcFacebook", AppConstants.CloudFrontUrl + AppConstants.Facebook).Replace("srcInstagram", AppConstants.CloudFrontUrl + AppConstants.Instagram)
                                                    .Replace("srcTwitter", AppConstants.CloudFrontUrl + AppConstants.Twitter).Replace("srcLinkedin", AppConstants.CloudFrontUrl + AppConstants.Linkedin)
                                                    .Replace("ijk", AppConstants.InstagramUrl).Replace("lk", AppConstants.LinkedInUrl)
                                                    .Replace("fb", AppConstants.FacebookURL).Replace("terp", AppConstants.TwitterUrl)
                                                    .Replace("name", userData.FirstName).Replace("<Button>", AppConstants.ApplicationUrl + "?redirectUrl=KRAcceptDecline/1/" + sourceOldestDraftOkr.GoalObjectiveId + "&empId=" + user)
                                                    .Replace("messageInterm", AppConstants.CloudFrontUrl + AppConstants.MessageIntermImage).Replace("supportEmailId", AppConstants.UnlockSupportEmailId)
                                                    .Replace("year", Convert.ToString(DateTime.Now.Year));

                                                subject = subject.Replace("<username>", userData.FirstName);

                                                ////we are getting the dates on which mail should be send to source in planning session
                                                var dates = new List<DateTime>();
                                                var date = new DateTime();
                                                date = sourceOldestDraftOkr.CreatedOn.AddBusinessDays(3);

                                                var goalUnlockDate = await _adminDataRepository.GetGoalUnlockDateData();
                                                var goalLockedDate = goalUnlockDate.Where(x => x.OrganisationCycleId == cycleItem.OrganisationCycleId);
                                                var goalSubmitDate = goalLockedDate.Count() != 0 ? goalLockedDate.FirstOrDefault(x => x.Type == AppConstants.SubmitData).SubmitDate : cycleItem.CycleStartDate.AddDays(AppConstants.OkrLockDuration);

                                                do
                                                {
                                                    dates.Add(date);
                                                    date = date.AddBusinessDays(3);
                                                } while (date <= goalSubmitDate);

                                                foreach (var day in dates)
                                                {
                                                    if (day.ToString("dd-MM-yyyy") == DateTime.Now.ToString("dd-MM-yyyy"))
                                                    {
                                                        if (userData.EmailId != null && template.Subject != "")
                                                        {
                                                            var mailRequest = new MailRequest
                                                            {
                                                                MailTo = userData.EmailId,
                                                                Subject = subject,
                                                                Body = body
                                                            };
                                                            await _notificationsAndEmails.SentMailWithoutAuthenticationAsync(mailRequest);
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }


        [FunctionName("SourceAfter3days")]
        public async Task SourceAfter3days([TimerTrigger("0 30 18 * * *")] TimerInfo myTimer, ILogger log)
        {
            var organisations = await _adminDataRepository.GetOrganisationsData();
            var userDetails = await _adminDataRepository.GetAdminData();
            if (organisations != null)
            {
                foreach (var item in organisations)
                {
                    var cycle = await _adminDataRepository.GetOrganisationCycles(item.OrganisationId);
                    foreach (var cycleItem in cycle)
                    {
                        bool isCurrentCycle = cycleItem.CycleStartDate <= DateTime.UtcNow && cycleItem.CycleEndDate >= DateTime.UtcNow;
                        if (isCurrentCycle)
                        {

                            var goalUnlockDate = await _adminDataRepository.GetGoalUnlockDateData();
                            var goalLockedDate = goalUnlockDate.Where(x => x.OrganisationCycleId == cycleItem.OrganisationCycleId);

                            DateTime goalSubmitDate = goalLockedDate.Count() != 0 ? goalLockedDate.FirstOrDefault(x => x.Type == AppConstants.SubmitData).SubmitDate : cycleItem.CycleStartDate.AddDays(AppConstants.OkrLockDuration);

                            if (goalSubmitDate.ToString("dd-MM-yyyy") != DateTime.Now.ToString("dd-MM-yyyy"))
                            {
                                var pendingKeysOfCurrentCycle = await _okrServiceDataRepository.GetKeydetailspending(cycleItem.OrganisationCycleId);
                                var pendingkeysforBusinessDay = pendingKeysOfCurrentCycle.ToList().Where(x => Convert.ToDateTime(x.CreatedOn.AddBusinessDays(3)).ToString("dd-MM-yyyy") == DateTime.Now.ToString("dd-MM-yyyy"));
                                var sourceUsers = pendingkeysforBusinessDay.GroupBy(x => x.CreatedBy).Select(x => Convert.ToInt64(x.Key)).ToList();
                                if (sourceUsers.Count > 0)
                                {
                                    foreach (var user in sourceUsers)
                                    {
                                        var userData = userDetails.FirstOrDefault(x => x.EmployeeId == user && x.IsActive);
                                        if (userData != null)
                                        {
                                            var contributorWithPendingKey = pendingKeysOfCurrentCycle.Where(x => x.CreatedBy == user && x.KrStatusId == (int)KrStatus.Pending && x.IsActive);
                                            if (contributorWithPendingKey != null)
                                            {
                                                var template = await _notificationsAndEmails.GetMailerTemplate(TemplateCodes.CPS.ToString());
                                                string body = template.Body;
                                                body = body.Replace("topBar", AppConstants.CloudFrontUrl + AppConstants.TopBar).Replace("logo", AppConstants.CloudFrontUrl + AppConstants.LogoImage)
                                                    .Replace("<url>", AppConstants.ApplicationUrl).Replace("login", AppConstants.CloudFrontUrl + AppConstants.LoginImage).Replace("name", userData.FirstName).Replace("watch", AppConstants.CloudFrontUrl + AppConstants.Watch)
                                                    .Replace("<RedirectOkR>", AppConstants.ApplicationUrl).Replace("<supportEmailId>", AppConstants.UnlockSupportEmailId).Replace("<unlocklink>", AppConstants.ApplicationUrl).Replace("dot", AppConstants.CloudFrontUrl + AppConstants.DotImage)
                                                    .Replace("footer", AppConstants.CloudFrontUrl + AppConstants.footer).Replace("<pri>", AppConstants.PrivacyPolicy).Replace("<tos>", AppConstants.TermsOfUse)
                                                    .Replace("srcFacebook", AppConstants.CloudFrontUrl + AppConstants.Facebook).Replace("srcInstagram", AppConstants.CloudFrontUrl + AppConstants.Instagram)
                                                .Replace("srcTwitter", AppConstants.CloudFrontUrl + AppConstants.Twitter).Replace("srcLinkedin", AppConstants.CloudFrontUrl + AppConstants.Linkedin)
                                                .Replace("ijk", AppConstants.InstagramUrl).Replace("lk", AppConstants.LinkedInUrl).Replace("year", Convert.ToString(DateTime.Now.Year))
                                                .Replace("fb", AppConstants.FacebookURL).Replace("terp", AppConstants.TwitterUrl);

                                                Dictionary<long, int> KeyCount = new Dictionary<long, int>();
                                                foreach (var contKey in contributorWithPendingKey)
                                                {
                                                    var contributorDetails = contributorWithPendingKey.FirstOrDefault(x => x.EmployeeId == contKey.EmployeeId && x.IsActive);

                                                    if (!KeyCount.ContainsKey((long)contributorDetails.EmployeeId))
                                                    {
                                                        KeyCount.Add((long)contributorDetails.EmployeeId, 1);
                                                    }

                                                }


                                                MailRequest mailRequest = new MailRequest();
                                                var summary = string.Empty;
                                                var counter = 0;
                                                foreach (var cont in KeyCount)
                                                {
                                                    counter = counter + 1;
                                                    ////Contributors details 
                                                    var childDetails = userDetails.FirstOrDefault(x => x.EmployeeId == cont.Key && x.IsActive);
                                                    summary = summary + "<tr><td valign =\"top\" cellpadding=\"0\" cellspacing=\"0\" style=\"font-size:16px;line-height:24px;color:#292929;font-family: Calibri,Arial;padding-right: 3px;\">" + " " + counter + " " + "." + "</td><td valign =\"top\" cellpadding=\"0\" cellspacing=\"0\" style=\"font-size:16px;line-height:24px;color:#292929;font-family: Calibri,Arial;\">" + " " + childDetails.FirstName + " " + "has " + " " + cont.Value.ToWords() + " " + "  pending assignment</td></tr>";
                                                }
                                                var updatedBody = body;
                                                updatedBody = updatedBody.Replace("<Gist>", summary);
                                                mailRequest.Body = updatedBody;
                                                mailRequest.MailTo = userData.EmailId;
                                                mailRequest.Subject = template.Subject;

                                                await _notificationsAndEmails.SentMailWithoutAuthenticationAsync(mailRequest);

                                                MailRequest mailRequests = new MailRequest();
                                                var contributorsTemplate = await _notificationsAndEmails.GetMailerTemplate(TemplateCodes.PC.ToString());
                                                string detail = string.Empty;
                                                string mailId = string.Empty;


                                                string contributorsBody = contributorsTemplate.Body;
                                                contributorsBody = contributorsBody.Replace("topBar", AppConstants.CloudFrontUrl + AppConstants.TopBar).Replace("logo", AppConstants.CloudFrontUrl + AppConstants.LogoImage)
                                                    .Replace("<URL>", AppConstants.ApplicationUrl).Replace("login", AppConstants.CloudFrontUrl + AppConstants.LoginImage).Replace("name", userData.FirstName).Replace("assignments", AppConstants.CloudFrontUrl + AppConstants.assignments)
                                                    .Replace("<RedirectOkR>", AppConstants.ApplicationUrl).Replace("supportEmailId", AppConstants.UnlockSupportEmailId).Replace("<unlocklink>", AppConstants.ApplicationUrl).Replace("dot", AppConstants.CloudFrontUrl + AppConstants.DotImage)
                                                    .Replace("footer", AppConstants.CloudFrontUrl + AppConstants.footer).Replace("srcFacebook", AppConstants.CloudFrontUrl + AppConstants.Facebook).Replace("srcInstagram", AppConstants.CloudFrontUrl + AppConstants.Instagram)
                                                    .Replace("srcTwitter", AppConstants.CloudFrontUrl + AppConstants.Twitter).Replace("srcLinkedin", AppConstants.CloudFrontUrl + AppConstants.Linkedin).Replace("ijk", AppConstants.InstagramUrl).Replace("lk", AppConstants.LinkedInUrl)
                                                    .Replace("fb", AppConstants.FacebookURL).Replace("terp", AppConstants.TwitterUrl).Replace("policy", AppConstants.PrivacyPolicy).Replace("terming", AppConstants.TermsOfUse).Replace("year", Convert.ToString(DateTime.Now.Year));

                                                var contributorSummary = string.Empty;

                                                foreach (var con in contributorWithPendingKey)
                                                {
                                                    detail = userDetails.FirstOrDefault(x => x.EmployeeId == con.EmployeeId && x.IsActive).FirstName;
                                                    mailId = userDetails.FirstOrDefault(x => x.EmployeeId == con.EmployeeId && x.IsActive).EmailId;
                                                    var details = contributorWithPendingKey.FirstOrDefault(x => x.EmployeeId == con.EmployeeId && x.IsActive);
                                                    contributorSummary = "<td valign =\"top\" cellpadding=\"0\" cellspacing =\"0\"style =\"font-size:16px;line-height:24px;color:#292929;font-family: Calibri,Arial;padding-right: 3px\"> </td><td valign =\"top\" cellpadding=\"0\" cellspacing =\"0\" style =\"font-size:16px;line-height:24px;color:#39A3FA;font-family: Calibri,Arial;font-weight: bold;text-decoration: none;\">OKR/KR</a><strong  style =\"font-size:16px;line-height:24px;color:#292929;font-family: Calibri,Arial;\"> <a href =\"#\"  style =\"color: #39A3FA;\">from " + userData.FirstName + "</td></tr>";
                                                    var updatedContributors = contributorsBody;
                                                    updatedContributors = updatedContributors.Replace("<subordinate>", contributorSummary).Replace("Contri", detail);
                                                    mailRequests.Body = updatedContributors;
                                                    mailRequests.MailTo = mailId;
                                                    mailRequests.Subject = contributorsTemplate.Subject;

                                                    await _notificationsAndEmails.SentMailWithoutAuthenticationAsync(mailRequests);

                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }


        [FunctionName("SendMailForLogin")]
        public async Task SendMailForLogin([TimerTrigger("0 30 18 * * *")] TimerInfo myTimer, ILogger log)
        {
            List<long> to = new List<long>();
            NotificationsRequest notificationsRequest = new NotificationsRequest();
            var employees = await _adminDataRepository.GetAdminData();
            var userData = employees.ToList();

            var userToken = await _adminDataRepository.GetUserTokenDetails();
            //var currentDate = DateTime.UtcNow.AddDays(-7).ToString("dd-MM-yyyy");
            var currentDate = DateTime.UtcNow.ToString("dd-MM-yyyy");
            var data = userData.Where(x => x.CreatedOn.ToString("dd-MM-yyyy") == currentDate).ToList();
            if (data.Count > 0)
            {
                foreach (var emp in data)
                {
                    var tokenDetails = userToken.FirstOrDefault(x => x.EmployeeId == emp.EmployeeId && x.LastLoginDate == null && x.CurrentLoginDate == null);
                    var reporting = employees.FirstOrDefault(x => x.EmployeeId == emp.ReportingTo);
                    if (tokenDetails != null)
                    {
                        var template = await _notificationsAndEmails.GetMailerTemplate(TemplateCodes.LR.ToString());
                        string body = template.Body.Replace("topBar", AppConstants.CloudFrontUrl + AppConstants.TopBar).Replace("logo", AppConstants.CloudFrontUrl + AppConstants.LogoImage)
                            .Replace("login", AppConstants.CloudFrontUrl + AppConstants.LoginButtonImage).Replace("userManger", AppConstants.CloudFrontUrl + AppConstants.HandShakeImage).Replace("lambdaUrl", AppConstants.ApplicationUrl).Replace("srcFacebook", AppConstants.CloudFrontUrl + AppConstants.Facebook).Replace("srcInstagram", AppConstants.CloudFrontUrl + AppConstants.Instagram)
                            .Replace("srcTwitter", AppConstants.CloudFrontUrl + AppConstants.Twitter).Replace("srcLinkedin", AppConstants.CloudFrontUrl + AppConstants.Linkedin)
                            .Replace("ijk", AppConstants.InstagramUrl).Replace("lk", AppConstants.LinkedInUrl)
                            .Replace("fb", AppConstants.FacebookURL).Replace("terp", AppConstants.TwitterUrl);
                        string subject = template.Subject;
                        body = body.Replace("managerName", reporting.FirstName).Replace("userName", emp.FirstName);
                        MailRequest mailRequest = new MailRequest();
                        if (emp.EmailId != null && template.Subject != "")
                        {
                            mailRequest.MailTo = emp.EmailId;
                            mailRequest.Subject = subject;
                            mailRequest.Body = body;
                            await _notificationsAndEmails.SentMailWithoutAuthenticationAsync(mailRequest);
                        }

                        var managerTemplate = await _notificationsAndEmails.GetMailerTemplate(TemplateCodes.LRM.ToString());
                        string managerTemplateBody = managerTemplate.Body.Replace("topBar", AppConstants.CloudFrontUrl + AppConstants.TopBar).Replace("logo", AppConstants.CloudFrontUrl + AppConstants.LogoImage)
                            .Replace("login", AppConstants.CloudFrontUrl + AppConstants.LoginButtonImage).Replace("userManger", AppConstants.CloudFrontUrl + AppConstants.HandShakeImage).Replace("lambdaUrl", AppConstants.ApplicationUrl).Replace("srcFacebook", AppConstants.CloudFrontUrl + AppConstants.Facebook).Replace("srcInstagram", AppConstants.CloudFrontUrl + AppConstants.Instagram)
                            .Replace("srcTwitter", AppConstants.CloudFrontUrl + AppConstants.Twitter).Replace("srcLinkedin", AppConstants.CloudFrontUrl + AppConstants.Linkedin)
                            .Replace("ijk", AppConstants.InstagramUrl).Replace("lk", AppConstants.LinkedInUrl).Replace("year", Convert.ToString(DateTime.Now.Year))
                            .Replace("fb", AppConstants.FacebookURL).Replace("terp", AppConstants.TwitterUrl);
                        string managerTemplateSubject = managerTemplate.Subject;
                        managerTemplateBody = managerTemplateBody.Replace("managerName", reporting.FirstName).Replace("userName", emp.FirstName);
                        MailRequest mailRequests = new MailRequest();
                        if (reporting.EmailId != null && managerTemplateSubject != "")
                        {
                            mailRequests.MailTo = reporting.EmailId;
                            mailRequests.Subject = managerTemplateSubject;
                            mailRequests.Body = managerTemplateBody;
                            await _notificationsAndEmails.SentMailWithoutAuthenticationAsync(mailRequests);
                        }

                        ////Notification To ReportingManager
                        to.Add(reporting.EmployeeId);
                        notificationsRequest.To = to;
                        notificationsRequest.By = reporting.EmployeeId;
                        notificationsRequest.Url = "";
                        notificationsRequest.Text = AppConstants.ReminderByManagerForUserMessage;
                        notificationsRequest.AppId = Apps.AppId;
                        notificationsRequest.NotificationType = (int)NotificationType.LoginReminderForUser;
                        notificationsRequest.MessageType = (int)MessageTypeForNotifications.NotificationsMessages;
                        await _notificationsAndEmails.InsertNotificationDetails(notificationsRequest);
                    }
                }
            }
        }

        [FunctionName("UpdateStatusAfterPlanningSession")]
        public async Task UpdateStatusAfterPlanningSession([TimerTrigger("0 30 18 * * *")] TimerInfo myTimer, ILogger log)
        {
            var organisations = await _adminDataRepository.GetOrganisationsData();

            if (organisations != null)
            {
                foreach (var item in organisations)
                {
                    ////Will fetch active organisationCycle
                    var cycle = await _adminDataRepository.GetOrganisationCycles(item.OrganisationId);
                    foreach (var cycleItem in cycle)
                    {
                        ////will find which cycle is active now
                        bool isCurrentCycle = cycleItem.CycleStartDate <= DateTime.UtcNow && cycleItem.CycleEndDate >= DateTime.UtcNow;
                        if (isCurrentCycle)
                        {
                            var goalUnlockDate = await _adminDataRepository.GetGoalUnlockDateData();
                            var goalLockedDate = goalUnlockDate.Where(x => x.OrganisationCycleId == cycleItem.OrganisationCycleId);

                            DateTime goalSubmitDate = goalLockedDate.Count() != 0 ? goalLockedDate.FirstOrDefault(x => x.Type == AppConstants.SubmitData).SubmitDate : cycleItem.CycleStartDate.AddDays(AppConstants.OkrLockDuration);
                            if (goalSubmitDate <= DateTime.Now)
                            {
                                var keyDetails = await _okrServiceDataRepository.GetAllKeysAsync();
                                if (keyDetails != null)
                                {
                                    var pendingKeys = keyDetails.Where(x => (x.KrStatusId == (int)KrStatus.Pending || x.GoalStatusId == (int)GoalStatus.Draft) && (x.CycleId == cycleItem.OrganisationCycleId)).ToList();

                                    foreach (var key in pendingKeys)
                                    {
                                        if (key.GoalObjectiveId > 0)
                                        {
                                            var okrDetails = await _okrServiceDataRepository.GetAllOkrAsync();
                                            var goalKeys = keyDetails.Where(x => x.GoalObjectiveId == key.GoalObjectiveId).ToList();
                                            var isGoalExists = okrDetails.FirstOrDefault(x => x.GoalObjectiveId == key.GoalObjectiveId && x.IsActive);
                                            if (isGoalExists != null && !goalKeys.Any(x => x.KrStatusId == (int)KrStatus.Accepted))
                                            {
                                                var updateObjectiveStatus = await _okrServiceDataRepository.UpdateGoalKeyStatus(isGoalExists);
                                            }
                                        }
                                    }
                                    var keys = pendingKeys.Select(x => x.GoalKeyId).ToList();
                                    var updateStatus = await _okrServiceDataRepository.UpdateGoalKeyStatus(keys);
                                    var okrDetailsWithoutKr = await _okrServiceDataRepository.GetAllOkrWithoutKeyResultAsync();
                                    var updateOkrWithoutKrKeys = okrDetailsWithoutKr.Where(x => x.GoalStatusId == (int)GoalStatus.Draft && x.ObjectiveCycleId == cycleItem.OrganisationCycleId).Select(x => x.GoalObjectiveId).ToList();
                                    var statusOkrWithoutKrKeys = await _okrServiceDataRepository.UpdateGoalObjectiveWithoutKeyStatus(updateOkrWithoutKrKeys);

                                }
                            }
                        }
                    }
                }
            }
        }

        [FunctionName("ClosingOKRCycle")]
        public async Task ClosingOKRCycle([TimerTrigger("0 30 18 * * *")] TimerInfo myTimer, ILogger log)
        {
            var organisations = await _adminDataRepository.GetOrganisationsData();
            var userDetails = await _adminDataRepository.GetAdminData();
            if (organisations != null)
            {
                foreach (var item in organisations)
                {
                    ////Will fetch active organisationCycle
                    var cycle = await _adminDataRepository.GetOrganisationCycles(item.OrganisationId);
                    foreach (var cycleItem in cycle)
                    {
                        ////will find which cycle is active now
                        bool isCurrentCycle = cycleItem.CycleStartDate <= DateTime.UtcNow && cycleItem.CycleEndDate >= DateTime.UtcNow;
                        //&& Convert.ToDateTime(cycleItem.CycleEndDate).ToString("dd-MM-yyyy") == DateTime.UtcNow.AddDays(AppConstants.OkrCycleDuration).ToString("dd-MM-yyyy");
                        if (isCurrentCycle)
                        {
                            var goalDetails = await _okrServiceDataRepository.GetCycleBaseGoalKeyAsync(cycleItem.OrganisationCycleId);
                            if (goalDetails != null)
                            {
                                var activeUsers = goalDetails.Where(x => x.KrStatusId == (int)KrStatus.Accepted && x.GoalStatusId == (int)GoalStatus.Public).Select(x => Convert.ToInt64(x.EmployeeId)).Distinct().ToList();
                                foreach (var user in activeUsers)
                                {
                                    var userData = userDetails.FirstOrDefault(x => x.EmployeeId == user && x.IsActive);
                                    if (userData != null)
                                    {
                                        string theDate = string.Format("{0}, {1} {2}", cycleItem.CycleEndDate.Value.ToString("dddd"), GetOrdinal(cycleItem.CycleEndDate.Value.Day), cycleItem.CycleEndDate.Value.ToString("MMMM yyyy"));
                                        var template = await _notificationsAndEmails.GetMailerTemplate(TemplateCodes.COC.ToString());
                                        string body = template.Body;
                                        body = body.Replace("topBar", AppConstants.CloudFrontUrl + AppConstants.TopBar)
                                            .Replace("logo", AppConstants.CloudFrontUrl + AppConstants.LogoImage)
                                            .Replace("<RedirectOkR>", AppConstants.ApplicationUrl + "?redirectUrl=unlock-me&empId=" + userData.EmployeeId)
                                            .Replace("<URL>", AppConstants.ApplicationUrl)
                                            .Replace("login", AppConstants.CloudFrontUrl + AppConstants.LoginImage)
                                            .Replace("Leader", userData.FirstName)
                                             .Replace("Iwill", "I'll")
                                            .Replace("Closing", theDate)
                                            .Replace("watch", AppConstants.CloudFrontUrl + AppConstants.Watch)
                                            .Replace("<dashUrl>", AppConstants.ApplicationUrl + "?redirectUrl=unlock-me&empId=" + userData.EmployeeId)
                                            .Replace("<RedirectOkR>", AppConstants.ApplicationUrl)
                                            .Replace("<supportEmailId>", AppConstants.UnlockSupportEmailId)
                                            .Replace("<unlocklink>", AppConstants.ApplicationUrl).Replace("dot", AppConstants.CloudFrontUrl + AppConstants.DotImage).Replace("year", Convert.ToString(DateTime.Now.Year))
                                            .Replace("footer", AppConstants.CloudFrontUrl + AppConstants.footer)
                                            .Replace("srcFacebook", AppConstants.CloudFrontUrl + AppConstants.Facebook).Replace("srcInstagram", AppConstants.CloudFrontUrl + AppConstants.Instagram)
                                            .Replace("srcTwitter", AppConstants.CloudFrontUrl + AppConstants.Twitter).Replace("srcLinkedin", AppConstants.CloudFrontUrl + AppConstants.Linkedin)
                                            .Replace("ijk", AppConstants.InstagramUrl).Replace("lk", AppConstants.LinkedInUrl)
                                            .Replace("fb", AppConstants.FacebookURL).Replace("terp", AppConstants.TwitterUrl)
                                            .Replace("Heres", "Here's");

                                        MailRequest mailRequest = new MailRequest();
                                        mailRequest.Body = body;
                                        mailRequest.MailTo = userData.EmailId;
                                        mailRequest.Subject = template.Subject;

                                        await _notificationsAndEmails.SentMailWithoutAuthenticationAsync(mailRequest);

                                        List<long> to = new List<long>();
                                        NotificationsRequest notificationsRequest = new NotificationsRequest();
                                        ////Notification To ReportingManager
                                        to.Add(userData.EmployeeId);
                                        notificationsRequest.To = to;
                                        notificationsRequest.By = 195;
                                        notificationsRequest.Url = "";
                                        notificationsRequest.Text = AppConstants.NotificationsClosingOKRCycle;
                                        notificationsRequest.AppId = Apps.AppId;
                                        notificationsRequest.NotificationType = (int)NotificationType.LoginReminderForUser;
                                        notificationsRequest.MessageType = (int)MessageTypeForNotifications.Alerts;
                                        await _notificationsAndEmails.InsertNotificationDetails(notificationsRequest);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        [FunctionName("UpdateSourceBeforePlanningSession")]
        public async Task UpdateSourceBeforePlanningSession([TimerTrigger("0 30 18 * * *")] TimerInfo myTimer, ILogger log)
        {
            var organisations = await _adminDataRepository.GetOrganisationsData();
            var userDetails = await _adminDataRepository.GetAdminData();

            if (organisations != null)
            {
                foreach (var item in organisations)
                {
                    ////Will fetch active organisationCycle
                    var cycle = await _adminDataRepository.GetOrganisationCycles(item.OrganisationId);
                    foreach (var cycleItem in cycle)
                    {
                        ////will find which cycle is active now
                        bool isCurrentCycle = cycleItem.CycleStartDate <= DateTime.UtcNow && cycleItem.CycleEndDate >= DateTime.UtcNow;
                        if (isCurrentCycle)
                        {
                            ////we are getting the dates on which mail should be send to source in planning session
                            var goalUnlockDate = await _adminDataRepository.GetGoalUnlockDateData();
                            var goalLockedDate = goalUnlockDate.Where(x => x.OrganisationCycleId == cycleItem.OrganisationCycleId);

                            DateTime goalSubmitDate = goalLockedDate.Count() != 0 ? goalLockedDate.FirstOrDefault(x => x.Type == AppConstants.SubmitData).SubmitDate : cycleItem.CycleStartDate.AddDays(AppConstants.OkrLockDuration);

                            var date = goalSubmitDate.AddDays(-2);// 2 days before the planning session

                            //// if (date.ToString("dd-MM-yyyy") == DateTime.Now.ToString("dd-MM-yyyy"))
                            //// {

                            var source = await _okrServiceDataRepository.GetAllSource(cycleItem.OrganisationCycleId);

                            if (source != null)
                            {
                                foreach (var user in source)
                                {
                                    var userData = userDetails.FirstOrDefault(x => x.EmployeeId == user && x.IsActive);
                                    if (userData != null)
                                    {

                                        var distinctContributors = await _okrServiceDataRepository.GetAllContributors(user, cycleItem.OrganisationCycleId);
                                        var contributorsUsers = distinctContributors.Select(x => x.EmployeeId).Distinct().Take(3);

                                        Dictionary<long, int> KeyCount = new Dictionary<long, int>();
                                        foreach (var contri in contributorsUsers)
                                        {
                                            var topKey = distinctContributors.Where(x => x.EmployeeId == contri).ToList();
                                            if (topKey != null && topKey.Count > 0)
                                            {
                                                foreach (var cont in topKey)
                                                {

                                                    if (!KeyCount.ContainsKey((long)cont.EmployeeId))
                                                    {
                                                        KeyCount.Add((long)cont.EmployeeId, 1);
                                                    }
                                                    else
                                                    {
                                                        KeyCount[(long)cont.EmployeeId]++;
                                                    }

                                                }

                                            }

                                        }


                                        var template = await _notificationsAndEmails.GetMailerTemplate(TemplateCodes.ES.ToString());
                                        string body = template.Body;
                                        body = body.Replace("topBar", AppConstants.CloudFrontUrl + AppConstants.TopBar).Replace("logo", AppConstants.CloudFrontUrl + AppConstants.LogoImage).Replace("<RedirectOkR>", AppConstants.ApplicationUrl + "?redirectUrl=unlock-me&empId=" + user)
                                       .Replace("<URL>", AppConstants.ApplicationUrl).Replace("login", AppConstants.CloudFrontUrl + AppConstants.LoginImage).Replace("name", userData.FirstName).Replace("watch", AppConstants.CloudFrontUrl + AppConstants.Watch).Replace("<dashUrl>", AppConstants.ApplicationUrl + "?redirectUrl=unlock-me&empId=" + user)
                                       .Replace("<RedirectOkR>", AppConstants.ApplicationUrl).Replace("<supportEmailId>", AppConstants.UnlockSupportEmailId).Replace("<unlocklink>", AppConstants.ApplicationUrl).Replace("dot", AppConstants.CloudFrontUrl + AppConstants.DotImage).Replace("year", Convert.ToString(DateTime.Now.Year))
                                       .Replace("footer", AppConstants.CloudFrontUrl + AppConstants.footer)
                                       .Replace("srcFacebook", AppConstants.CloudFrontUrl + AppConstants.Facebook).Replace("srcInstagram", AppConstants.CloudFrontUrl + AppConstants.Instagram)
                                       .Replace("srcTwitter", AppConstants.CloudFrontUrl + AppConstants.Twitter).Replace("srcLinkedin", AppConstants.CloudFrontUrl + AppConstants.Linkedin)
                                       .Replace("ijk", AppConstants.InstagramUrl).Replace("lk", AppConstants.LinkedInUrl)
                                       .Replace("fb", AppConstants.FacebookURL).Replace("terp", AppConstants.TwitterUrl)
                                       .Replace("Heres", "Here's");

                                        if (KeyCount.Count > 0)
                                        {
                                            MailRequest mailRequest = new MailRequest();
                                            var summary = string.Empty;
                                            var counter = 0;
                                            foreach (var cont in KeyCount)
                                            {
                                                counter = counter + 1;
                                                ////Contributors details 
                                                var childDetails = userDetails.FirstOrDefault(x => x.EmployeeId == cont.Key && x.IsActive);
                                                summary = summary + "<tr><td valign =\"top\" cellpadding=\"0\" cellspacing=\"0\" style=\"font-size:16px;line-height:24px;color:#292929;font-family: Calibri,Arial;padding-right: 3px;\">" + " " + counter + " " + "." + " </td><td valign =\"top\" cellpadding=\"0\" cellspacing=\"0\" style=\"font-size:16px;line-height:24px;color:#292929;font-family: Calibri,Arial;\">" + " " + childDetails.FirstName + " " + "has" + " " + cont.Value.ToWords() + " " + "pending assignment(s).</td></tr>";
                                            }
                                            var updatedBody = body;
                                            updatedBody = updatedBody.Replace("<Gist>", summary);
                                            mailRequest.Body = updatedBody;
                                            mailRequest.MailTo = userData.EmailId;
                                            mailRequest.Subject = template.Subject;

                                            await _notificationsAndEmails.SentMailWithoutAuthenticationAsync(mailRequest);

                                            NotificationsRequest notificationsRequest = new NotificationsRequest();
                                            List<long> to = new List<long>();

                                            to.Add(userData.EmployeeId);
                                            notificationsRequest.To = to;
                                            notificationsRequest.By = 620;
                                            notificationsRequest.Url = "";
                                            notificationsRequest.Text = AppConstants.BeforePlanningSession;
                                            notificationsRequest.AppId = Apps.AppId;
                                            notificationsRequest.NotificationType = (int)NotificationType.LoginReminderForUser;
                                            notificationsRequest.MessageType = (int)MessageTypeForNotifications.Alerts;
                                            await _notificationsAndEmails.InsertNotificationDetails(notificationsRequest);

                                        }

                                    }
                                }

                            }
                            //// }
                            //}
                        }
                    }
                }
            }

        }


        /// <summary>
        /// Weekly mails to users every friday if users have not picked up draft OKRs in the planning session
        /// </summary>
        /// <returns></returns>
        /// 
        [FunctionName("SendWeeklyMailForDraftOkr")]
        public async Task SendWeeklyMailForDraftOkr([TimerTrigger("0 30 18 * * *")] TimerInfo myTimer, ILogger log)
        {
            var organisations = await _adminDataRepository.GetOrganisationsData();
            var userDetails = await _adminDataRepository.GetAdminData();

            if (organisations != null)
            {
                foreach (var item in organisations)
                {
                    ////Will fetch active organisationCycle
                    var cycle = await _adminDataRepository.GetOrganisationCycles(item.OrganisationId);
                    foreach (var cycleItem in cycle)
                    {
                        ////will find which cycle is active now
                        bool isCurrentCycle = cycleItem.CycleStartDate <= DateTime.UtcNow && cycleItem.CycleEndDate >= DateTime.UtcNow;

                        if (isCurrentCycle && DateTime.Now.DayOfWeek == DayOfWeek.Friday)
                        {
                            var goalDetails = await _okrServiceDataRepository.GetAllOkrAsync();
                            if (goalDetails != null)
                            {
                                var draftOkrOfCurrentCycle = goalDetails.Where(x => x.IsActive && x.GoalStatusId == (int)GoalStatus.Draft && x.ObjectiveCycleId == cycleItem.OrganisationCycleId).ToList();
                                if (draftOkrOfCurrentCycle.Count > 0 && draftOkrOfCurrentCycle.Any())
                                {
                                    var sourceUsers = draftOkrOfCurrentCycle.GroupBy(x => x.CreatedBy).Select(x => Convert.ToInt64(x.Key)).ToList();
                                    foreach (var user in sourceUsers)
                                    {
                                        var userData = userDetails.FirstOrDefault(x => x.EmployeeId == user && x.IsActive);
                                        if (userData != null)
                                        {
                                            var sourceWithDraftOkr = draftOkrOfCurrentCycle.Where(x => x.CreatedBy == user).ToList();

                                            if (sourceWithDraftOkr.Count > 0 && sourceWithDraftOkr.Any())
                                            {
                                                var summary = string.Empty;
                                                var count = string.Empty;
                                                var cycleSymbolDetails = _adminDataRepository.GetCycleSymbolById(cycleItem.SymbolId);
                                                var okrList = sourceWithDraftOkr.Take(3);
                                                foreach (var draftOkr in okrList)
                                                {
                                                    var keyDetails = await _okrServiceDataRepository.GetKeyByGoalObjectiveIdAsync(draftOkr.GoalObjectiveId);
                                                    var keyCount = keyDetails.Count();
                                                    if (keyCount <= 9)
                                                    {
                                                        count = "0" + Convert.ToString(keyCount);
                                                    }
                                                    else
                                                    {
                                                        count = Convert.ToString(keyCount);
                                                    }

                                                    var stringLen = draftOkr.ObjectiveName.Length;
                                                    if (stringLen > 117)
                                                    {
                                                        draftOkr.ObjectiveName = draftOkr.ObjectiveName.Substring(0, 117) + "...";
                                                    }

                                                    summary = summary + "<tr><td cellspacing =\"0\" cellpadding=\"0\" style=\"padding-bottom: 10px;\"><table width =\"100%\" cellspacing=\"0\" cellpadding=\"0\"style =\"background-color: #ffffff;  border-radius: 6px;box-shadow:0px 0px 5px rgba(41, 41, 41, 0.1);\"><tr><td cellspacing =\"0\" cellpadding=\"0\" style=\"padding: 5px;\"><table width =\"100%\" cellspacing=\"0\" cellpadding=\"0\"><tr><td cellspacing =\"0\" cellpadding=\"0\"style =\"padding: 5px 15px;\"><table width =\"100%\" cellspacing=\"0\" cellpadding=\"0\"><tr><td width =\"75%\" cellspacing=\"0\" cellpadding=\"0\"style =\"width:75%\"><table width =\"100%\" cellspacing=\"0\"cellpadding =\"0\"><tr><td cellspacing =\"0\" cellpadding=\"0\"style =\"font-size:16px;line-height:22px;font-weight:400;color:#292929;font-family: Calibri,Arial;padding-bottom: 16px;\">" + draftOkr.ObjectiveName + "</td></tr><tr><td cellspacing =\"0\" cellpadding=\"0\"><table width =\"auto\"cellspacing =\"0\"cellpadding =\"0\"><tr><td cellspacing =\"0\"cellpadding =\"0\"valign =\"middle\"align =\"center\"height =\"20\"style =\"color: #ffffff; padding-left: 10px;padding-right:8px;border-radius: 3px;\"bgcolor =\"#39A3FA\"><table width =\"100%\"cellspacing =\"0\"cellpadding =\"0\"><tr><td cellspacing =\"0\"cellpadding =\"0\"valign =\"middle\"><img src =\"" + AppConstants.CloudFrontUrl + AppConstants.RightImage + "\"alt =\"arrow\"style =\"display: block;\" /></td><td cellspacing =\"0\"cellpadding =\"0\"valign =\"middle\"style =\"font-size:12px;line-height:14px;font-weight:bold;color:#ffffff;font-family: Calibri,Arial;padding-left: 6px;\">" + count + " Key Results</td></tr></table></td></tr></table></tr></table></td><td cellspacing =\"0\" cellpadding=\"0\"align =\"right\" valign=\"top\"><table width =\"100%\" cellspacing=\"0\"cellpadding =\"0\"><tr><td cellspacing =\"0\" cellpadding=\"0\"align =\"right\"style =\"padding-top: 7px;\"valign =\"top\"><table cellspacing =\"0\"cellpadding =\"0\"><tr><td cellspacing =\"0\"cellpadding =\"0\"valign =\"top\"style =\"font-size:16px;line-height:18px;font-weight:500;color:#292929;font-family: Calibri,Arial;padding-right: 18px;\">" + CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(draftOkr.Enddate.Month) + " " + draftOkr.Enddate.Day + "</td><td cellspacing =\"0\"cellpadding =\"0\"valign =\"top\"><img src =\"" + AppConstants.CloudFrontUrl + AppConstants.Calendar + "\"alt =\"cal\"style =\"display: inline-block;\" /></td></tr></table></td></tr><tr><td cellspacing =\"0\" cellpadding=\"0\"align =\"right\" valign=\"top\" style =\"text-align:right;font-size:12px;line-height:12px;font-weight:500;color:#626262;font-family: Calibri,Arial;padding-right: 5px;\">Cycle: " + cycleSymbolDetails.Symbol + ", " + cycleItem.CycleYear + "</td></tr></table></td></tr></table></td></tr></table></td></tr></table></td></tr> ";
                                                }

                                                var template = await _notificationsAndEmails.GetMailerTemplate(TemplateCodes.DWS.ToString());
                                                string body = template.Body;
                                                var subject = template.Subject;
                                                var loginUrl = AppConstants.ApplicationUrl;
                                                if (!string.IsNullOrEmpty(loginUrl))
                                                {
                                                    loginUrl = loginUrl + "?redirectUrl=unlock-me&empId=" + user;
                                                }

                                                body = body.Replace("topBar", AppConstants.CloudFrontUrl + AppConstants.TopBar).Replace("<URL>", loginUrl).Replace("logo", AppConstants.CloudFrontUrl + AppConstants.LogoImage)
                                                        .Replace("srcFacebook", AppConstants.CloudFrontUrl + AppConstants.Facebook).Replace("srcInstagram", AppConstants.CloudFrontUrl + AppConstants.Instagram)
                                                        .Replace("srcTwitter", AppConstants.CloudFrontUrl + AppConstants.Twitter).Replace("srcLinkedin", AppConstants.CloudFrontUrl + AppConstants.Linkedin)
                                                        .Replace("ijk", AppConstants.InstagramUrl).Replace("lk", AppConstants.LinkedInUrl)
                                                        .Replace("fb", AppConstants.FacebookURL).Replace("terp", AppConstants.TwitterUrl)
                                                        .Replace("name", userData.FirstName).Replace("infoIcon", AppConstants.CloudFrontUrl + AppConstants.InfoIcon).Replace("count", Convert.ToString(sourceWithDraftOkr.Count))
                                                        .Replace("Listing", summary).Replace("<Button>", loginUrl).Replace("supportEmailId", AppConstants.UnlockSupportEmailId)
                                                        .Replace("year", Convert.ToString(DateTime.Now.Year));

                                                subject = subject.Replace("<username>", userData.FirstName);

                                                if (userData.EmailId != null && template.Subject != "")
                                                {
                                                    var mailRequest = new MailRequest
                                                    {
                                                        MailTo = userData.EmailId,
                                                        Subject = subject,
                                                        Body = body
                                                    };
                                                    await _notificationsAndEmails.SentMailWithoutAuthenticationAsync(mailRequest);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary> 
        /// Mail to users post the conclusion of planning session if users have not picked up draft OKRs
        /// </summary>
        /// <returns></returns>
        /// 
        [FunctionName("SendMailForDraftOkrArchived")]
        public async Task SendMailForDraftOkrArchived([TimerTrigger("0 30 18 * * *")] TimerInfo myTimer, ILogger log)
        {
            var organisations = await _adminDataRepository.GetOrganisationsData();
            var userDetails = await _adminDataRepository.GetAdminData();

            if (organisations != null)
            {
                foreach (var item in organisations)
                {
                    ////Will fetch active organisationCycle
                    var cycle = await _adminDataRepository.GetOrganisationCycles(item.OrganisationId);
                    foreach (var cycleItem in cycle)
                    {
                        ////will find which cycle is active now
                        bool isCurrentCycle = cycleItem.CycleStartDate <= DateTime.UtcNow && cycleItem.CycleEndDate >= DateTime.UtcNow;
                        if (isCurrentCycle)
                        {
                            var goalUnlockDate = await _adminDataRepository.GetGoalUnlockDateData();
                            var goalLockedDate = goalUnlockDate.Where(x => x.OrganisationCycleId == cycleItem.OrganisationCycleId);

                            DateTime goalSubmitDate = goalLockedDate.Count() != 0 ? goalLockedDate.FirstOrDefault(x => x.Type == AppConstants.SubmitData).SubmitDate : cycleItem.CycleStartDate.AddDays(AppConstants.OkrLockDuration);
                            var archiveDraftObjectives = new List<GoalObjective>();

                            // if (goalSubmitDate.ToString("dd-MM-yyyy") == DateTime.Now.ToString("dd-MM-yyyy"))
                            //  {
                            var goalDetails = await _okrServiceDataRepository.GetAllOkrAsync();
                            if (goalDetails != null)
                            {
                                var draftOkrOfCurrentCycle = goalDetails.Where(x => x.IsActive && x.GoalStatusId == (int)GoalStatus.Archive && x.ObjectiveCycleId == cycleItem.OrganisationCycleId).ToList();
                                foreach (var archiveOkr in draftOkrOfCurrentCycle)
                                {
                                    var keyDetails = await _okrServiceDataRepository.GetKeyByGoalObjectiveIdAsync(archiveOkr.GoalObjectiveId);
                                    var archiveKey = keyDetails.Where(x => x.GoalStatusId == (int)GoalStatus.Archive && x.KrStatusId == (int)KrStatus.Accepted).ToList();
                                    if (keyDetails.Count() == 0 || archiveKey.Count > 0)
                                    {
                                        archiveDraftObjectives.Add(archiveOkr);
                                    }
                                }

                                if (archiveDraftObjectives.Count > 0 && archiveDraftObjectives.Any())
                                {
                                    var sourceUsers = archiveDraftObjectives.GroupBy(x => x.CreatedBy).Select(x => Convert.ToInt64(x.Key)).ToList();
                                    foreach (var user in sourceUsers)
                                    {
                                        var userData = userDetails.FirstOrDefault(x => x.EmployeeId == user && x.IsActive);
                                        if (userData != null)
                                        {
                                            var sourceWithDraftOkr = archiveDraftObjectives.Where(x => x.CreatedBy == user).ToList();

                                            if (sourceWithDraftOkr.Count > 0 && sourceWithDraftOkr.Any())
                                            {
                                                var template = await _notificationsAndEmails.GetMailerTemplate(TemplateCodes.DPS.ToString());
                                                var body = template.Body;
                                                var subject = template.Subject;
                                                var loginUrl = AppConstants.ApplicationUrl;
                                                if (!string.IsNullOrEmpty(loginUrl))
                                                {
                                                    loginUrl = loginUrl + "?redirectUrl=unlock-me&empId=" + user;
                                                }
                                                var getUnlockedUrl = AppConstants.ApplicationUrl;
                                                if (!string.IsNullOrEmpty(loginUrl))
                                                {
                                                    getUnlockedUrl = getUnlockedUrl + "?redirectUrl=unlockaccount/user&empId=" + user;
                                                }

                                                body = body.Replace("topBar", AppConstants.CloudFrontUrl + AppConstants.TopBar).Replace("<URL>", loginUrl).Replace("logo", AppConstants.CloudFrontUrl + AppConstants.LogoImage)
                                                       .Replace("srcFacebook", AppConstants.CloudFrontUrl + AppConstants.Facebook).Replace("srcInstagram", AppConstants.CloudFrontUrl + AppConstants.Instagram)
                                                    .Replace("srcTwitter", AppConstants.CloudFrontUrl + AppConstants.Twitter).Replace("srcLinkedin", AppConstants.CloudFrontUrl + AppConstants.Linkedin)
                                                    .Replace("ijk", AppConstants.InstagramUrl).Replace("lk", AppConstants.LinkedInUrl)
                                                    .Replace("fb", AppConstants.FacebookURL).Replace("terp", AppConstants.TwitterUrl).Replace("name", userData.FirstName)
                                                    .Replace("<Button>", getUnlockedUrl).Replace("messageInterm", AppConstants.CloudFrontUrl + AppConstants.MessageIntermImage).Replace("supportEmailId", AppConstants.UnlockSupportEmailId)
                                                    .Replace("year", Convert.ToString(DateTime.Now.Year));

                                                subject = subject.Replace("<username>", userData.FirstName);

                                                if (userData.EmailId != null && template.Subject != "")
                                                {
                                                    var mailRequest = new MailRequest
                                                    {
                                                        MailTo = userData.EmailId,
                                                        Subject = subject,
                                                        Body = body
                                                    };
                                                    await _notificationsAndEmails.SentMailWithoutAuthenticationAsync(mailRequest);
                                                }

                                            }
                                        }
                                    }
                                }
                            }
                            // }
                        }
                    }
                }
            }
        }

        //[FunctionName("OkrKRContributorPendingAfter3Days")]
        //public async Task OkrKRContributorPendingAfter3Days([TimerTrigger("0 * * * * *")] TimerInfo myTimer, ILogger log)
        //{
        //    var organisations = await _adminDataRepository.GetOrganisationsData();
        //    var userDetails = await _adminDataRepository.GetAdminData();
        //    if (organisations != null)
        //    {
        //        foreach (var item in organisations)
        //        {
        //            ///Will fetch active organisationCycle
        //            var cycle = await _adminDataRepository.GetOrganisationCycles(item.OrganisationId);
        //            foreach (var cycleItem in cycle)
        //            {
        //                ///will find which cycle is active now
        //                bool isCurrentCycle = cycleItem.CycleStartDate <= DateTime.UtcNow && cycleItem.CycleEndDate >= DateTime.UtcNow;

        //                if (isCurrentCycle)
        //                {
        //                    var goalDetails = await _okrServiceDataRepository.GetCycleBaseGoalKeyAsync(cycleItem.OrganisationCycleId);
        //                    if (goalDetails != null)
        //                    {
        //                        var importedIds = goalDetails.Where(x => x.ImportedType == (int)GoalType.GoalKey
        //                                          && x.KrStatusId == (int)KrStatus.Pending
        //                                          //&& Convert.ToDateTime(x.CreatedOn.AddDays(3)).ToString("dd-MM-yyyy") == DateTime.Now.ToString("dd-MM-yyyy")
        //                                          ).Select(x => x.ImportedId).Distinct().ToList();


        //                        var sourceUsers = goalDetails.Where(x => importedIds.Contains(x.GoalKeyId))
        //                                          .Select(x => x.EmployeeId).Distinct().ToList();

        //                        foreach (var user in sourceUsers)
        //                        {

        //                            var contributorWithPendingKey = goalDetails.Where(x => x.ImportedType == (int)GoalType.GoalKey
        //                                         && x.KrStatusId == (int)KrStatus.Pending
        //                                         && Convert.ToDateTime(x.CreatedOn.AddDays(3)).ToString("dd-MM-yyyy") == DateTime.Now.ToString("dd-MM-yyyy")
        //                                         ).OrderByDescending(x => x.CreatedOn).Take(3).ToList();

        //                            if (contributorWithPendingKey != null)
        //                            {
        //                                Dictionary<long, int> KeyCount = new Dictionary<long, int>();
        //                                foreach (var cont in contributorWithPendingKey)
        //                                {
        //                                    if (!KeyCount.ContainsKey((long)cont.EmployeeId))
        //                                    {
        //                                        KeyCount.Add((long)cont.EmployeeId, 1);
        //                                    }
        //                                    else
        //                                    {
        //                                        KeyCount[(long)cont.EmployeeId]++;
        //                                    }
        //                                }

        //                                var userData = userDetails.FirstOrDefault(x => x.EmployeeId == user && x.IsActive);

        //                                var template = await _notificationsAndEmails.GetMailerTemplate(TemplateCodes.COKPS.ToString());
        //                                string body = template.Body;
        //                                body = body.Replace("topBar", AppConstants.CloudFrontUrl + AppConstants.TopBar).Replace("logo", AppConstants.CloudFrontUrl + AppConstants.LogoImage).Replace("<RedirectOkR>", AppConstants.ApplicationUrl + "?redirectUrl=unlock-me&empId=" + user)
        //                                       .Replace("<url>", AppConstants.ApplicationUrl).Replace("login", AppConstants.CloudFrontUrl + AppConstants.LoginImage).Replace("name", userData.FirstName).Replace("watch", AppConstants.CloudFrontUrl + AppConstants.Watch).Replace("<dashUrl>", AppConstants.ApplicationUrl + "?redirectUrl=unlock-me&empId=" + user)
        //                                       .Replace("<RedirectOkR>", AppConstants.ApplicationUrl).Replace("<supportEmailId>", AppConstants.UnlockSupportEmailId).Replace("<unlocklink>", AppConstants.ApplicationUrl).Replace("dot", AppConstants.CloudFrontUrl + AppConstants.DotImage).Replace("yer", Convert.ToString(DateTime.Now.Year))
        //                                       .Replace("footer", AppConstants.CloudFrontUrl + AppConstants.footer).Replace("Heres", "Here's").Replace("<linkedin>", AppConstants.LinkedinLink).Replace("linkedin", AppConstants.CloudFrontUrl + AppConstants.LinkedInImage).Replace("<pri>", AppConstants.PrivacyPolicy).Replace("<tos>", AppConstants.TermsOfUse);

        //                                if (KeyCount.Count > 0)
        //                                {
        //                                    MailRequest mailRequest = new MailRequest();
        //                                    var summary = string.Empty;
        //                                    var counter = 0;
        //                                    foreach (var cont in KeyCount)
        //                                    {
        //                                        counter = counter + 1;
        //                                        ///Contributors details 
        //                                        var childDetails = userDetails.FirstOrDefault(x => x.EmployeeId == cont.Key && x.IsActive);
        //                                        summary = summary + "<tr><td valign =\"top\" cellpadding=\"0\" cellspacing=\"0\" style=\"font-size:16px;line-height:24px;color:#292929;font-family: Calibri,Arial;padding-right: 3px;\">" + " " + counter + " " + "." + " </td><td valign =\"top\" cellpadding=\"0\" cellspacing=\"0\" style=\"font-size:16px;line-height:24px;color:#292929;font-family: Calibri,Arial;\">" + " " + childDetails.FirstName + " " + "has" + " " + cont.Value.ToWords() + " " + "pending assignment(s).</td></tr>";
        //                                    }
        //                                    var updatedBody = body;
        //                                    updatedBody = updatedBody.Replace("<Gist>", summary);
        //                                    mailRequest.Body = updatedBody;
        //                                    mailRequest.MailTo = userData.EmailId;
        //                                    mailRequest.Subject = template.Subject;

        //                                    await _notificationsAndEmails.SentMailWithoutAuthenticationAsync(mailRequest);
        //                                }

        //                            }

        //                        }

        //                    }
        //                }
        //            }
        //        }
        //    }
        //}

        [FunctionName("OkrKRAssignmentPendingAfter7Days")]
        public async Task OkrKRAssignmentPendingAfter7Days([TimerTrigger("0 30 18 * * *")] TimerInfo myTimer, ILogger log)
        {
            var organisations = await _adminDataRepository.GetOrganisationsData();
            var userDetails = await _adminDataRepository.GetAdminData();
            if (organisations != null)
            {
                foreach (var item in organisations)
                {
                    ////Will fetch active organisationCycle
                    var cycle = await _adminDataRepository.GetOrganisationCycles(item.OrganisationId);
                    foreach (var cycleItem in cycle)
                    {
                        ////will find which cycle is active now
                        bool isCurrentCycle = cycleItem.CycleStartDate <= DateTime.UtcNow && cycleItem.CycleEndDate >= DateTime.UtcNow;

                        if (isCurrentCycle)
                        {
                            ////we are getting the dates on which mail should be send to source in planning session
                            var goalUnlockDate = await _adminDataRepository.GetGoalUnlockDateData();
                            var goalLockedDate = goalUnlockDate.Where(x => x.OrganisationCycleId == cycleItem.OrganisationCycleId);

                            DateTime goalSubmitDate = goalLockedDate.Count() != 0 ? goalLockedDate.FirstOrDefault(x => x.Type == AppConstants.SubmitData).SubmitDate : cycleItem.CycleStartDate.AddDays(AppConstants.OkrLockDuration);
                            if (goalSubmitDate >= DateTime.Now)
                            {
                                var goalDetails = await _okrServiceDataRepository.GetCycleBaseGoalKeyAsync(cycleItem.OrganisationCycleId);
                                if (goalDetails != null)
                                {

                                    var activeUsers = goalDetails.Where(x => x.KrStatusId == (int)KrStatus.Pending
                                                     //&& Convert.ToDateTime(x.CreatedOn.AddDays(7)).ToString("dd-MM-yyyy") == DateTime.Now.ToString("dd-MM-yyyy")
                                                     ).ToList();

                                    foreach (var user in activeUsers)
                                    {
                                        var userData = userDetails.FirstOrDefault(x => x.EmployeeId == user.EmployeeId && x.IsActive);
                                        if (userData != null)
                                        {
                                            string theDate = string.Format("{0} {1}", GetOrdinal(user.CreatedOn.Day), user.CreatedOn.ToString("MMMM"));
                                            var template = await _notificationsAndEmails.GetMailerTemplate(TemplateCodes.KRP7.ToString());
                                            var keyUrl = user.GoalObjectiveId != 0 ? AppConstants.ApplicationUrl + "?redirectUrl=" + "KRAcceptDecline" + "/" + user.AssignmentTypeId + "/" + user.GoalObjectiveId + "/" + user.GoalKeyId + "&empId=" + user.EmployeeId : AppConstants.ApplicationUrl + "?redirectUrl=" + "KRAcceptDecline" + "/" + user.AssignmentTypeId + "/" + user.GoalKeyId + "/" + user.GoalObjectiveId + "&empId=" + user.EmployeeId;
                                            string body = template.Body;
                                            body = body.Replace("topBar", AppConstants.CloudFrontUrl + AppConstants.TopBar)
                                                .Replace("logo", AppConstants.CloudFrontUrl + AppConstants.LogoImage)
                                                .Replace("<RedirectOkR>", keyUrl)
                                                .Replace("<URL>", AppConstants.ApplicationUrl)
                                                .Replace("login", AppConstants.CloudFrontUrl + AppConstants.LoginImage)
                                                .Replace("name", userData.FirstName)
                                                .Replace("ObjectiveOKR", '"' + user.KeyDescription.Trim() + '"')
                                                 .Replace("OKRDate", '"' + theDate + '"')
                                                .Replace("watch", AppConstants.CloudFrontUrl + AppConstants.Watch)
                                                .Replace("<dashUrl>", AppConstants.ApplicationUrl + "?redirectUrl=unlock-me&empId=" + userData.EmployeeId)
                                                //.Replace("<RedirectOkR>", AppConstants.ApplicationUrl)
                                                .Replace("<supportEmailId>", AppConstants.UnlockSupportEmailId)
                                                .Replace("<unlocklink>", AppConstants.ApplicationUrl)
                                                .Replace("year", Convert.ToString(DateTime.Now.Year))
                                                .Replace("footer", AppConstants.CloudFrontUrl + AppConstants.footer)
                                                .Replace("srcFacebook", AppConstants.CloudFrontUrl + AppConstants.Facebook)
                                                .Replace("srcTwitter", AppConstants.CloudFrontUrl + AppConstants.Twitter)
                                                .Replace("fb", AppConstants.FacebookURL).Replace("terp", AppConstants.TwitterUrl)
                                                .Replace("srcInstagram", AppConstants.CloudFrontUrl + AppConstants.Instagram)
                                                .Replace("ijk", AppConstants.InstagramUrl)
                                                .Replace("donot", "don't").Replace("srcLinkedin", AppConstants.CloudFrontUrl + AppConstants.Linkedin)
                                                .Replace("lk", AppConstants.LinkedInUrl)
                                                .Replace("hand-shake2", AppConstants.CloudFrontUrl + AppConstants.HandShakeImage);

                                            MailRequest mailRequest = new MailRequest();
                                            mailRequest.Body = body;
                                            mailRequest.MailTo = userData.EmailId;
                                            mailRequest.Subject = template.Subject;

                                            await _notificationsAndEmails.SentMailWithoutAuthenticationAsync(mailRequest);

                                            List<long> to = new List<long>();
                                            NotificationsRequest notificationsRequest = new NotificationsRequest();
                                            ////Notification To ReportingManager
                                            to.Add(userData.EmployeeId);
                                            notificationsRequest.To = to;
                                            notificationsRequest.By = 195;
                                            notificationsRequest.Url = "";
                                            notificationsRequest.Text = AppConstants.NotificationsOkrKRAssignmentPendingAfter7Days.Replace("<Contributor>", userData.FirstName).Replace("<krdate>", '"' + theDate + '"').Replace("<OKR/KRName>", '"' + user.KeyDescription.Trim() + '"');

                                            notificationsRequest.AppId = Apps.AppId;
                                            notificationsRequest.NotificationType = (int)NotificationType.LoginReminderForUser;
                                            notificationsRequest.MessageType = (int)MessageTypeForNotifications.Alerts;
                                            await _notificationsAndEmails.InsertNotificationDetails(notificationsRequest);
                                        }

                                    }


                                }
                            }
                        }
                    }
                }
            }
        }

        [FunctionName("OkrKRAssignmentPendingAfter14Days")]
        public async Task OkrKRAssignmentPendingAfter14Days([TimerTrigger("0 30 18 * * *")] TimerInfo myTimer, ILogger log)
        {
            var organisations = await _adminDataRepository.GetOrganisationsData();
            var userDetails = await _adminDataRepository.GetAdminData();
            if (organisations != null)
            {
                foreach (var item in organisations)
                {
                    ////Will fetch active organisationCycle
                    var cycle = await _adminDataRepository.GetOrganisationCycles(item.OrganisationId);
                    foreach (var cycleItem in cycle)
                    {
                        ////will find which cycle is active now
                        bool isCurrentCycle = cycleItem.CycleStartDate <= DateTime.UtcNow && cycleItem.CycleEndDate >= DateTime.UtcNow;

                        if (isCurrentCycle)
                        {
                            ////we are getting the dates on which mail should be send to source in planning session
                            var goalUnlockDate = await _adminDataRepository.GetGoalUnlockDateData();
                            var goalLockedDate = goalUnlockDate.Where(x => x.OrganisationCycleId == cycleItem.OrganisationCycleId);

                            DateTime goalSubmitDate = goalLockedDate.Count() != 0 ? goalLockedDate.FirstOrDefault(x => x.Type == AppConstants.SubmitData).SubmitDate : cycleItem.CycleStartDate.AddDays(AppConstants.OkrLockDuration);
                            if (goalSubmitDate >= DateTime.Now)
                            {
                                var goalDetails = await _okrServiceDataRepository.GetCycleBaseGoalKeyAsync(cycleItem.OrganisationCycleId);
                                if (goalDetails != null)
                                {

                                    var contributers = goalDetails.Where(x => x.KrStatusId == (int)KrStatus.Pending
                                                     //&& Convert.ToDateTime(x.CreatedOn.AddDays(14)).ToString("dd-MM-yyyy") == DateTime.Now.ToString("dd-MM-yyyy")
                                                     ).ToList();

                                    foreach (var user in contributers)
                                    {
                                        var userData = userDetails.FirstOrDefault(x => x.EmployeeId == user.EmployeeId && x.IsActive);
                                        if (userData != null)
                                        {
                                            string theDate = string.Format("{0} {1}", GetOrdinal(user.CreatedOn.Day), user.CreatedOn.ToString("MMMM"));
                                            var template = await _notificationsAndEmails.GetMailerTemplate(TemplateCodes.KRP14.ToString());
                                            var keyUrl = user.GoalObjectiveId != 0 ? AppConstants.ApplicationUrl + "?redirectUrl=" + "KRAcceptDecline" + "/" + user.AssignmentTypeId + "/" + user.GoalObjectiveId + "/" + user.GoalKeyId + "&empId=" + user.EmployeeId : AppConstants.ApplicationUrl + "?redirectUrl=" + "KRAcceptDecline" + "/" + user.AssignmentTypeId + "/" + user.GoalKeyId + "/" + user.GoalObjectiveId + "&empId=" + user.EmployeeId;
                                            string body = template.Body;
                                            body = body.Replace("topBar", AppConstants.CloudFrontUrl + AppConstants.TopBar)
                                                .Replace("logo", AppConstants.CloudFrontUrl + AppConstants.LogoImage)
                                                .Replace("<RedirectOkR>", keyUrl)
                                                .Replace("<URL>", AppConstants.ApplicationUrl)
                                                .Replace("login", AppConstants.CloudFrontUrl + AppConstants.LoginImage)
                                                .Replace("name", userData.FirstName)
                                                .Replace("watch", AppConstants.CloudFrontUrl + AppConstants.Watch)
                                                .Replace("<dashUrl>", AppConstants.ApplicationUrl + "?redirectUrl=unlock-me&empId=" + userData.EmployeeId)
                                                //.Replace("<RedirectOkR>", AppConstants.ApplicationUrl)
                                                .Replace("<supportEmailId>", AppConstants.UnlockSupportEmailId)
                                                .Replace("<unlocklink>", AppConstants.ApplicationUrl)
                                                .Replace("year", Convert.ToString(DateTime.Now.Year))
                                                .Replace("footer", AppConstants.CloudFrontUrl + AppConstants.footer)
                                                .Replace("srcFacebook", AppConstants.CloudFrontUrl + AppConstants.Facebook).Replace("srcInstagram", AppConstants.CloudFrontUrl + AppConstants.Instagram)
                                                .Replace("srcTwitter", AppConstants.CloudFrontUrl + AppConstants.Twitter).Replace("srcLinkedin", AppConstants.CloudFrontUrl + AppConstants.Linkedin)
                                                .Replace("fb", AppConstants.FacebookURL).Replace("terp", AppConstants.TwitterUrl)
                                                .Replace("ijk", AppConstants.InstagramUrl)
                                                .Replace("donot", "don't")
                                                .Replace("lk", AppConstants.LinkedInUrl)
                                                .Replace("OKRDate", '"' + theDate + '"')
                                                .Replace("hand-shake2", AppConstants.CloudFrontUrl + AppConstants.HandShakeImage)
                                                .Replace("ObjectiveOKR", '"' + user.KeyDescription.Trim() + '"');

                                            MailRequest mailRequest = new MailRequest();
                                            mailRequest.Body = body;
                                            mailRequest.MailTo = userData.EmailId;
                                            mailRequest.Subject = template.Subject;

                                            await _notificationsAndEmails.SentMailWithoutAuthenticationAsync(mailRequest);

                                            List<long> to = new List<long>();
                                            NotificationsRequest notificationsRequest = new NotificationsRequest();
                                            ////Notification To ReportingManager
                                            to.Add(userData.EmployeeId);
                                            notificationsRequest.To = to;
                                            notificationsRequest.By = 195;
                                            notificationsRequest.Url = "";
                                            notificationsRequest.Text = AppConstants.NotificationsOkrKRAssignmentPendingAfter14Days.Replace("<Contributor>", userData.FirstName).Replace("<krdate>", '"' + theDate + '"').Replace("<OKR/KRName>", '"' + user.KeyDescription.Trim() + '"');
                                            notificationsRequest.AppId = Apps.AppId;
                                            notificationsRequest.NotificationType = (int)NotificationType.LoginReminderForUser;
                                            notificationsRequest.MessageType = (int)MessageTypeForNotifications.Alerts;
                                            await _notificationsAndEmails.InsertNotificationDetails(notificationsRequest);
                                        }

                                    }


                                }
                            }
                        }
                    }
                }
            }
        }

        [FunctionName("OkrPlanningSessionClose")]
        public async Task OkrPlanningSessionClose([TimerTrigger("0 30 18 * * *")] TimerInfo myTimer, ILogger log)
        {
            var organisations = await _adminDataRepository.GetOrganisationsData();
            var userDetails = await _adminDataRepository.GetAdminData();
            if (organisations != null)
            {
                foreach (var item in organisations)
                {
                    ////Will fetch active organisationCycle
                    var cycle = await _adminDataRepository.GetOrganisationCycles(item.OrganisationId);
                    foreach (var cycleItem in cycle)
                    {
                        ////will find which cycle is active now
                        bool isCurrentCycle = cycleItem.CycleStartDate <= DateTime.UtcNow && cycleItem.CycleEndDate >= DateTime.UtcNow;

                        if (isCurrentCycle)
                        {
                            ////we are getting the dates on which mail should be send to source in planning session
                            var goalUnlockDate = await _adminDataRepository.GetGoalUnlockDateData();
                            var goalLockedDate = goalUnlockDate.Where(x => x.OrganisationCycleId == cycleItem.OrganisationCycleId);

                            DateTime goalSubmitDate = goalLockedDate.Count() != 0 ? goalLockedDate.FirstOrDefault(x => x.Type == AppConstants.SubmitData).SubmitDate : cycleItem.CycleStartDate.AddDays(AppConstants.OkrLockDuration);
                            //if (goalSubmitDate.ToString("dd-MM-yyyy") == DateTime.Now.ToString("dd-MM-yyyy"))
                            //{
                            var goalDetails = await _okrServiceDataRepository.GetCycleBaseGoalKeyAsync(cycleItem.OrganisationCycleId);
                            if (goalDetails != null)
                            {
                                ////For pending KR lat 7 days                               
                                var allGoalDetails = goalDetails.Where(x => x.ImportedType == (int)GoalType.GoalKey
                                                  && x.KrStatusId == (int)KrStatus.Pending
                                                  //&& Convert.ToDateTime(x.CreatedOn).ToString("dd-MM-yyyy") == DateTime.Now.ToString("dd-MM-yyyy")
                                                  ).Distinct().ToList();

                                //Assigne EmployeeList/Contributer
                                var allEmpgoals = allGoalDetails.Select(x => x.EmployeeId).Distinct().ToList();

                                foreach (var itemEmp in allEmpgoals)
                                {
                                    //user name
                                    var userData = userDetails.FirstOrDefault(x => x.EmployeeId == itemEmp && x.IsActive);
                                    if (userData != null)
                                    {
                                        var userGoals = allGoalDetails.Where(x => x.EmployeeId == itemEmp).Distinct().ToList();

                                        // get Source user importdis

                                        var topThreegoals = userGoals.OrderByDescending(x => x.CreatedOn).Take(3).ToList();

                                        var importedIds = topThreegoals.Select(x => x.ImportedId).Distinct().ToList();

                                        // get unique Source user employyeid
                                        //var sourceImportUsersGolas = goalDetails.Where(x => importedIds.Contains(x.GoalKeyId))
                                        //             .Distinct().ToList();
                                        var sourceImportUsers = goalDetails.Where(x => importedIds.Contains(x.GoalKeyId)).Select(x => x.EmployeeId)
                                                    .Distinct().ToList();

                                        var counter = 0;
                                        var summary = string.Empty;
                                        var template = await _notificationsAndEmails.GetMailerTemplate(TemplateCodes.KRP.ToString());
                                        string body = template.Body;
                                        body = body.Replace("topBar", AppConstants.CloudFrontUrl + AppConstants.TopBar)
                                            .Replace("logo", AppConstants.CloudFrontUrl + AppConstants.LogoImage)
                                            .Replace("<RedirectOkR>", AppConstants.ApplicationUrl + "?redirectUrl=unlockaccount/user&empId=" + userData.EmployeeId)
                                            .Replace("<URL>", AppConstants.ApplicationUrl)
                                            .Replace("login", AppConstants.CloudFrontUrl + AppConstants.LoginImage)
                                            .Replace("name", userData.FirstName)
                                            .Replace("hand-shake2", AppConstants.CloudFrontUrl + AppConstants.HandShakeImage)
                                            .Replace("watch", AppConstants.CloudFrontUrl + AppConstants.Watch)
                                            .Replace("<dashUrl>", AppConstants.ApplicationUrl + "?redirectUrl=unlock-me&empId=" + userData.EmployeeId)
                                            .Replace("<supportEmailId>", AppConstants.UnlockSupportEmailId)
                                            .Replace("year", Convert.ToString(DateTime.Now.Year))
                                            .Replace("footer", AppConstants.CloudFrontUrl + AppConstants.footer)
                                            .Replace("srcFacebook", AppConstants.CloudFrontUrl + AppConstants.Facebook).Replace("srcInstagram", AppConstants.CloudFrontUrl + AppConstants.Instagram)
                                            .Replace("srcTwitter", AppConstants.CloudFrontUrl + AppConstants.Twitter).Replace("srcLinkedin", AppConstants.CloudFrontUrl + AppConstants.Linkedin)
                                            .Replace("fb", AppConstants.FacebookURL).Replace("terp", AppConstants.TwitterUrl)
                                            .Replace("ijk", AppConstants.InstagramUrl)
                                            .Replace("Heres", "Here's").Replace("lk", AppConstants.LinkedInUrl);
                                        if (sourceImportUsers.Count > 0)
                                        {
                                            foreach (var sourceuser in sourceImportUsers)
                                            {
                                                string kRresult = string.Empty;
                                                counter = counter + 1;
                                                var userSourceName = userDetails.FirstOrDefault(x => x.EmployeeId == sourceuser && x.IsActive);

                                                var sourceIdlist = goalDetails.Where(x => importedIds.Contains(x.GoalKeyId) && x.EmployeeId == sourceuser).Select(x => x.GoalKeyId).ToList();

                                                var keylist = userGoals.Where(x => sourceIdlist.Contains(x.ImportedId)).ToList();
                                                foreach (var itemkey in keylist)
                                                {
                                                    var keyUrl = itemkey.GoalObjectiveId != 0 ? AppConstants.ApplicationUrl + "?redirectUrl=" + "KRAcceptDecline" + "/" + itemkey.AssignmentTypeId + "/" + itemkey.GoalObjectiveId + "/" + itemkey.GoalKeyId + "&empId=" + userData.EmployeeId : AppConstants.ApplicationUrl + "?redirectUrl=" + "KRAcceptDecline" + "/" + itemkey.AssignmentTypeId + "/" + itemkey.GoalKeyId + "/" + itemkey.GoalObjectiveId + "&empId=" + userData.EmployeeId;
                                                    kRresult += "<a href =\"<UserRedirectOkR>\" style=\"font-size:16px;line-height:24px;color:#39A3FA;font-family: Calibri,Arial;font-weight: bold;text-decoration: none;\">" + itemkey.KeyDescription.Trim() + "</a>,";
                                                    kRresult = kRresult.Replace("<UserRedirectOkR>", keyUrl);//userdata.employyeid

                                                }
                                                kRresult = kRresult.Remove(kRresult.Length - 1);
                                                kRresult = kRresult + " from " + userSourceName.FirstName;
                                                summary = summary + "<tr><td valign =\"top\" cellpadding=\"0\" cellspacing=\"0\" style=\"font-size:16px;line-height:24px;color:#292929;font-family: Calibri,Arial;padding-right: 3px;\">" + " " + counter + "." + " </td><td valign =\"top\" cellpadding=\"0\" cellspacing=\"0\" style=\"font-size:16px;line-height:24px;color:#292929;font-family: Calibri,Arial;\">" + kRresult + "</td></tr>";

                                            }

                                            MailRequest mailRequest = new MailRequest();
                                            var updatedBody = body;
                                            updatedBody = updatedBody.Replace("<Gist>", summary);
                                            mailRequest.Body = updatedBody;
                                            mailRequest.MailTo = userData.EmailId;
                                            mailRequest.Subject = template.Subject;

                                            await _notificationsAndEmails.SentMailWithoutAuthenticationAsync(mailRequest);
                                        }
                                    }

                                }


                            }
                            //}
                        }
                    }
                }
            }
        }

        [FunctionName("OkrKRContributorPendingAfter3Days")]
        public async Task OkrKRContributorPendingAfter3Days([TimerTrigger("0 30 18 * * *")] TimerInfo myTimer, ILogger log)
        {
            var organisations = await _adminDataRepository.GetOrganisationsData();
            var userDetails = await _adminDataRepository.GetAdminData();

            if (organisations != null)
            {
                foreach (var item in organisations)
                {
                    ////Will fetch active organisationCycle
                    var cycle = await _adminDataRepository.GetOrganisationCycles(item.OrganisationId);
                    foreach (var cycleItem in cycle)
                    {
                        ////will find which cycle is active now
                        bool isCurrentCycle = cycleItem.CycleStartDate <= DateTime.UtcNow && cycleItem.CycleEndDate >= DateTime.UtcNow;
                        if (isCurrentCycle)
                        {
                            ////we are getting the dates on which mail should be send to source in planning session
                            var goalUnlockDate = await _adminDataRepository.GetGoalUnlockDateData();
                            var goalLockedDate = goalUnlockDate.Where(x => x.OrganisationCycleId == cycleItem.OrganisationCycleId);

                            DateTime goalSubmitDate = goalLockedDate.Count() != 0 ? goalLockedDate.FirstOrDefault(x => x.Type == AppConstants.SubmitData).SubmitDate : cycleItem.CycleStartDate.AddDays(AppConstants.OkrLockDuration);

                            var date = goalSubmitDate.AddDays(-2);// 2 days before the planning session

                            //// if (date.ToString("dd-MM-yyyy") == DateTime.Now.ToString("dd-MM-yyyy"))
                            //// {

                            var source = await _okrServiceDataRepository.GetAllSource(cycleItem.OrganisationCycleId);

                            if (source != null)
                            {
                                foreach (var user in source)
                                {
                                    var userData = userDetails.FirstOrDefault(x => x.EmployeeId == user && x.IsActive);
                                    if (userData != null)
                                    {
                                        var distinctContributors = await _okrServiceDataRepository.GetAllContributors(user, cycleItem.OrganisationCycleId);
                                        var contributorsUsers = distinctContributors.Select(x => x.EmployeeId).Distinct().Take(3);

                                        Dictionary<long, int> KeyCount = new Dictionary<long, int>();
                                        foreach (var contri in contributorsUsers)
                                        {
                                            var topKey = distinctContributors.Where(x => x.EmployeeId == contri).ToList();
                                            if (topKey != null && topKey.Count > 0)
                                            {
                                                foreach (var cont in topKey)
                                                {

                                                    if (!KeyCount.ContainsKey((long)cont.EmployeeId))
                                                    {
                                                        KeyCount.Add((long)cont.EmployeeId, 1);
                                                    }
                                                    else
                                                    {
                                                        KeyCount[(long)cont.EmployeeId]++;
                                                    }

                                                }

                                            }

                                        }
                                        var template = await _notificationsAndEmails.GetMailerTemplate(TemplateCodes.COKPS.ToString());
                                        string body = template.Body;
                                        body = body.Replace("topBar", AppConstants.CloudFrontUrl + AppConstants.TopBar).Replace("logo", AppConstants.CloudFrontUrl + AppConstants.LogoImage).Replace("<RedirectOkR>", AppConstants.ApplicationUrl + "?redirectUrl=unlock-me&empId=" + user)
                                       .Replace("<URL>", AppConstants.ApplicationUrl).Replace("login", AppConstants.CloudFrontUrl + AppConstants.LoginImage).Replace("name", userData.FirstName).Replace("watch", AppConstants.CloudFrontUrl + AppConstants.Watch).Replace("<dashUrl>", AppConstants.ApplicationUrl + "?redirectUrl=unlock-me&empId=" + user)
                                       .Replace("<RedirectOkR>", AppConstants.ApplicationUrl).Replace("<supportEmailId>", AppConstants.UnlockSupportEmailId).Replace("<unlocklink>", AppConstants.ApplicationUrl).Replace("dot", AppConstants.CloudFrontUrl + AppConstants.DotImage).Replace("year", Convert.ToString(DateTime.Now.Year))
                                       .Replace("footer", AppConstants.CloudFrontUrl + AppConstants.footer)
                                       .Replace("srcFacebook", AppConstants.CloudFrontUrl + AppConstants.Facebook).Replace("srcInstagram", AppConstants.CloudFrontUrl + AppConstants.Instagram)
                                      .Replace("srcTwitter", AppConstants.CloudFrontUrl + AppConstants.Twitter).Replace("srcLinkedin", AppConstants.CloudFrontUrl + AppConstants.Linkedin)
                                      .Replace("ijk", AppConstants.InstagramUrl).Replace("lk", AppConstants.LinkedInUrl)
                                      .Replace("fb", AppConstants.FacebookURL).Replace("terp", AppConstants.TwitterUrl)
                                      .Replace("Heres", "Here's");

                                        if (KeyCount.Count > 0)
                                        {
                                            MailRequest mailRequest = new MailRequest();
                                            var summary = string.Empty;
                                            var counter = 0;
                                            foreach (var cont in KeyCount)
                                            {
                                                counter = counter + 1;
                                                ////Contributors details 
                                                var childDetails = userDetails.FirstOrDefault(x => x.EmployeeId == cont.Key && x.IsActive);
                                                summary = summary + "<tr><td valign =\"top\" cellpadding=\"0\" cellspacing=\"0\" style=\"font-size:16px;line-height:24px;color:#292929;font-family: Calibri,Arial;padding-right: 3px;\">" + " " + counter + " " + "." + " </td><td valign =\"top\" cellpadding=\"0\" cellspacing=\"0\" style=\"font-size:16px;line-height:24px;color:#292929;font-family: Calibri,Arial;\">" + " " + childDetails.FirstName + " " + "has" + " " + cont.Value.ToWords() + " " + "pending assignment(s).</td></tr>";
                                            }
                                            var updatedBody = body;
                                            updatedBody = updatedBody.Replace("<Gist>", summary);
                                            mailRequest.Body = updatedBody;
                                            mailRequest.MailTo = userData.EmailId;
                                            mailRequest.Subject = template.Subject;

                                            await _notificationsAndEmails.SentMailWithoutAuthenticationAsync(mailRequest);

                                            NotificationsRequest notificationsRequest = new NotificationsRequest();
                                            List<long> to = new List<long>();

                                            to.Add(userData.EmployeeId);
                                            notificationsRequest.To = to;
                                            notificationsRequest.By = 620;
                                            notificationsRequest.Url = "";
                                            notificationsRequest.Text = AppConstants.BeforePlanningSession;
                                            notificationsRequest.AppId = Apps.AppId;
                                            notificationsRequest.NotificationType = (int)NotificationType.LoginReminderForUser;
                                            notificationsRequest.MessageType = (int)MessageTypeForNotifications.Alerts;
                                            await _notificationsAndEmails.InsertNotificationDetails(notificationsRequest);

                                        }
                                    }
                                }

                            }
                            //// }
                            //}
                        }
                    }
                }
            }

        }

        [FunctionName("UsersInActiveOKR")]
        public async Task UsersInActiveOKR([TimerTrigger("0 30 18 * * *")] TimerInfo myTimer, ILogger log)
        {
            var organisations = await _adminDataRepository.GetOrganisationsData();
            var userDetails = await _adminDataRepository.GetAdminData();

            if (organisations != null)
            {
                foreach (var item in organisations)
                {
                    ////Will fetch active organisationCycle
                    var cycle = await _adminDataRepository.GetOrganisationCycles(item.OrganisationId);
                    foreach (var cycleItem in cycle)
                    {
                        ////will find which cycle is active now
                        bool isCurrentCycle = cycleItem.CycleStartDate <= DateTime.UtcNow && cycleItem.CycleEndDate >= DateTime.UtcNow;
                        if (isCurrentCycle)
                        {

                            var goalAccepted = await _okrServiceDataRepository.GetAllGoalKeyParentobjective(cycleItem.OrganisationCycleId);
                            var finalGoalKeyList = (from accepted in goalAccepted
                                                    where (cycleItem.CycleEndDate > DateTime.UtcNow
                                                    && ((accepted.UpdatedOn != null && Convert.ToDateTime(accepted.UpdatedOn).Date != DateTime.UtcNow.Date && ((DateTime.UtcNow.Date - Convert.ToDateTime(accepted.UpdatedOn).Date).TotalDays) % 7 == 0)
                                                    || (accepted.CreatedOn != null && Convert.ToDateTime(accepted.CreatedOn).Date != DateTime.UtcNow.Date && ((DateTime.UtcNow.Date - Convert.ToDateTime(accepted.CreatedOn).Date).TotalDays) % 7 == 0))
                                                    )
                                                    select accepted).ToList();

                            var contributerUsers = finalGoalKeyList.GroupBy(x => x.EmployeeId).Select(x => Convert.ToInt64(x.Key)).ToList();
                            var goalOkrList = await _okrServiceDataRepository.GetAllOkrAsync(cycleItem.OrganisationCycleId);

                            foreach (var user in contributerUsers)
                            {
                                var summary = string.Empty;
                                var userData = userDetails.FirstOrDefault(x => x.EmployeeId == user && x.IsActive);
                                if (userData != null)
                                {
                                    var NormalGoalKeyList = finalGoalKeyList.Where(x => x.GoalObjectiveId != 0 && x.EmployeeId == user).GroupBy(x => x.GoalObjectiveId).Select(x => x.Key).ToList();
                                    foreach (var okr in NormalGoalKeyList)
                                    {
                                        var count = string.Empty;
                                        var cycleSymbolDetails = _adminDataRepository.GetCycleSymbolById(cycleItem.SymbolId);
                                        var goalkey = finalGoalKeyList.FirstOrDefault(x => x.GoalObjectiveId == okr && x.EmployeeId == user);
                                        var goalObjectivedetails = goalOkrList.FirstOrDefault(x => x.GoalObjectiveId == okr);

                                        var keyCount = finalGoalKeyList.Where(x => x.GoalObjectiveId == okr && x.KrStatusId == (int)KrStatus.Accepted).ToList().Count;
                                        if (keyCount <= 9)
                                        {
                                            count = "0" + Convert.ToString(keyCount);
                                        }
                                        else
                                        {
                                            count = Convert.ToString(keyCount);
                                        }

                                        var stringLen = goalObjectivedetails.ObjectiveName.Length;
                                        if (stringLen > 117)
                                        {
                                            goalObjectivedetails.ObjectiveName = goalObjectivedetails.ObjectiveName.Substring(0, 117) + "...";
                                        }

                                        summary = summary + "<tr><td cellspacing =\"0\" cellpadding=\"0\" style=\"padding-bottom: 10px;\"><table width =\"100%\" cellspacing=\"0\" cellpadding=\"0\"style =\"background-color: #ffffff;  border-radius: 6px;box-shadow:0px 0px 5px rgba(41, 41, 41, 0.1);\"><tr><td cellspacing =\"0\" cellpadding=\"0\" style=\"padding: 5px;\"><table width =\"100%\" cellspacing=\"0\" cellpadding=\"0\"><tr><td cellspacing =\"0\" cellpadding=\"0\"style =\"padding: 5px 15px;\"><table width =\"100%\" cellspacing=\"0\" cellpadding=\"0\"><tr><td width =\"75%\" cellspacing=\"0\" cellpadding=\"0\"style =\"width:75%\"><table width =\"100%\" cellspacing=\"0\"cellpadding =\"0\"><tr><td cellspacing =\"0\" cellpadding=\"0\"style =\"font-size:16px;line-height:22px;font-weight:400;color:#292929;font-family: Calibri,Arial;padding-bottom: 16px;\">" + goalObjectivedetails.ObjectiveName + "</td></tr><tr><td cellspacing =\"0\" cellpadding=\"0\"><table width =\"auto\"cellspacing =\"0\"cellpadding =\"0\"><tr><td cellspacing =\"0\"cellpadding =\"0\"valign =\"middle\"align =\"center\"height =\"20\"style =\"color: #ffffff; padding-left: 10px;padding-right:8px;border-radius: 3px;\"bgcolor =\"#39A3FA\"><table width =\"100%\"cellspacing =\"0\"cellpadding =\"0\"><tr><td cellspacing =\"0\"cellpadding =\"0\"valign =\"middle\"><img src =\"" + AppConstants.CloudFrontUrl + AppConstants.RightImage + "\"alt =\"arrow\"style =\"display: block;\" /></td><td cellspacing =\"0\"cellpadding =\"0\"valign =\"middle\"style =\"font-size:12px;line-height:14px;font-weight:bold;color:#ffffff;font-family: Calibri,Arial;padding-left: 6px;\">" + count + " Key Results</td></tr></table></td></tr></table></tr></table></td><td cellspacing =\"0\" cellpadding=\"0\"align =\"right\" valign=\"top\"><table width =\"100%\" cellspacing=\"0\"cellpadding =\"0\"><tr><td cellspacing =\"0\" cellpadding=\"0\"align =\"right\"style =\"padding-top: 7px;\"valign =\"top\"><table cellspacing =\"0\"cellpadding =\"0\"><tr><td cellspacing =\"0\"cellpadding =\"0\"valign =\"top\"style =\"font-size:16px;line-height:18px;font-weight:500;color:#292929;font-family: Calibri,Arial;padding-right: 18px;\">" + CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(goalkey.DueDate.Month) + " " + goalkey.DueDate.Day + "</td><td cellspacing =\"0\"cellpadding =\"0\"valign =\"top\"><img src =\"" + AppConstants.CloudFrontUrl + AppConstants.Calendar + "\"alt =\"cal\"style =\"display: inline-block;\" /></td></tr></table></td></tr><tr><td cellspacing =\"0\" cellpadding=\"0\"align =\"right\" valign=\"top\" style =\"text-align:right;font-size:12px;line-height:12px;font-weight:500;color:#626262;font-family: Calibri,Arial;padding-right: 5px;\">Cycle: " + cycleSymbolDetails.Symbol + ", " + cycleItem.CycleYear + "</td></tr></table></td></tr></table></td></tr></table></td></tr></table></td></tr> ";

                                    }

                                    var goalKeyStandAlone = finalGoalKeyList.Where(x => x.GoalObjectiveId == 0 && x.EmployeeId == user).ToList();

                                    foreach (var standalone in goalKeyStandAlone)
                                    {
                                        var count = string.Empty;
                                        var cycleSymbolDetails = _adminDataRepository.GetCycleSymbolById(cycleItem.SymbolId);
                                        var stringLen = standalone.KeyDescription.Length;
                                        if (stringLen > 117)
                                        {
                                            standalone.KeyDescription = standalone.KeyDescription.Substring(0, 117) + "...";
                                        }
                                        summary = summary + "<tr><td cellspacing =\"0\" cellpadding=\"0\" style=\"padding-bottom: 10px;\"><table width =\"100%\" cellspacing=\"0\" cellpadding=\"0\"style =\"background-color: #ffffff;  border-radius: 6px;box-shadow:0px 0px 5px rgba(41, 41, 41, 0.1);\"><tr><td cellspacing =\"0\" cellpadding=\"0\" style=\"padding: 5px;\"><table width =\"100%\" cellspacing=\"0\" cellpadding=\"0\"><tr><td cellspacing =\"0\" cellpadding=\"0\" bgcolor=\"#F1F3F4\" style =\"padding: 10px 15px;\"><table width =\"100%\" cellspacing=\"0\" cellpadding=\"0\"><tr><td width =\"75%\" cellspacing=\"0\" cellpadding=\"0\"style =\"width:75%\"><table width =\"100%\" cellspacing=\"0\"cellpadding =\"0\"><tr><td cellspacing =\"0\" cellpadding=\"0\"style =\"font-size:16px;line-height:22px;font-weight:400;color:#292929;font-family: Calibri,Arial;padding-bottom: 16px;\">" + standalone.KeyDescription + "</td></tr><tr><td cellspacing =\"0\" cellpadding=\"0\"><table width =\"auto\"cellspacing =\"0\"cellpadding =\"0\"><tr><td cellspacing =\"0\"cellpadding =\"0\"valign =\"middle\"align =\"center\"height =\"20\"style =\"color: #ffffff; padding-left: 10px;padding-right:8px;border-radius: 3px;\"bgcolor =\"#e3e5e5\"><table width =\"100%\"cellspacing =\"0\"cellpadding =\"0\"><tr><td cellspacing =\"0\"cellpadding =\"0\"valign =\"middle\"><img src =\"" + AppConstants.CloudFrontUrl + AppConstants.LinkImage + "\"alt =\"arrow\"style =\"display: block;\" /></td><td cellspacing =\"0\"cellpadding =\"0\"valign =\"middle\"style =\"font-size:12px;line-height:14px;font-weight:bold;color:#626262;font-family: Calibri,Arial;padding-left: 7px;\">" + count + " Key Results</td></tr></table></td></tr></table></tr></table></td><td cellspacing =\"0\" cellpadding=\"0\"align =\"right\" valign=\"top\"><table width =\"100%\" cellspacing=\"0\"cellpadding =\"0\"><tr><td cellspacing =\"0\" cellpadding=\"0\"align =\"right\"style =\"padding-top: 7px;\"valign =\"top\"><table cellspacing =\"0\"cellpadding =\"0\"><tr><td cellspacing =\"0\"cellpadding =\"0\"valign =\"top\"style =\"font-size:16px;line-height:18px;font-weight:500;color:#292929;font-family: Calibri,Arial;padding-right: 18px;\">" + CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(standalone.DueDate.Month) + " " + standalone.DueDate.Day + "</td><td cellspacing =\"0\"cellpadding =\"0\"valign =\"top\"><img src =\"" + AppConstants.CloudFrontUrl + AppConstants.Calendar + "\"alt =\"cal\"style =\"display: inline-block;\" /></td></tr></table></td></tr><tr><td cellspacing =\"0\" cellpadding=\"0\"align =\"right\" valign=\"top\" style =\"text-align:right;font-size:12px;line-height:12px;font-weight:500;color:#626262;font-family: Calibri,Arial;padding-right: 5px;\">Cycle: " + cycleSymbolDetails.Symbol + ", " + cycleItem.CycleYear + "</td></tr></table></td></tr></table></td></tr></table></td></tr></table></td></tr> ";
                                    }

                                    if (summary != "")
                                    {
                                        var template = await _notificationsAndEmails.GetMailerTemplate(TemplateCodes.NO_ACTIVITY_KR.ToString());
                                        string body = template.Body;
                                        var subject = template.Subject;
                                        var loginUrl = AppConstants.ApplicationUrl;
                                        if (!string.IsNullOrEmpty(loginUrl))
                                        {
                                            loginUrl = loginUrl + "?redirectUrl=unlock-me&empId=" + user;
                                        }
                                        body = body.Replace("topBar", AppConstants.CloudFrontUrl + AppConstants.TopBar).Replace("<URL>", loginUrl).Replace("logo", AppConstants.CloudFrontUrl + AppConstants.LogoImage)
                                            .Replace("srcFacebook", AppConstants.CloudFrontUrl + AppConstants.Facebook).Replace("srcInstagram", AppConstants.CloudFrontUrl + AppConstants.Instagram)
                                            .Replace("srcTwitter", AppConstants.CloudFrontUrl + AppConstants.Twitter).Replace("srcLinkedin", AppConstants.CloudFrontUrl + AppConstants.Linkedin)
                                            .Replace("ijk", AppConstants.InstagramUrl).Replace("lk", AppConstants.LinkedInUrl)
                                            .Replace("fb", AppConstants.FacebookURL).Replace("terp", AppConstants.TwitterUrl)
                                            .Replace("name", userData.FirstName).Replace("infoIcon", AppConstants.CloudFrontUrl + AppConstants.InfoIcon)
                                            .Replace("Listing", summary).Replace("<Button>", loginUrl).Replace("supportEmailId", AppConstants.UnlockSupportEmailId)
                                            .Replace("year", Convert.ToString(DateTime.Now.Year));

                                        subject = subject.Replace("<username>", userData.FirstName);

                                        if (userData.EmailId != null && template.Subject != "")
                                        {
                                            var mailRequest = new MailRequest
                                            {
                                                MailTo = userData.EmailId,
                                                Subject = subject,
                                                Body = body
                                            };
                                            await _notificationsAndEmails.SentMailWithoutAuthenticationAsync(mailRequest);
                                        }
                                    }
                                }
                            }

                        }
                    }
                }
            }
        }

        #region Private Function
        public static string GetOrdinal(int number)
        {
            string suffix = String.Empty;

            int ones = number % 10;
            int tens = (int)Math.Floor(number / 10M) % 10;

            if (tens == 1)
            {
                suffix = "th";
            }
            else
            {
                switch (ones)
                {
                    case 1:
                        suffix = "st";
                        break;

                    case 2:
                        suffix = "nd";
                        break;

                    case 3:
                        suffix = "rd";
                        break;

                    default:
                        suffix = "th";
                        break;
                }
            }
            return String.Format("{0}{1}", number, suffix);
        }
        #endregion

        #endregion

    }
}
