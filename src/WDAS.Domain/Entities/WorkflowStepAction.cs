using WDAS.Domain.Common;
using WDAS.Domain.Enums;

namespace WDAS.Domain.Entities;

public class WorkflowStepAction : Entity
{
    public int WorkflowStepId { get; set; }
    public int ActorUserId { get; set; }
    public int? OnBehalfOfUserId { get; set; }
    public WorkflowActionType ActionType { get; set; }
    public string? Comment { get; set; }
    public DateTime ActionAtUtc { get; set; }

    public WorkflowStep WorkflowStep { get; set; } = null!;
    public User Actor { get; set; } = null!;
}
