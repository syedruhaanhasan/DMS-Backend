using WDAS.Domain.Common;

namespace WDAS.Domain.Entities;

public class DocumentRecipient : Entity
{
    public int DocumentId { get; set; }
    public string RecipientName { get; set; } = string.Empty;
    public string? RecipientEmail { get; set; }

    public Document Document { get; set; } = null!;
}
