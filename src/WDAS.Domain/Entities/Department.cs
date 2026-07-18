using WDAS.Domain.Common;

namespace WDAS.Domain.Entities;

public class Department : Entity
{
    public string AdObjectId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public int? ParentDepartmentId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime LastSyncedAtUtc { get; set; }

    public Department? ParentDepartment { get; set; }
    public ICollection<Department> ChildDepartments { get; set; } = new List<Department>();
    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<Workflow> Workflows { get; set; } = new List<Workflow>();
}
