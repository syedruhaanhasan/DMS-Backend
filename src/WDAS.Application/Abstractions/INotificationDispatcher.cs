using WDAS.Domain.Enums;

namespace WDAS.Application.Abstractions;

public record NotificationRequest(
    NotificationEventType EventType,
    Guid? RecipientUserId,
    string? RecipientEmail,
    Guid? DocumentId,
    Guid? WorkflowStepId,
    string Subject,
    string Body,
    string? HtmlBody = null,
    string? RecipientPhone = null,
    IReadOnlyCollection<NotificationChannel>? Channels = null);

public interface INotificationDispatcher
{
    Task DispatchAsync(NotificationRequest request, CancellationToken cancellationToken = default);
}
