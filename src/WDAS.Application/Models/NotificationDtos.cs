namespace WDAS.Application.Models;

public record NotificationDto(
    string Id,
    string EventType,
    string Channel,
    string Subject,
    string Body,
    string? DocumentId,
    DateTime CreatedAtUtc,
    DateTime? ReadAtUtc);

public record ExternalApproverListItemDto(
    string Id,
    string WorkflowStepId,
    string DocumentId,
    string DocumentSubject,
    string ApproverName,
    string ApproverEmail,
    DateTime LinkSentAtUtc,
    DateTime LinkExpiresAtUtc,
    bool OtpVerified,
    bool IsRevoked,
    string ActionTaken);
