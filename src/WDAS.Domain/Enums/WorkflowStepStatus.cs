namespace WDAS.Domain.Enums;

public enum WorkflowStepStatus
{
    Pending = 1,
    Active = 2,
    Approved = 3,
    Rejected = 4,
    Returned = 5,
    Skipped = 6,
    /// <summary>Approval step paused while an approver-added reviewer completes review.</summary>
    Paused = 7
}
