using WDAS.Domain.Common;

namespace WDAS.Domain.Entities;

public class SecurityRole : Entity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsSystem { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<SecurityRolePermission> Permissions { get; set; } = new List<SecurityRolePermission>();
    public ICollection<RoleMapping> RoleMappings { get; set; } = new List<RoleMapping>();
}

public class SecurityRolePermission : Entity
{
    public int RoleId { get; set; }
    public string PermissionKey { get; set; } = string.Empty;

    public SecurityRole Role { get; set; } = null!;
}
