using WDAS.Domain.Common;
using WDAS.Domain.Enums;

namespace WDAS.Domain.Entities;

public class RoleMapping : Entity
{
    public Guid UserId { get; set; }
    public ApplicationRole Role { get; set; }
    public Guid? DepartmentId { get; set; }

    public User User { get; set; } = null!;
    public Department? Department { get; set; }
}
