using WDAS.Domain.Common;
using WDAS.Domain.Enums;

namespace WDAS.Domain.Entities;

public class WorkflowVersion : Entity
{
    public int WorkflowId { get; set; }
    public int VersionNumber { get; set; }
    public WorkflowVersionState State { get; set; } = WorkflowVersionState.Draft;
    public ApprovalMode ApprovalMode { get; set; }
    public ApprovalSequence ApprovalSequence { get; set; } = ApprovalSequence.Sequential;
    public ReturnResumePolicy ReturnResumePolicy { get; set; } = ReturnResumePolicy.RestartFromFirst;
    public int? SlaThresholdHours { get; set; }
    public bool EscalationEnabled { get; set; }
    public string? DefaultRecipientsJson { get; set; }
    public string? NotificationSettingsJson { get; set; }
    public DateTime? ActivatedAtUtc { get; set; }

    public Workflow Workflow { get; set; } = null!;
    public ICollection<ApprovalMatrixTier> MatrixTiers { get; set; } = new List<ApprovalMatrixTier>();
    public ICollection<ApproverGroup> ApproverGroups { get; set; } = new List<ApproverGroup>();
    public ICollection<Document> Documents { get; set; } = new List<Document>();
}
