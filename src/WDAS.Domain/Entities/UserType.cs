using WDAS.Domain.Common;

namespace WDAS.Domain.Entities;

/// <summary>
/// A configurable classification (e.g. Permanent, Contractor, Intern) that can be bound to a user.
/// Purely descriptive metadata — it does not affect permissions or workflow behaviour.
/// </summary>
public class UserType : Entity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<User> Users { get; set; } = new List<User>();
}
