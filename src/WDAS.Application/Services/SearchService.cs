using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using WDAS.Application;
using WDAS.Application.Abstractions;
using WDAS.Application.Models;
using WDAS.Domain.Entities;
using WDAS.Domain.Enums;
using WDAS.Domain.Exceptions;

namespace WDAS.Application.Services;

public partial class SearchService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditWriter _auditWriter;

    public SearchService(IApplicationDbContext db, ICurrentUserService currentUser, IAuditWriter auditWriter)
    {
        _db = db;
        _currentUser = currentUser;
        _auditWriter = auditWriter;
    }

    public async Task<SearchResultDto> SearchAsync(SearchRequest request, CancellationToken cancellationToken = default)
    {
        if (request.RepositoryOnly)
        {
            return await SearchRepositoryAsync(request, cancellationToken);
        }

        var query = _db.DocumentSearchIndexes.AsNoTracking().AsQueryable();
        query = ApplyVisibilityScope(query);

        if (!string.IsNullOrWhiteSpace(request.Query))
        {
            var term = request.Query.Trim().ToLowerInvariant();
            query = query.Where(i => i.SearchableText.Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(request.ArchiveDocumentId))
        {
            query = query.Where(i => i.ArchiveDocumentId == request.ArchiveDocumentId);
        }

        var ownerUserId = IdParsing.ParseOptional(request.OwnerUserId);
        if (ownerUserId.HasValue)
        {
            query = query.Where(i => i.OwnerUserId == ownerUserId);
        }

        if (request.Status.HasValue)
        {
            var status = request.Status.Value.ToString();
            query = query.Where(i => i.Status == status);
        }

        if (request.MinAmount.HasValue)
        {
            query = query.Where(i => i.Amount >= request.MinAmount);
        }

        if (request.MaxAmount.HasValue)
        {
            query = query.Where(i => i.Amount <= request.MaxAmount);
        }

        if (request.FromUtc.HasValue)
        {
            query = query.Where(i => i.SubmittedAtUtc >= request.FromUtc);
        }

        if (request.ToUtc.HasValue)
        {
            query = query.Where(i => i.SubmittedAtUtc <= request.ToUtc);
        }

        var approverUserId = IdParsing.ParseOptional(request.ApproverUserId);
        if (approverUserId.HasValue)
        {
            var approverId = approverUserId.Value;
            var inApprovalStatus = DocumentStatus.InApproval.ToString();
            var docIds = _db.WorkflowSteps
                .Where(s => s.ApproverUserId == approverId &&
                            s.Status == WorkflowStepStatus.Active &&
                            s.Document.Status == DocumentStatus.InApproval)
                .Select(s => s.DocumentId)
                .Distinct();
            query = query.Where(i => docIds.Contains(i.DocumentId) && i.Status == inApprovalStatus);
        }

        var items = await query
            .OrderByDescending(i => i.SubmittedAtUtc ?? i.IndexedAtUtc)
            .Skip(request.Skip)
            .Take(Math.Min(request.Take, 100))
            .ToListAsync(cancellationToken);

        var total = items.Count < request.Take ? request.Skip + items.Count : -1;

        await _auditWriter.WriteAsync(new(
            Domain.Enums.AuditEventType.Search,
            "Document search executed",
            DetailsJson: System.Text.Json.JsonSerializer.Serialize(new { request.Query, total })),
            cancellationToken);

        return new SearchResultDto(
            total,
            items.Select(i => new SearchResultItemDto(
                IdParsing.ToApi(i.DocumentId),
                i.RecordNumber,
                i.ArchiveDocumentId,
                i.Subject,
                i.OwnerDisplayName,
                IdParsing.ToApi(i.OwnerUserId),
                i.Status,
                i.Amount,
                i.SubmittedAtUtc,
                BuildSnippet(i, request.Query))).ToList());
    }

    private IQueryable<DocumentSearchIndex> ApplyVisibilityScope(IQueryable<DocumentSearchIndex> query)
    {
        if (_currentUser.IsInRole(RoleNames.SuperAdmin) || _currentUser.IsInRole(RoleNames.Auditor))
        {
            return query;
        }

        if (_currentUser.IsInRole(RoleNames.DepartmentAdmin) && _currentUser.DepartmentId.HasValue)
        {
            var deptId = _currentUser.DepartmentId.Value;
            return query.Where(i => i.DepartmentId == deptId || i.OwnerUserId == _currentUser.UserId);
        }

        return query.Where(i => i.OwnerUserId == _currentUser.UserId);
    }

    private async Task<SearchResultDto> SearchRepositoryAsync(SearchRequest request, CancellationToken cancellationToken)
    {
        var query = _db.Documents
            .AsNoTracking()
            .Include(d => d.Owner)
            .Where(d =>
                d.Status == DocumentStatus.Finalized ||
                d.Status == DocumentStatus.ReadyForFinalization ||
                d.Status == DocumentStatus.Rejected ||
                d.Status == DocumentStatus.Cancelled);

        if (!string.IsNullOrWhiteSpace(request.Query))
        {
            var term = request.Query.Trim().ToLowerInvariant();
            query = query.Where(d =>
                d.Subject.ToLower().Contains(term) ||
                d.BodyHtml.ToLower().Contains(term) ||
                d.Owner.DisplayName.ToLower().Contains(term) ||
                (d.ArchiveDocumentId != null && d.ArchiveDocumentId.ToLower().Contains(term)) ||
                d.RecordNumber.ToString().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(request.ArchiveDocumentId))
        {
            query = query.Where(d => d.ArchiveDocumentId == request.ArchiveDocumentId);
        }

        var ownerUserId = IdParsing.ParseOptional(request.OwnerUserId);
        if (ownerUserId.HasValue)
        {
            query = query.Where(d => d.OwnerUserId == ownerUserId);
        }

        if (request.Status.HasValue)
        {
            query = query.Where(d => d.Status == request.Status.Value);
        }

        if (request.MinAmount.HasValue)
        {
            query = query.Where(d => d.Amount >= request.MinAmount);
        }

        if (request.MaxAmount.HasValue)
        {
            query = query.Where(d => d.Amount <= request.MaxAmount);
        }

        if (request.FromUtc.HasValue)
        {
            query = query.Where(d => d.SubmittedAtUtc >= request.FromUtc);
        }

        if (request.ToUtc.HasValue)
        {
            query = query.Where(d => d.SubmittedAtUtc <= request.ToUtc);
        }

        var approverUserId = IdParsing.ParseOptional(request.ApproverUserId);
        if (approverUserId.HasValue)
        {
            var approverId = approverUserId.Value;
            var docIds = _db.WorkflowSteps
                .Where(s => s.ApproverUserId == approverId)
                .Select(s => s.DocumentId)
                .Distinct();
            query = query.Where(d => docIds.Contains(d.Id));
        }

        var items = await query
            .OrderByDescending(d => d.SubmittedAtUtc ?? d.UpdatedAtUtc ?? d.CreatedAtUtc)
            .Skip(request.Skip)
            .Take(Math.Min(request.Take, 100))
            .ToListAsync(cancellationToken);

        var total = items.Count < request.Take ? request.Skip + items.Count : -1;

        await _auditWriter.WriteAsync(new(
            Domain.Enums.AuditEventType.Search,
            "Repository search executed",
            DetailsJson: System.Text.Json.JsonSerializer.Serialize(new { request.Query, total, request.RepositoryOnly })),
            cancellationToken);

        return new SearchResultDto(
            total,
            items.Select(d => new SearchResultItemDto(
                IdParsing.ToApi(d.Id),
                d.RecordNumber,
                d.ArchiveDocumentId,
                d.Subject,
                d.Owner.DisplayName,
                IdParsing.ToApi(d.OwnerUserId),
                d.Status.ToString(),
                d.Amount,
                d.SubmittedAtUtc,
                d.Subject)).ToList());
    }

    private static string BuildSnippet(DocumentSearchIndex index, string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return index.Subject;
        }

        var text = index.SearchableText;
        var idx = text.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
        {
            return index.Subject;
        }

        var start = Math.Max(0, idx - 40);
        var length = Math.Min(120, text.Length - start);
        return text.Substring(start, length);
    }
}

public partial class DocumentSearchIndexer : IDocumentSearchIndexer
{
    private readonly IApplicationDbContext _db;
    private readonly IClock _clock;

    public DocumentSearchIndexer(IApplicationDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task IndexDocumentAsync(int documentId, CancellationToken cancellationToken = default)
    {
        var document = await _db.Documents
            .Include(d => d.Owner)
            .FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken);

        if (document is null)
        {
            return;
        }

        var bodyText = HtmlStripRegex().Replace(document.BodyHtml, " ");
        var searchable = $"{document.RecordNumber} {document.Subject} {bodyText} {document.Owner.DisplayName} {document.ArchiveDocumentId}".ToLowerInvariant();

        var existing = await _db.DocumentSearchIndexes.FirstOrDefaultAsync(i => i.DocumentId == documentId, cancellationToken);
        if (existing is null)
        {
            _db.Add(new DocumentSearchIndex
            {
                DocumentId = document.Id,
                RecordNumber = document.RecordNumber,
                ArchiveDocumentId = document.ArchiveDocumentId,
                Subject = document.Subject,
                BodyText = bodyText,
                OwnerDisplayName = document.Owner.DisplayName,
                OwnerUserId = document.OwnerUserId,
                DepartmentId = document.DepartmentId,
                Status = document.Status.ToString(),
                Amount = document.Amount,
                SubmittedAtUtc = document.SubmittedAtUtc,
                FinalizedAtUtc = document.FinalizedAtUtc,
                SearchableText = searchable,
                IndexedAtUtc = _clock.UtcNow
            });
        }
        else
        {
            existing.RecordNumber = document.RecordNumber;
            existing.ArchiveDocumentId = document.ArchiveDocumentId;
            existing.Subject = document.Subject;
            existing.BodyText = bodyText;
            existing.OwnerDisplayName = document.Owner.DisplayName;
            existing.Status = document.Status.ToString();
            existing.Amount = document.Amount;
            existing.SubmittedAtUtc = document.SubmittedAtUtc;
            existing.FinalizedAtUtc = document.FinalizedAtUtc;
            existing.SearchableText = searchable;
            existing.IndexedAtUtc = _clock.UtcNow;
        }

        if (_db is IUnitOfWork unitOfWork)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex HtmlStripRegex();
}
