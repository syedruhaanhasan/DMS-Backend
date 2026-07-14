using WDAS.Domain.Enums;

namespace WDAS.Application.Models;

public record LoginRequest(string Username, string Password);

public record LoginResponse(string AccessToken, DateTime ExpiresAtUtc, UserSummaryDto User);

public record AssignedRoleDto(Guid Id, string Name, string Code);

public record UserSummaryDto(
    Guid Id,
    string AdObjectId,
    string UserPrincipalName,
    string DisplayName,
    string Email,
    string Title,
    Guid DepartmentId,
    string DepartmentName,
    IReadOnlyCollection<AssignedRoleDto> Roles,
    IReadOnlyCollection<string> Permissions,
    bool IsActive);

public record DepartmentDto(Guid Id, string Name, string Code, Guid? ParentDepartmentId, bool IsActive);

public record CreateDepartmentRequest(
    string Name,
    string Code,
    Guid? ParentDepartmentId);

public record UpdateDepartmentRequest(
    string? Name,
    string? Code,
    bool? IsActive);

public record SyncResultDto(int UsersSynced, int DepartmentsSynced, DateTime SyncedAtUtc, string Source);

public record CreateUserRequest(
    string UserPrincipalName,
    string? Password,
    string DisplayName,
    string Email,
    string Title,
    Guid DepartmentId,
    List<Guid>? RoleIds = null,
    /// <summary>Legacy enum roles — mapped to seeded SecurityRole ids when RoleIds is empty.</summary>
    List<ApplicationRole>? Roles = null,
    ApplicationRole? Role = null,
    UserAccountType AccountType = UserAccountType.Local,
    string? AdObjectId = null);

public record UpdateUserRoleRequest(
    List<Guid>? RoleIds = null,
    List<ApplicationRole>? Roles = null,
    ApplicationRole? Role = null);

public record UserPreferencesDto(
    string? NotificationPreferencesJson,
    string? OutOfOfficeMessage,
    string PreferredLanguage);

public record UpdateUserPreferencesRequest(
    string? NotificationPreferences,
    string? OutOfOfficeMessage,
    string? PreferredLanguage);
