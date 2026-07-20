using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using WDAS.Application.Abstractions;
using WDAS.Application.Audit;
using WDAS.Domain.Entities;

namespace WDAS.Infrastructure.Audit;

public class AuditWriter : IAuditWriter
{
    private const int MaxAttempts = 8;

    private readonly IApplicationDbContext _db;
    private readonly IClock _clock;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<AuditWriter> _logger;

    public AuditWriter(
        IApplicationDbContext db,
        IClock clock,
        IHttpContextAccessor httpContextAccessor,
        ILogger<AuditWriter> logger)
    {
        _db = db;
        _clock = clock;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task WriteAsync(AuditWriteRequest request, CancellationToken cancellationToken = default)
    {
        var http = _httpContextAccessor.HttpContext;
        var ip = http?.Connection.RemoteIpAddress?.ToString();
        var clientType = http?.Request.Headers["X-Client-Type"].FirstOrDefault() ?? "Web";

        var actorDisplayName = request.ActorDisplayName;
        var actorEmail = request.ActorEmail;
        if (request.ActorUserId is int actorUserId &&
            string.IsNullOrWhiteSpace(actorDisplayName))
        {
            var actor = await _db.Users
                .AsNoTracking()
                .Where(u => u.Id == actorUserId)
                .Select(u => new { u.DisplayName, u.Email })
                .FirstOrDefaultAsync(cancellationToken);

            if (actor is not null)
            {
                actorDisplayName = actor.DisplayName;
                actorEmail ??= actor.Email;
            }
        }

        var detailsJson = AuditDetailsSanitizer.Sanitize(request.DetailsJson);

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            var last = await _db.AuditLogEntries
                .AsNoTracking()
                .OrderByDescending(e => e.SequenceNumber)
                .FirstOrDefaultAsync(cancellationToken);

            var sequence = (last?.SequenceNumber ?? 0) + 1;
            var previousHash = last?.EntryHash ?? "GENESIS";
            var now = _clock.UtcNow;

            var entry = new AuditLogEntry
            {
                SequenceNumber = sequence,
                PreviousHash = previousHash,
                EventType = request.EventType,
                ActorUserId = request.ActorUserId,
                ActorDisplayName = actorDisplayName,
                ActorEmail = actorEmail,
                DocumentId = request.DocumentId,
                EntityType = request.EntityType,
                EntityId = request.EntityId,
                Action = request.Action,
                DetailsJson = detailsJson,
                IpAddress = ip,
                ClientType = clientType,
                CreatedAtUtc = now
            };

            entry.EntryHash = Domain.Services.AuditChainVerifier.ComputeEntryHash(previousHash, entry);
            _db.Add(entry);

            try
            {
                if (_db is IUnitOfWork unitOfWork)
                {
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                }

                return;
            }
            catch (DbUpdateException ex) when (
                attempt < MaxAttempts &&
                ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg &&
                pg.ConstraintName?.Contains("SequenceNumber", StringComparison.OrdinalIgnoreCase) == true)
            {
                _logger.LogWarning(
                    "Audit sequence conflict on {Sequence} (attempt {Attempt}/{Max}). Retrying.",
                    sequence,
                    attempt,
                    MaxAttempts);
                DetachAddedAuditEntries();
            }
        }
    }

    private void DetachAddedAuditEntries()
    {
        if (_db is not DbContext ef)
        {
            return;
        }

        foreach (var entry in ef.ChangeTracker.Entries<AuditLogEntry>()
                     .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Unchanged)
                     .ToList())
        {
            if (entry.State == EntityState.Added)
            {
                entry.State = EntityState.Detached;
            }
        }
    }
}
