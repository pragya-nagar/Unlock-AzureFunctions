using AzureFunctions.Models;
using AzureFunctions.Repository.Interfaces;
using Dapper;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace AzureFunctions.Repository
{
    public class OkrServiceRepository : BaseRepository, IOkrServiceRepository
    {
        public OkrServiceRepository(IConfiguration configuration, IAdminRepository adminRepository)
   : base(configuration)
        {
        }

        /// <summary>
        /// Get all the KR's of users
        /// </summary>
        /// <returns></returns>
        public async Task<IEnumerable<GoalKey>> GetUsersKRAsync()
        {
            IEnumerable<GoalKey> data = null;
            using (var connection = DbConnectionOkrService)
            {
                if (ConnOkrService != null)
                {

                    data = await connection.QueryAsync<GoalKey>("select EmployeeId  , COUNT(*) count from goalkey where IsActive=1 and DueDate>dateadd(month, +3, getdate()) group by EmployeeId having count(*) >4");
                }
            }

            return data;
        }


        /// <summary>
        /// Get all the Okr's of users where start date should be greater than current date
        /// </summary>
        /// <returns></returns>
        public async Task<IEnumerable<GoalObjective>> GetUsersOkrAsync()
        {
            IEnumerable<GoalObjective> data = null;
            using (var connection = DbConnectionOkrService)
            {
                if (ConnOkrService != null)
                {

                    data = await connection.QueryAsync<GoalObjective>("select EmployeeId  , COUNT(*) count from goalobjective where IsActive=1 and startDate>getdate()  group by EmployeeId having count(*) >4");
                }
            }

            return data;
        }

        public async Task<IEnumerable<GoalObjective>> GetUsersWhoHaveCreatedOkrForNewQuarterAsync()
        {
            IEnumerable<GoalObjective> data = null;
            using (var connection = DbConnectionOkrService)
            {
                if (ConnOkrService != null)
                {

                    data = await connection.QueryAsync<GoalObjective>("select employeeid from GoalObjective where isActive=1 and startDate>getdate() group by employeeid");
                }
            }

            return data;
        }

        /// <summary>
        /// Get all the data from GoalObjective table
        /// </summary>
        /// <returns></returns>
        public async Task<IEnumerable<GoalObjective>> GetAllOkrAsync()
        {
            IEnumerable<GoalObjective> data = null;
            using (var connection = DbConnectionOkrService)
            {
                if (ConnAdmin != null)
                {

                    data = await connection.QueryAsync<GoalObjective>("select * from GoalObjective where IsActive=1");
                }
            }

            return data;

        }

        /// <summary>
        /// Update Goalkey Status to archive 
        /// </summary>
        /// <param name="goalKey"></param>
        /// <returns></returns>
        public async Task<long> UpdateGoalKeyStatus(List<long> goalKey)
        {
            using (var connection = DbConnectionOkrService)
            {
                if (ConnOkrService != null)
                {
                    foreach (var item in goalKey)
                    {
                        string updateQuery = @"UPDATE [DBO].[GoalKey] SET GoalStatusId=3 where goalkeyId =" + item;
                        var result = await connection.ExecuteAsync(updateQuery);

                    }
                }
            }

            return 1;
        }


        /// <summary>
        /// Get all the data from GoalKey table
        /// </summary>
        /// <returns></returns>
        public async Task<IEnumerable<GoalKey>> GetAllKeysAsync()
        {
            IEnumerable<GoalKey> data = null;
            using (var connection = DbConnectionOkrService)
            {
                if (ConnOkrService != null)
                {

                    data = await connection.QueryAsync<GoalKey>("select * from GoalKey where isActive=1");
                }
            }

            return data;

        }


        public async Task<IEnumerable<long>> GetAllSource(long cycleId)
        {
            IEnumerable<long> data = null;
            using (var connection = DbConnectionOkrService)
            {
                if (ConnOkrService != null)
                {

                    data = await connection.QueryAsync<long>("select Distinct EmployeeId from GoalKey where isActive=1 and goalstatusId = 2  and krstatusid = 2 and ImportedId = 0 and cycleId =" + cycleId);
                }
            }

            return data;

        }

        public async Task<IEnumerable<GoalKey>> GetAllContributors(long? employeeId,long cycleId)
        {
            IEnumerable<GoalKey> data = null;
            using (var connection = DbConnectionOkrService)
            {
                if (ConnOkrService != null)
                {

                    data = await connection.QueryAsync<GoalKey>("select * from GoalKey where isActive=1  and  cycleId= " + cycleId + " and  KrStatusId = 1 and ImportedId IN (select goalkeyId from goalkey where cycleId =" + cycleId + " and employeeid = " + employeeId + "and isActive = 1 and goalstatusId = 2 )");
                }
            }

            return data;

        }


       





        /// <summary>
        /// Update Objective status to archive
        /// </summary>
        /// <param name="goalObjective"></param>
        /// <returns></returns>
        public async Task<long> UpdateGoalKeyStatus(GoalObjective goalObjective)
        {
            using (var connection = DbConnectionOkrService)
            {
                if (ConnOkrService != null)
                {

                    string updateQuery = @"UPDATE [DBO].[GoalObjective] SET GoalStatusId=3 where GoalObjectiveId =" + goalObjective.GoalObjectiveId;

                    var result = await connection.ExecuteAsync(updateQuery, new
                    {
                        goalObjectiveId = goalObjective.GoalObjectiveId
                    });

                }
            }

            return 1;

        }

        /// <summary>
        /// Getting objective details on the basis of goalObjectiveId
        /// </summary>
        /// <param name="templateCode"></param>
        /// <returns></returns>
        public GoalObjective GetGoalObjectiveById(long goalObjectiveId)
        {
            var data = new GoalObjective();
            using (var connection = DbConnectionOkrService)
            {
                if (ConnOkrService != null)
                {

                    data = connection.QueryFirst<GoalObjective>("select * from GoalObjective where goalObjectiveId = @goalObjectiveId ", new
                    {

                        GoalObjectiveId = goalObjectiveId,
                        IsActive = 1

                    });
                }
            }
            return data;
        }

        /// <summary>
        /// Get all the key against a particular GoalObjectiveId from GoalKey table
        /// </summary>
        /// <returns></returns>
        public async Task<IEnumerable<GoalKey>> GetKeyByGoalObjectiveIdAsync(long goalObjectiveId)
        {
            IEnumerable<GoalKey> data = null;
            using (var connection = DbConnectionOkrService)
            {
                if (ConnOkrService != null)
                {

                    data = await connection.QueryAsync<GoalKey>("select * from GoalKey where isActive=1 and GoalObjectiveId=" + goalObjectiveId);
                }
            }

            return data;

        }

        public async Task<IEnumerable<GoalKey>> GetKeydetailspending(long cycleId)
        {
            IEnumerable<GoalKey> data = null;
            using (var connection = DbConnectionOkrService)
            {
                if (ConnOkrService != null)
                {

                    data = await connection.QueryAsync<GoalKey>("select  * from Goalkey where isActive = 1 and KrstatusId = 1 and CycleId = " + cycleId + " and GoalStatusId != 3 ");
                }
            }

            return data;

        }

        /// <summary>
        /// Get all the data from GoalObjective table
        /// </summary>
        /// <returns></returns>
        public async Task<IEnumerable<GoalKey>> GetCycleBaseGoalKeyAsync(long cycleId)
        {
            IEnumerable<GoalKey> data = null;
            using (var connection = DbConnectionOkrService)
            {
                if (ConnAdmin != null)
                {

                    data = await connection.QueryAsync<GoalKey>("select * from goalkey where CycleId=" + cycleId + " and IsActive=1");
                }
            }

            return data;

        }

        /// <summary>
        /// Get all the data All Okr Without Key Result from GoalObjective table
        /// </summary>
        /// <returns></returns>
        public async Task<IEnumerable<GoalObjective>> GetAllOkrWithoutKeyResultAsync()
        {
            IEnumerable<GoalObjective> data = null;
            using (var connection = DbConnectionOkrService)
            {
                if (ConnAdmin != null)
                {

                    data = await connection.QueryAsync<GoalObjective>("select * from GoalObjective where GoalStatusId=1 and IsActive=1");
                }
            }

            return data;

        }

        /// <summary>
        /// Update GoalObjectives without Kr Status to archive 
        /// </summary>
        /// <param name="goalKey"></param>
        /// <returns></returns>
        public async Task<long> UpdateGoalObjectiveWithoutKeyStatus(List<long> goalKey)
        {
            using (var connection = DbConnectionOkrService)
            {
                if (ConnOkrService != null)
                {
                    foreach (var item in goalKey)
                    {
                        string updateQuery = @"UPDATE [DBO].[GoalObjective] SET GoalStatusId=3 where GoalObjectiveId =" + item;
                        var result = await connection.ExecuteAsync(updateQuery);

                    }
                }
            }

            return 1;
        }

        /// <summary>
        /// Get all the data from GoalObjective table
        /// </summary>
        /// <returns></returns>
        public async Task<IEnumerable<GoalObjective>> GetAllOkrAsync(long cycleId)
        {
            IEnumerable<GoalObjective> data = null;
            using (var connection = DbConnectionOkrService)
            {
                if (ConnAdmin != null)
                {

                    data = await connection.QueryAsync<GoalObjective>("select * from GoalObjective where ObjectiveCycleId=" + cycleId + " and IsActive=1 order by 1 desc");
                }
            }

            return data;

        }
        
        public async Task<IEnumerable<GoalKey>> GetAllGoalKeyParentobjective(long cycleId)
        {
            IEnumerable<GoalKey> data = null;
            using (var connection = DbConnectionOkrService)
            {
                if (ConnOkrService != null)
                {

                    data = await connection.QueryAsync<GoalKey>("select * from GoalKey where isActive=1 and GoalStatusId=2 and cycleId =" + cycleId + " order by 1 desc");
                }
            }

            return data;

        }

        /// <summary>
        /// Get all the Standalone data from Goal Key table
        /// </summary>
        /// <returns></returns>
        public async Task<IEnumerable<GoalKey>> GetStandaloneKRAsync(long employeeId, long cycleId)
        {
            IEnumerable<GoalKey> data = null;
            using (var connection = DbConnectionOkrService)
            {
                if (ConnAdmin != null)
                {

                    data = await connection.QueryAsync<GoalKey>("select * from goalkey where EmployeeId=" + employeeId + " andCycleId=" + cycleId + " and GoalObjectiveId = 0 and IsActive=1 order by 1 desc ");
                }
            }

            return data;

        }



    }
}
