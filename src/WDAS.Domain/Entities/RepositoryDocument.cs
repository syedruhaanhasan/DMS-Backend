using WDAS.Domain.Common;

namespace WDAS.Domain.Entities;

public class RepositoryDocument : Entity
{
    public Guid SourceDocumentId { get; set; }
    public string ArchiveDocumentId { get; set; } = string.Empty;
    public Guid FinalizedByUserId { get; set; }
    public DateTime FinalizedAtUtc { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string BodyHtmlSnapshot { get; set; } = string.Empty;
    public string ApprovalTrailJson { get; set; } = string.Empty;
    public string? ArchivePdfStorageKey { get; set; }
    public bool IsImmutable { get; set; } = true;

    public Document SourceDocument { get; set; } = null!;
    public User FinalizedBy { get; set; } = null!;
}
