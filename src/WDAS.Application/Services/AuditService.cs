using Microsoft.EntityFrameworkCore;
using WDAS.Application;
using WDAS.Application.Abstractions;
using WDAS.Application.Models;
using WDAS.Domain.Exceptions;
using WDAS.Domain.Services;

namespace WDAS.Application.Services;

public class AuditService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditWriter _auditWriter;

    public AuditService(IApplicationDbContext db, ICurrentUserService currentUser, IAuditWriter auditWriter)
    {
        _db = db;
        _currentUser = currentUser;
        _auditWriter = auditWriter;
    }

    public async Task<AuditExportResult> ExportAsync(AuditExportRequest request, CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsInRole(RoleNames.SuperAdmin) && !_currentUser.IsInRole(RoleNames.Auditor))
        {
            throw new DomainException("Audit export requires Auditor or Super Admin role.");
        }

        var query = _db.AuditLogEntries.AsNoTracking().AsQueryable();

        var documentId = IdParsing.ParseOptional(request.DocumentId);
        if (documentId.HasValue)
        {
            query = query.Where(e => e.DocumentId == documentId);
        }

        if (request.FromUtc.HasValue)
        {
            query = query.Where(e => e.CreatedAtUtc >= request.FromUtc);
        }

        if (request.ToUtc.HasValue)
        {
            query = query.Where(e => e.CreatedAtUtc <= request.ToUtc);
        }

        var departmentId = IdParsing.ParseOptional(request.DepartmentId);
        if (departmentId.HasValue)
        {
            var docIds = _db.Documents
                .Where(d => d.DepartmentId == departmentId)
                .Select(d => d.Id);
            query = query.Where(e => e.DocumentId.HasValue && docIds.Contains(e.DocumentId.Value));
        }

        var entries = await query.OrderBy(e => e.SequenceNumber).Take(5000).ToListAsync(cancellationToken);
        var (valid, message) = AuditChainVerifier.Verify(entries);

        await _auditWriter.WriteAsync(new AuditWriteRequest(
            Domain.Enums.AuditEventType.Export,
            "Audit trail exported",
            documentId,
            DetailsJson: System.Text.Json.JsonSerializer.Serialize(new { request.DepartmentId, Count = entries.Count })),
            cancellationToken);

        return new AuditExportResult(
            entries.Select(e => new AuditLogEntryDto(
                e.SequenceNumber,
                e.EventType.ToString(),
                e.Action,
                e.ActorUserId is int actorId ? IdParsing.ToApi(actorId) : null,
                e.ActorDisplayName,
                e.DocumentId is int docId ? IdParsing.ToApi(docId) : null,
                e.EntityType,
                e.EntityId,
                e.DetailsJson,
                e.IpAddress,
                e.CreatedAtUtc,
                e.EntryHash)).ToList(),
            valid,
            message);
    }
}
