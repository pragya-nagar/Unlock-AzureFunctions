
namespace AzureFunctions.Models
{
    public class Organisations
    {
        public long OrganisationId
        {
            get;
            set;
        }

        public string OrganisationName
        {
            get;
            set;
        }

        public long? OrganisationHead
        {
            get;
            set;
        }

        public string ImagePath
        {
            get;
            set;
        }

        public bool IsActive
        {
            get;
            set;
        }

        public virtual long CreatedBy
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

        public virtual System.DateTime? UpdatedOn
        {
            get;
            set;
        }

        public bool? IsDeleted
        {
            get;
            set;
        }

        public virtual long? ParentId
        {
            get;
            set;
        }

        public string Description
        {
            get;
            set;
        }

        public string LogoName
        {
            get;
            set;
        }
    }
}
