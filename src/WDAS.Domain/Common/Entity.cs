namespace WDAS.Domain.Common;

public abstract class Entity
{
    public int Id { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}
