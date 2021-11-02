using System;
using System.Collections.Generic;
using System.Text;

namespace AzureFunctions.Common
{
    public static class AppConstants
    {
        public const string NotificationsForNormalCycleStarts = "Planning Session has ended, good luck with your OKRs.";
        public const string NotificationsForReminderOfQuarterEnd = "Only 3 weeks left for your OKRs to progress";
        public const string ReminderByManagerForUserMessage = " <user> has yet to log in!";
        public const string TwentyOneDaysNoProgress = "Your OKRs deserve a bit more of your time. Don't you agree?";
        public const string TwetyEightDaysNoProgress = "It's been almost a month and <user> has made little progress.";
        public const int AppId = 4;
        public const string CloudFrontUrl = "https://unlockokrcdnuat.azureedge.net/unlockuat/";
        public const string ApplicationUrl = "https://uat.unlockokr.com/";
        public const string resetPasswordUrl = "https://uat.unlockokr.com/reset-password?id=";
        public const string NotificationUrl = "https://unlockokr-apim-qa.azure-api.net/notification/";
        public const string PrivacyPolicy = "https://okr-dev-v2.compunnel.com/privacy-policy";
        public const string TermsOfUse = "https://okr-dev-v2.compunnel.com/terms-of-use";
        public const string LoginImage = "EmailersImages/login.png";
        public const string TopBar = "EmailersImages/topBar.png";
        public const string LogoImage = "EmailersImages/logo.png";
        public const string LoginButtonImage = "EmailersImages/login.png";
        public const string HandShakeImage = "EmailersImages/user-manager.png";
        public const string ProgressImage = "EmailersImages/progress.png";
        public const string CalendarImage = "EmailersImages/Calendar.png";
        public const string RightImage = "EmailersImages/right.png";
        public const string Watch = "EmailersImages/watch.png";
        public const string Calendar = "EmailersImages/cal.icon.png";
        public const string LinkImage = "EmailersImages/link.png";
        public const string footer = "EmailersImages/footer-logo.png";
        public const string InfoIcon = "EmailersImages/info-icon.png";
        public const string MessageIntermImage = "EmailersImages/message-interm.png";
        public const string DotImage = "EmailersImages/dot.png";
        public const string LinkedInImage = "EmailersImages/linkden.png";
        public const string LinkedinLink = "https://www.linkedin.com/company/unlock-okr/";
        public const int SubmitData = 1;
        public const int OkrLockDuration = 20;
        public const string UnlockSupportEmailId = "adminsupport@unlockokr.com";
        public const string assignments = "EmailersImages/assignments.png";

        public const string AwsEmailId = "adminsupport@unlockokr.com";
        public const string AccountName = "AKIAJVT7R6HES36CNLWQ";
        public const string Password = "AmbzlYKroTfzrc2+tXUTXYcO55HBd0EfOn1rheEma6Kp";
        public const int Port = 587;
        public const string Host = "email-smtp.us-east-1.amazonaws.com";
        public const string Environment = "Dev";

        public const string Learning = "Learning";
        public const string Digital = "Digital";
        public const string Staffing = "Staffing";
        public const string InfoproLearning = "Infopro Learning";
        public const string CompunnelDigital = "Compunnel Digital";
        public const string CompunnelStaffing = "Compunnel Staffing";
        public const string CompunnelSoftwareGroup = "Compunnel Software Group";
        public const string PassportUserType = "Employee";
        public const string PassportBaseAddress = "https://passporthr.compunnel.com/API/";
        public const string UnlockLearn = "Unlock:learn";
        public const string InfoProLearning = "InfoProLearning";
        public const string Unlocklearn = "Unlock Learn";
        public const string FacebookURL = "https://www.facebook.com/unlockokr";
        public const string TwitterUrl = "https://twitter.com/unlockokr";
        public const string LinkedInUrl = "https://www.linkedin.com/company/unlock-okr";
        public const string InstagramUrl = "https://www.instagram.com/unlockokr";
        public const string Facebook = "EmailersImages/facebook.png";
        public const string Linkedin = "EmailersImages/linkedin.png";
        public const string Twitter = "EmailersImages/twitter.png";
      
        public const string Instagram = "EmailersImages/instagram.png";
        public const int OkrCycleDuration = 8;
        public const string NotificationsClosingOKRCycle = "It's 8 days left from now for the closing cycle. Please take an action to update your OKR's";
        public const string NotificationsOkrKRAssignmentPendingAfter7Days = "<Contributor>, we have not seen an update on your KR <OKR/KRName> assignment done <krdate> since last 7 days. Please act against assigned OKR.";
        public const string BeforePlanningSession = "It's the last 2 days left and you might have to nudge your assigned contributors.";
        public const string NotificationsOkrKRAssignmentPendingAfter14Days = "<Contributor>, we have not seen an update on your KR <OKR/KRName> assignment done <krdate> since last 14 days. Please act against assigned OKR.";


    }

    public static class Message
    {
        public const int Alerts = 2;
    }

    public static class Apps
    {
        public const int AppId = 3;
    }
}
