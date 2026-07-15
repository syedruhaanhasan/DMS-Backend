using WDAS.Domain.Enums;

namespace WDAS.Application.Models;

public record WorkflowDto(
    Guid Id,
    Guid DepartmentId,
    string Name,
    string DocumentType,
    string? Description,
    bool IsActive,
    WorkflowVersionSummaryDto? ActiveVersion);

public record WorkflowVersionSummaryDto(
    Guid Id,
    int VersionNumber,
    WorkflowVersionState State,
    ApprovalMode ApprovalMode,
    ApprovalSequence ApprovalSequence,
    ReturnResumePolicy ReturnResumePolicy,
    int? SlaThresholdHours,
    bool EscalationEnabled);

public record CreateWorkflowRequest(
    Guid DepartmentId,
    string Name,
    string DocumentType,
    string? Description,
    ApprovalMode ApprovalMode,
    ApprovalSequence ApprovalSequence = ApprovalSequence.Sequential,
    ReturnResumePolicy ReturnResumePolicy = ReturnResumePolicy.RestartFromFirst,
    int? SlaThresholdHours = null,
    bool EscalationEnabled = true,
    string? NotificationSettingsJson = null,
    IReadOnlyCollection<ApproverGroupInput>? Groups = null,
    IReadOnlyCollection<MatrixTierInput>? MatrixTiers = null,
    /// <summary>When true (checker), create Active immediately. Makers should leave this false → Pending approval.</summary>
    bool PublishImmediately = false);

public record UpdateWorkflowRequest(
    string Name,
    string? Description,
    ApprovalMode ApprovalMode,
    ApprovalSequence ApprovalSequence,
    ReturnResumePolicy ReturnResumePolicy,
    int? SlaThresholdHours,
    bool EscalationEnabled,
    WorkflowVersionState TargetState,
    string? NotificationSettingsJson = null,
    IReadOnlyCollection<ApproverGroupInput>? Groups = null,
    IReadOnlyCollection<MatrixTierInput>? MatrixTiers = null);

public record MatrixTierDto(
    Guid Id,
    int SequenceOrder,
    decimal MinAmount,
    decimal? MaxAmount,
    IReadOnlyCollection<Guid> ApproverUserIds);

public record SaveMatrixTiersRequest(IReadOnlyCollection<MatrixTierInput> Tiers);

public record MatrixTierInput(
    int SequenceOrder,
    decimal MinAmount,
    decimal? MaxAmount,
    IReadOnlyCollection<Guid> ApproverUserIds);

public record ApproverGroupDto(
    Guid Id,
    string Name,
    int SequenceOrder,
    GroupApprovalRequirement Requirement,
    IReadOnlyCollection<Guid> MemberUserIds);

public record SaveApproverGroupsRequest(IReadOnlyCollection<ApproverGroupInput> Groups);

public record ApproverGroupInput(
    string Name,
    int SequenceOrder,
    GroupApprovalRequirement Requirement,
    IReadOnlyCollection<Guid> MemberUserIds);
