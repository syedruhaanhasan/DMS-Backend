using WDAS.Domain.Common;

namespace WDAS.Domain.Entities;

public class User : Entity
{
    public string AdObjectId { get; set; } = string.Empty;
    public string UserPrincipalName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public Guid DepartmentId { get; set; }
    public Guid? ManagerUserId { get; set; }
    public bool IsEnabledInAd { get; set; } = true;
    public bool IsDisabledInApp { get; set; }
    public DateTime? AdDisabledAtUtc { get; set; }
    public DateTime LastSyncedAtUtc { get; set; }
    public string? PasswordHash { get; set; }
    public string? NotificationPreferencesJson { get; set; }
    public string? OutOfOfficeMessage { get; set; }
    public string PreferredLanguage { get; set; } = "en";

    public Department Department { get; set; } = null!;
    public User? Manager { get; set; }
    public ICollection<User> DirectReports { get; set; } = new List<User>();
    public ICollection<RoleMapping> RoleMappings { get; set; } = new List<RoleMapping>();
    public ICollection<Document> OwnedDocuments { get; set; } = new List<Document>();
}
