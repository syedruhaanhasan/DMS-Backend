using WDAS.Domain.Common;

namespace WDAS.Domain.Entities;

public class Workflow : Entity
{
    public int DepartmentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public Department Department { get; set; } = null!;
    public ICollection<WorkflowVersion> Versions { get; set; } = new List<WorkflowVersion>();
}
