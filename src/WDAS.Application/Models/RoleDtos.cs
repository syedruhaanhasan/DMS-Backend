namespace WDAS.Application.Models;

public record RoleSummaryDto(string Id, string Name, string Code, bool IsSystem, bool IsActive, int PermissionCount);

public record RoleDetailDto(
    string Id,
    string Name,
    string Code,
    string? Description,
    bool IsSystem,
    bool IsActive,
    IReadOnlyCollection<string> Permissions);

public record CreateSecurityRoleRequest(
    string Name,
    string? Code,
    string? Description,
    IReadOnlyCollection<string> Permissions);

public record UpdateSecurityRoleRequest(
    string Name,
    string? Description,
    bool IsActive,
    IReadOnlyCollection<string> Permissions);

public record PermissionDefinitionDto(string Key, string Group, string Label);
