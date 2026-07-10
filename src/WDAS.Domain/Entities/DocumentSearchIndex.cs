namespace WDAS.Domain.Entities;

public class DocumentSearchIndex
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DocumentId { get; set; }
    public string? ArchiveDocumentId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string BodyText { get; set; } = string.Empty;
    public string OwnerDisplayName { get; set; } = string.Empty;
    public Guid OwnerUserId { get; set; }
    public Guid DepartmentId { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal? Amount { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }
    public DateTime? FinalizedAtUtc { get; set; }
    public string SearchableText { get; set; } = string.Empty;
    public DateTime IndexedAtUtc { get; set; }

    public Document Document { get; set; } = null!;
}
