using WDAS.Domain.Entities;

namespace WDAS.Application.Abstractions;

public interface ICurrentUserService
{
    Guid UserId { get; }
    string AdObjectId { get; }
    IReadOnlyCollection<string> Roles { get; }
    Guid? DepartmentId { get; }
    bool IsInRole(string role);
    bool HasPermission(string permission);
}
