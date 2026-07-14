using Microsoft.AspNetCore.Authorization;
using WDAS.Application.Abstractions;

namespace WDAS.Infrastructure.Identity;

public class PermissionRequirement : IAuthorizationRequirement
{
    public PermissionRequirement(params string[] permissions)
    {
        Permissions = permissions.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct().ToArray();
    }

    /// <summary>Succeeds if the user has any of these permissions.</summary>
    public IReadOnlyList<string> Permissions { get; }
}

public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly ICurrentUserService _currentUser;

    public PermissionAuthorizationHandler(ICurrentUserService currentUser)
    {
        _currentUser = currentUser;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (requirement.Permissions.Any(_currentUser.HasPermission))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
