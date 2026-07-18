using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WDAS.Application;
using WDAS.Application.Abstractions;
using WDAS.Application.Models;
using WDAS.Domain.Entities;
using WDAS.Domain.Enums;
using WDAS.Domain.Exceptions;
using WDAS.Domain.Services;

namespace WDAS.Application.Services;

public class DocumentService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;
    private readonly ApprovalChainResolver _chainResolver = new();
    private readonly IDocumentSearchIndexer _searchIndexer;
    private readonly INotificationDispatcher _notifications;

    public DocumentService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IClock clock,
        IDocumentSearchIndexer searchIndexer,
        INotificationDispatcher notifications)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _searchIndexer = searchIndexer;
        _notifications = notifications;
    }

    public async Task<DocumentDto> CreateDocumentAsync(CreateDocumentRequest request, CancellationToken cancellationToken = default)
    {
        var owner = await GetCurrentUserEntityAsync(cancellationToken);
        var workflowId = IdParsing.ParseRequired(request.WorkflowId, "Workflow id");
        var workflow = await _db.Workflows
            .Include(w => w.Versions)
            .FirstOrDefaultAsync(w => w.Id == workflowId, cancellationToken)
            ?? throw new DomainException("Workflow not found.");

        if (!workflow.IsActive)
        {
            throw new DomainException("This workflow is inactive. Activate it under Configuration → Workflows or choose another workflow.");
        }

        var activeVersion = workflow.Versions
            .Where(v => v.State == WorkflowVersionState.Active)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefault()
            ?? throw new DomainException(
                $"Workflow '{workflow.Name}' has no published version. Open Configuration → Workflows and publish the workflow before creating documents.");

        var adHocIds = ParseAdHocFromRequest(request.AdHocApproverUserIds);
        ValidateAdHocRequirements(activeVersion, adHocIds);

        var now = _clock.UtcNow;
        var document = new Document
        {
            OwnerUserId = owner.Id,
            DepartmentId = owner.DepartmentId,
            WorkflowId = workflow.Id,
            WorkflowVersionId = null,
            ToRecipients = request.ToRecipients,
            FromDisplay = $"{owner.DisplayName} <{owner.Email}>",
            Subject = request.Subject,
            BodyHtml = request.BodyHtml,
            Amount = request.Amount,
            Priority = request.Priority,
            Status = DocumentStatus.Draft,
            RevisionNumber = 1,
            AdHocApproverUserIdsJson = adHocIds is null
                ? null
                : JsonSerializer.Serialize(adHocIds),
            CreatedAtUtc = now
        };

        foreach (var recipient in request.Recipients)
        {
            document.Recipients.Add(new DocumentRecipient
            {
                RecipientName = recipient.RecipientName,
                RecipientEmail = recipient.RecipientEmail,
                CreatedAtUtc = now
            });
        }

        _db.Add(document);

        // Persist the document first so identity DocumentId exists before workflow steps are inserted.
        await SaveAsync(cancellationToken);

        if (request.Submit)
        {
            await ApplySubmitAsync(document, request.IdempotencyKey, cancellationToken);
            await SaveAsync(cancellationToken);
        }

        return await GetDocumentDtoAsync(document.Id, cancellationToken);
    }

    public async Task<DocumentDto> UpdateDocumentAsync(int documentId, UpdateDocumentRequest request, CancellationToken cancellationToken = default)
    {
        // Avoid loading recipients when submitting — tracked recipient rows were causing
        // DELETE+UPDATE concurrency conflicts in the same SaveChanges batch.
        IQueryable<Document> query = _db.Documents.Include(d => d.Owner);
        if (!request.Submit && request.Recipients is not null)
        {
            query = query.Include(d => d.Recipients);
        }

        var document = await query.FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken)
            ?? throw new DomainException("Document not found.");
        EnsureOwner(document);

        if (document.Status is DocumentStatus.Finalized or DocumentStatus.Cancelled)
        {
            throw new DomainException("Document cannot be edited in its current state.");
        }

        if (document.IsBodyLocked && document.Status is not DocumentStatus.ReturnedForCorrection)
        {
            throw new DomainException("Document body is locked and cannot be edited.");
        }

        if (document.Status is not (DocumentStatus.Draft or DocumentStatus.ReturnedForCorrection))
        {
            throw new DomainException("Only draft or returned documents can be edited.");
        }

        document.Subject = request.Subject;
        document.BodyHtml = request.BodyHtml;
        document.Amount = request.Amount;
        document.Priority = request.Priority;
        if (!string.IsNullOrWhiteSpace(request.ToRecipients))
        {
            document.ToRecipients = request.ToRecipients;
        }

        if (request.AdHocApproverUserIds is not null)
        {
            var adHocIds = ParseAdHocFromRequest(request.AdHocApproverUserIds) ?? [];
            document.AdHocApproverUserIdsJson = adHocIds.Count == 0
                ? null
                : JsonSerializer.Serialize(adHocIds);
        }

        document.UpdatedAtUtc = _clock.UtcNow;

        // Never mutate recipients during submit — only when explicitly editing without submit.
        if (!request.Submit && request.Recipients is not null)
        {
            var previousRecipients = document.Recipients.ToList();
            foreach (var previous in previousRecipients)
            {
                document.Recipients.Remove(previous);
            }

            foreach (var recipient in request.Recipients)
            {
                document.Recipients.Add(new DocumentRecipient
                {
                    DocumentId = document.Id,
                    RecipientName = recipient.RecipientName,
                    RecipientEmail = recipient.RecipientEmail,
                    CreatedAtUtc = _clock.UtcNow
                });
            }
        }

        if (request.Submit)
        {
            await ApplySubmitAsync(document, request.IdempotencyKey, cancellationToken);
            await SaveAsync(cancellationToken);
            return await GetDocumentDtoAsync(document.Id, cancellationToken);
        }

        await SaveAsync(cancellationToken);
        return await GetDocumentDtoAsync(document.Id, cancellationToken);
    }

    public async Task<DocumentDto> GetDocumentAsync(int documentId, CancellationToken cancellationToken = default)
    {
        var document = await LoadDocumentAsync(documentId, cancellationToken);
        EnsureCanView(document);
        return MapDocument(document);
    }

    /// <summary>
    /// Owner reopens a rejected document for correction. Bumps revision (v2, v3, …)
    /// and unlocks the body so the same document can be edited and resubmitted.
    /// </summary>
    public async Task<DocumentDto> ReviseRejectedDocumentAsync(int documentId, CancellationToken cancellationToken = default)
    {
        var document = await _db.Documents
            .Include(d => d.Owner)
            .FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken)
            ?? throw new DomainException("Document not found.");
        EnsureOwner(document);

        if (document.Status == DocumentStatus.ReturnedForCorrection && !document.IsBodyLocked)
        {
            // Idempotent: already opened for correction (e.g. prior Update click succeeded).
            return await GetDocumentDtoAsync(document.Id, cancellationToken);
        }

        if (document.Status != DocumentStatus.Rejected)
        {
            throw new DomainException("Only rejected documents can be revised for a new version.");
        }

        var now = _clock.UtcNow;
        document.RevisionNumber = document.RevisionNumber < 1 ? 2 : document.RevisionNumber + 1;
        document.Status = DocumentStatus.ReturnedForCorrection;
        document.IsBodyLocked = false;
        document.UpdatedAtUtc = now;

        await SaveAsync(cancellationToken);
        return await GetDocumentDtoAsync(document.Id, cancellationToken);
    }

    public async Task DeleteDocumentAsync(int documentId, CancellationToken cancellationToken = default)
    {
        var document = await _db.Documents
            .Include(d => d.Recipients)
            .Include(d => d.Attachments)
            .Include(d => d.WorkflowSteps)
                .ThenInclude(s => s.Actions)
            .FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken)
            ?? throw new DomainException("Document not found.");

        if (document.OwnerUserId != _currentUser.UserId)
        {
            throw new DomainException("Only the document owner can delete this document.");
        }

        if (document.Status is not (DocumentStatus.Draft or DocumentStatus.Cancelled))
        {
            throw new DomainException("Only draft or cancelled documents can be deleted.");
        }

        if (await _db.RepositoryDocuments.AnyAsync(r => r.SourceDocumentId == document.Id, cancellationToken))
        {
            throw new DomainException("Finalized documents cannot be deleted.");
        }

        var notifications = await _db.Notifications.Where(n => n.DocumentId == document.Id).ToListAsync(cancellationToken);
        _db.RemoveRange(notifications);

        var stepIds = document.WorkflowSteps.Select(s => s.Id).ToList();
        if (stepIds.Count > 0)
        {
            var externalSessions = await _db.ExternalApproverSessions
                .Where(s => stepIds.Contains(s.WorkflowStepId))
                .ToListAsync(cancellationToken);
            _db.RemoveRange(externalSessions);
        }

        foreach (var step in document.WorkflowSteps)
        {
            _db.RemoveRange(step.Actions);
        }

        _db.RemoveRange(document.WorkflowSteps);
        _db.RemoveRange(document.Attachments);
        _db.RemoveRange(document.Recipients);

        var searchIndex = await _db.DocumentSearchIndexes.FirstOrDefaultAsync(i => i.DocumentId == document.Id, cancellationToken);
        if (searchIndex is not null)
        {
            _db.RemoveRange(new[] { searchIndex });
        }

        _db.RemoveRange(new[] { document });
        await SaveAsync(cancellationToken);
    }

    public Task<DocumentDto> SubmitDocumentAsync(int documentId, string? idempotencyKey, CancellationToken cancellationToken = default) =>
        SubmitDocumentInternalAsync(documentId, idempotencyKey, cancellationToken);

    private async Task<DocumentDto> SubmitDocumentInternalAsync(int documentId, string? idempotencyKey, CancellationToken cancellationToken)
    {
        var document = await LoadDocumentForSubmitAsync(documentId, cancellationToken);
        EnsureOwner(document);

        if (!string.IsNullOrWhiteSpace(document.SubmitIdempotencyKey) &&
            !string.IsNullOrWhiteSpace(idempotencyKey) &&
            document.SubmitIdempotencyKey == idempotencyKey &&
            document.Status is not DocumentStatus.Draft and not DocumentStatus.ReturnedForCorrection)
        {
            return MapDocument(await LoadDocumentAsync(documentId, cancellationToken));
        }

        await ApplySubmitAsync(document, idempotencyKey, cancellationToken);
        await SaveAsync(cancellationToken);
        return await GetDocumentDtoAsync(document.Id, cancellationToken);
    }

    public Task RunPostSubmitSideEffectsAsync(int documentId, CancellationToken cancellationToken = default) =>
        FinalizeSubmissionSideEffectsAsync(documentId, cancellationToken);

    /// <summary>
    /// Search index + notifications must not block the submit HTTP response.
    /// Email/SMTP failures (e.g. Mailpit down) previously stalled "Submit for approval".
    /// </summary>
    private async Task FinalizeSubmissionSideEffectsAsync(int documentId, CancellationToken cancellationToken)
    {
        try
        {
            await _searchIndexer.IndexDocumentAsync(documentId, cancellationToken);
        }
        catch (Exception)
        {
            // Indexing can catch up later; submission already succeeded.
        }

        try
        {
            using var notifyCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            notifyCts.CancelAfter(TimeSpan.FromSeconds(4));
            await NotifySubmittedAsync(documentId, notifyCts.Token);
        }
        catch (OperationCanceledException)
        {
            // Timed out — in-app/email can retry via jobs; don't fail submit.
        }
        catch (Exception)
        {
            // Notification failures must never fail document submission.
        }
    }

    private async Task ApplySubmitAsync(Document document, string? idempotencyKey, CancellationToken cancellationToken)
    {
        if (document.Status is not (DocumentStatus.Draft or DocumentStatus.ReturnedForCorrection))
        {
            throw new DomainException("Document has already been submitted.");
        }

        var workflowVersion = await _db.WorkflowVersions
            .Include(v => v.MatrixTiers)
            .Include(v => v.ApproverGroups)
                .ThenInclude(g => g.Members)
            .FirstOrDefaultAsync(v => v.WorkflowId == document.WorkflowId && v.State == WorkflowVersionState.Active, cancellationToken)
            ?? throw new DomainException("Active workflow version not found.");

        ValidateAdHocRequirements(workflowVersion, ParseAdHocIds(document.AdHocApproverUserIdsJson));

        if (document.Status == DocumentStatus.ReturnedForCorrection)
        {
            // Use store ExecuteDelete so tracked entities / cascade order cannot cause concurrency conflicts.
            var stepIds = await _db.WorkflowSteps
                .AsNoTracking()
                .Where(s => s.DocumentId == document.Id)
                .Select(s => s.Id)
                .ToListAsync(cancellationToken);

            if (stepIds.Count > 0)
            {
                var actionIds = await _db.WorkflowStepActions
                    .AsNoTracking()
                    .Where(a => stepIds.Contains(a.WorkflowStepId))
                    .Select(a => a.Id)
                    .ToListAsync(cancellationToken);

                if (actionIds.Count > 0)
                {
                    await _db.Attachments
                        .Where(a => a.WorkflowStepActionId != null && actionIds.Contains(a.WorkflowStepActionId.Value))
                        .ExecuteUpdateAsync(
                            setters => setters.SetProperty(a => a.WorkflowStepActionId, (int?)null),
                            cancellationToken);
                }

                await _db.ExternalApproverSessions
                    .Where(s => stepIds.Contains(s.WorkflowStepId))
                    .ExecuteDeleteAsync(cancellationToken);

                await _db.WorkflowStepActions
                    .Where(a => stepIds.Contains(a.WorkflowStepId))
                    .ExecuteDeleteAsync(cancellationToken);

                await _db.WorkflowSteps
                    .Where(s => s.DocumentId == document.Id)
                    .ExecuteDeleteAsync(cancellationToken);
            }
        }

        var resolvedSteps = _chainResolver.Resolve(workflowVersion, document.Amount, ParseAdHocIds(document.AdHocApproverUserIdsJson));
        if (resolvedSteps.Count == 0)
        {
            throw new DomainException("Resolved approval chain is empty.");
        }

        var now = _clock.UtcNow;
        document.WorkflowVersionId = workflowVersion.Id;
        document.IsBodyLocked = true;
        document.Status = DocumentStatus.InApproval;
        document.SubmittedAtUtc = now;
        document.SubmitIdempotencyKey = idempotencyKey ?? Guid.NewGuid().ToString();
        document.UpdatedAtUtc = now;

        for (var i = 0; i < resolvedSteps.Count; i++)
        {
            var resolved = resolvedSteps[i];
            var activateNow = workflowVersion.ApprovalSequence == ApprovalSequence.Parallel || i == 0;
            document.WorkflowSteps.Add(new WorkflowStep
            {
                DocumentId = document.Id,
                WorkflowVersionId = workflowVersion.Id,
                StepOrder = resolved.StepOrder,
                ApproverUserId = resolved.ApproverUserId,
                ApproverGroupId = resolved.ApproverGroupId,
                GroupName = resolved.GroupName,
                GroupRequirement = resolved.GroupRequirement,
                Status = activateNow ? WorkflowStepStatus.Active : WorkflowStepStatus.Pending,
                ActivatedAtUtc = activateNow ? now : null,
                SlaDueAtUtc = workflowVersion.SlaThresholdHours.HasValue && activateNow
                    ? now.AddHours(workflowVersion.SlaThresholdHours.Value)
                    : null,
                CreatedAtUtc = now
            });
        }
    }

    private async Task<Document> LoadDocumentAsync(int documentId, CancellationToken cancellationToken)
    {
        return await _db.Documents
            .Include(d => d.Owner)
            .Include(d => d.Recipients)
            .Include(d => d.WorkflowSteps)
                .ThenInclude(s => s.ApproverUser)
            .Include(d => d.WorkflowSteps)
                .ThenInclude(s => s.Actions)
                    .ThenInclude(a => a.Actor)
            .FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken)
            ?? throw new DomainException("Document not found.");
    }

    private async Task<Document> LoadDocumentForSubmitAsync(int documentId, CancellationToken cancellationToken)
    {
        return await _db.Documents
            .Include(d => d.WorkflowSteps)
                .ThenInclude(s => s.Actions)
            .FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken)
            ?? throw new DomainException("Document not found.");
    }

    private async Task<DocumentDto> GetDocumentDtoAsync(int documentId, CancellationToken cancellationToken)
    {
        var document = await LoadDocumentAsync(documentId, cancellationToken);
        return MapDocument(document);
    }

    private async Task<User> GetCurrentUserEntityAsync(CancellationToken cancellationToken)
    {
        return await _db.Users.FirstOrDefaultAsync(u => u.Id == _currentUser.UserId, cancellationToken)
            ?? throw new DomainException("Current user not found.");
    }

    private void EnsureOwner(Document document)
    {
        if (document.OwnerUserId != _currentUser.UserId && !_currentUser.IsInRole(RoleNames.SuperAdmin))
        {
            throw new DomainException("Only the document owner can perform this action.");
        }
    }

    private void EnsureCanView(Document document)
    {
        if (document.OwnerUserId == _currentUser.UserId ||
            _currentUser.IsInRole(RoleNames.SuperAdmin) ||
            _currentUser.IsInRole(RoleNames.Auditor))
        {
            return;
        }

        var visibleStep = document.WorkflowSteps
            .Where(s => s.Status == WorkflowStepStatus.Active)
            .Any(s => s.ApproverUserId == _currentUser.UserId);

        var priorCompleted = document.WorkflowSteps
            .Where(s => s.Status == WorkflowStepStatus.Approved)
            .Any(s => s.Actions.Any(a => a.ActorUserId == _currentUser.UserId));

        if (visibleStep || priorCompleted)
        {
            return;
        }

        if (document.Status == DocumentStatus.Draft)
        {
            throw new DomainException("Draft documents are visible to the owner only.");
        }

        throw new DomainException("You are not authorized to view this document.");
    }

    private static void ValidateAdHocRequirements(WorkflowVersion version, IReadOnlyCollection<int>? adHocApproverUserIds)
    {
        var needsSelectedApprovers = version.ApprovalMode switch
        {
            ApprovalMode.AdHoc => true,
            ApprovalMode.Group => version.ApproverGroups.Count == 0,
            ApprovalMode.Hybrid => version.MatrixTiers.Count == 0 && version.ApproverGroups.Count == 0,
            _ => false,
        };

        if (needsSelectedApprovers && (adHocApproverUserIds is null || adHocApproverUserIds.Count == 0))
        {
            throw new DomainException("At least one approver is required.");
        }
    }

    private static List<int>? ParseAdHocFromRequest(IReadOnlyCollection<string>? values)
    {
        if (values is null)
        {
            return null;
        }

        return values.Select(v => IdParsing.ParseRequired(v, "Ad-hoc approver user id")).ToList();
    }

    private static List<int>? ParseAdHocIds(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return JsonSerializer.Deserialize<List<int>>(json);
    }

    private static DocumentDto MapDocument(Document document) =>
        new(
            IdParsing.ToApi(document.Id),
            document.RecordNumber,
            document.RevisionNumber < 1 ? 1 : document.RevisionNumber,
            IdParsing.ToApi(document.OwnerUserId),
            document.Owner?.DisplayName ?? "Unknown",
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
            document.Recipients.Select(r => new DocumentRecipientDto(IdParsing.ToApi(r.Id), r.RecipientName, r.RecipientEmail)).ToList(),
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
                        a.Actor?.DisplayName ?? "Unknown",
                        a.ActionType,
                        a.Comment,
                        a.ActionAtUtc)).ToList()))
                .ToList());

    private async Task NotifySubmittedAsync(int documentId, CancellationToken cancellationToken)
    {
        var document = await _db.Documents.AsNoTracking()
            .Where(d => d.Id == documentId)
            .Select(d => new { d.Id, d.Subject })
            .FirstOrDefaultAsync(cancellationToken);

        if (document is null)
        {
            return;
        }

        var activeStep = await _db.WorkflowSteps.AsNoTracking()
            .Where(s => s.DocumentId == documentId && s.Status == WorkflowStepStatus.Active)
            .OrderBy(s => s.StepOrder)
            .Select(s => new { s.Id, s.ApproverUserId, s.ApproverGroupId })
            .FirstOrDefaultAsync(cancellationToken);

        if (activeStep is null)
        {
            return;
        }

        var recipientIds = new List<int>();
        if (activeStep.ApproverUserId.HasValue)
        {
            recipientIds.Add(activeStep.ApproverUserId.Value);
        }
        else if (activeStep.ApproverGroupId.HasValue)
        {
            recipientIds.AddRange(await _db.ApproverGroupMembers.AsNoTracking()
                .Where(m => m.ApproverGroupId == activeStep.ApproverGroupId.Value)
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
                activeStep.Id,
                $"Approval required: {document.Subject}",
                "A document has been submitted and requires your approval."),
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
