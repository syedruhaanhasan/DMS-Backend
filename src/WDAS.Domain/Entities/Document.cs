using WDAS.Domain.Common;
using WDAS.Domain.Enums;

namespace WDAS.Domain.Entities;

public class Document : Entity
{
    public long RecordNumber { get; set; }
    /// <summary>Document content revision (v1, v2, …). Bumps when owner revises after rejection.</summary>
    public int RevisionNumber { get; set; } = 1;
    public int OwnerUserId { get; set; }
    public int DepartmentId { get; set; }
    public int WorkflowId { get; set; }
    public int? WorkflowVersionId { get; set; }
    public string ToRecipients { get; set; } = string.Empty;
    public string FromDisplay { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string BodyHtml { get; set; } = string.Empty;
    public decimal? Amount { get; set; }
    public DocumentPriority Priority { get; set; } = DocumentPriority.Normal;
    public DocumentStatus Status { get; set; } = DocumentStatus.Draft;
    public bool IsBodyLocked { get; set; }
    public string? SubmitIdempotencyKey { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }
    public string? AdHocApproverUserIdsJson { get; set; }
    public string? CancellationReason { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public string? ArchiveDocumentId { get; set; }
    public DateTime? FinalizedAtUtc { get; set; }

    public User Owner { get; set; } = null!;
    public Department Department { get; set; } = null!;
    public Workflow Workflow { get; set; } = null!;
    public WorkflowVersion? WorkflowVersion { get; set; }
    public ICollection<DocumentRecipient> Recipients { get; set; } = new List<DocumentRecipient>();
    public ICollection<WorkflowStep> WorkflowSteps { get; set; } = new List<WorkflowStep>();
    public ICollection<Attachment> Attachments { get; set; } = new List<Attachment>();
}
