using AzureFunctions.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace AzureFunctions.Repository.Interfaces
{
    public interface IOkrServiceRepository
    {
        Task<IEnumerable<GoalObjective>> GetUsersOkrAsync();
        Task<IEnumerable<GoalKey>> GetUsersKRAsync();
        Task<IEnumerable<GoalObjective>> GetUsersWhoHaveCreatedOkrForNewQuarterAsync();
        Task<IEnumerable<GoalObjective>> GetAllOkrAsync();
        Task<long> UpdateGoalKeyStatus(List<long> goalKey);
        Task<IEnumerable<GoalKey>> GetAllKeysAsync();
        Task<long> UpdateGoalKeyStatus(GoalObjective goalObjective);
        GoalObjective GetGoalObjectiveById(long goalObjectiveId);
        Task<IEnumerable<GoalKey>> GetKeyByGoalObjectiveIdAsync(long goalObjectiveId);
        Task<IEnumerable<GoalKey>> GetKeydetailspending(long cycleId);
        Task<IEnumerable<GoalKey>> GetCycleBaseGoalKeyAsync(long cycleId);
        Task<IEnumerable<long>> GetAllSource(long cycleId);
        Task<IEnumerable<GoalKey>> GetAllContributors(long? employeeId, long cycleId);
        Task<long> UpdateGoalObjectiveWithoutKeyStatus(List<long> goalKey);
        Task<IEnumerable<GoalObjective>> GetAllOkrWithoutKeyResultAsync();
        Task<IEnumerable<GoalObjective>> GetAllOkrAsync(long cycleId);
        Task<IEnumerable<GoalKey>> GetAllGoalKeyParentobjective(long cycleId);
        Task<IEnumerable<GoalKey>> GetStandaloneKRAsync(long employeeId, long cycleId);
    }
}
