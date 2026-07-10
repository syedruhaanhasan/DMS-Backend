using WDAS.Domain.Common;

namespace WDAS.Domain.Entities;

public class ApproverGroupMember : Entity
{
    public Guid ApproverGroupId { get; set; }
    public Guid UserId { get; set; }

    public ApproverGroup ApproverGroup { get; set; } = null!;
    public User User { get; set; } = null!;
}
