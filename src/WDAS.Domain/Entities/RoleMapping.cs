using WDAS.Domain.Common;

namespace WDAS.Domain.Entities;

public class RoleMapping : Entity
{
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }
    public Guid? DepartmentId { get; set; }

    public User User { get; set; } = null!;
    public SecurityRole Role { get; set; } = null!;
    public Department? Department { get; set; }
}
