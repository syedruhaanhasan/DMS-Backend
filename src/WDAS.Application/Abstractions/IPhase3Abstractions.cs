using WDAS.Domain.Enums;

namespace WDAS.Application.Abstractions;

public record AuditWriteRequest(
    AuditEventType EventType,
    string Action,
    Guid? DocumentId = null,
    string? EntityType = null,
    string? EntityId = null,
    string? DetailsJson = null,
    Guid? ActorUserId = null,
    string? ActorDisplayName = null,
    string? ActorEmail = null);

public interface IAuditWriter
{
    Task WriteAsync(AuditWriteRequest request, CancellationToken cancellationToken = default);
}

public interface IPushNotificationSender
{
    Task SendAsync(Guid userId, string title, string body, CancellationToken cancellationToken = default);
}

public interface IDocumentSearchIndexer
{
    Task IndexDocumentAsync(Guid documentId, CancellationToken cancellationToken = default);
}
