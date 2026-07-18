namespace WDAS.Application.Models;

public record AttachmentDto(
    string Id,
    string DocumentId,
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
    string Id,
    string ContentType,
    string FileName,
    bool IsPreviewGenerated);

public record CreateDelegationRequest(
    string DelegateUserId,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    string? Reason,
    string? AutoReplyMessage,
    bool IsActive = true);

public record DelegationDto(
    string Id,
    string ApproverUserId,
    string ApproverDisplayName,
    string DelegateUserId,
    string DelegateDisplayName,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    bool IsActive,
    string? Reason);

public record ReassignStepRequest(string NewApproverUserId, string Reason);

public record CreateExternalApproverRequest(
    string WorkflowStepId,
    string ApproverName,
    string ApproverEmail);

public record ExternalApproverSessionDto(
    string Id,
    string WorkflowStepId,
    string ApproverEmail,
    DateTime LinkExpiresAtUtc,
    string SecureLinkToken);

public record VerifyExternalOtpRequest(string SecureLinkToken, string Otp, string? ClientIp);

public record ExternalSessionDto(
    string AccessToken,
    string WorkflowStepId,
    string DocumentId,
    DateTime ExpiresAtUtc);

public record CancelDocumentRequest(string? Reason);

public record FinalizeDocumentRequest(string? Comment);

public record RepositoryDocumentDto(
    string Id,
    string ArchiveDocumentId,
    string SourceDocumentId,
    string Subject,
    string BodyHtmlSnapshot,
    string ApprovalTrailJson,
    DateTime FinalizedAtUtc,
    string FinalizedByDisplayName,
    bool HasArchivePdf);

public record DashboardDocumentItemDto(
    string DocumentId,
    long RecordNumber,
    string OwnerUserId,
    string Subject,
    string Status,
    string WorkflowName,
    DateTime? SubmittedAtUtc,
    DateTime? DueAtUtc,
    bool IsSlaBreached,
    string SlaClassification,
    string? ActiveStepId,
    bool IsDelegated = false,
    bool ActiveStepSeenByApprover = false);

public record PersonalDashboardDto(
    IReadOnlyCollection<DashboardDocumentItemDto> PendingMyApproval,
    IReadOnlyCollection<DashboardDocumentItemDto> DelegatedPendingApproval,
    IReadOnlyCollection<DashboardDocumentItemDto> MyDocuments,
    IReadOnlyCollection<DashboardDocumentItemDto> RecentlyCompleted);

public record DepartmentDashboardDto(
    string DepartmentId,
    string DepartmentName,
    IReadOnlyCollection<DashboardDocumentItemDto> Documents);
