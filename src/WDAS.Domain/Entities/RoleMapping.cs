using WDAS.Domain.Common;

namespace WDAS.Domain.Entities;

public class RoleMapping : Entity
{
    public int UserId { get; set; }
    public int RoleId { get; set; }
    public int? DepartmentId { get; set; }

    public User User { get; set; } = null!;
    public SecurityRole Role { get; set; } = null!;
    public Department? Department { get; set; }
}
