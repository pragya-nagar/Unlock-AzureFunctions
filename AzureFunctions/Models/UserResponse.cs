using System.Collections.Generic;


namespace AzureFunctions.Models
{
    public class UserResponse
    {
        public long EmployeeId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string EmployeeCode { get; set; }
        public string EmailId { get; set; }
        public IList<RoleDetails> RoleDetails { get; set; }
        public long OrganisationID { get; set; }
        public string OrganisationName { get; set; }
        public bool IsActive { get; set; }
        public string ReportingTo { get; set; }
        public string ImagePath { get; set; }
        public string Designation { get; set; }
    }
}
