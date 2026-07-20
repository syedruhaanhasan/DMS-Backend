using WDAS.Domain.Common;

namespace WDAS.Domain.Entities;

public class DocumentRecipient : Entity
{
    public int DocumentId { get; set; }
    public string RecipientName { get; set; } = string.Empty;
    public string? RecipientEmail { get; set; }

    /// <summary>Directory user id of the reviewer, when the recipient is a known user.</summary>
    public int? ReviewerUserId { get; set; }

    /// <summary>User who added this reviewer (owner at creation, or an approver during approval).</summary>
    public int? AddedByUserId { get; set; }

    /// <summary>When the reviewer marked their review complete (creator-gated flow).</summary>
    public DateTime? ReviewedAtUtc { get; set; }

    public string? ReviewComment { get; set; }

    /// <summary>When set, reviewer completion reactivates this approval step (approver-gated review).</summary>
    public int? ReturnWorkflowStepId { get; set; }

    public Document Document { get; set; } = null!;
}
