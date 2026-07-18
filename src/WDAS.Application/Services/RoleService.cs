using Microsoft.EntityFrameworkCore;
using WDAS.Application;
using WDAS.Application.Abstractions;
using WDAS.Application.Models;
using WDAS.Domain;
using WDAS.Domain.Common;
using WDAS.Domain.Entities;
using WDAS.Domain.Exceptions;

namespace WDAS.Application.Services;

public class RoleService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;

    public RoleService(IApplicationDbContext db, ICurrentUserService currentUser, IClock clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public Task<IReadOnlyList<PermissionDefinitionDto>> GetPermissionCatalogAsync(CancellationToken cancellationToken = default)
    {
        EnsureCanManageRoles();
        IReadOnlyList<PermissionDefinitionDto> list = PermissionCatalog.Definitions
            .Select(d => new PermissionDefinitionDto(d.Key, d.Group, d.Label))
            .ToList();
        return Task.FromResult(list);
    }

    public async Task<IReadOnlyList<RoleSummaryDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        EnsureCanViewRoles();
        return await _db.SecurityRoles
            .AsNoTracking()
            .Where(r => r.IsActive)
            .OrderByDescending(r => r.IsSystem)
            .ThenBy(r => r.Name)
            .Select(r => new RoleSummaryDto(
                IdParsing.ToApi(r.Id),
                r.Name,
                r.Code,
                r.IsSystem,
                r.IsActive,
                r.Permissions.Count))
            .ToListAsync(cancellationToken);
    }

    public async Task<RoleDetailDto> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        EnsureCanManageRoles();
        var role = await _db.SecurityRoles
            .AsNoTracking()
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new DomainException("Role not found.");

        return MapDetail(role);
    }

    public async Task<RoleDetailDto> CreateAsync(CreateSecurityRoleRequest request, CancellationToken cancellationToken = default)
    {
        EnsureCanManageRoles();

        var name = request.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Role name is required.");
        }

        var code = string.IsNullOrWhiteSpace(request.Code)
            ? ToCode(name)
            : ToCode(request.Code);

        if (await _db.SecurityRoles.AnyAsync(r => r.Code == code, cancellationToken))
        {
            throw new ConflictException("role_code_taken", $"A role with code '{code}' already exists.");
        }

        if (await _db.SecurityRoles.AnyAsync(r => r.Name == name, cancellationToken))
        {
            throw new ConflictException("role_name_taken", $"A role named '{name}' already exists.");
        }

        var permissions = NormalizePermissions(request.Permissions);
        var now = _clock.UtcNow;
        var role = new SecurityRole
        {
            Name = name,
            Code = code,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            IsSystem = false,
            IsActive = true,
            CreatedAtUtc = now
        };

        foreach (var key in permissions)
        {
            role.Permissions.Add(new SecurityRolePermission
            {
                PermissionKey = key,
                CreatedAtUtc = now
            });
        }

        _db.Add(role);
        await SaveAsync(cancellationToken);
        return MapDetail(role);
    }

    public async Task<RoleDetailDto> UpdateAsync(int id, UpdateSecurityRoleRequest request, CancellationToken cancellationToken = default)
    {
        EnsureCanManageRoles();

        var role = await _db.SecurityRoles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new DomainException("Role not found.");

        var name = request.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Role name is required.");
        }

        if (await _db.SecurityRoles.AnyAsync(r => r.Id != id && r.Name == name, cancellationToken))
        {
            throw new ConflictException("role_name_taken", $"A role named '{name}' already exists.");
        }

        var permissions = NormalizePermissions(request.Permissions);
        if (role.IsSystem && role.Code == RoleNames.SuperAdmin && permissions.Count < PermissionCatalog.AllKeys.Count)
        {
            // Super Admin must retain full access — force-all.
            permissions = PermissionCatalog.AllKeys.ToList();
        }

        role.Name = role.IsSystem ? role.Name : name;
        role.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        if (!role.IsSystem)
        {
            role.IsActive = request.IsActive;
        }

        _db.RemoveRange(role.Permissions);
        role.Permissions.Clear();
        var now = _clock.UtcNow;
        foreach (var key in permissions)
        {
            role.Permissions.Add(new SecurityRolePermission
            {
                RoleId = role.Id,
                PermissionKey = key,
                CreatedAtUtc = now
            });
        }

        role.UpdatedAtUtc = now;
        await SaveAsync(cancellationToken);
        return MapDetail(role);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        EnsureCanManageRoles();

        var role = await _db.SecurityRoles
            .Include(r => r.Permissions)
            .Include(r => r.RoleMappings)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new DomainException("Role not found.");

        if (role.IsSystem)
        {
            throw new DomainException("System roles cannot be deleted.");
        }

        if (role.RoleMappings.Count > 0)
        {
            throw new DomainException("Remove this role from all users before deleting it.");
        }

        _db.RemoveRange(role.Permissions);
        _db.RemoveRange(new[] { role });
        await SaveAsync(cancellationToken);
    }

    private void EnsureCanViewRoles()
    {
        if (_currentUser.IsInRole(RoleNames.SuperAdmin) ||
            _currentUser.HasPermission(PermissionCatalog.Config.Roles) ||
            _currentUser.HasPermission(PermissionCatalog.Config.Users) ||
            _currentUser.HasPermission(PermissionCatalog.Config.RolesMake) ||
            _currentUser.HasPermission(PermissionCatalog.Config.RolesCheck) ||
            _currentUser.HasPermission(PermissionCatalog.Config.UsersMake) ||
            _currentUser.HasPermission(PermissionCatalog.Config.UsersCheck))
        {
            return;
        }

        throw new DomainException("You do not have permission to view roles.");
    }

    private void EnsureCanManageRoles()
    {
        if (_currentUser.IsInRole(RoleNames.SuperAdmin) ||
            _currentUser.HasPermission(PermissionCatalog.Config.Roles) ||
            _currentUser.HasPermission(PermissionCatalog.Config.RolesMake) ||
            _currentUser.HasPermission(PermissionCatalog.Config.RolesCheck))
        {
            return;
        }

        throw new DomainException("You do not have permission to manage roles.");
    }

    private static List<string> NormalizePermissions(IReadOnlyCollection<string>? permissions)
    {
        var keys = PermissionCatalog.ExpandImplied(permissions ?? Array.Empty<string>()).ToList();

        if (keys.Count == 0)
        {
            throw new DomainException("Select at least one permission.");
        }

        return keys;
    }

    private static string ToCode(string value)
    {
        var cleaned = new string(value.Trim()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '_')
            .ToArray());
        while (cleaned.Contains("__", StringComparison.Ordinal))
        {
            cleaned = cleaned.Replace("__", "_", StringComparison.Ordinal);
        }

        return cleaned.Trim('_');
    }

    private static RoleDetailDto MapDetail(SecurityRole role) =>
        new(
            IdParsing.ToApi(role.Id),
            role.Name,
            role.Code,
            role.Description,
            role.IsSystem,
            role.IsActive,
            role.Permissions.Select(p => p.PermissionKey).OrderBy(k => k).ToList());

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        if (_db is IUnitOfWork unitOfWork)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
