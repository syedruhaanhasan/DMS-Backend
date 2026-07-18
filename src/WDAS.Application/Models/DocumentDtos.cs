using WDAS.Domain.Enums;

namespace WDAS.Application.Models;

public record DocumentDto(
    string Id,
    long RecordNumber,
    int RevisionNumber,
    string OwnerUserId,
    string OwnerDisplayName,
    string DepartmentId,
    string WorkflowId,
    string? WorkflowVersionId,
    string ToRecipients,
    string FromDisplay,
    string Subject,
    string BodyHtml,
    decimal? Amount,
    DocumentPriority Priority,
    DocumentStatus Status,
    bool IsBodyLocked,
    DateTime? SubmittedAtUtc,
    string? ArchiveDocumentId,
    DateTime? FinalizedAtUtc,
    string? CancellationReason,
    IReadOnlyCollection<string>? AdHocApproverUserIds,
    IReadOnlyCollection<DocumentRecipientDto> Recipients,
    IReadOnlyCollection<WorkflowStepDto> WorkflowSteps);

public record DocumentRecipientDto(
    string Id,
    string RecipientName,
    string? RecipientEmail,
    string? ReviewerUserId,
    string? AddedById);

public record AddReviewerRequest(string ReviewerUserId);

public record CreateDocumentRequest(
    string WorkflowId,
    string ToRecipients,
    string Subject,
    string BodyHtml,
    decimal? Amount,
    DocumentPriority Priority,
    IReadOnlyCollection<DocumentRecipientInput> Recipients,
    IReadOnlyCollection<string>? AdHocApproverUserIds,
    bool Submit,
    string? IdempotencyKey);

public record UpdateDocumentRequest(
    string ToRecipients,
    string Subject,
    string BodyHtml,
    decimal? Amount,
    DocumentPriority Priority,
    IReadOnlyCollection<DocumentRecipientInput>? Recipients,
    IReadOnlyCollection<string>? AdHocApproverUserIds,
    bool Submit,
    string? IdempotencyKey);

public record SubmitDocumentRequest(string? IdempotencyKey);

public record DocumentRecipientInput(string RecipientName, string? RecipientEmail, string? ReviewerUserId = null);

public record WorkflowStepDto(
    string Id,
    int StepOrder,
    string? ApproverUserId,
    string? ApproverDisplayName,
    string? GroupName,
    WorkflowStepStatus Status,
    DateTime? ActivatedAtUtc,
    DateTime? CompletedAtUtc,
    DateTime? SlaDueAtUtc,
    bool IsSlaBreached,
    IReadOnlyCollection<WorkflowStepActionDto> Actions);

public record WorkflowStepActionDto(
    string Id,
    string ActorUserId,
    string ActorDisplayName,
    WorkflowActionType ActionType,
    string? Comment,
    DateTime ActionAtUtc);

public record WorkflowActionRequest(string? Comment, DateTime? ClientActionAtUtc = null, int? ExpectedStepVersion = null);
