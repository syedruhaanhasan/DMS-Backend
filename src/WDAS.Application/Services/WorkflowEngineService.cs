using Microsoft.EntityFrameworkCore;
using WDAS.Application;
using WDAS.Application.Abstractions;
using WDAS.Application.Models;
using WDAS.Domain.Entities;
using WDAS.Domain.Enums;
using WDAS.Domain.Exceptions;
using WDAS.Domain.Services;

namespace WDAS.Application.Services;

public class WorkflowEngineService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;
    private readonly INotificationDispatcher _notifications;
    private readonly IAuditWriter _auditWriter;

    public WorkflowEngineService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IClock clock,
        INotificationDispatcher notifications,
        IAuditWriter auditWriter)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _notifications = notifications;
        _auditWriter = auditWriter;
    }

    public Task<DocumentDto> ApproveAsync(int stepId, WorkflowActionRequest request, CancellationToken cancellationToken = default)
        => ExecuteActionAsync(stepId, WorkflowActionType.Approve, request, cancellationToken);

    public Task<DocumentDto> RejectAsync(int stepId, WorkflowActionRequest request, CancellationToken cancellationToken = default)
        => ExecuteActionAsync(stepId, WorkflowActionType.Reject, request, cancellationToken);

    public Task<DocumentDto> ReturnAsync(int stepId, WorkflowActionRequest request, CancellationToken cancellationToken = default)
        => ExecuteActionAsync(stepId, WorkflowActionType.ReturnForCorrection, request, cancellationToken);

    public async Task<DocumentDto> CommentAsync(int stepId, WorkflowActionRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Comment))
        {
            throw new DomainException("Comment text is required.");
        }

        return await ExecuteActionAsync(stepId, WorkflowActionType.Comment, request, cancellationToken, commentOnly: true);
    }

    private async Task<DocumentDto> ExecuteActionAsync(
        int stepId,
        WorkflowActionType actionType,
        WorkflowActionRequest request,
        CancellationToken cancellationToken,
        bool commentOnly = false)
    {
        var documentId = await _db.WorkflowSteps
            .Where(s => s.Id == stepId)
            .Select(s => s.DocumentId)
            .FirstOrDefaultAsync(cancellationToken);

        if (documentId == 0)
        {
            throw new DomainException("Workflow step not found.");
        }

        var document = await _db.Documents
            .Include(d => d.WorkflowSteps)
            .FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken)
            ?? throw new DomainException("Document not found.");

        var step = document.WorkflowSteps.First(s => s.Id == stepId);

        if (request.ExpectedStepVersion.HasValue)
        {
            var currentVersion = await _db.WorkflowStepActions.CountAsync(a => a.WorkflowStepId == step.Id, cancellationToken);
            if (currentVersion != request.ExpectedStepVersion.Value)
            {
                throw new ConflictException(
                    "STALE_OFFLINE_ACTION",
                    "The workflow step has changed since this action was queued.",
                    new ConflictResponseDto(
                        "STALE_OFFLINE_ACTION",
                        "Re-validate sequential visibility and retry.",
                        IdParsing.ToApi(document.Id),
                        IdParsing.ToApi(step.Id),
                        step.Status.ToString(),
                        _clock.UtcNow));
            }
        }

        if (document.Status is not (DocumentStatus.InApproval or DocumentStatus.Submitted))
        {
            throw new DomainException("Document is not in an approvable state.");
        }

        var groupMemberIds = await GetGroupMemberIdsAsync(step, cancellationToken);
        var onBehalfOfUserId = await ResolveOnBehalfOfAsync(step, cancellationToken);
        var canAct = WorkflowEngineRules.CanUserActOnStep(step, _currentUser.UserId, groupMemberIds)
                     || onBehalfOfUserId.HasValue;
        if (!canAct && !commentOnly)
        {
            throw new DomainException("You are not authorized to act on this workflow step.");
        }

        if (!commentOnly)
        {
            var approvalSequence = await _db.WorkflowVersions
                .Where(v => v.Id == step.WorkflowVersionId)
                .Select(v => v.ApprovalSequence)
                .FirstOrDefaultAsync(cancellationToken);

            WorkflowEngineRules.ValidateSequentialVisibility(document, step, approvalSequence);
        }
        else if (step.Status != WorkflowStepStatus.Active)
        {
            throw new DomainException("Comments can only be added on the active step.");
        }

        var now = _clock.UtcNow;
        UpdateSlaBreach(step, now);

        _db.Add(new WorkflowStepAction
        {
            WorkflowStepId = step.Id,
            ActorUserId = _currentUser.UserId,
            OnBehalfOfUserId = onBehalfOfUserId,
            ActionType = actionType,
            Comment = request.Comment,
            ActionAtUtc = now,
            CreatedAtUtc = now
        });

        if (commentOnly)
        {
            await SaveAsync(cancellationToken);
            return await LoadDocumentDtoAsync(document.Id, cancellationToken);
        }

        switch (actionType)
        {
            case WorkflowActionType.Approve:
                await HandleApproveAsync(document, step, now, cancellationToken);
                break;
            case WorkflowActionType.Reject:
                HandleReject(document, step, now);
                break;
            case WorkflowActionType.ReturnForCorrection:
                HandleReturn(document, step, now);
                break;
        }

        await SaveAsync(cancellationToken);

        try
        {
            await DispatchWorkflowNotificationAsync(document, step, actionType, cancellationToken);
        }
        catch
        {
            // Approval must not fail because notification delivery failed.
        }

        try
        {
            await WriteAuditAsync(document, step, actionType, request, cancellationToken);
        }
        catch
        {
            // Approval must not fail because audit append raced on SequenceNumber.
        }

        return await LoadDocumentDtoAsync(document.Id, cancellationToken);
    }

    private async Task WriteAuditAsync(
        Document document,
        WorkflowStep step,
        WorkflowActionType actionType,
        WorkflowActionRequest request,
        CancellationToken cancellationToken)
    {
        var auditType = actionType switch
        {
            WorkflowActionType.Approve => AuditEventType.Approve,
            WorkflowActionType.Reject => AuditEventType.Reject,
            WorkflowActionType.ReturnForCorrection => AuditEventType.ReturnForCorrection,
            WorkflowActionType.Comment => AuditEventType.Comment,
            _ => AuditEventType.Update
        };

        await _auditWriter.WriteAsync(new AuditWriteRequest(
            auditType,
            actionType.ToString(),
            document.Id,
            nameof(WorkflowStep),
            step.Id.ToString(),
            System.Text.Json.JsonSerializer.Serialize(new { request.Comment, request.ClientActionAtUtc }),
            _currentUser.UserId),
            cancellationToken);
    }

    private async Task HandleApproveAsync(Document document, WorkflowStep step, DateTime now, CancellationToken cancellationToken)
    {
        step.Status = WorkflowStepStatus.Approved;
        step.CompletedAtUtc = now;
        step.UpdatedAtUtc = now;

        var approvalSequence = await _db.WorkflowVersions
            .Where(v => v.Id == step.WorkflowVersionId)
            .Select(v => v.ApprovalSequence)
            .FirstOrDefaultAsync(cancellationToken);

        if (approvalSequence == ApprovalSequence.Parallel)
        {
            if (document.WorkflowSteps.All(s => s.Status is WorkflowStepStatus.Approved or WorkflowStepStatus.Skipped))
            {
                document.Status = DocumentStatus.ReadyForFinalization;
                document.UpdatedAtUtc = now;
            }

            return;
        }

        var nextStep = document.WorkflowSteps
            .OrderBy(s => s.StepOrder)
            .FirstOrDefault(s => s.Status == WorkflowStepStatus.Pending);

        if (nextStep is null)
        {
            document.Status = DocumentStatus.ReadyForFinalization;
            document.UpdatedAtUtc = now;
            return;
        }

        nextStep.Status = WorkflowStepStatus.Active;
        nextStep.ActivatedAtUtc = now;
        nextStep.UpdatedAtUtc = now;

        var slaHours = await _db.WorkflowVersions
            .Where(v => v.Id == step.WorkflowVersionId)
            .Select(v => v.SlaThresholdHours)
            .FirstOrDefaultAsync(cancellationToken);

        if (slaHours.HasValue)
        {
            nextStep.SlaDueAtUtc = now.AddHours(slaHours.Value);
        }
    }

    private static void HandleReject(Document document, WorkflowStep step, DateTime now)
    {
        step.Status = WorkflowStepStatus.Rejected;
        step.CompletedAtUtc = now;
        step.UpdatedAtUtc = now;
        document.Status = DocumentStatus.Rejected;
        document.UpdatedAtUtc = now;

        foreach (var pending in document.WorkflowSteps.Where(s => s.Id != step.Id && s.Status is WorkflowStepStatus.Pending or WorkflowStepStatus.Active))
        {
            pending.Status = WorkflowStepStatus.Skipped;
            pending.UpdatedAtUtc = now;
        }
    }

    private static void HandleReturn(Document document, WorkflowStep step, DateTime now)
    {
        step.Status = WorkflowStepStatus.Returned;
        step.CompletedAtUtc = now;
        step.UpdatedAtUtc = now;
        document.Status = DocumentStatus.ReturnedForCorrection;
        document.IsBodyLocked = false;
        document.UpdatedAtUtc = now;

        foreach (var pending in document.WorkflowSteps.Where(s => s.Status == WorkflowStepStatus.Pending))
        {
            pending.Status = WorkflowStepStatus.Skipped;
            pending.UpdatedAtUtc = now;
        }
    }

    private async Task<DocumentDto> LoadDocumentDtoAsync(int documentId, CancellationToken cancellationToken)
    {
        var document = await _db.Documents
            .AsNoTracking()
            .Include(d => d.Owner)
            .Include(d => d.Recipients)
            .Include(d => d.WorkflowSteps)
                .ThenInclude(s => s.ApproverUser)
            .Include(d => d.WorkflowSteps)
                .ThenInclude(s => s.Actions)
                    .ThenInclude(a => a.Actor)
            .FirstAsync(d => d.Id == documentId, cancellationToken);

        return MapDocument(document);
    }

    private async Task<IReadOnlyCollection<int>> GetGroupMemberIdsAsync(WorkflowStep step, CancellationToken cancellationToken)
    {
        if (!step.ApproverGroupId.HasValue)
        {
            return [];
        }

        return await _db.ApproverGroupMembers
            .Where(m => m.ApproverGroupId == step.ApproverGroupId.Value)
            .Select(m => m.UserId)
            .ToListAsync(cancellationToken);
    }

    private static void UpdateSlaBreach(WorkflowStep step, DateTime now)
    {
        if (step.SlaDueAtUtc.HasValue && now > step.SlaDueAtUtc.Value)
        {
            step.IsSlaBreached = true;
            step.UpdatedAtUtc = now;
        }
    }

    private static DocumentDto MapDocument(Document document) =>
        new(
            IdParsing.ToApi(document.Id),
            document.RecordNumber,
            document.RevisionNumber < 1 ? 1 : document.RevisionNumber,
            IdParsing.ToApi(document.OwnerUserId),
            document.Owner.DisplayName,
            IdParsing.ToApi(document.DepartmentId),
            IdParsing.ToApi(document.WorkflowId),
            document.WorkflowVersionId is int wv ? IdParsing.ToApi(wv) : null,
            document.ToRecipients,
            document.FromDisplay,
            document.Subject,
            document.BodyHtml,
            document.Amount,
            document.Priority,
            document.Status,
            document.IsBodyLocked,
            document.SubmittedAtUtc,
            document.ArchiveDocumentId,
            document.FinalizedAtUtc,
            document.CancellationReason,
            ParseAdHocIds(document.AdHocApproverUserIdsJson)?.Select(IdParsing.ToApi).ToList(),
            document.Recipients.Select(r => new DocumentRecipientDto(
                IdParsing.ToApi(r.Id),
                r.RecipientName,
                r.RecipientEmail,
                r.ReviewerUserId is int ru ? IdParsing.ToApi(ru) : null,
                r.AddedByUserId is int ab ? IdParsing.ToApi(ab) : null)).ToList(),
            document.WorkflowSteps
                .OrderBy(s => s.StepOrder)
                .Select(s => new WorkflowStepDto(
                    IdParsing.ToApi(s.Id),
                    s.StepOrder,
                    s.ApproverUserId is int au ? IdParsing.ToApi(au) : null,
                    s.ApproverUser?.DisplayName,
                    s.GroupName,
                    s.Status,
                    s.ActivatedAtUtc,
                    s.CompletedAtUtc,
                    s.SlaDueAtUtc,
                    s.IsSlaBreached,
                    s.Actions.OrderBy(a => a.ActionAtUtc).Select(a => new WorkflowStepActionDto(
                        IdParsing.ToApi(a.Id),
                        IdParsing.ToApi(a.ActorUserId),
                        a.Actor.DisplayName,
                        a.ActionType,
                        a.Comment,
                        a.ActionAtUtc)).ToList()))
                .ToList());

    private static List<int>? ParseAdHocIds(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return System.Text.Json.JsonSerializer.Deserialize<List<int>>(json);
    }

    private async Task<int?> ResolveOnBehalfOfAsync(WorkflowStep step, CancellationToken cancellationToken)
    {
        if (step.ApproverUserId == _currentUser.UserId)
        {
            return null;
        }

        var now = _clock.UtcNow;
        var isDelegate = await _db.Delegations.AnyAsync(
            d => d.DelegateUserId == _currentUser.UserId &&
                 d.ApproverUserId == step.ApproverUserId &&
                 d.IsActive &&
                 d.StartsAtUtc <= now &&
                 d.EndsAtUtc >= now,
            cancellationToken);

        return isDelegate ? step.ApproverUserId : null;
    }

    private async Task DispatchWorkflowNotificationAsync(
        Document document,
        WorkflowStep step,
        WorkflowActionType actionType,
        CancellationToken cancellationToken)
    {
        var ownerEventType = actionType switch
        {
            WorkflowActionType.Approve => NotificationEventType.ApprovalRecorded,
            WorkflowActionType.Reject => NotificationEventType.Rejected,
            WorkflowActionType.ReturnForCorrection => NotificationEventType.ReturnedForCorrection,
            _ => (NotificationEventType?)null
        };

        if (ownerEventType is not null)
        {
            var ownerMessage = actionType switch
            {
                WorkflowActionType.Approve => $"Step {step.StepOrder} was approved on your document.",
                WorkflowActionType.Reject => $"Your document was rejected at step {step.StepOrder}.",
                WorkflowActionType.ReturnForCorrection => $"Your document was returned for correction at step {step.StepOrder}.",
                _ => $"Action {actionType} recorded on step {step.StepOrder}."
            };

            await _notifications.DispatchAsync(new NotificationRequest(
                ownerEventType.Value,
                document.OwnerUserId,
                null,
                document.Id,
                step.Id,
                $"Workflow update: {document.Subject}",
                ownerMessage),
                cancellationToken);
        }

        if (actionType != WorkflowActionType.Approve)
        {
            return;
        }

        var nextStep = document.WorkflowSteps
            .OrderBy(s => s.StepOrder)
            .FirstOrDefault(s => s.Status == WorkflowStepStatus.Active);

        if (nextStep is null)
        {
            return;
        }

        var recipientIds = new List<int>();
        if (nextStep.ApproverUserId.HasValue)
        {
            recipientIds.Add(nextStep.ApproverUserId.Value);
        }
        else if (nextStep.ApproverGroupId.HasValue)
        {
            recipientIds.AddRange(await _db.ApproverGroupMembers
                .Where(m => m.ApproverGroupId == nextStep.ApproverGroupId.Value)
                .Select(m => m.UserId)
                .ToListAsync(cancellationToken));
        }

        foreach (var userId in recipientIds.Distinct())
        {
            await _notifications.DispatchAsync(new NotificationRequest(
                NotificationEventType.SubmittedForApproval,
                userId,
                null,
                document.Id,
                nextStep.Id,
                $"Approval required: {document.Subject}",
                $"Step {step.StepOrder} was approved. Your approval is now required."),
                cancellationToken);
        }
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        if (_db is IUnitOfWork unitOfWork)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
