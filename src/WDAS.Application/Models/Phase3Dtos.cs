using WDAS.Domain.Enums;

namespace WDAS.Application.Models;

public record AuditLogEntryDto(
    long SequenceNumber,
    string EventType,
    string Action,
    Guid? ActorUserId,
    string? ActorDisplayName,
    Guid? DocumentId,
    string? EntityType,
    string? EntityId,
    string? DetailsJson,
    string? IpAddress,
    DateTime CreatedAtUtc,
    string EntryHash);

public record AuditExportRequest(
    Guid? DocumentId,
    Guid? DepartmentId,
    DateTime? FromUtc,
    DateTime? ToUtc);

public record AuditExportResult(
    IReadOnlyCollection<AuditLogEntryDto> Entries,
    bool ChainValid,
    string? ChainValidationMessage);

public record SearchRequest(
    string? Query,
    string? ArchiveDocumentId,
    Guid? OwnerUserId,
    Guid? ApproverUserId,
    DocumentStatus? Status,
    decimal? MinAmount,
    decimal? MaxAmount,
    DateTime? FromUtc,
    DateTime? ToUtc,
    int Skip = 0,
    int Take = 25);

public record SearchResultItemDto(
    Guid DocumentId,
    long RecordNumber,
    string? ArchiveDocumentId,
    string Subject,
    string OwnerDisplayName,
    string Status,
    decimal? Amount,
    DateTime? SubmittedAtUtc,
    string Snippet);

public record SearchResultDto(
    int TotalCount,
    IReadOnlyCollection<SearchResultItemDto> Items);

public record ApprovalTimeReportDto(
    Guid DepartmentId,
    string DepartmentName,
    string WorkflowName,
    double AverageEndToEndHours,
    double AverageStepHours,
    int DocumentCount);

public record BottleneckReportDto(
    string ApproverDisplayName,
    Guid ApproverUserId,
    int StepCount,
    double AverageDelayHours,
    int OverdueCount);

public record VolumeTrendReportDto(
    string Period,
    int SubmittedCount,
    int ApprovedCount,
    int RejectedCount,
    double RejectionRate);

public record SuccessMetricsDto(
    double AverageCycleTimeDays,
    double AdoptionRatePercent,
    int SlaBreachCount,
    int AuditExportCount,
    double SlaCompliancePercent,
    int ActiveUsersLast30Days,
    int DocumentsSubmittedLast30Days);

public record RegisterPushDeviceRequest(string Platform, string DeviceToken);

public record OfflineWorkflowActionRequest(
    string? Comment,
    DateTime? ClientActionAtUtc,
    int? ExpectedStepVersion);

public record ConflictResponseDto(
    string Code,
    string Message,
    Guid DocumentId,
    Guid StepId,
    string CurrentStepStatus,
    DateTime ServerTimestampUtc);

public record StepUpChallengeDto(bool Required, string? Reason);
