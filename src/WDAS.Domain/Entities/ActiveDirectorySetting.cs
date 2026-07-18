using WDAS.Domain.Common;

namespace WDAS.Domain.Entities;

/// <summary>
/// Single-row table (Id = 1) holding the Active Directory / LDAP connection
/// configuration managed from the Configuration → Active Directory screen.
/// </summary>
public class ActiveDirectorySetting : Entity
{
    public bool Enabled { get; set; }
    public string DomainName { get; set; } = string.Empty;
    public int Port { get; set; } = 389;
    public bool UseSsl { get; set; }
}
