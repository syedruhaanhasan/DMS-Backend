using WDAS.Application.Models;

namespace WDAS.Application.Abstractions;

public interface IIdentityProvider
{
    Task<AuthenticatedUser?> AuthenticateAsync(string username, string password, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DirectoryUserSnapshot>> GetDirectoryUsersAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DirectoryDepartmentSnapshot>> GetDirectoryDepartmentsAsync(CancellationToken cancellationToken = default);
}
