using WDAS.Domain.Common;

namespace WDAS.Domain.Entities;

public class ApproverGroupMember : Entity
{
    public int ApproverGroupId { get; set; }
    public int UserId { get; set; }

    public ApproverGroup ApproverGroup { get; set; } = null!;
    public User User { get; set; } = null!;
}
