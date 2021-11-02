

namespace AzureFunctions.Models
{
    public class Employees
    {
        public long EmployeeId
        {
            get;
            set;
        }

        public string EmployeeCode
        {
            get;
            set;
        }

        public virtual string FirstName
        {
            get;
            set;
        }

        public string LastName
        {
            get;
            set;
        }

        public string Password
        {
            get;
            set;
        }

        public string PasswordSalt
        {
            get;
            set;
        }

        public string Designation
        {
            get;
            set;
        }

        public virtual string EmailId
        {
            get;
            set;
        }

        public long? ReportingTo
        {
            get;
            set;
        }

        public string ImagePath
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

        public virtual System.DateTime CreatedOn
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

        public long RoleId
        {
            get;
            set;
        }

        public int? LoginFailCount
        {
            get;
            set;
        }

        public string ProfileImageFile
        {
            get;
            set;
        }
    }
}
