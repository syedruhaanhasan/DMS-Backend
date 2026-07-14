using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using WDAS.Application;
using WDAS.Application.Abstractions;
using WDAS.Infrastructure.Persistence;

namespace WDAS.Infrastructure.Identity;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly WdasDbContext _db;
    private HashSet<string>? _permissions;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor, WdasDbContext db)
    {
        _httpContextAccessor = httpContextAccessor;
        _db = db;
    }

    public Guid UserId => Guid.Parse(GetClaim(ClaimTypes.NameIdentifier) ?? GetClaim("sub") ?? throw new InvalidOperationException("User is not authenticated."));

    public string AdObjectId => GetClaim("ad_oid") ?? string.Empty;

    public IReadOnlyCollection<string> Roles
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user is null)
            {
                return [];
            }

            return user.Claims
                .Where(c => c.Type is ClaimTypes.Role or "role")
                .Select(c => c.Value)
                .Distinct()
                .ToList();
        }
    }

    public Guid? DepartmentId
    {
        get
        {
            var value = GetClaim("department_id");
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    public bool IsInRole(string role)
    {
        if (Roles.Contains(RoleNames.SuperAdmin))
        {
            return true;
        }

        return Roles.Contains(role);
    }

    public bool HasPermission(string permission)
    {
        if (string.IsNullOrWhiteSpace(permission))
        {
            return false;
        }

        if (IsInRole(RoleNames.SuperAdmin))
        {
            return true;
        }

        _permissions ??= LoadPermissions();
        return _permissions.Contains(permission);
    }

    private HashSet<string> LoadPermissions()
    {
        try
        {
            var userId = UserId;
            var keys = _db.RoleMappings
                .AsNoTracking()
                .Where(m => m.UserId == userId && m.Role.IsActive)
                .SelectMany(m => m.Role.Permissions.Select(p => p.PermissionKey))
                .Distinct()
                .ToList();

            return PermissionCatalog.ExpandImplied(keys).ToHashSet(StringComparer.Ordinal);
        }
        catch
        {
            return [];
        }
    }

    private string? GetClaim(string type) => _httpContextAccessor.HttpContext?.User.FindFirst(type)?.Value;
}
