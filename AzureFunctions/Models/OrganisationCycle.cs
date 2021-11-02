
namespace AzureFunctions.Models
{
    public class OrganisationCycle
    {
        public long OrganisationCycleId
        {
            get;
            set;
        }

        public long CycleDurationId
        {
            get;
            set;
        }

        public int SymbolId
        {
            get;
            set;
        }

        public long OrganisationId
        {
            get;
            set;
        }

        public bool IsActive
        {
            get;
            set;
        }

        public long CreatedBy
        {
            get;
            set;
        }

        public System.DateTime CreatedOn
        {
            get;
            set;
        }

        public long? UpdatedBy
        {
            get;
            set;
        }

        public System.DateTime? UpdatedOn
        {
            get;
            set;
        }

        public System.DateTime CycleStartDate
        {
            get;
            set;
        }

        public System.DateTime? CycleEndDate
        {
            get;
            set;
        }

        public int? CycleYear
        {
            get;
            set;
        }

        public bool? IsDiscarded
        {
            get;
            set;
        }
    }
}
