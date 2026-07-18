using WDAS.Domain.Common;
using WDAS.Domain.Enums;

namespace WDAS.Domain.Entities;

public class Attachment : Entity
{
    public int DocumentId { get; set; }
    public int UploadedByUserId { get; set; }
    public int? WorkflowStepActionId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string? LogicalName { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public string StorageKey { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public int VersionNumber { get; set; } = 1;
    public bool IsLatest { get; set; } = true;
    public AttachmentScanStatus ScanStatus { get; set; } = AttachmentScanStatus.Pending;
    public string? PreviewStorageKey { get; set; }
    public string? PreviewContentType { get; set; }
    public bool DownloadRestricted { get; set; }
    public DateTime? SupersededAtUtc { get; set; }

    public Document Document { get; set; } = null!;
    public User UploadedBy { get; set; } = null!;
    public WorkflowStepAction? WorkflowStepAction { get; set; }
}
