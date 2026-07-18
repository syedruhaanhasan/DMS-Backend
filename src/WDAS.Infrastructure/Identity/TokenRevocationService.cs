using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WDAS.Application.Abstractions;
using WDAS.Domain.Entities;
using WDAS.Infrastructure.Persistence;

namespace WDAS.Infrastructure.Identity;

/// <summary>
/// DB-backed denylist of revoked JWT ids with an in-memory cache so the check adds
/// no per-request database round-trip. Revocations survive restarts (loaded lazily
/// from the database on first use) and expired entries are pruned opportunistically.
/// Registered as a singleton, so it uses a scope factory for database access.
/// </summary>
public class TokenRevocationService : ITokenRevocationService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ConcurrentDictionary<string, DateTime> _revoked = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private volatile bool _loaded;

    public TokenRevocationService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task<bool> IsRevokedAsync(string jti, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(jti))
        {
            return false;
        }

        await EnsureLoadedAsync(cancellationToken);

        if (_revoked.TryGetValue(jti, out var expiresAt))
        {
            if (expiresAt > DateTime.UtcNow)
            {
                return true;
            }

            // Past its expiry — no longer meaningful, drop it.
            _revoked.TryRemove(jti, out _);
        }

        return false;
    }

    public async Task RevokeAsync(string jti, DateTime expiresAtUtc, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(jti) || expiresAtUtc <= DateTime.UtcNow)
        {
            return;
        }

        await EnsureLoadedAsync(cancellationToken);

        // Apply in-memory first so this instance rejects the token immediately.
        _revoked[jti] = expiresAtUtc;
        Prune();

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<WdasDbContext>();
            if (db.Database.IsRelational() &&
                !await db.RevokedTokens.AnyAsync(t => t.Jti == jti, cancellationToken))
            {
                db.RevokedTokens.Add(new RevokedToken
                {
                    Jti = jti,
                    ExpiresAtUtc = expiresAtUtc,
                    CreatedAtUtc = DateTime.UtcNow
                });
                await db.SaveChangesAsync(cancellationToken);
            }
        }
        catch
        {
            // Persistence failed — the in-memory revocation still protects this instance.
        }
    }

    private async Task EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        if (_loaded)
        {
            return;
        }

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (_loaded)
            {
                return;
            }

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<WdasDbContext>();
                if (db.Database.IsRelational())
                {
                    var now = DateTime.UtcNow;
                    var active = await db.RevokedTokens
                        .Where(t => t.ExpiresAtUtc > now)
                        .Select(t => new { t.Jti, t.ExpiresAtUtc })
                        .ToListAsync(cancellationToken);

                    foreach (var entry in active)
                    {
                        _revoked[entry.Jti] = entry.ExpiresAtUtc;
                    }
                }
            }
            catch
            {
                // If the denylist can't be loaded, fail open (start empty) rather than
                // blocking all authentication; new revocations are still recorded.
            }

            _loaded = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private void Prune()
    {
        var now = DateTime.UtcNow;
        foreach (var pair in _revoked)
        {
            if (pair.Value <= now)
            {
                _revoked.TryRemove(pair.Key, out _);
            }
        }
    }
}
