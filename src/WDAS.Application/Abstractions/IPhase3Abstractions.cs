using WDAS.Domain.Enums;

namespace WDAS.Application.Abstractions;

public record AuditWriteRequest(
    AuditEventType EventType,
    string Action,
    int? DocumentId = null,
    string? EntityType = null,
    string? EntityId = null,
    string? DetailsJson = null,
    int? ActorUserId = null,
    string? ActorDisplayName = null,
    string? ActorEmail = null);

public interface IAuditWriter
{
    Task WriteAsync(AuditWriteRequest request, CancellationToken cancellationToken = default);
}

public interface IPushNotificationSender
{
    Task SendAsync(int userId, string title, string body, CancellationToken cancellationToken = default);
}

public interface IDocumentSearchIndexer
{
    Task IndexDocumentAsync(int documentId, CancellationToken cancellationToken = default);
}
