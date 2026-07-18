using WDAS.Domain.Common;
using WDAS.Domain.Enums;

namespace WDAS.Domain.Entities;

public class ApproverGroup : Entity
{
    public int WorkflowVersionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SequenceOrder { get; set; }
    public GroupApprovalRequirement Requirement { get; set; } = GroupApprovalRequirement.AnyOneMember;

    public WorkflowVersion WorkflowVersion { get; set; } = null!;
    public ICollection<ApproverGroupMember> Members { get; set; } = new List<ApproverGroupMember>();
}
