using WDAS.Domain.Common;

namespace WDAS.Domain.Entities;

public class ApprovalMatrixTier : Entity
{
    public Guid WorkflowVersionId { get; set; }
    public int SequenceOrder { get; set; }
    public decimal MinAmount { get; set; }
    public decimal? MaxAmount { get; set; }
    public string ApproverUserIdsJson { get; set; } = "[]";

    public WorkflowVersion WorkflowVersion { get; set; } = null!;
}
