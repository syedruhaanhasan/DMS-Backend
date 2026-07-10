using WDAS.Domain.Enums;

namespace WDAS.Application.Models;

public record AuthenticatedUser(
    Guid UserId,
    string AdObjectId,
    string DisplayName,
    string Email,
    Guid DepartmentId,
    IReadOnlyCollection<ApplicationRole> Roles);

public record DirectoryUserSnapshot(
    string AdObjectId,
    string UserPrincipalName,
    string DisplayName,
    string Email,
    string Title,
    string DepartmentAdObjectId,
    string? ManagerAdObjectId,
    bool IsEnabled);

public record DirectoryDepartmentSnapshot(
    string AdObjectId,
    string Name,
    string Code,
    string? ParentAdObjectId);
