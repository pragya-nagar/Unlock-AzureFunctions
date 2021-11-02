using AzureFunctions.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace AzureFunctions.Repository.Interfaces
{
   public interface IAdminRepository
    {
        Task<IEnumerable<Employees>> GetAdminData();
        Task<IEnumerable<UserToken>> GetUserTokenDetails();
        Task<IEnumerable<OrganisationCycle>> GetOrganisationCycles(long orgId);
        Task<IEnumerable<Organisations>> GetOrganisationsData();
        Task<IEnumerable<GoalUnlockDate>> GetGoalUnlockDateData();
        CycleDurationSymbols GetCycleSymbolById(int id);
        Employees GetEmployeeDetails(string id);
        Organisations GetOrganisationDetails(string organisationName);
        Employees GetEmailDetails(string mailId);
        Employees GetReportingTo(string to);
        
        Task UpdateEmployee(string first, string last, string mailId, string designation, long reporting, bool isActive, long employeeId);
        Task InsertEmployees(string code, string first, string last, string mailId, string designation, long reporting, string password, string salt, long orgid, bool isActive, long roleId);
        Task UpdateInactiveEmployee(bool isActive, long id);
        Task<UserToken> GetToken(long id);
        Task UpdateToken(string expireTime, string lastLogin, long id);
    }
}
