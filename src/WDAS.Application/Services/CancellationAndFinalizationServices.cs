using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WDAS.Application.Abstractions;
using WDAS.Application.Models;
using WDAS.Domain.Entities;
using WDAS.Domain.Enums;
using WDAS.Domain.Exceptions;

namespace WDAS.Application.Services;

public class CancellationService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;
    private readonly INotificationDispatcher _notifications;

    public CancellationService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IClock clock,
        INotificationDispatcher notifications)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _notifications = notifications;
    }

    public async Task<DocumentDto> CancelAsync(Guid documentId, CancelDocumentRequest request, CancellationToken cancellationToken = default)
    {
        var document = await _db.Documents
            .Include(d => d.Owner)
            .Include(d => d.WorkflowSteps)
                .ThenInclude(s => s.Actions)
                    .ThenInclude(a => a.Actor)
            .Include(d => d.Recipients)
            .FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken)
            ?? throw new DomainException("Document not found.");

        if (document.OwnerUserId != _currentUser.UserId)
        {
            throw new DomainException("Only the document owner can cancel.");
        }

        if (document.Status is DocumentStatus.Finalized or DocumentStatus.Cancelled)
        {
            throw new DomainException("Document cannot be cancelled in its current state.");
        }

        var now = _clock.UtcNow;
        document.Status = DocumentStatus.Cancelled;
        document.CancellationReason = request.Reason;
        document.CancelledAtUtc = now;
        document.UpdatedAtUtc = now;

        foreach (var step in document.WorkflowSteps.Where(s => s.Status is WorkflowStepStatus.Active or WorkflowStepStatus.Pending))
        {
            step.Status = WorkflowStepStatus.Skipped;
            step.UpdatedAtUtc = now;
        }

        if (document.WorkflowSteps.Count > 0)
        {
            _db.Add(new WorkflowStepAction
            {
                WorkflowStepId = document.WorkflowSteps.OrderBy(s => s.StepOrder).First().Id,
                ActorUserId = _currentUser.UserId,
                ActionType = WorkflowActionType.Cancel,
                Comment = request.Reason,
                ActionAtUtc = now,
                CreatedAtUtc = now
            });
        }

        await SaveAsync(cancellationToken);

        var notifiedUserIds = document.WorkflowSteps
            .SelectMany(s => s.Actions)
            .Select(a => a.ActorUserId)
            .Distinct()
            .ToList();

        foreach (var userId in notifiedUserIds)
        {
            await _notifications.DispatchAsync(new NotificationRequest(
                NotificationEventType.Cancelled,
                userId,
                null,
                document.Id,
                null,
                $"Document cancelled: {document.Subject}",
                request.Reason ?? "The document owner cancelled this workflow."),
                cancellationToken);
        }

        return MapDocument(document);
    }

    private static DocumentDto MapDocument(Document document) =>
        new(
            document.Id,
            document.RecordNumber,
            document.RevisionNumber < 1 ? 1 : document.RevisionNumber,
            document.OwnerUserId,
            document.Owner.DisplayName,
            document.DepartmentId,
            document.WorkflowId,
            document.WorkflowVersionId,
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
            ParseAdHocIds(document.AdHocApproverUserIdsJson),
            document.Recipients.Select(r => new DocumentRecipientDto(r.Id, r.RecipientName, r.RecipientEmail)).ToList(),
            document.WorkflowSteps.OrderBy(s => s.StepOrder).Select(s => new WorkflowStepDto(
                s.Id, s.StepOrder, s.ApproverUserId, s.ApproverUser?.DisplayName, s.GroupName,
                s.Status, s.ActivatedAtUtc, s.CompletedAtUtc, s.SlaDueAtUtc, s.IsSlaBreached,
                s.Actions.OrderBy(a => a.ActionAtUtc).Select(a => new WorkflowStepActionDto(
                    a.Id, a.ActorUserId, a.Actor?.DisplayName ?? "Unknown", a.ActionType, a.Comment, a.ActionAtUtc)).ToList())).ToList());

    private static List<Guid>? ParseAdHocIds(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return JsonSerializer.Deserialize<List<Guid>>(json);
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        if (_db is IUnitOfWork unitOfWork)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}

public class FinalizationService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;
    private readonly IFileStorage _fileStorage;
    private readonly INotificationDispatcher _notifications;
    private readonly IDocumentSearchIndexer _searchIndexer;
    private readonly IAuditWriter _auditWriter;
    private readonly IArchivePdfGenerator _pdfGenerator;

    public FinalizationService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IClock clock,
        IFileStorage fileStorage,
        INotificationDispatcher notifications,
        IDocumentSearchIndexer searchIndexer,
        IAuditWriter auditWriter,
        IArchivePdfGenerator pdfGenerator)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _fileStorage = fileStorage;
        _notifications = notifications;
        _searchIndexer = searchIndexer;
        _auditWriter = auditWriter;
        _pdfGenerator = pdfGenerator;
    }

    public async Task<RepositoryDocumentDto> FinalizeAsync(Guid documentId, FinalizeDocumentRequest request, CancellationToken cancellationToken = default)
    {
        var document = await _db.Documents
            .Include(d => d.Owner)
            .Include(d => d.Workflow)
            .Include(d => d.WorkflowSteps)
                .ThenInclude(s => s.ApproverUser)
            .Include(d => d.WorkflowSteps)
                .ThenInclude(s => s.Actions)
                    .ThenInclude(a => a.Actor)
            .Include(d => d.Attachments.Where(a => a.IsLatest))
            .FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken)
            ?? throw new DomainException("Document not found.");

        if (document.Status != DocumentStatus.ReadyForFinalization)
        {
            throw new DomainException("Document is not ready for finalization.");
        }

        if (document.OwnerUserId != _currentUser.UserId && !_currentUser.IsInRole(RoleNames.SuperAdmin))
        {
            throw new DomainException("Only the document owner or a super admin can finalize.");
        }

        var existing = await _db.RepositoryDocuments.AnyAsync(r => r.SourceDocumentId == documentId, cancellationToken);
        if (existing)
        {
            throw new DomainException("Document has already been finalized.");
        }

        var now = _clock.UtcNow;
        var archiveId = await GenerateArchiveIdAsync(document.DepartmentId, cancellationToken);
        var trail = document.WorkflowSteps
            .OrderBy(s => s.StepOrder)
            .Select(s => new
            {
                s.StepOrder,
                Approver = s.ApproverUser?.DisplayName,
                s.Status,
                Actions = s.Actions.Select(a => new
                {
                    Actor = a.Actor.DisplayName,
                    a.OnBehalfOfUserId,
                    a.ActionType,
                    a.Comment,
                    a.ActionAtUtc
                })
            });

        var trailJson = JsonSerializer.Serialize(trail);
        var htmlKey = $"repository/{archiveId}.html";
        var pdfKey = $"repository/{archiveId}.pdf";
        var archiveHtml = BuildArchiveHtml(document, archiveId, trailJson);
        await using (var archiveStream = new MemoryStream(Encoding.UTF8.GetBytes(archiveHtml)))
        {
            await _fileStorage.SaveAsync(archiveStream, htmlKey, cancellationToken);
        }

        var pdfBytes = _pdfGenerator.Generate(
            archiveId,
            document.Subject,
            document.BodyHtml,
            trailJson,
            document.Owner.DisplayName,
            now);
        await using (var pdfStream = new MemoryStream(pdfBytes))
        {
            await _fileStorage.SaveAsync(pdfStream, pdfKey, cancellationToken);
        }

        var repository = new RepositoryDocument
        {
            SourceDocumentId = document.Id,
            ArchiveDocumentId = archiveId,
            FinalizedByUserId = _currentUser.UserId,
            FinalizedAtUtc = now,
            Subject = document.Subject,
            BodyHtmlSnapshot = document.BodyHtml,
            ApprovalTrailJson = trailJson,
            ArchivePdfStorageKey = pdfKey,
            IsImmutable = true,
            CreatedAtUtc = now
        };

        document.Status = DocumentStatus.Finalized;
        document.ArchiveDocumentId = archiveId;
        document.FinalizedAtUtc = now;
        document.IsBodyLocked = true;
        document.UpdatedAtUtc = now;

        _db.Add(repository);

        var finalizeStep = document.WorkflowSteps.OrderByDescending(s => s.StepOrder).First();
        _db.Add(new WorkflowStepAction
        {
            WorkflowStepId = finalizeStep.Id,
            ActorUserId = _currentUser.UserId,
            ActionType = WorkflowActionType.Finalize,
            Comment = request.Comment,
            ActionAtUtc = now,
            CreatedAtUtc = now
        });

        await SaveAsync(cancellationToken);

        await _notifications.DispatchAsync(new NotificationRequest(
            NotificationEventType.Finalized,
            document.OwnerUserId,
            document.Owner.Email,
            document.Id,
            null,
            $"Document finalized: {document.Subject}",
            $"Archive ID: {archiveId}"),
            cancellationToken);

        await _searchIndexer.IndexDocumentAsync(document.Id, cancellationToken);

        await _auditWriter.WriteAsync(new AuditWriteRequest(
            Domain.Enums.AuditEventType.Finalize,
            "Document finalized",
            document.Id,
            EntityType: nameof(RepositoryDocument),
            EntityId: archiveId,
            ActorUserId: _currentUser.UserId),
            cancellationToken);

        var finalizedBy = await _db.Users.FirstAsync(u => u.Id == _currentUser.UserId, cancellationToken);
        return new RepositoryDocumentDto(
            repository.Id,
            repository.ArchiveDocumentId,
            repository.SourceDocumentId,
            repository.Subject,
            repository.BodyHtmlSnapshot,
            repository.ApprovalTrailJson,
            repository.FinalizedAtUtc,
            finalizedBy.DisplayName,
            repository.ArchivePdfStorageKey is not null);
    }

    public async Task<RepositoryDocumentDto> GetRepositoryDocumentAsync(string archiveDocumentId, CancellationToken cancellationToken = default)
    {
        var repository = await _db.RepositoryDocuments
            .Include(r => r.FinalizedBy)
            .FirstOrDefaultAsync(r => r.ArchiveDocumentId == archiveDocumentId, cancellationToken)
            ?? throw new DomainException("Repository document not found.");

        if (!_currentUser.IsInRole(RoleNames.SuperAdmin) &&
            !_currentUser.IsInRole(RoleNames.Auditor) &&
            !_currentUser.IsInRole(RoleNames.DepartmentAdmin))
        {
            var source = await _db.Documents.FirstAsync(d => d.Id == repository.SourceDocumentId, cancellationToken);
            if (source.OwnerUserId != _currentUser.UserId)
            {
                throw new DomainException("You are not authorized to view this repository record.");
            }
        }

        return new RepositoryDocumentDto(
            repository.Id,
            repository.ArchiveDocumentId,
            repository.SourceDocumentId,
            repository.Subject,
            repository.BodyHtmlSnapshot,
            repository.ApprovalTrailJson,
            repository.FinalizedAtUtc,
            repository.FinalizedBy.DisplayName,
            repository.ArchivePdfStorageKey is not null);
    }

    public async Task<(Stream Content, string ContentType, string FileName)> DownloadArchiveAsync(
        string archiveDocumentId,
        string format,
        CancellationToken cancellationToken = default)
    {
        var repository = await _db.RepositoryDocuments
            .Include(r => r.FinalizedBy)
            .Include(r => r.SourceDocument)
                .ThenInclude(d => d.Owner)
            .FirstOrDefaultAsync(r => r.ArchiveDocumentId == archiveDocumentId, cancellationToken)
            ?? throw new DomainException("Repository document not found.");

        await EnsureCanViewRepositoryAsync(repository, cancellationToken);

        format = format.Equals("html", StringComparison.OrdinalIgnoreCase) ? "html" : "pdf";
        var pdfKey = repository.ArchivePdfStorageKey ?? $"repository/{archiveDocumentId}.pdf";
        var htmlKey = $"repository/{archiveDocumentId}.html";

        if (format == "pdf")
        {
            if (!await _fileStorage.ExistsAsync(pdfKey, cancellationToken))
            {
                var pdfBytes = _pdfGenerator.Generate(
                    repository.ArchiveDocumentId,
                    repository.Subject,
                    repository.BodyHtmlSnapshot,
                    repository.ApprovalTrailJson,
                    repository.SourceDocument.Owner.DisplayName,
                    repository.FinalizedAtUtc);
                await using var pdfStream = new MemoryStream(pdfBytes);
                await _fileStorage.SaveAsync(pdfStream, pdfKey, cancellationToken);
                repository.ArchivePdfStorageKey = pdfKey;
                await SaveAsync(cancellationToken);
            }

            var stream = await _fileStorage.OpenReadAsync(pdfKey, cancellationToken);
            return (stream, "application/pdf", $"{archiveDocumentId}.pdf");
        }

        if (await _fileStorage.ExistsAsync(htmlKey, cancellationToken))
        {
            var htmlStream = await _fileStorage.OpenReadAsync(htmlKey, cancellationToken);
            return (htmlStream, "text/html", $"{archiveDocumentId}.html");
        }

        var html = BuildArchiveHtml(repository.SourceDocument, archiveDocumentId, repository.ApprovalTrailJson);
        return (new MemoryStream(Encoding.UTF8.GetBytes(html)), "text/html", $"{archiveDocumentId}.html");
    }

    private async Task EnsureCanViewRepositoryAsync(RepositoryDocument repository, CancellationToken cancellationToken)
    {
        if (_currentUser.IsInRole(RoleNames.SuperAdmin) ||
            _currentUser.IsInRole(RoleNames.Auditor) ||
            _currentUser.IsInRole(RoleNames.DepartmentAdmin))
        {
            return;
        }

        var source = repository.SourceDocument
            ?? await _db.Documents.FirstAsync(d => d.Id == repository.SourceDocumentId, cancellationToken);

        if (source.OwnerUserId != _currentUser.UserId)
        {
            throw new DomainException("You are not authorized to download this archive.");
        }
    }

    private async Task<string> GenerateArchiveIdAsync(Guid departmentId, CancellationToken cancellationToken)
    {
        var deptCode = await _db.Departments.Where(d => d.Id == departmentId).Select(d => d.Code).FirstAsync(cancellationToken);
        var year = _clock.UtcNow.Year;
        var count = await _db.RepositoryDocuments.CountAsync(r => r.ArchiveDocumentId.StartsWith($"{deptCode}-{year}-"), cancellationToken);
        return $"{deptCode}-{year}-{(count + 1):D5}";
    }

    private static string BuildArchiveHtml(Document document, string archiveId, string trailJson) =>
        $"""
        <!DOCTYPE html>
        <html><head><title>{archiveId}</title></head>
        <body>
        <h1>{archiveId}</h1>
        <h2>{document.Subject}</h2>
        <div>{document.BodyHtml}</div>
        <h3>Approval Trail</h3>
        <pre>{trailJson}</pre>
        </body></html>
        """;

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        if (_db is IUnitOfWork unitOfWork)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
