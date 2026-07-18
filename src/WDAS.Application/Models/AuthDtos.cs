using WDAS.Domain.Enums;

namespace WDAS.Application.Models;

public record LoginRequest(string Username, string Password);

public record LoginResponse(string AccessToken, DateTime ExpiresAtUtc, UserSummaryDto User);

/// <summary>A freshly issued access token together with its id and true expiry.</summary>
public record AccessTokenResult(string Token, DateTime ExpiresAtUtc, string Jti);

public record AssignedRoleDto(string Id, string Name, string Code);

public record UserSummaryDto(
    string Id,
    string AdObjectId,
    string UserPrincipalName,
    string DisplayName,
    string Email,
    string? PhoneNumber,
    string Title,
    string DepartmentId,
    string DepartmentName,
    IReadOnlyCollection<AssignedRoleDto> Roles,
    IReadOnlyCollection<string> Permissions,
    bool IsActive);

public record DepartmentDto(string Id, string Name, string Code, string? ParentDepartmentId, bool IsActive);

public record CreateDepartmentRequest(
    string Name,
    string Code,
    string? ParentDepartmentId);

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
    string DepartmentId,
    List<string>? RoleIds = null,
    /// <summary>Legacy enum roles — mapped to seeded SecurityRole ids when RoleIds is empty.</summary>
    List<ApplicationRole>? Roles = null,
    ApplicationRole? Role = null,
    UserAccountType AccountType = UserAccountType.Local,
    string? AdObjectId = null);

public record UpdateUserRoleRequest(
    List<string>? RoleIds = null,
    List<ApplicationRole>? Roles = null,
    ApplicationRole? Role = null);

/// <summary>Admin update of an existing user. Password is intentionally omitted.</summary>
public record UpdateUserRequest(
    string UserPrincipalName,
    string DisplayName,
    string Email,
    string? PhoneNumber,
    string Title,
    string DepartmentId,
    List<string>? RoleIds = null,
    List<ApplicationRole>? Roles = null,
    ApplicationRole? Role = null,
    bool? IsActive = null);

public record UserPreferencesDto(
    string? NotificationPreferencesJson,
    string? OutOfOfficeMessage,
    string PreferredLanguage);

public record UpdateUserPreferencesRequest(
    string? NotificationPreferences,
    string? OutOfOfficeMessage,
    string? PreferredLanguage);
