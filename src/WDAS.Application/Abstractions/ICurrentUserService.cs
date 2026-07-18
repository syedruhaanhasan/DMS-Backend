using WDAS.Domain.Entities;

namespace WDAS.Application.Abstractions;

public interface ICurrentUserService
{
    int UserId { get; }
    string AdObjectId { get; }
    IReadOnlyCollection<string> Roles { get; }
    int? DepartmentId { get; }
    bool IsInRole(string role);
    bool HasPermission(string permission);
}
