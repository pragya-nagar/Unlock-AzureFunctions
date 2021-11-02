

namespace AzureFunctions.Models
{
    public class GoalUnlockDate
    {
        public long Id
        {
            get;
            set;
        }

        public long OrganisationCycleId
        {
            get;
            set;
        }

        public int Type
        {
            get;
            set;
        }

        public bool IsActive
        {
            get;
            set;
        }

        public System.DateTime SubmitDate
        {
            get;
            set;
        }
    }
}
