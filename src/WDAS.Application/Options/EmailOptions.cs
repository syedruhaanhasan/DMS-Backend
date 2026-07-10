namespace WDAS.Application.Options;

public class EmailOptions
{
    public const string SectionName = "Email";

    public bool Enabled { get; set; }
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 25;
    public bool UseSsl { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string FromAddress { get; set; } = "noreply@wdas.local";
    public string FromDisplayName { get; set; } = "WDAS";
}
