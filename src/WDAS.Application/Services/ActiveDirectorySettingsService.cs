using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WDAS.Application;
using WDAS.Application.Abstractions;
using WDAS.Application.Audit;
using WDAS.Application.Models;
using WDAS.Domain.Entities;
using WDAS.Domain.Enums;
using WDAS.Domain.Exceptions;

namespace WDAS.Application.Services;

public class ActiveDirectorySettingsService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;
    private readonly IAuditWriter _auditWriter;

    public ActiveDirectorySettingsService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IClock clock,
        IAuditWriter auditWriter)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _auditWriter = auditWriter;
    }

    public async Task<ActiveDirectorySettingsDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _db.ActiveDirectorySettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        return settings is null
            ? new ActiveDirectorySettingsDto(false, string.Empty, 389, false, null)
            : Map(settings);
    }

    /// <summary>Enabled flag only — safe for any authenticated caller (used by login gating / user creation UI).</summary>
    public async Task<ActiveDirectoryStatusDto> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var enabled = await _db.ActiveDirectorySettings
            .AsNoTracking()
            .Select(s => s.Enabled)
            .FirstOrDefaultAsync(cancellationToken);
        return new ActiveDirectoryStatusDto(enabled);
    }

    public async Task<ActiveDirectorySettingsDto> UpdateAsync(
        UpdateActiveDirectorySettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureCanManage();

        var now = _clock.UtcNow;
        var settings = await _db.ActiveDirectorySettings.FirstOrDefaultAsync(cancellationToken);
        var oldEnabled = settings?.Enabled ?? false;
        var oldDomainName = settings?.DomainName ?? string.Empty;
        var oldPort = settings?.Port ?? 389;
        var oldUseSsl = settings?.UseSsl ?? false;

        if (settings is null)
        {
            settings = new ActiveDirectorySetting { CreatedAtUtc = now, Port = 389 };
            _db.Add(settings);
        }

        settings.Enabled = request.Enabled;

        if (request.DomainName is not null)
        {
            settings.DomainName = request.DomainName.Trim();
        }

        if (request.Port.HasValue)
        {
            if (request.Port.Value < 1 || request.Port.Value > 65535)
            {
                throw new DomainException("Port must be a number between 1 and 65535.");
            }
            settings.Port = request.Port.Value;
        }

        if (request.UseSsl.HasValue)
        {
            settings.UseSsl = request.UseSsl.Value;
        }

        if (settings.Enabled && string.IsNullOrWhiteSpace(settings.DomainName))
        {
            throw new DomainException("Domain name is required when Active Directory is enabled.");
        }

        if (settings.Port is < 1 or > 65535)
        {
            settings.Port = 389;
        }

        settings.UpdatedAtUtc = now;
        await SaveAsync(cancellationToken);

        await _auditWriter.WriteAsync(new AuditWriteRequest(
            AuditEventType.Update,
            "Active Directory settings updated",
            ActorUserId: _currentUser.UserId,
            EntityType: "ActiveDirectorySettings",
            DetailsJson: AuditDetailsBuilder.Create()
                .Track("Enabled", oldEnabled, settings.Enabled)
                .Track("Domain name", oldDomainName, settings.DomainName)
                .Track("Port", oldPort, settings.Port)
                .Track("Use SSL", oldUseSsl, settings.UseSsl)
                .ToJson()),
            cancellationToken);

        return Map(settings);
    }

    private void EnsureCanManage()
    {
        if (!_currentUser.IsInRole(RoleNames.SuperAdmin) &&
            !_currentUser.HasPermission(PermissionCatalog.Config.ActiveDirectory) &&
            !_currentUser.HasPermission(PermissionCatalog.Config.ActiveDirectoryMake) &&
            !_currentUser.HasPermission(PermissionCatalog.Config.ActiveDirectoryCheck))
        {
            throw new DomainException("You do not have permission to manage Active Directory settings.");
        }
    }

    private static ActiveDirectorySettingsDto Map(ActiveDirectorySetting settings) =>
        new(settings.Enabled, settings.DomainName, settings.Port, settings.UseSsl, settings.UpdatedAtUtc);

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        if (_db is IUnitOfWork unitOfWork)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
