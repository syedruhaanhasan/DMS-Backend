namespace WDAS.Application.Models;

public record NotificationDto(
    Guid Id,
    string EventType,
    string Channel,
    string Subject,
    string Body,
    Guid? DocumentId,
    DateTime CreatedAtUtc,
    DateTime? ReadAtUtc);

public record ExternalApproverListItemDto(
    Guid Id,
    Guid WorkflowStepId,
    Guid DocumentId,
    string DocumentSubject,
    string ApproverName,
    string ApproverEmail,
    DateTime LinkSentAtUtc,
    DateTime LinkExpiresAtUtc,
    bool OtpVerified,
    bool IsRevoked,
    string ActionTaken);
