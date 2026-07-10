namespace WDAS.Application.Models;

public record AttachmentDto(
    Guid Id,
    Guid DocumentId,
    string FileName,
    string? LogicalName,
    string ContentType,
    long FileSizeBytes,
    int VersionNumber,
    bool IsLatest,
    string ScanStatus,
    bool DownloadRestricted,
    DateTime CreatedAtUtc);

public record AttachmentPreviewDto(
    Guid Id,
    string ContentType,
    string FileName,
    bool IsPreviewGenerated);

public record CreateDelegationRequest(
    Guid DelegateUserId,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    string? Reason,
    string? AutoReplyMessage);

public record DelegationDto(
    Guid Id,
    Guid ApproverUserId,
    string ApproverDisplayName,
    Guid DelegateUserId,
    string DelegateDisplayName,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    bool IsActive,
    string? Reason);

public record ReassignStepRequest(Guid NewApproverUserId, string Reason);

public record CreateExternalApproverRequest(
    Guid WorkflowStepId,
    string ApproverName,
    string ApproverEmail);

public record ExternalApproverSessionDto(
    Guid Id,
    Guid WorkflowStepId,
    string ApproverEmail,
    DateTime LinkExpiresAtUtc,
    string SecureLinkToken);

public record VerifyExternalOtpRequest(string SecureLinkToken, string Otp, string? ClientIp);

public record ExternalSessionDto(
    string AccessToken,
    Guid WorkflowStepId,
    Guid DocumentId,
    DateTime ExpiresAtUtc);

public record CancelDocumentRequest(string? Reason);

public record FinalizeDocumentRequest(string? Comment);

public record RepositoryDocumentDto(
    Guid Id,
    string ArchiveDocumentId,
    Guid SourceDocumentId,
    string Subject,
    string BodyHtmlSnapshot,
    string ApprovalTrailJson,
    DateTime FinalizedAtUtc,
    string FinalizedByDisplayName,
    bool HasArchivePdf);

public record DashboardDocumentItemDto(
    Guid DocumentId,
    Guid OwnerUserId,
    string Subject,
    string Status,
    string WorkflowName,
    DateTime? SubmittedAtUtc,
    DateTime? DueAtUtc,
    bool IsSlaBreached,
    string SlaClassification,
    Guid? ActiveStepId,
    bool IsDelegated = false);

public record PersonalDashboardDto(
    IReadOnlyCollection<DashboardDocumentItemDto> PendingMyApproval,
    IReadOnlyCollection<DashboardDocumentItemDto> DelegatedPendingApproval,
    IReadOnlyCollection<DashboardDocumentItemDto> MyDocuments,
    IReadOnlyCollection<DashboardDocumentItemDto> RecentlyCompleted);

public record DepartmentDashboardDto(
    Guid DepartmentId,
    string DepartmentName,
    IReadOnlyCollection<DashboardDocumentItemDto> Documents);
