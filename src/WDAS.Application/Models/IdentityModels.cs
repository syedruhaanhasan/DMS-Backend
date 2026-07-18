namespace WDAS.Application.Models;

public record AuthenticatedUser(
    int UserId,
    string AdObjectId,
    string DisplayName,
    string Email,
    int DepartmentId,
    IReadOnlyCollection<string> RoleCodes);

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
