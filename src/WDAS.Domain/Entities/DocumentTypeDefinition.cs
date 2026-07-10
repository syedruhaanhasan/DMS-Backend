using WDAS.Domain.Common;

namespace WDAS.Domain.Entities;

public class DocumentTypeDefinition : Entity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Category { get; set; } = "non_financial";
    public bool AmountRequired { get; set; }
    public bool IsActive { get; set; } = true;
}
