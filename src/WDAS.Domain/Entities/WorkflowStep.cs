using WDAS.Domain.Common;
using WDAS.Domain.Enums;

namespace WDAS.Domain.Entities;

public class WorkflowStep : Entity
{
    public int DocumentId { get; set; }
    public int WorkflowVersionId { get; set; }
    public int StepOrder { get; set; }
    public int? ApproverUserId { get; set; }
    public int? ApproverGroupId { get; set; }
    public string? GroupName { get; set; }
    public GroupApprovalRequirement? GroupRequirement { get; set; }
    public WorkflowStepStatus Status { get; set; } = WorkflowStepStatus.Pending;
    public DateTime? ActivatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime? SlaDueAtUtc { get; set; }
    public bool IsSlaBreached { get; set; }
    public bool RequiresReconfirmation { get; set; }

    public Document Document { get; set; } = null!;
    public WorkflowVersion WorkflowVersion { get; set; } = null!;
    public User? ApproverUser { get; set; }
    public ICollection<WorkflowStepAction> Actions { get; set; } = new List<WorkflowStepAction>();
}
