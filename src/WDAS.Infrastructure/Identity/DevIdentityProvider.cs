using WDAS.Application.Abstractions;
using WDAS.Application.Models;
using WDAS.Domain.Enums;

namespace WDAS.Infrastructure.Identity;

public class DevIdentityProvider : IIdentityProvider
{
    private static readonly IReadOnlyList<DevUser> Users =
    [
        new(
            "ad-superadmin",
            "superadmin",
            "SuperAdmin123!",
            "Super Admin",
            "superadmin@wdas.local",
            "System Administrator",
            "IT",
            "ad-it",
            ApplicationRole.SuperAdmin),
        new(
            "ad-super",
            "super.admin",
            "Super123!",
            "Super Admin (Legacy)",
            "super.admin@wdas.local",
            "IT",
            "IT",
            "ad-it",
            ApplicationRole.SuperAdmin),
        new(
            "ad-deptadmin",
            "finance.admin",
            "Finance123!",
            "Finance Admin",
            "finance.admin@wdas.local",
            "Finance Manager",
            "FIN",
            "ad-fin",
            ApplicationRole.DepartmentAdmin),
        new(
            "ad-owner",
            "maker.owner",
            "Owner123!",
            "Document Owner",
            "owner@wdas.local",
            "Analyst",
            "FIN",
            "ad-fin",
            ApplicationRole.MakerOwner),
        new(
            "ad-approver1",
            "approver.one",
            "Approver123!",
            "Approver One",
            "approver1@wdas.local",
            "Manager",
            "FIN",
            "ad-fin",
            ApplicationRole.Approver),
        new(
            "ad-approver2",
            "approver.two",
            "Approver123!",
            "Approver Two",
            "approver2@wdas.local",
            "Director",
            "FIN",
            "ad-fin",
            ApplicationRole.Approver),
        new(
            "ad-auditor",
            "auditor.user",
            "Auditor123!",
            "Auditor User",
            "auditor@wdas.local",
            "Auditor",
            "IT",
            "ad-it",
            ApplicationRole.Auditor)
    ];

    private static readonly IReadOnlyList<DirectoryDepartmentSnapshot> Departments =
    [
        new("ad-it", "Information Technology", "IT", null),
        new("ad-fin", "Finance", "FIN", null)
    ];

    public Task<AuthenticatedUser?> AuthenticateAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        var user = Users.FirstOrDefault(u =>
            u.UserPrincipalName.Equals(username, StringComparison.OrdinalIgnoreCase) &&
            u.Password == password);

        if (user is null)
        {
            return Task.FromResult<AuthenticatedUser?>(null);
        }

        return Task.FromResult<AuthenticatedUser?>(new AuthenticatedUser(
            0,
            user.AdObjectId,
            user.DisplayName,
            user.Email,
            0,
            [ToRoleCode(user.Role)]));
    }

    private static string ToRoleCode(ApplicationRole role) => role switch
    {
        ApplicationRole.SuperAdmin => Application.RoleNames.SuperAdmin,
        ApplicationRole.DepartmentAdmin => Application.RoleNames.DepartmentAdmin,
        ApplicationRole.MakerOwner => Application.RoleNames.MakerOwner,
        ApplicationRole.Approver => Application.RoleNames.Approver,
        ApplicationRole.Auditor => Application.RoleNames.Auditor,
        ApplicationRole.ItAdmin => Application.RoleNames.ItAdmin,
        _ => role.ToString(),
    };

    public Task<IReadOnlyList<DirectoryUserSnapshot>> GetDirectoryUsersAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<DirectoryUserSnapshot> snapshots = Users.Select(u => new DirectoryUserSnapshot(
            u.AdObjectId,
            u.UserPrincipalName,
            u.DisplayName,
            u.Email,
            u.Title,
            u.DepartmentAdObjectId,
            null,
            true)).ToList();

        return Task.FromResult(snapshots);
    }

    public Task<IReadOnlyList<DirectoryDepartmentSnapshot>> GetDirectoryDepartmentsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Departments);

    internal static IReadOnlyList<DevUser> GetSeedUsers() => Users;

    internal static DevUser? FindSeedUser(string userPrincipalName) =>
        Users.FirstOrDefault(u => u.UserPrincipalName.Equals(userPrincipalName, StringComparison.OrdinalIgnoreCase));

    internal record DevUser(
        string AdObjectId,
        string UserPrincipalName,
        string Password,
        string DisplayName,
        string Email,
        string Title,
        string DepartmentCode,
        string DepartmentAdObjectId,
        ApplicationRole Role)
    {
        public int DepartmentId { get; set; }
    }
}
