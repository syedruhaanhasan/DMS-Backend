namespace WDAS.Domain.Entities;

using WDAS.Domain.Enums;

public class AuditLogEntry
{
    public int Id { get; set; }
    public long SequenceNumber { get; set; }
    public string PreviousHash { get; set; } = string.Empty;
    public string EntryHash { get; set; } = string.Empty;
    public int HashVersion { get; set; } = 1;
    public AuditEventType EventType { get; set; }
    public int? ActorUserId { get; set; }
    public Guid? LegacyActorUserId { get; set; }
    public string? ActorDisplayName { get; set; }
    public string? ActorEmail { get; set; }
    public int? DocumentId { get; set; }
    public Guid? LegacyDocumentId { get; set; }
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? DetailsJson { get; set; }
    public string? IpAddress { get; set; }
    public string? ClientType { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
