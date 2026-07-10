using WDAS.Domain.Common;

namespace WDAS.Domain.Entities;

public class Delegation : Entity
{
    public Guid ApproverUserId { get; set; }
    public Guid DelegateUserId { get; set; }
    public DateTime StartsAtUtc { get; set; }
    public DateTime EndsAtUtc { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Reason { get; set; }
    public string? AutoReplyMessage { get; set; }

    public User Approver { get; set; } = null!;
    public User Delegate { get; set; } = null!;
}
