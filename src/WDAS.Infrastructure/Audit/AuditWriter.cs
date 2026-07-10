using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using WDAS.Application.Abstractions;
using WDAS.Domain.Entities;
using WDAS.Domain.Services;

namespace WDAS.Infrastructure.Audit;

public class AuditWriter : IAuditWriter
{
    private readonly IApplicationDbContext _db;
    private readonly IClock _clock;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditWriter(IApplicationDbContext db, IClock clock, IHttpContextAccessor httpContextAccessor)
    {
        _db = db;
        _clock = clock;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task WriteAsync(AuditWriteRequest request, CancellationToken cancellationToken = default)
    {
        var last = await _db.AuditLogEntries
            .OrderByDescending(e => e.SequenceNumber)
            .FirstOrDefaultAsync(cancellationToken);

        var sequence = (last?.SequenceNumber ?? 0) + 1;
        var previousHash = last?.EntryHash ?? "GENESIS";
        var now = _clock.UtcNow;
        var http = _httpContextAccessor.HttpContext;

        var entry = new AuditLogEntry
        {
            SequenceNumber = sequence,
            PreviousHash = previousHash,
            EventType = request.EventType,
            ActorUserId = request.ActorUserId,
            ActorDisplayName = request.ActorDisplayName,
            ActorEmail = request.ActorEmail,
            DocumentId = request.DocumentId,
            EntityType = request.EntityType,
            EntityId = request.EntityId,
            Action = request.Action,
            DetailsJson = request.DetailsJson,
            IpAddress = http?.Connection.RemoteIpAddress?.ToString(),
            ClientType = http?.Request.Headers["X-Client-Type"].FirstOrDefault() ?? "Web",
            CreatedAtUtc = now
        };

        entry.EntryHash = Domain.Services.AuditChainVerifier.ComputeEntryHash(previousHash, entry);
        _db.Add(entry);

        if (_db is IUnitOfWork unitOfWork)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

}
