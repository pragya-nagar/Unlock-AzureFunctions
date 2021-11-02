using System;
using System.ComponentModel.DataAnnotations;

namespace AzureFunctions.Models
{
    public class GoalKey
    {
        [Key]
        public long GoalKeyId { get; set; }
        public long GoalObjectiveId { get; set; }
        public string KeyDescription { get; set; }
        public decimal Score { get; set; }
        public DateTime DueDate { get; set; }
        public int ImportedType { get; set; }
        public long ImportedId { get; set; }
        public long CreatedBy { get; set; }
        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
        public long? UpdatedBy { get; set; } = null;
        public DateTime? UpdatedOn { get; set; } = null;
        public bool IsActive { get; set; } = true;
        public int Progress { get; set; }
        public long? EmployeeId { get; set; } = null;
        public long Source { get; set; }
        public DateTime StartDate { get; set; }
        public int MetricId { get; set; }
        public int AssignmentTypeId { get; set; }
        public int CurrencyId { get; set; }
        public decimal CurrentValue { get; set; }
        public decimal TargetValue { get; set; }
        public int KrStatusId { get; set; }
        public int CycleId { get; set; }
        public string CurrencyCode { get; set; }
        public int GoalStatusId { get; set; }
        public decimal ContributorValue { get; set; }
        public decimal StartValue { get; set; }
    }
}
