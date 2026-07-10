using Microsoft.EntityFrameworkCore;
using WDAS.Application;
using WDAS.Application.Abstractions;
using WDAS.Application.Models;
using WDAS.Domain.Entities;
using WDAS.Domain.Enums;
using WDAS.Domain.Exceptions;

namespace WDAS.Application.Services;

public class WorkflowService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;

    public WorkflowService(IApplicationDbContext db, ICurrentUserService currentUser, IClock clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<IReadOnlyList<WorkflowDto>> GetWorkflowsAsync(Guid? departmentId, CancellationToken cancellationToken = default)
    {
        var query = _db.Workflows.Include(w => w.Versions).AsQueryable();

        if (!_currentUser.IsInRole(RoleNames.SuperAdmin))
        {
            var scopedDepartmentId = _currentUser.DepartmentId
                ?? throw new DomainException("Department context is required.");

            query = query.Where(w => w.DepartmentId == scopedDepartmentId);
        }
        else if (departmentId.HasValue)
        {
            query = query.Where(w => w.DepartmentId == departmentId.Value);
        }

        var workflows = await query.OrderBy(w => w.Name).ToListAsync(cancellationToken);
        return workflows.Select(MapWorkflow).ToList();
    }

    public async Task<WorkflowDto> CreateWorkflowAsync(CreateWorkflowRequest request, CancellationToken cancellationToken = default)
    {
        EnsureSuperAdmin();

        var name = request.Name.Trim();
        var documentType = request.DocumentType.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Workflow name is required.");
        }

        if (string.IsNullOrWhiteSpace(documentType))
        {
            throw new DomainException("Document type is required.");
        }

        if (await _db.Workflows.AnyAsync(
                w => w.DepartmentId == request.DepartmentId &&
                     w.DocumentType == documentType &&
                     w.Name == name,
                cancellationToken))
        {
            var existingId = await _db.Workflows
                .Where(w => w.DepartmentId == request.DepartmentId &&
                            w.DocumentType == documentType &&
                            w.Name == name)
                .Select(w => w.Id)
                .FirstAsync(cancellationToken);

            throw new ConflictException(
                "workflow_exists",
                $"A workflow named '{name}' already exists for this department and document type. Use a different workflow name or document type.",
                new { workflowId = existingId });
        }

        var now = _clock.UtcNow;
        var workflow = new Workflow
        {
            DepartmentId = request.DepartmentId,
            Name = name,
            DocumentType = documentType,
            Description = request.Description,
            IsActive = true,
            CreatedAtUtc = now
        };

        var version = CreateVersion(workflow, request.ApprovalMode, request.ApprovalSequence, request.ReturnResumePolicy, request.SlaThresholdHours, request.EscalationEnabled, 1, WorkflowVersionState.Active, now);
        version.NotificationSettingsJson = request.NotificationSettingsJson;

        if (request.Groups is { Count: > 0 })
        {
            AddApproverGroups(version, request.Groups, now);
        }

        if (request.MatrixTiers is { Count: > 0 })
        {
            AddMatrixTiers(version, request.MatrixTiers, now);
        }

        workflow.Versions.Add(version);

        _db.Add(workflow);
        await SaveAsync(cancellationToken);
        return MapWorkflow(workflow);
    }

    public async Task<WorkflowDto> UpdateWorkflowAsync(Guid workflowId, UpdateWorkflowRequest request, CancellationToken cancellationToken = default)
    {
        var workflow = await _db.Workflows
            .FirstOrDefaultAsync(w => w.Id == workflowId, cancellationToken)
            ?? throw new DomainException("Workflow not found.");

        EnsureSuperAdmin();

        workflow.Name = request.Name;
        workflow.Description = request.Description;
        workflow.UpdatedAtUtc = _clock.UtcNow;

        var latest = await _db.WorkflowVersions
            .Where(v => v.WorkflowId == workflowId)
            .OrderByDescending(v => v.VersionNumber)
            .FirstAsync(cancellationToken);

        if (latest.State == WorkflowVersionState.Draft)
        {
            ApplyVersionSettings(latest, request);
            if (request.TargetState == WorkflowVersionState.Active)
            {
                latest.ActivatedAtUtc = _clock.UtcNow;
            }

            latest.UpdatedAtUtc = _clock.UtcNow;
            await SaveAsync(cancellationToken);
        }
        else
        {
            var sourceVersionId = latest.Id;
            var nextVersionNumber = latest.VersionNumber + 1;
            var now = _clock.UtcNow;

            if (request.TargetState == WorkflowVersionState.Active)
            {
                await _db.WorkflowVersions
                    .Where(v => v.WorkflowId == workflowId && v.State == WorkflowVersionState.Active)
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(v => v.State, WorkflowVersionState.Retired)
                            .SetProperty(v => v.UpdatedAtUtc, now),
                        cancellationToken);
            }

            var sourceVersion = await _db.WorkflowVersions
                .AsNoTracking()
                .Include(v => v.ApproverGroups)
                    .ThenInclude(g => g.Members)
                .Include(v => v.MatrixTiers)
                .FirstAsync(v => v.Id == sourceVersionId, cancellationToken);

            var newVersion = CreateVersion(
                workflow,
                request.ApprovalMode,
                request.ApprovalSequence,
                request.ReturnResumePolicy,
                request.SlaThresholdHours,
                request.EscalationEnabled,
                nextVersionNumber,
                request.TargetState,
                now);
            newVersion.NotificationSettingsJson = request.NotificationSettingsJson ?? sourceVersion.NotificationSettingsJson;

            foreach (var sourceGroup in sourceVersion.ApproverGroups.OrderBy(g => g.SequenceOrder))
            {
                var copiedGroup = new ApproverGroup
                {
                    WorkflowVersionId = newVersion.Id,
                    Name = sourceGroup.Name,
                    SequenceOrder = sourceGroup.SequenceOrder,
                    Requirement = sourceGroup.Requirement,
                    CreatedAtUtc = now
                };

                foreach (var member in sourceGroup.Members)
                {
                    copiedGroup.Members.Add(new ApproverGroupMember
                    {
                        UserId = member.UserId,
                        CreatedAtUtc = now
                    });
                }

                newVersion.ApproverGroups.Add(copiedGroup);
            }

            foreach (var sourceTier in sourceVersion.MatrixTiers.OrderBy(t => t.SequenceOrder))
            {
                newVersion.MatrixTiers.Add(new ApprovalMatrixTier
                {
                    WorkflowVersionId = newVersion.Id,
                    SequenceOrder = sourceTier.SequenceOrder,
                    MinAmount = sourceTier.MinAmount,
                    MaxAmount = sourceTier.MaxAmount,
                    ApproverUserIdsJson = sourceTier.ApproverUserIdsJson,
                    CreatedAtUtc = now
                });
            }

            _db.Add(newVersion);
            await SaveAsync(cancellationToken);
        }

        var refreshed = await _db.Workflows
            .Include(w => w.Versions)
            .FirstAsync(w => w.Id == workflowId, cancellationToken);

        return MapWorkflow(refreshed);
    }

    public async Task<IReadOnlyList<WorkflowVersionSummaryDto>> GetVersionsAsync(Guid workflowId, CancellationToken cancellationToken = default)
    {
        var workflow = await _db.Workflows
            .Include(w => w.Versions)
            .FirstOrDefaultAsync(w => w.Id == workflowId, cancellationToken)
            ?? throw new DomainException("Workflow not found.");

        EnsureSuperAdmin();

        return workflow.Versions
            .OrderByDescending(v => v.VersionNumber)
            .Select(MapVersion)
            .ToList();
    }

    public async Task<IReadOnlyList<MatrixTierDto>> GetMatrixTiersAsync(Guid workflowId, CancellationToken cancellationToken = default)
    {
        var version = await GetEditableVersionAsync(workflowId, cancellationToken);
        return version.MatrixTiers
            .OrderBy(t => t.SequenceOrder)
            .Select(t => new MatrixTierDto(t.Id, t.SequenceOrder, t.MinAmount, t.MaxAmount, Domain.Services.MatrixTierValidator.ParseApproverIds(t.ApproverUserIdsJson)))
            .ToList();
    }

    public async Task<IReadOnlyList<MatrixTierDto>> SaveMatrixTiersAsync(Guid workflowId, SaveMatrixTiersRequest request, CancellationToken cancellationToken = default)
    {
        var version = await GetEditableVersionAsync(workflowId, cancellationToken, includeTiers: true);

        var existingTiers = version.MatrixTiers.ToList();
        if (existingTiers.Count > 0)
        {
            _db.RemoveRange(existingTiers);
            version.MatrixTiers.Clear();
            await SaveAsync(cancellationToken);
        }

        var tiers = request.Tiers.Select(t => new ApprovalMatrixTier
        {
            WorkflowVersionId = version.Id,
            SequenceOrder = t.SequenceOrder,
            MinAmount = t.MinAmount,
            MaxAmount = t.MaxAmount,
            ApproverUserIdsJson = System.Text.Json.JsonSerializer.Serialize(t.ApproverUserIds),
            CreatedAtUtc = _clock.UtcNow
        }).ToList();

        Domain.Services.MatrixTierValidator.Validate(tiers);

        foreach (var tier in tiers)
        {
            version.MatrixTiers.Add(tier);
        }

        version.UpdatedAtUtc = _clock.UtcNow;
        await SaveAsync(cancellationToken);

        return tiers.Select(t => new MatrixTierDto(t.Id, t.SequenceOrder, t.MinAmount, t.MaxAmount, Domain.Services.MatrixTierValidator.ParseApproverIds(t.ApproverUserIdsJson))).ToList();
    }

    public async Task<IReadOnlyList<ApproverGroupDto>> GetApproverGroupsAsync(Guid workflowId, CancellationToken cancellationToken = default)
    {
        var version = await GetEditableVersionAsync(workflowId, cancellationToken, includeGroups: true);
        return version.ApproverGroups
            .OrderBy(g => g.SequenceOrder)
            .Select(MapGroup)
            .ToList();
    }

    public async Task<IReadOnlyList<ApproverGroupDto>> SaveApproverGroupsAsync(Guid workflowId, SaveApproverGroupsRequest request, CancellationToken cancellationToken = default)
    {
        var version = await GetEditableVersionAsync(workflowId, cancellationToken, includeGroups: true);

        var existingGroups = version.ApproverGroups.ToList();
        if (existingGroups.Count > 0)
        {
            _db.RemoveRange(existingGroups.SelectMany(g => g.Members));
            _db.RemoveRange(existingGroups);
            version.ApproverGroups.Clear();
            await SaveAsync(cancellationToken);
        }

        foreach (var groupInput in request.Groups.OrderBy(g => g.SequenceOrder))
        {
            var group = new ApproverGroup
            {
                WorkflowVersionId = version.Id,
                Name = groupInput.Name,
                SequenceOrder = groupInput.SequenceOrder,
                Requirement = groupInput.Requirement,
                CreatedAtUtc = _clock.UtcNow
            };

            foreach (var memberId in groupInput.MemberUserIds.Distinct())
            {
                group.Members.Add(new ApproverGroupMember
                {
                    UserId = memberId,
                    CreatedAtUtc = _clock.UtcNow
                });
            }

            version.ApproverGroups.Add(group);
        }

        version.UpdatedAtUtc = _clock.UtcNow;
        await SaveAsync(cancellationToken);

        return version.ApproverGroups.OrderBy(g => g.SequenceOrder).Select(MapGroup).ToList();
    }

    public async Task<IReadOnlyList<MatrixTierDto>> CloneMatrixFromWorkflowAsync(
        Guid targetWorkflowId,
        Guid sourceWorkflowId,
        CancellationToken cancellationToken = default)
    {
        var sourceTiers = await GetMatrixTiersAsync(sourceWorkflowId, cancellationToken);
        return await SaveMatrixTiersAsync(
            targetWorkflowId,
            new SaveMatrixTiersRequest(sourceTiers.Select(t => new MatrixTierInput(
                t.SequenceOrder,
                t.MinAmount,
                t.MaxAmount,
                t.ApproverUserIds)).ToList()),
            cancellationToken);
    }

    private async Task<WorkflowVersion> GetEditableVersionAsync(
        Guid workflowId,
        CancellationToken cancellationToken,
        bool includeTiers = false,
        bool includeGroups = false)
    {
        var workflow = await _db.Workflows
            .AsNoTracking()
            .Where(w => w.Id == workflowId)
            .Select(w => new { w.DepartmentId })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new DomainException("Workflow not found.");

        EnsureSuperAdmin();

        var versionId = await _db.WorkflowVersions
            .Where(v => v.WorkflowId == workflowId &&
                        (v.State == WorkflowVersionState.Draft || v.State == WorkflowVersionState.TestPreview))
            .OrderByDescending(v => v.VersionNumber)
            .Select(v => v.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (versionId == Guid.Empty)
        {
            versionId = await _db.WorkflowVersions
                .Where(v => v.WorkflowId == workflowId)
                .OrderByDescending(v => v.VersionNumber)
                .Select(v => v.Id)
                .FirstAsync(cancellationToken);
        }

        IQueryable<WorkflowVersion> query = _db.WorkflowVersions.Where(v => v.Id == versionId);
        if (includeTiers)
        {
            query = query.Include(v => v.MatrixTiers);
        }

        if (includeGroups)
        {
            query = query.Include(v => v.ApproverGroups).ThenInclude(g => g.Members);
        }

        return await query.FirstAsync(cancellationToken);
    }

    public async Task DeleteWorkflowAsync(Guid workflowId, CancellationToken cancellationToken = default)
    {
        var workflow = await _db.Workflows.FirstOrDefaultAsync(w => w.Id == workflowId, cancellationToken)
            ?? throw new DomainException("Workflow not found.");

        EnsureSuperAdmin();

        if (await _db.Documents.AnyAsync(d => d.WorkflowId == workflowId, cancellationToken))
        {
            throw new DomainException("Cannot delete a workflow that has documents. Deactivate it instead.");
        }

        workflow.IsActive = false;
        workflow.UpdatedAtUtc = _clock.UtcNow;
        await SaveAsync(cancellationToken);
    }

    private void EnsureSuperAdmin()
    {
        if (!_currentUser.IsInRole(RoleNames.SuperAdmin))
        {
            throw new DomainException("Only Super Admin can manage workflows.");
        }
    }

    private static WorkflowVersion CreateVersion(
        Workflow workflow,
        ApprovalMode approvalMode,
        ApprovalSequence approvalSequence,
        ReturnResumePolicy returnResumePolicy,
        int? slaThresholdHours,
        bool escalationEnabled,
        int versionNumber,
        WorkflowVersionState state,
        DateTime now)
    {
        return new WorkflowVersion
        {
            WorkflowId = workflow.Id,
            VersionNumber = versionNumber,
            ApprovalMode = approvalMode,
            ApprovalSequence = approvalSequence,
            ReturnResumePolicy = returnResumePolicy,
            SlaThresholdHours = slaThresholdHours,
            EscalationEnabled = escalationEnabled,
            State = state,
            ActivatedAtUtc = state == WorkflowVersionState.Active ? now : null,
            CreatedAtUtc = now
        };
    }

    private static void ApplyVersionSettings(WorkflowVersion version, UpdateWorkflowRequest request)
    {
        version.ApprovalMode = request.ApprovalMode;
        version.ApprovalSequence = request.ApprovalSequence;
        version.ReturnResumePolicy = request.ReturnResumePolicy;
        version.SlaThresholdHours = request.SlaThresholdHours;
        version.EscalationEnabled = request.EscalationEnabled;
        version.State = request.TargetState;
        if (request.NotificationSettingsJson is not null)
        {
            version.NotificationSettingsJson = request.NotificationSettingsJson;
        }
    }

    private static void AddApproverGroups(WorkflowVersion version, IEnumerable<ApproverGroupInput> groups, DateTime now)
    {
        foreach (var groupInput in groups.OrderBy(g => g.SequenceOrder))
        {
            var group = new ApproverGroup
            {
                WorkflowVersionId = version.Id,
                Name = groupInput.Name,
                SequenceOrder = groupInput.SequenceOrder,
                Requirement = groupInput.Requirement,
                CreatedAtUtc = now
            };

            foreach (var memberId in groupInput.MemberUserIds.Distinct())
            {
                group.Members.Add(new ApproverGroupMember
                {
                    UserId = memberId,
                    CreatedAtUtc = now
                });
            }

            version.ApproverGroups.Add(group);
        }
    }

    private static void AddMatrixTiers(WorkflowVersion version, IEnumerable<MatrixTierInput> tierInputs, DateTime now)
    {
        var tiers = tierInputs.Select(t => new ApprovalMatrixTier
        {
            WorkflowVersionId = version.Id,
            SequenceOrder = t.SequenceOrder,
            MinAmount = t.MinAmount,
            MaxAmount = t.MaxAmount,
            ApproverUserIdsJson = System.Text.Json.JsonSerializer.Serialize(t.ApproverUserIds),
            CreatedAtUtc = now
        }).ToList();

        Domain.Services.MatrixTierValidator.Validate(tiers);

        foreach (var tier in tiers)
        {
            version.MatrixTiers.Add(tier);
        }
    }

    private static WorkflowDto MapWorkflow(Workflow workflow)
    {
        var activeVersion = workflow.Versions
            .Where(v => v.State == WorkflowVersionState.Active)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefault();

        return new WorkflowDto(
            workflow.Id,
            workflow.DepartmentId,
            workflow.Name,
            workflow.DocumentType,
            workflow.Description,
            workflow.IsActive,
            activeVersion is null ? null : MapVersion(activeVersion));
    }

    private static WorkflowVersionSummaryDto MapVersion(WorkflowVersion version) =>
        new(version.Id, version.VersionNumber, version.State, version.ApprovalMode, version.ApprovalSequence, version.ReturnResumePolicy, version.SlaThresholdHours, version.EscalationEnabled);

    private static ApproverGroupDto MapGroup(ApproverGroup group) =>
        new(group.Id, group.Name, group.SequenceOrder, group.Requirement, group.Members.Select(m => m.UserId).ToList());

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        if (_db is IUnitOfWork unitOfWork)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
