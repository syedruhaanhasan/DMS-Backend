using WDAS.Domain.Enums;

namespace WDAS.Application.Abstractions;

public record NotificationRequest(
    NotificationEventType EventType,
    int? RecipientUserId,
    string? RecipientEmail,
    int? DocumentId,
    int? WorkflowStepId,
    string Subject,
    string Body,
    string? HtmlBody = null,
    string? RecipientPhone = null,
    IReadOnlyCollection<NotificationChannel>? Channels = null);

public interface INotificationDispatcher
{
    Task DispatchAsync(NotificationRequest request, CancellationToken cancellationToken = default);
}
