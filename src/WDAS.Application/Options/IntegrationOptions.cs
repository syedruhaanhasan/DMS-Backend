namespace WDAS.Application.Options;

public class LdapOptions
{
    public const string SectionName = "Ldap";

    public bool Enabled { get; set; }
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 389;
    public bool UseSsl { get; set; }
    public string BaseDn { get; set; } = "DC=company,DC=local";
    public string BindDn { get; set; } = string.Empty;
    public string BindPassword { get; set; } = string.Empty;
    public string UserFilter { get; set; } = "(&(objectClass=user)(sAMAccountName={0}))";
    public string UserSearchBase { get; set; } = string.Empty;
}

public class SmsOptions
{
    public const string SectionName = "Sms";

    public bool Enabled { get; set; }
    public string Provider { get; set; } = "Twilio";
    public string AccountSid { get; set; } = string.Empty;
    public string AuthToken { get; set; } = string.Empty;
    public string FromNumber { get; set; } = string.Empty;
}
