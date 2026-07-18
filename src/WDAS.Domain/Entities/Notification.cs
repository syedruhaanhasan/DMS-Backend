using WDAS.Domain.Common;
using WDAS.Domain.Enums;

namespace WDAS.Domain.Entities;

public class Notification : Entity
{
    public int? RecipientUserId { get; set; }
    public string? RecipientEmail { get; set; }
    public NotificationEventType EventType { get; set; }
    public NotificationChannel Channel { get; set; }
    public NotificationDeliveryStatus Status { get; set; } = NotificationDeliveryStatus.Pending;
    public int? DocumentId { get; set; }
    public int? WorkflowStepId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime? SentAtUtc { get; set; }
    public DateTime? DeliveredAtUtc { get; set; }
    public DateTime? ReadAtUtc { get; set; }
    public int RetryCount { get; set; }
    public string? LastError { get; set; }

    public User? RecipientUser { get; set; }
    public Document? Document { get; set; }
}
