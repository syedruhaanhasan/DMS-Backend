using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using WDAS.Application;
using WDAS.Application.Abstractions;

namespace WDAS.Infrastructure.Identity;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
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

    private string? GetClaim(string type) => _httpContextAccessor.HttpContext?.User.FindFirst(type)?.Value;
}
