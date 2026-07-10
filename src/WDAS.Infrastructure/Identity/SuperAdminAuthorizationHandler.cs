using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using WDAS.Application;

namespace WDAS.Infrastructure.Identity;

/// <summary>
/// Super Admin bypasses all authorization requirements (full system access).
/// </summary>
public class SuperAdminAuthorizationHandler : IAuthorizationHandler
{
    public Task HandleAsync(AuthorizationHandlerContext context)
    {
        if (!IsSuperAdmin(context.User))
        {
            return Task.CompletedTask;
        }

        foreach (var requirement in context.PendingRequirements.ToList())
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }

    internal static bool IsSuperAdmin(ClaimsPrincipal user) =>
        user.Claims.Any(c =>
            c.Type is ClaimTypes.Role or "role" &&
            c.Value == RoleNames.SuperAdmin);
}
