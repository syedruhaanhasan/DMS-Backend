using WDAS.Domain.Enums;

namespace WDAS.Application.Models;

public record AuditLogEntryDto(
    long SequenceNumber,
    string EventType,
    string Action,
    string? ActorUserId,
    string? ActorDisplayName,
    string? DocumentId,
    string? EntityType,
    string? EntityId,
    string? DetailsJson,
    string? IpAddress,
    DateTime CreatedAtUtc,
    string EntryHash);

public record AuditExportRequest(
    string? DocumentId,
    string? DepartmentId,
    DateTime? FromUtc,
    DateTime? ToUtc);

public record AuditExportResult(
    IReadOnlyCollection<AuditLogEntryDto> Entries,
    bool ChainValid,
    string? ChainValidationMessage);

public record SearchRequest(
    string? Query,
    string? ArchiveDocumentId,
    string? OwnerUserId,
    string? ApproverUserId,
    DocumentStatus? Status,
    decimal? MinAmount,
    decimal? MaxAmount,
    DateTime? FromUtc,
    DateTime? ToUtc,
    int Skip = 0,
    int Take = 25,
    bool RepositoryOnly = false);

public record SearchResultItemDto(
    string DocumentId,
    long RecordNumber,
    string? ArchiveDocumentId,
    string Subject,
    string OwnerDisplayName,
    string OwnerUserId,
    string Status,
    decimal? Amount,
    DateTime? SubmittedAtUtc,
    string Snippet);

public record SearchResultDto(
    int TotalCount,
    IReadOnlyCollection<SearchResultItemDto> Items);

public record ApprovalTimeReportDto(
    string DepartmentId,
    string DepartmentName,
    string WorkflowName,
    double AverageEndToEndHours,
    double AverageStepHours,
    int DocumentCount);

public record BottleneckReportDto(
    string ApproverDisplayName,
    string ApproverUserId,
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
    string DocumentId,
    string StepId,
    string CurrentStepStatus,
    DateTime ServerTimestampUtc);

public record StepUpChallengeDto(bool Required, string? Reason);
