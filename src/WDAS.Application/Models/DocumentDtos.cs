using WDAS.Domain.Enums;

namespace WDAS.Application.Models;

public record DocumentDto(
    Guid Id,
    long RecordNumber,
    int RevisionNumber,
    Guid OwnerUserId,
    string OwnerDisplayName,
    Guid DepartmentId,
    Guid WorkflowId,
    Guid? WorkflowVersionId,
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
    IReadOnlyCollection<Guid>? AdHocApproverUserIds,
    IReadOnlyCollection<DocumentRecipientDto> Recipients,
    IReadOnlyCollection<WorkflowStepDto> WorkflowSteps);

public record DocumentRecipientDto(Guid Id, string RecipientName, string? RecipientEmail);

public record CreateDocumentRequest(
    Guid WorkflowId,
    string ToRecipients,
    string Subject,
    string BodyHtml,
    decimal? Amount,
    DocumentPriority Priority,
    IReadOnlyCollection<DocumentRecipientInput> Recipients,
    IReadOnlyCollection<Guid>? AdHocApproverUserIds,
    bool Submit,
    string? IdempotencyKey);

public record UpdateDocumentRequest(
    string ToRecipients,
    string Subject,
    string BodyHtml,
    decimal? Amount,
    DocumentPriority Priority,
    IReadOnlyCollection<DocumentRecipientInput>? Recipients,
    IReadOnlyCollection<Guid>? AdHocApproverUserIds,
    bool Submit,
    string? IdempotencyKey);

public record SubmitDocumentRequest(string? IdempotencyKey);

public record DocumentRecipientInput(string RecipientName, string? RecipientEmail);

public record WorkflowStepDto(
    Guid Id,
    int StepOrder,
    Guid? ApproverUserId,
    string? ApproverDisplayName,
    string? GroupName,
    WorkflowStepStatus Status,
    DateTime? ActivatedAtUtc,
    DateTime? CompletedAtUtc,
    DateTime? SlaDueAtUtc,
    bool IsSlaBreached,
    IReadOnlyCollection<WorkflowStepActionDto> Actions);

public record WorkflowStepActionDto(
    Guid Id,
    Guid ActorUserId,
    string ActorDisplayName,
    WorkflowActionType ActionType,
    string? Comment,
    DateTime ActionAtUtc);

public record WorkflowActionRequest(string? Comment, DateTime? ClientActionAtUtc = null, int? ExpectedStepVersion = null);
