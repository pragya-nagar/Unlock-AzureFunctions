using AzureFunctions.Models;
using AzureFunctions.Repository.Interfaces;
using Dapper;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Text;
using System.Threading.Tasks;

namespace AzureFunctions.Repository
{
    public class AdminRepository : BaseRepository, IAdminRepository
    {
        public AdminRepository(IConfiguration configuration)
            : base(configuration)
        {

        }

        /// <summary>
        /// Gets all the data from employees table
        /// </summary>
        /// <returns></returns>
        public async Task<IEnumerable<Employees>> GetAdminData()
        {
            IEnumerable<Employees> data = null;
            using (var connection = DbConnectionAdmin)
            {
                if (ConnAdmin != null)
                {

                    data = await connection.QueryAsync<Employees>("Select * from Employees where IsActive = 1");
                }
            }

            return data;

        }

        /// <summary>
        /// Gets all the data from usertoken table
        /// </summary>
        /// <returns></returns>
        public async Task<IEnumerable<UserToken>> GetUserTokenDetails()
        {
            IEnumerable<UserToken> data = null;
            using (var connection = DbConnectionAdmin)
            {
                if (ConnAdmin != null)
                {

                    data = await connection.QueryAsync<UserToken>("Select  * from UserToken where tokentype in (1,3)");
                }
            }

            return data;
        }

        public async Task<IEnumerable<OrganisationCycle>> GetOrganisationCycles(long orgId)
        {
            string currentYear = DateTime.Now.Year.ToString();
            IEnumerable<OrganisationCycle> data = null;
            using (var connection = DbConnectionAdmin)
            {
                if (ConnAdmin != null)
                {

                    data = await connection.QueryAsync<OrganisationCycle>("select * from OrganisationCycle where OrganisationId = " + orgId + "and isActive=1");
                }
            }

            return data;

        }

        /// <summary>
        /// Gets all the data from employees table
        /// </summary>
        /// <returns></returns>
        public async Task<IEnumerable<Organisations>> GetOrganisationsData()
        {
            IEnumerable<Organisations> data = null;
            using (var connection = DbConnectionAdmin)
            {
                if (ConnAdmin != null)
                {

                    data = await connection.QueryAsync<Organisations>("Select * from Organisations where IsActive = 1");
                }
            }

            return data;

        }

        /// <summary>
        /// Gets all the data from GoalUnlockDate table
        /// </summary>
        /// <returns></returns>
        public async Task<IEnumerable<GoalUnlockDate>> GetGoalUnlockDateData()
        {
            IEnumerable<GoalUnlockDate> data = null;
            using (var connection = DbConnectionAdmin)
            {
                if (ConnAdmin != null)
                {

                    data = await connection.QueryAsync<GoalUnlockDate>("Select * from GoalUnlockDate where IsActive = 1");
                }
            }

            return data;

        }

        /// <summary>
        /// Gets Cycle symbol like Q1,Q2 etc by symbolId
        /// </summary>
        /// <returns></returns>
        public CycleDurationSymbols GetCycleSymbolById(int id)
        {
            var data = new CycleDurationSymbols();
            using (var connection = DbConnectionAdmin)
            {
                if (ConnAdmin != null)
                {

                    data = connection.QueryFirst<CycleDurationSymbols>("select * from CycleDurationSymbols where id = @id ", new
                    {

                        Id = id,
                        IsActive = 1

                    });
                }
            }
            return data;
        }


        public Employees GetEmployeeDetails(string id)
        {
            var data = new Employees();
            using (var connection = DbConnectionAdmin)
            {
                if (ConnAdmin != null)
                {

                    data = connection.QueryFirstOrDefault<Employees>("select * from Employees where EmployeeCode = '" + id + "' and isActive = 1");

                }
            }
            return data;
        }


        public Organisations GetOrganisationDetails(string organisationName)
        {
            var data = new Organisations();
            using (var connection = DbConnectionAdmin)
            {
                if (ConnAdmin != null)
                {

                    data = connection.QueryFirstOrDefault<Organisations>("select * from Organisations where OrganisationName = '" + organisationName + "' and isActive = 1");

                }
            }
            return data;
        }

        public Employees GetEmailDetails(string mailId)
        {
            var data = new Employees();
            using (var connection = DbConnectionAdmin)
            {
                if (ConnAdmin != null)
                {

                    data = connection.QueryFirstOrDefault<Employees>("select * from Employees where EmailId = '" + mailId + "' and isActive = 1");

                }
            }
            return data;
        }

        public Employees GetReportingTo(string to)
        {
            var data = new Employees();
            using (var connection = DbConnectionAdmin)
            {
                if (ConnAdmin != null)
                {

                    data = connection.QueryFirstOrDefault<Employees>("select * from Employees where EmployeeCode = '" + to + "' and isActive = 1");

                }
            }
            return data;
        }

    
        public async Task UpdateEmployee(string first, string last, string mailId, string designation, long reporting, bool isActive, long employeeId)
        {

            try
            {
                using (var connection = DbConnectionAdmin)
                {
                    if (ConnAdmin != null)
                    {
                        int present = 0;
                        if (isActive)
                        {
                            present = 1;
                        }
                        var abc = connection.QueryFirstOrDefaultAsync<Employees>("Update Employees set FirstName = '" + first + "', Lastname ='" + last + "', Designation = '" + designation + "',ReportingTo = " + reporting + ", UpdatedOn = getdate(), isActive = " + present + ", emailId = '" + mailId + "' where EmployeeId = " + employeeId);

                    }
                }
            }
            catch (Exception e)
            {
                var msg = e.Message;
            }


        }

        public async Task InsertEmployees(string code, string first, string last, string mailId, string designation, long reporting, string password, string salt, long orgid, bool isActive, long roleId)
        {
            try
            {
                using (var connection = DbConnectionAdmin)
                {
                    if (ConnAdmin != null)
                    {
                        int present = 0;
                        if (isActive)
                        {
                            present = 1;
                        }
                        await connection.QueryFirstOrDefaultAsync<Employees>("INSERT INTO Employees Values('" + code + "','" + first + "','" + last + "','" + password + "','" + salt + "','" + designation + "','" + mailId + "'," + reporting + ",null," + orgid + "," + present + ",0,getdate(),0,getdate()," + roleId + ",0,null)");

                    }
                }
            }
            catch (Exception e)
            {
                var msg = e.Message;
            }


        }


        public async Task UpdateInactiveEmployee(bool isActive, long id)
        {
            try
            {
                using (var connection = DbConnectionAdmin)
                {
                    if (ConnAdmin != null)
                    {
                        int present = 1;
                        if (!isActive)
                        {
                            present = 0;
                        }

                        await connection.QueryFirstOrDefaultAsync<Employees>("Update Employees set  UpdatedOn = getdate(), isActive = " + present + " where EmployeeId =" + id);

                    }
                }
            }
            catch (Exception e)
            {
                var msg = e.Message;
            }


        }


        public async Task<UserToken> GetToken(long id)
        {
            var data = new UserToken();
            using (var connection = DbConnectionAdmin)
            {
                if (ConnAdmin != null)
                {

                    data = await connection.QueryFirstOrDefaultAsync<UserToken>("select * from UserToken where EmployeeId = " + id + " and TokenType = 1");

                }
            }
            return data;
        }

        public async Task UpdateToken(string expireTime, string lastLogin, long id)
        {
            try
            {
                using (var connection = DbConnectionAdmin)
                {
                    if (ConnAdmin != null)
                    {

                        await connection.QueryFirstOrDefaultAsync<UserToken>("Update UserToken set  ExpireTime = '" + expireTime + "',LastLoginDate = '" + lastLogin + "' where EmployeeId = " + id);

                    }
                }
            }
            catch (Exception e)
            {
                var msg = e.Message;
            }
        }



    }
}
