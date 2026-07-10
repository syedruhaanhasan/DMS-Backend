namespace WDAS.Domain.Entities;

public class PushDeviceRegistration
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Platform { get; set; } = string.Empty;
    public string DeviceToken { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime RegisteredAtUtc { get; set; }
    public DateTime? LastUsedAtUtc { get; set; }

    public User User { get; set; } = null!;
}
