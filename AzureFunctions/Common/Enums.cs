using System;
using System.Collections.Generic;
using System.Text;

namespace AzureFunctions.Common
{
    public enum NotificationType
    {
        LoginReminderForUser = 21,

    }
    public enum MessageTypeForNotifications
    {
        NotificationsMessages = 1,
        Alerts = 2
    }

    public enum TemplateCodes
    {
        LR = 1,
        LRM = 2,
        NCS = 3,
        WRU = 4,
        WRM = 5,
        GD = 6,
        TNP = 7,
        TENP = 8,
        FDNP = 9,
        CPS = 10,
        DIM = 11,
        LDS = 12,
        DOS = 13,
        PC = 14,
        ES = 15,
        COC=16,
        KRP=17,
        DWS = 18,
        DPS = 19,
        COKPS=20,
        KRP7=21,
        KRP14 = 22,
        NO_ACTIVITY_KR = 23
    }

    public enum KrStatus
    {
        Pending = 1,
        Accepted,
        Declined
    }

    public enum GoalStatus
    {
        Draft = 1,
        Public,
        Archive
    }
    public enum GoalType
    {
        GoalObjective = 1,
        GoalKey = 2
    }
}
